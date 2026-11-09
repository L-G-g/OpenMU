// <copyright file="ISkillQuestViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// A view plugin which shows the skill quests to a player, for quests.dll of the PeroniaMU client.
/// </summary>
public interface ISkillQuestViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the marks above the NPCs with skill quests near the player.
    /// </summary>
    /// <param name="marks">The NPCs and their marks.</param>
    /// <returns>The task.</returns>
    ValueTask ShowMarksAsync(IReadOnlyList<(NonPlayerCharacter Npc, SkillQuestMark Mark)> marks);

    /// <summary>
    /// Shows the active skill quests and their progress.
    /// </summary>
    /// <param name="lines">The lines of the tracker; empty, if there is no active skill quest.</param>
    /// <returns>The task.</returns>
    ValueTask ShowTrackerAsync(IReadOnlyList<SkillQuestLine> lines);

    /// <summary>
    /// Shows the quest window of an NPC.
    /// </summary>
    /// <param name="npc">The NPC.</param>
    /// <param name="dialog">The content of the window.</param>
    /// <returns>The task.</returns>
    ValueTask ShowDialogAsync(NonPlayerCharacter npc, SkillQuestDialog dialog);
}
