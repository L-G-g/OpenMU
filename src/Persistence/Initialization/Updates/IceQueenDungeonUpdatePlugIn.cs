// <copyright file="IceQueenDungeonUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the first dungeon, an instance of Devias with the Ice Queen as boss, see <see cref="IceQueenDungeon"/>.
/// </summary>
/// <remarks>
/// It's optional, because it adds content instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.IceQueenDungeonUpdatePlugIn_Name), Description = nameof(PlugInResources.IceQueenDungeonUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2F8B6D4A-7C1E-4A9B-B3D5-6E0F2A4C8B19")]
public class IceQueenDungeonUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Ice Queen Dungeon";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the first dungeon: a guard in the town of Devias (Dungeon Guard) lets a party enter its own instance of Devias for 15 minutes, once per day. The monsters are 3 times stronger and drop 5 times more excellent items, and after 10 minutes the Ice Queen Boss appears. The rules can be changed in the plugin Dungeon.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        IceQueenDungeon.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
