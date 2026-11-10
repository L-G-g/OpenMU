// <copyright file="StoryNpcTalkPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Starts the scene of an actor of the story mode which has no window of its own (the stranger), when a player clicks
/// on it. The other actors (Lumen, Sebina) keep their windows: their scenes start with a click on the mark above them.
/// </summary>
[Guid("9B3D6F28-4A1C-4E75-8D2B-6C7E1F5A3D94")]
[PlugIn]
[Display(Name = "Story mode: talk to the stranger", Description = "Starts the scene of the stranger of the story mode when a player clicks on it.")]
public class StoryNpcTalkPlugIn : IPlayerTalkToNpcPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (eventArgs.HasBeenHandled
            || StoryModeFeaturePlugIn.GetActor(npc.Definition) is null
            || StoryModeFeaturePlugIn.Get(player.GameContext) is not { } story)
        {
            return;
        }

        if (await story.TryShowSceneAsync(player, npc, false).ConfigureAwait(false))
        {
            eventArgs.HasBeenHandled = true;
        }
    }
}
