// <copyright file="SkillQuestConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

/// <summary>
/// The configuration of the <see cref="SkillQuestFeaturePlugIn"/>.
/// </summary>
public class SkillQuestConfiguration
{
    /// <summary>
    /// Gets or sets the range around the player in which the NPCs with skill quests get a mark.
    /// </summary>
    [Display(Name = "Mark range", Description = "The range (in tiles) around the player in which the NPCs with skill quests get a mark.")]
    public int MarkRange { get; set; } = 18;

    /// <summary>
    /// Gets or sets the maximum distance between the player and the NPC to open its quest window.
    /// </summary>
    [Display(Name = "Interaction range", Description = "The maximum distance (in tiles) between the player and the NPC to open its quest window, accept or deliver a quest.")]
    public int InteractionRange { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether the kills of a party member on the same map count for the whole party.
    /// </summary>
    [Display(Name = "Count party kills", Description = "If enabled, the kills of a party member (or its summoned monster or spectre) on the same map count for every member of the party.")]
    public bool CountPartyKills { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval in which the marks are updated.
    /// </summary>
    [Display(Name = "Mark interval (seconds)", Description = "The interval in which the marks above the NPCs are updated.")]
    public int MarkIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the interval in which unchanged marks are sent again.
    /// </summary>
    [Display(Name = "Mark repeat (seconds)", Description = "Unchanged marks are sent again after this time; the client forgets a mark which isn't repeated within 6 seconds.")]
    public int MarkRepeatSeconds { get; set; } = 3;

    /// <summary>
    /// Gets or sets the minimum resets of the third quests (tier 3).
    /// </summary>
    [Display(Name = "Tier 3: minimum resets", Description = "The minimum resets to start a skill quest with the number 3.")]
    public int Tier3MinimumResets { get; set; } = 10;

    /// <summary>
    /// Gets or sets the minimum resets of the fourth quests (tier 4).
    /// </summary>
    [Display(Name = "Tier 4: minimum resets", Description = "The minimum resets to start a skill quest with the number 4.")]
    public int Tier4MinimumResets { get; set; } = 25;

    /// <summary>
    /// Gets or sets a value indicating whether the fourth quests require the first class change.
    /// </summary>
    [Display(Name = "Tier 4: requires the first class change", Description = "If enabled, wizards, knights, elves and summoners need their first class change to start a skill quest with the number 4. Magic gladiators, dark lords and rage fighters don't have one.")]
    public bool Tier4RequiresClassChange { get; set; } = true;

    /// <summary>
    /// Gets the minimum resets of a quest number.
    /// </summary>
    /// <param name="questNumber">The quest number (tier).</param>
    /// <returns>The minimum resets.</returns>
    public int GetMinimumResets(short questNumber) => questNumber switch
    {
        3 => this.Tier3MinimumResets,
        >= 4 => this.Tier4MinimumResets,
        _ => 0,
    };

    /// <summary>
    /// Determines whether a quest number requires the first class change.
    /// </summary>
    /// <param name="questNumber">The quest number (tier).</param>
    /// <returns><c>true</c>, if it requires the first class change.</returns>
    public bool RequiresClassChange(short questNumber) => questNumber >= 4 && this.Tier4RequiresClassChange;
}
