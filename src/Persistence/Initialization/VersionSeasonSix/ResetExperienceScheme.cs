// <copyright file="ResetExperienceScheme.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using System.Text.Json;
using System.Text.Json.Nodes;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.Experience;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A scheme of resets and experience: the required level of the resets rises from 300 to 400, the buff of the
/// Elf Soldier is permanent below 10 resets instead of given by the NPC, the levels 300 to 399 gain more experience
/// and a monster always gives the same experience.
/// </summary>
internal static class ResetExperienceScheme
{
    /// <summary>
    /// The required level of the first resets.
    /// </summary>
    internal const int FirstRequiredLevel = 300;

    /// <summary>
    /// The number of resets which require the <see cref="FirstRequiredLevel"/>.
    /// </summary>
    internal const int ResetsAtFirstRequiredLevel = 10;

    /// <summary>
    /// The levels which the required level rises per step.
    /// </summary>
    internal const int RequiredLevelStep = 10;

    /// <summary>
    /// The number of resets per step of the required level.
    /// </summary>
    internal const int ResetsPerRequiredLevelStep = 10;

    /// <summary>
    /// The highest required level. It's reached at the 101st reset.
    /// </summary>
    internal const int LastRequiredLevel = 400;

    private const short ElfSoldierNumber = 257;

    private const string RequiredLevelTiersPropertyName = nameof(ResetConfiguration.RequiredLevelTiers);

    /// <summary>
    /// Gets the required level tiers of the scheme.
    /// </summary>
    /// <returns>The required level tiers.</returns>
    internal static List<ResetConfiguration.ResetLevelTier> CreateRequiredLevelTiers()
    {
        var tiers = new List<ResetConfiguration.ResetLevelTier>
        {
            new() { MinimumResetCount = 1, RequiredLevel = FirstRequiredLevel },
        };

        var minimumResetCount = ResetsAtFirstRequiredLevel + 1;
        for (var level = FirstRequiredLevel + RequiredLevelStep; level <= LastRequiredLevel; level += RequiredLevelStep)
        {
            tiers.Add(new() { MinimumResetCount = minimumResetCount, RequiredLevel = level });
            minimumResetCount += ResetsPerRequiredLevelStep;
        }

        return tiers;
    }

    /// <summary>
    /// Applies the scheme to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        SetRequiredLevelTiers(context, gameConfiguration);
        MakeExperienceFixed(context, gameConfiguration);
        ActivatePlugIn(context, gameConfiguration, typeof(PermanentBuffBelowResetCountPlugIn), new PermanentBuffBelowResetCountConfiguration());
        ActivatePlugIn(context, gameConfiguration, typeof(LevelRangeExperiencePlugIn), new LevelRangeExperienceConfiguration());
    }

    /// <summary>
    /// Removes the buff from the Elf Soldier, because the <see cref="PermanentBuffBelowResetCountPlugIn"/> gives it.
    /// A new database doesn't need it, because the Elf Soldier is created without the buff.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static async ValueTask RemoveElfSoldierBuffAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ElfSoldierNumber) is not { } elfSoldier)
        {
            return;
        }

        foreach (var buff in elfSoldier.Buffs.ToList())
        {
            elfSoldier.Buffs.Remove(buff);
            await context.DeleteAsync(buff).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets the required level tiers in the configuration of the reset feature and keeps the other values of it.
    /// </summary>
    private static void SetRequiredLevelTiers(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(ResetFeaturePlugIn).GUID) is not { } plugInConfiguration)
        {
            // A new database: the reset feature stays disabled by default, but it's configured with the tiers.
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(typeof(ResetFeaturePlugIn).GUID);
            plugInConfiguration.TypeId = typeof(ResetFeaturePlugIn).GUID;
            plugInConfiguration.IsActive = false;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        var configurationNode = string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration)
            ? JsonSerializer.SerializeToNode(new ResetConfiguration())!.AsObject()
            : JsonNode.Parse(plugInConfiguration.CustomConfiguration)!.AsObject();
        configurationNode[RequiredLevelTiersPropertyName] = JsonSerializer.SerializeToNode(CreateRequiredLevelTiers());
        plugInConfiguration.CustomConfiguration = configurationNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Sets the random experience multipliers to 1, so that a monster always gives the same experience.
    /// </summary>
    private static void MakeExperienceFixed(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var attribute in new[] { Stats.RandomExperienceMinMultiplier, Stats.RandomExperienceMaxMultiplier })
        {
            if (gameConfiguration.Attributes.FirstOrDefault(a => a.Id == attribute.Id) is not { } definition)
            {
                continue;
            }

            foreach (var value in gameConfiguration.GlobalBaseAttributeValues.Where(v => v.Definition?.Id == definition.Id).ToList())
            {
                gameConfiguration.GlobalBaseAttributeValues.Remove(value);
            }

            gameConfiguration.GlobalBaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(1f, definition));
        }
    }

    private static void ActivatePlugIn(IContext context, GameConfiguration gameConfiguration, Type plugInType, object configuration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInType.GUID) is not { } plugInConfiguration)
        {
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(plugInType.GUID);
            plugInConfiguration.TypeId = plugInType.GUID;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        plugInConfiguration.IsActive = true;
        if (string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            plugInConfiguration.SetConfiguration(configuration, null);
        }
    }
}
