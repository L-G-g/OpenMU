// <copyright file="MoreDungeonsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the dungeons of Tarkan and Kanturu Ruins and the extreme versions of all dungeons, see <see cref="MoreDungeons"/>.
/// </summary>
/// <remarks>
/// It's optional, because it adds content instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MoreDungeonsUpdatePlugIn_Name), Description = nameof(PlugInResources.MoreDungeonsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7B3E9A15-4D2C-4F86-A1B7-9C5D0E6F2A34")]
public class MoreDungeonsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Tarkan And Kanturu Dungeons";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the dungeons of Tarkan (Mutant, Beam Knight and Death Beam Knight with three Zaikans) and of Kanturu Ruins (Satyros, Kentauros Warrior and Genocider Warrior), and an extreme version of each dungeon, including the one of Devias: stronger monsters, and the high half of the items drops more often. Each dungeon has two guards in the town of its map, one for the normal and one for the extreme version, and its own daily entry. The dungeon of Devias drops the items of Lorencia, Noria, Elbeland and Devias; Tarkan the ones of Dungeon, Lost Tower, Atlans and Tarkan; Kanturu the ones of Aida, Icarus, Kanturu Ruins and Kanturu Relics. The rules can be changed in the plugin Dungeon.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>The bosses are copies of the monsters of the ladder, and the dungeon of Devias gets its extreme version.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn =>
    [
        (typeof(MapLevelLadderUpdatePlugIn).GUID, 2),
        typeof(IceQueenDungeonUpdatePlugIn).GUID,
    ];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MoreDungeons.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
