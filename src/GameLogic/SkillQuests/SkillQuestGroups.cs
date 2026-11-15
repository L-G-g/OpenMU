// <copyright file="SkillQuestGroups.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

using MUnique.OpenMU.DataModel.Configuration.Quests;

/// <summary>
/// The quest groups of the skill quests of PeroniaMU (see <see cref="SkillQuestFeaturePlugIn"/>).
/// </summary>
/// <remarks>
/// The skill quests are normal <see cref="QuestDefinition"/>s of an NPC, but their group is in the range of
/// <see cref="First"/> to <see cref="Last"/>. The original quest system of the client doesn't know them, so they're
/// never sent with its packets; they're shown with the quest marks and the quest window of quests.dll.
/// </remarks>
public static class SkillQuestGroups
{
    /// <summary>
    /// The first quest group of the skill quests.
    /// </summary>
    public const short First = 1000;

    /// <summary>
    /// The last quest group of the skill quests.
    /// </summary>
    public const short Last = 1999;

    /// <summary>
    /// Determines whether the group is a group of the skill quests.
    /// </summary>
    /// <param name="group">The quest group.</param>
    /// <returns><c>true</c>, if it's a skill quest group; otherwise, <c>false</c>.</returns>
    public static bool IsSkillQuestGroup(short group) => group is >= First and <= Last;

    /// <summary>
    /// Determines whether the quest is a skill quest.
    /// </summary>
    /// <param name="quest">The quest.</param>
    /// <returns><c>true</c>, if it's a skill quest; otherwise, <c>false</c>.</returns>
    public static bool IsSkillQuest(this QuestDefinition? quest) => quest is not null && IsSkillQuestGroup(quest.Group);
}
