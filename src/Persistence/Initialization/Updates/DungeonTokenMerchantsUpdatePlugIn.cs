// <copyright file="DungeonTokenMerchantsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the data of the tokens of the dungeons and of their merchants, see <see cref="DungeonTokenMerchants"/>.
/// </summary>
/// <remarks>
/// It's optional, because it adds content instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.DungeonTokenMerchantsUpdatePlugIn_Name), Description = nameof(PlugInResources.DungeonTokenMerchantsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E4B7A2C9-5D18-4F63-9A0E-6C3D8B1F7E25")]
public class DungeonTokenMerchantsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Dungeon Token Merchants";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Completing a dungeon gives each player a token of the dungeon (two in the extreme version): Gelid Token in Devias, Arid Token in Tarkan and Ancient Token in Kanturu. Zienna in Devias and two new merchants next to the dungeon guards of Tarkan and Kanturu Ruins sell the best armor set, shield and weapons of their dungeon for each character class for these tokens. The sold items are usual excellent items with luck and the excellent options of a dropped one. Adds the attributes of the tokens and the two merchants. The prices and rules can be changed in the plugin 'Dungeon token merchants'.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>The merchants stand next to the guards of the dungeons and sell the items of their drop maps.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(MoreDungeonsUpdatePlugIn).GUID];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        DungeonTokenMerchants.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
