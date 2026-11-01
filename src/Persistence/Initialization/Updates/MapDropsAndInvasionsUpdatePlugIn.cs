// <copyright file="MapDropsAndInvasionsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the jewels, feathers, gemstones and spheres drop each in its own maps, and activates the invasions of fifteen maps,
/// see <see cref="MapDropsAndInvasions"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MapDropsAndInvasionsUpdatePlugIn_Name), Description = nameof(PlugInResources.MapDropsAndInvasionsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A3D7C5E1-9B24-4F6A-8C13-5E2B7D9F0A48")]
public class MapDropsAndInvasionsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Map Drops And Invasions";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The Jewels of Chaos, Bless, Soul and Life, the Jewel of Guardian, the feathers, the Gemstone and the spheres don't drop from the common drops anymore, but each from the monsters of its maps: Chaos in Lorencia, Noria and Elbeland, Bless in Devias and Atlans, Soul in Dungeon and Tarkan, Life and Creation in Lost Tower and Aida, Loch's Feather and Crest of Monarch in Icarus, Gemstone in Kanturu, Guardian in Kanturu Relics, the four jewels in Swamp of Calmness and only the Sphere (Mono) in Raklion. Crywolf keeps the materials of the Fenrir, each from some of its monsters, and the Jewel of Creation stays in Swamp of Calmness and Crywolf. Activates the plugin 'Map invasions': every 24 minutes one of these 15 maps is invaded by 10 monsters, which stay until the next invasion of the map and always drop its items. The invasion monsters don't spawn there otherwise. The invasions of the Red Dragon and the White Wizard are deactivated.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>It changes the drops of the ladder, of the Fenrir materials and of the Crest of Monarch, and the spots of the maps.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn =>
    [
        (typeof(MapLevelLadderUpdatePlugIn).GUID, 2),
        (typeof(GroupMonsterSpotsUpdatePlugIn).GUID, 3),
        typeof(AddFenrirMaterialDropGroupsUpdateSeason6).GUID,
        typeof(AddCrestOfMonarchDropGroupUpdateSeason6).GUID,
    ];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MapDropsAndInvasions.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
