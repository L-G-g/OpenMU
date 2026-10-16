// <copyright file="AddEliteQuestMonstersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the elite monsters which are the only ones dropping the items of the first class change quest.
/// See <see cref="EliteQuestMonsters"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the quest plays instead of correcting data.
/// It should run after <see cref="GroupMonsterSpotsUpdatePlugIn"/>, so that the elite monsters spawn at spots of their originals.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AddEliteQuestMonstersUpdatePlugIn_Name), Description = nameof(PlugInResources.AddEliteQuestMonstersUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("382C8DF7-1207-448C-80A8-27BCFDFA30F9")]
public class AddEliteQuestMonstersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Elite Monsters For The First Class Change Quest";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Shadow Elite (Lost Tower), which is the only monster dropping the Scroll of Emperor, and the Death Knight Elite (Lost Tower), Lizard King Elite (Atlans) and Mutant Elite (Tarkan), which are the only ones dropping the second item of the quest. Each spawns alone at 3 places of its map, respawns after 5 minutes and drops the item to a player who needs it.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>The drops rely on the quest items being marked as such.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddQuestItemLimitPlugIn).GUID];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        EliteQuestMonsters.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
