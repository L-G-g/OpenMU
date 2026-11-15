// <copyright file="IStorySceneViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// A view plugin which shows the scenes of the story mode to a player, for cinematicas.dll of the PeroniaMU client.
/// </summary>
public interface IStorySceneViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a scene: the camera looks at the NPC in first person, and the NPC says the pages of the scene.
    /// </summary>
    /// <param name="npc">The NPC which talks.</param>
    /// <param name="scene">The scene.</param>
    /// <returns>The task.</returns>
    ValueTask ShowSceneAsync(NonPlayerCharacter npc, StoryScene scene);
}
