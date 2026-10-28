// <copyright file="MapLevelLadderUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Puts the hunting maps on a ladder of levels and lets each zone drop only its own sets, see <see cref="MapLevelLadder"/>,
/// and sells the items to enter the events at Lumen the Barmaid instead of dropping them, see <see cref="EventEntryItemShop"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// The game client shows the warp list with its own level requirements, which should be changed to the ones of the ladder.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MapLevelLadderUpdatePlugIn_Name), Description = nameof(PlugInResources.MapLevelLadderUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E5F130FC-CED1-4EE8-AD15-7632E97389C2")]
public class MapLevelLadderUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Map Level Ladder And Zone Drops";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Gives each hunting map a range of monster levels which doesn't overlap the others, from Lorencia, Noria and Elbeland (1 to 19) up to Raklion (140 to 148), and spreads the sets, weapons and shields of each class over the zones up to Karutan. Vulcanus stays as it is. Swamp of Calmness drops only jewels, more often, and gives double experience. Raklion drops only socket items with up to two sockets, and Selupan always drops one with three sockets. Monsters which also spawn in other maps (e.g. Devil Square) keep their original level there. The Symbol of Kundun and other level based drops are moved to the new levels. The items to enter Blood Castle, Devil Square, Illusion Temple, Doppelganger and Imperial Guardian don't drop anymore, Lumen the Barmaid sells them (adds Old Scroll +6, Illusion Sorcerer Covenant +6, Mirror of Dimensions and Gaion's Order). Warps are free and the warps and gates require the level of their map on the ladder. Version 2: Crywolf is a step in parallel to Swamp of Calmness, its monsters get levels 130 to 139 and the stats of the monsters there, it drops only jewels and the materials of the Fenrir, gives double experience, and the warp requires level 270.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>It moves the drops of the Symbol of Kundun and the Crest of Monarch, which these updates add.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddKalimaPlugIn).GUID, typeof(AddCrestOfMonarchDropGroupUpdateSeason6).GUID];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    /// <remarks>Version 2 puts Crywolf on the ladder, in parallel to Swamp of Calmness.</remarks>
    public override int Version => 2;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    /// <remarks>
    /// From version 1 on, only Crywolf is changed, so that the other maps which the owner customized
    /// after the first version stay as they are.
    /// </remarks>
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration, int? installedVersion)
    {
        if (installedVersion >= 1)
        {
            MapLevelLadder.ApplyCrywolf(context, gameConfiguration);
            AddCrywolfDifficulty(gameConfiguration);
            return ValueTask.CompletedTask;
        }

        return this.ApplyAsync(context, gameConfiguration);
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MapLevelLadder.Apply(context, gameConfiguration);
        EventEntryItemShop.Apply(context, gameConfiguration);
        AddCrywolfDifficulty(gameConfiguration);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gives the monsters of Crywolf the multipliers of Swamp of Calmness in an existing configuration
    /// of the <see cref="MapDifficultyScaler"/>, which keeps its other multipliers.
    /// A new configuration already has them, see <see cref="MapDifficultyScalerConfiguration.CreateDefaultMonsters"/>.
    /// </summary>
    private static void AddCrywolfDifficulty(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(MapDifficultyScaler).GUID) is not { } plugInConfiguration
            || plugInConfiguration.GetConfiguration<MapDifficultyScalerConfiguration>(new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler()) is not { } configuration)
        {
            return;
        }

        var defaults = MapDifficultyScalerConfiguration.CreateDefaultMonsters()
            .Where(m => MapLevelLadder.CrywolfMonsterNumbers.Contains(m.Number))
            .Where(m => configuration.Monsters.All(existing => existing.Number != m.Number))
            .ToList();
        if (defaults.Count == 0)
        {
            return;
        }

        defaults.ForEach(configuration.Monsters.Add);
        plugInConfiguration.SetConfiguration(configuration, new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler());
    }
}
