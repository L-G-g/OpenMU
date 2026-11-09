// <copyright file="DungeonAboveDeviasDifficultyUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes the weakest monsters of Dungeon at least as strong as the strongest ones of Devias, the map before it on the ladder,
/// in an existing configuration of the <see cref="MapDifficultyScaler"/>,
/// see <see cref="MapDifficultyScalerConfiguration.CreateDungeonAboveDeviasMonsters"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// A new configuration of the plugin already has these multipliers. Monsters which are already configured for Dungeon are kept.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.DungeonAboveDeviasDifficultyUpdatePlugIn_Name), Description = nameof(PlugInResources.DungeonAboveDeviasDifficultyUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4D62303D-1779-406A-9FFD-ABEA5A74B5C5")]
public class DungeonAboveDeviasDifficultyUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Dungeon Above Devias Difficulty";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "With the multipliers of the plugin Map difficulty scaler, the strongest monsters of Devias were stronger than the weakest half of Dungeon, the next map of the ladder, and the Ice Queen stronger than the Gorgon. The monsters of Dungeon get their own multipliers there (only on the map Dungeon), so that their stats rise from just above the strongest monster of Devias up to the Dark Knight, and the Gorgon is a bit stronger than the Ice Queen. Devias, the dungeon of Devias and the strongest monsters of Dungeon don't change. The monsters which are already on the map change after a restart.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>The multipliers fit the stats which the monsters get on the ladder of levels.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(MapLevelLadderUpdatePlugIn).GUID];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(MapDifficultyScaler).GUID) is not { } plugInConfiguration
            || plugInConfiguration.GetConfiguration<MapDifficultyScalerConfiguration>(new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler()) is not { } configuration)
        {
            return ValueTask.CompletedTask;
        }

        var added = MapDifficultyScalerConfiguration.CreateDungeonAboveDeviasMonsters()
            .Where(m => configuration.Monsters.All(existing => existing.Number != m.Number || existing.MapNumber != m.MapNumber))
            .ToList();
        if (added.Count > 0)
        {
            added.ForEach(configuration.Monsters.Add);
            plugInConfiguration.SetConfiguration(configuration, new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler());
        }

        return ValueTask.CompletedTask;
    }
}
