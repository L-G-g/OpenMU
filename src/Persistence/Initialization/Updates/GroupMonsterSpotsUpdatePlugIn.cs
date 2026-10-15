// <copyright file="GroupMonsterSpotsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Rearranges the monsters of the hunting maps into spots of 5 monsters, about 20 coordinates apart,
/// with some hotspots of 7 monsters, and at most <see cref="MonsterSpotGrouping.MaximumSpotsPerMap"/> spots per map.
/// See <see cref="MonsterSpotGrouping"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the maps play instead of correcting data.
/// It replaces the automatic monster spawns of the hunting maps, so changes which were made to them
/// before are replaced, too.
/// </remarks>
[PlugIn]
[Display(Name = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.GroupMonsterSpotsUpdatePlugIn_Name), Description = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.GroupMonsterSpotsUpdatePlugIn_Description), ResourceType = typeof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources))]
[Guid("CD3B7609-7242-4368-9F02-219911EF14A6")]
public class GroupMonsterSpotsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Group Monsters Into Spots";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Rearranges the monsters of the hunting maps into spots of 5 monsters in an area of 4x4, about 20 coordinates apart, at most 80 per map. About every fifth spot is a hotspot with 7 monsters.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>Version 2 limits the spots to <see cref="MonsterSpotGrouping.MaximumSpotsPerMap"/> per map.</remarks>
    public override int Version => 2;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MonsterSpotGrouping.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
