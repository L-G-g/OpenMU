// <copyright file="StoryModeUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the data of the story mode of PeroniaMU, see <see cref="StoryModeData"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("D4A7B2E6-8C3F-4E19-B5D2-7A9C1E3F6B48")]
public class StoryModeUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Story Mode";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The data of the story mode (plugin 'Story mode', cinematicas.dll and quests.dll in the client): the attributes which keep the progress of the characters, and the stranger, an NPC in the square of Lorencia. Enable the plugin 'Story mode' afterwards: it locks Devias, Dungeon, Lost Tower and the maps of the old world until the story opens them.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        StoryModeData.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
