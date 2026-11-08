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
    /// Gets or sets the additional requirements of the quests by their number (tier), e.g. resets.
    /// </summary>
    [Display(Name = "Tier requirements", Description = "Additional requirements of the skill quests by their number (tier): minimum resets, and whether the classes with a first class change must have done it. The minimum level is defined by the quest itself.")]
    public List<SkillQuestTierRequirement> TierRequirements { get; set; } =
    [
        new() { QuestNumber = 3, MinimumResets = 10 },
        new() { QuestNumber = 4, MinimumResets = 25, RequiresClassChange = true },
    ];
}

/// <summary>
/// Additional requirements of the skill quests with a number (tier).
/// </summary>
public class SkillQuestTierRequirement
{
    /// <summary>
    /// Gets or sets the number of the quests (the tier).
    /// </summary>
    [Display(Name = "Quest number (tier)")]
    public short QuestNumber { get; set; }

    /// <summary>
    /// Gets or sets the minimum resets.
    /// </summary>
    [Display(Name = "Minimum resets")]
    public int MinimumResets { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a class with a first class change (wizard, knight, elf, summoner) must
    /// have done it. Classes without one (magic gladiator, dark lord, rage fighter) don't need it.
    /// </summary>
    [Display(Name = "Requires the first class change")]
    public bool RequiresClassChange { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"Quest {this.QuestNumber}: {this.MinimumResets} resets{(this.RequiresClassChange ? ", class change" : string.Empty)}";
}
