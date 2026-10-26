// <copyright file="MapDifficultyScaler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;
using DifficultyMultipliers = MUnique.OpenMU.GameLogic.PlugIns.MapDifficultyScalerConfiguration.DifficultyMultipliers;

/// <summary>
/// A feature plugin which multiplies the stats of the monsters of each map, or of single monsters,
/// so that the maps need more resets without changing the level of the monsters.
/// </summary>
/// <remarks>
/// The multipliers of a monster replace the ones of its map. They are added to the monsters when they appear on a map,
/// so they also apply to instances of a map, e.g. the dungeon of Devias. Changes of the multipliers take effect
/// immediately; a map or monster which is added to the configuration applies to the monsters which appear afterwards.
/// Monsters which are summoned by players aren't changed.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MapDifficultyScaler_Name), Description = nameof(PlugInResources.MapDifficultyScaler_Description), ResourceType = typeof(PlugInResources))]
[Guid("C1E5A0B2-7F43-4D8E-9A61-2B7C3D4E5F60")]
public class MapDifficultyScaler : IFeaturePlugIn, IObjectAddedToMapPlugIn, IObjectRemovedFromMapPlugIn,
    ISupportCustomConfiguration<MapDifficultyScalerConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private readonly ConcurrentDictionary<(bool IsMonster, short Number), Multipliers> _multipliers = new();

    private readonly ConcurrentDictionary<AttackableNpcBase, Multipliers> _appliedMultipliers = new();

    private MapDifficultyScalerConfiguration? _configuration;

    /// <inheritdoc />
    public MapDifficultyScalerConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;
            foreach (var (key, multipliers) in this._multipliers)
            {
                multipliers.SetValues(Find(value, key.IsMonster, key.Number));
            }
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new MapDifficultyScalerConfiguration();
    }

    /// <inheritdoc />
    public ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not AttackableNpcBase npc
            || npc is Monster { SummonedBy: not null }
            || this.Configuration is not { } configuration)
        {
            return ValueTask.CompletedTask;
        }

        var key = GetKey(configuration, (short)map.MapId, npc.Definition.Number);
        if (key is null)
        {
            return ValueTask.CompletedTask;
        }

        var multipliers = this._multipliers.GetOrAdd(key.Value, k => new Multipliers(Find(this.Configuration, k.IsMonster, k.Number)));
        if (!this._appliedMultipliers.TryAdd(npc, multipliers))
        {
            return ValueTask.CompletedTask;
        }

        multipliers.AddTo(npc.Attributes);
        if (npc.SpawnArea.MaximumHealthOverride is null && npc.IsAlive)
        {
            // The health was set before the monster was added to the map.
            npc.Health = (int)npc.Attributes[Stats.MaximumHealth];
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is AttackableNpcBase npc && this._appliedMultipliers.TryRemove(npc, out var multipliers))
        {
            multipliers.RemoveFrom(npc.Attributes);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the key of the multipliers which apply to a monster: the ones of the monster, if configured, else the ones of the map.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="mapNumber">The number of the map.</param>
    /// <param name="monsterNumber">The number of the monster.</param>
    /// <returns>The key, or <c>null</c> if neither the monster nor the map is configured.</returns>
    internal static (bool IsMonster, short Number)? GetKey(MapDifficultyScalerConfiguration configuration, short mapNumber, short monsterNumber)
    {
        if (Find(configuration, true, monsterNumber) is not null)
        {
            return (true, monsterNumber);
        }

        if (Find(configuration, false, mapNumber) is not null)
        {
            return (false, mapNumber);
        }

        return null;
    }

    private static DifficultyMultipliers? Find(MapDifficultyScalerConfiguration? configuration, bool isMonster, short number)
    {
        var entries = isMonster ? configuration?.Monsters : configuration?.Maps;
        return entries?.FirstOrDefault(e => e.Number == number);
    }

    /// <summary>
    /// The shared multiplicative elements of a map or monster.
    /// </summary>
    internal sealed class Multipliers
    {
        private readonly SimpleElement _health = new(1.0f, AggregateType.Multiplicate);
        private readonly SimpleElement _damage = new(1.0f, AggregateType.Multiplicate);
        private readonly SimpleElement _defense = new(1.0f, AggregateType.Multiplicate);
        private readonly SimpleElement _attackRate = new(1.0f, AggregateType.Multiplicate);
        private readonly SimpleElement _defenseRate = new(1.0f, AggregateType.Multiplicate);

        /// <summary>
        /// Initializes a new instance of the <see cref="Multipliers"/> class.
        /// </summary>
        /// <param name="entry">The configured multipliers.</param>
        public Multipliers(DifficultyMultipliers? entry)
        {
            this.SetValues(entry);
        }

        /// <summary>
        /// Gets the multiplier of the health.
        /// </summary>
        public float Health => this._health.Value;

        /// <summary>
        /// Gets the multiplier of the damage.
        /// </summary>
        public float Damage => this._damage.Value;

        /// <summary>
        /// Sets the values of the multipliers. Without an entry, they don't change anything.
        /// </summary>
        /// <param name="entry">The configured multipliers.</param>
        public void SetValues(DifficultyMultipliers? entry)
        {
            this._health.Value = GetMultiplier(entry?.Health);
            this._damage.Value = GetMultiplier(entry?.Damage);
            this._defense.Value = GetMultiplier(entry?.Defense);
            this._attackRate.Value = GetMultiplier(entry?.AttackRate);
            this._defenseRate.Value = GetMultiplier(entry?.DefenseRate);
        }

        /// <summary>
        /// Adds the multipliers to the attributes of a monster.
        /// </summary>
        /// <param name="attributes">The attributes.</param>
        public void AddTo(IAttributeSystem attributes)
        {
            attributes.AddElement(this._health, Stats.MaximumHealth);
            attributes.AddElement(this._damage, Stats.MinimumPhysBaseDmg);
            attributes.AddElement(this._damage, Stats.MaximumPhysBaseDmg);
            attributes.AddElement(this._defense, Stats.DefenseBase);
            attributes.AddElement(this._attackRate, Stats.AttackRatePvm);
            attributes.AddElement(this._defenseRate, Stats.DefenseRatePvm);
        }

        /// <summary>
        /// Removes the multipliers from the attributes of a monster.
        /// </summary>
        /// <param name="attributes">The attributes.</param>
        public void RemoveFrom(IAttributeSystem attributes)
        {
            attributes.RemoveElement(this._health, Stats.MaximumHealth);
            attributes.RemoveElement(this._damage, Stats.MinimumPhysBaseDmg);
            attributes.RemoveElement(this._damage, Stats.MaximumPhysBaseDmg);
            attributes.RemoveElement(this._defense, Stats.DefenseBase);
            attributes.RemoveElement(this._attackRate, Stats.AttackRatePvm);
            attributes.RemoveElement(this._defenseRate, Stats.DefenseRatePvm);
        }

        private static float GetMultiplier(float? value)
        {
            // A multiplier of 0 or less would kill the monster or make it immortal, so it's ignored.
            return value is > 0 ? value.Value : 1.0f;
        }
    }
}
