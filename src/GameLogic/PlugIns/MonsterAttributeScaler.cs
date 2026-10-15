// <copyright file="MonsterAttributeScaler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A feature plugin which scales all monster base stats by a configurable percentage.
/// Positive percentages make monsters stronger, negative ones make them weaker.
/// Uses shared multiplicative IElement instances. Configuration changes take effect immediately.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MonsterAttributeScaler_Name), Description = nameof(PlugInResources.MonsterAttributeScaler_Description), ResourceType = typeof(PlugInResources))]
[Guid("59E5B45F-0A3D-4BAF-8B6C-9E1D2F3A4B5C")]
public class MonsterAttributeScaler : IFeaturePlugIn, IObjectAddedToMapPlugIn, IObjectRemovedFromMapPlugIn,
    ISupportCustomConfiguration<MonsterAttributeScalerConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>
    /// The lowest multiplier a negative percentage can result in, so that a stat never drops to zero or below.
    /// </summary>
    private const float MinimumMultiplier = 0.01f;

    private readonly SimpleElement _damageMultiplier = new(1.0f, AggregateType.Multiplicate);
    private readonly SimpleElement _attackRateMultiplier = new(1.0f, AggregateType.Multiplicate);
    private readonly SimpleElement _defenseRateMultiplier = new(1.0f, AggregateType.Multiplicate);
    private readonly SimpleElement _defenseMultiplier = new(1.0f, AggregateType.Multiplicate);
    private readonly SimpleElement _healthMultiplier = new(1.0f, AggregateType.Multiplicate);

    private MonsterAttributeScalerConfiguration? _configuration;

    /// <inheritdoc />
    public MonsterAttributeScalerConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;

            if (value is null)
            {
                this._damageMultiplier.Value = 1.0f;
                this._attackRateMultiplier.Value = 1.0f;
                this._defenseRateMultiplier.Value = 1.0f;
                this._defenseMultiplier.Value = 1.0f;
                this._healthMultiplier.Value = 1.0f;
                return;
            }

            this._damageMultiplier.Value = GetMultiplier(value.DamagePercentage);
            this._attackRateMultiplier.Value = GetMultiplier(value.AttackRatePercentage);
            this._defenseRateMultiplier.Value = GetMultiplier(value.DefenseRatePercentage);
            this._defenseMultiplier.Value = GetMultiplier(value.DefensePercentage);
            this._healthMultiplier.Value = GetMultiplier(value.HealthPercentage);
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new MonsterAttributeScalerConfiguration();
    }

    /// <inheritdoc />
    public ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not AttackableNpcBase monster)
        {
            return ValueTask.CompletedTask;
        }

        monster.Attributes.AddElement(this._damageMultiplier, Stats.MinimumPhysBaseDmg);
        monster.Attributes.AddElement(this._damageMultiplier, Stats.MaximumPhysBaseDmg);
        monster.Attributes.AddElement(this._attackRateMultiplier, Stats.AttackRatePvm);
        monster.Attributes.AddElement(this._defenseRateMultiplier, Stats.DefenseRatePvm);
        monster.Attributes.AddElement(this._defenseMultiplier, Stats.DefenseBase);
        monster.Attributes.AddElement(this._healthMultiplier, Stats.MaximumHealth);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is not AttackableNpcBase monster)
        {
            return ValueTask.CompletedTask;
        }

        monster.Attributes.RemoveElement(this._damageMultiplier, Stats.MinimumPhysBaseDmg);
        monster.Attributes.RemoveElement(this._damageMultiplier, Stats.MaximumPhysBaseDmg);
        monster.Attributes.RemoveElement(this._attackRateMultiplier, Stats.AttackRatePvm);
        monster.Attributes.RemoveElement(this._defenseRateMultiplier, Stats.DefenseRatePvm);
        monster.Attributes.RemoveElement(this._defenseMultiplier, Stats.DefenseBase);
        monster.Attributes.RemoveElement(this._healthMultiplier, Stats.MaximumHealth);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the multiplier for a configured percentage, e.g. 1.25 for 25 and 0.8 for -20.
    /// </summary>
    /// <param name="percentage">The configured percentage.</param>
    /// <returns>The multiplier, which is never below <see cref="MinimumMultiplier"/>.</returns>
    internal static float GetMultiplier(float percentage)
    {
        return Math.Max(MinimumMultiplier, 1.0f + (percentage / 100.0f));
    }
}
