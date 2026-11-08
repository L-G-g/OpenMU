// <copyright file="SkillQuestMark.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

/// <summary>
/// The mark which is shown above an NPC with skill quests.
/// </summary>
public enum SkillQuestMark : byte
{
    /// <summary>
    /// No mark.
    /// </summary>
    None = 0,

    /// <summary>
    /// The NPC has a quest which the player can start (golden exclamation mark).
    /// </summary>
    Available = 1,

    /// <summary>
    /// The player has an active quest of the NPC which isn't completed yet (gray question mark).
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The player has completed the objectives of a quest of the NPC and can deliver it (golden question mark).
    /// </summary>
    Ready = 3,
}

/// <summary>
/// The color of a line of text of the quest window or the quest tracker.
/// </summary>
public enum SkillQuestTextColor : byte
{
    /// <summary>
    /// Normal text.
    /// </summary>
    White = 0,

    /// <summary>
    /// Highlighted text, e.g. a title or a reward.
    /// </summary>
    Gold = 1,

    /// <summary>
    /// Something which is done.
    /// </summary>
    Green = 2,

    /// <summary>
    /// Less important text.
    /// </summary>
    Gray = 3,
}

/// <summary>
/// The action which the quest window offers with its button.
/// </summary>
public enum SkillQuestAction : byte
{
    /// <summary>
    /// No action, just close.
    /// </summary>
    None = 0,

    /// <summary>
    /// The player can accept the quest.
    /// </summary>
    Accept = 1,

    /// <summary>
    /// The player can deliver the completed quest.
    /// </summary>
    Deliver = 2,
}

/// <summary>
/// A line of text of the quest window or the quest tracker.
/// </summary>
/// <param name="Color">The color.</param>
/// <param name="Text">The text.</param>
public readonly record struct SkillQuestLine(SkillQuestTextColor Color, string Text);

/// <summary>
/// The content of the quest window of an NPC.
/// </summary>
/// <param name="Title">The title, e.g. the name of the NPC.</param>
/// <param name="Lines">The lines of text.</param>
/// <param name="Action">The action of the button.</param>
public sealed record SkillQuestDialog(string Title, IReadOnlyList<SkillQuestLine> Lines, SkillQuestAction Action);
