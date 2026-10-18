// <copyright file="MapLevelLadderUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Puts the hunting maps on a ladder of levels and lets each zone drop only its best sets, see <see cref="MapLevelLadder"/>,
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
    internal const string PlugInDescription = "Gives each hunting map a range of monster levels which doesn't overlap the others, from Lorencia, Noria and Elbeland (1 to 19) up to Raklion (140 to 148), and lets each zone drop only the best set it can give for each class. Swamp of Calmness and Raklion also drop socket items. Monsters which also spawn in other maps (e.g. Devil Square) keep their original level there. The Symbol of Kundun and other level based drops are moved to the new levels. The items to enter Blood Castle, Devil Square, Illusion Temple, Doppelganger and Imperial Guardian don't drop anymore, Lumen the Barmaid sells them (adds Old Scroll +6, Illusion Sorcerer Covenant +6, Mirror of Dimensions and Gaion's Order). Warps are free and the warps and gates require the level of their map on the ladder.";

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
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MapLevelLadder.Apply(context, gameConfiguration);
        EventEntryItemShop.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
