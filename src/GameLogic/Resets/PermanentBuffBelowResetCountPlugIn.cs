// <copyright file="PermanentBuffBelowResetCountPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives the characters which have less resets than configured a buff which doesn't run out, e.g. the buff of the Elf Soldier.
/// </summary>
/// <remarks>
/// The buff is checked whenever the character is added to a map, which is the case when it enters the game,
/// warps and respawns after its death. So it comes back after a death, and it's removed when a reset
/// reaches the configured count. The power-ups of the effect definition are applied like when an NPC gives
/// the buff, so they still follow the character level. To stop the NPC from giving it, remove the buff from
/// the NPC definition.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.PermanentBuffBelowResetCountPlugIn_Name), Description = nameof(PlugInResources.PermanentBuffBelowResetCountPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4B93C8A4-62B8-4CAA-9DAC-B1A8F426BEDE")]
public class PermanentBuffBelowResetCountPlugIn : IObjectAddedToMapPlugIn, ISupportCustomConfiguration<PermanentBuffBelowResetCountConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public PermanentBuffBelowResetCountConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new PermanentBuffBelowResetCountConfiguration();
    }

    /// <inheritdoc />
    public async ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not Player { Attributes: { } attributes } player
            || this.Configuration is not { } configuration
            || player.GameContext.Configuration.MagicEffects.FirstOrDefault(e => e.Number == configuration.EffectNumber) is not { } effectDefinition)
        {
            return;
        }

        var effect = player.MagicEffectList.TryGetEffect(effectDefinition.Number, out var activeEffect) ? activeEffect : null;
        if (attributes[Stats.Resets] >= configuration.ResetCount)
        {
            if (effect is not null)
            {
                await effect.DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        if (effect?.Duration == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        var boosts = effectDefinition.PowerUpDefinitions
            .Where(def => def.Boost is not null && def.TargetAttribute is not null)
            .Select(def => new MagicEffect.ElementWithTarget(attributes.CreateElement(def), def.TargetAttribute!))
            .ToArray();
        if (boosts.Length == 0)
        {
            return;
        }

        // An effect which the character already has, e.g. from the NPC, gets the infinite duration.
        await player.MagicEffectList.AddEffectAsync(new MagicEffect(Timeout.InfiniteTimeSpan, effectDefinition, boosts)).ConfigureAwait(false);
    }
}
