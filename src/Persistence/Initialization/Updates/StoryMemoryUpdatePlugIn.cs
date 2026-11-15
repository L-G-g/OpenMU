// <copyright file="StoryMemoryUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the seer of Atlans and the stage of the memories of the story mode, see <see cref="StoryMemoryData"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E5B8C3F7-9D4A-4F2B-A6E3-8B1D2F4A7C59")]
public class StoryMemoryUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Story Memories";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The data of the memories of the story mode (plugin 'Story mode', cinematicas.dll in the client): the seer, an NPC at the entrance of Atlans, and the stage of the first memory, an instance of the throne room of the castle of Devias per player.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        StoryMemoryData.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
