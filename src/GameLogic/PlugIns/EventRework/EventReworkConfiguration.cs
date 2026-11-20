// <copyright file="EventReworkConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// The configuration of the <see cref="EventReworkPlugIn"/>.
/// </summary>
public class EventReworkConfiguration
{
    /// <summary>
    /// Gets or sets the minimum resets of each level of the events, starting with level 1.
    /// A character enters the highest level whose minimum it reached, no matter its character level and the level of its ticket.
    /// </summary>
    [Display(Name = "Minimum resets per event level", Description = "The minimum resets of each level of the events, starting with level 1 (e.g. 0, 30, 75 means: level 1 from 0 resets, level 2 from 30, level 3 from 75). A character enters the highest level whose minimum it reached, no matter its character level and the level of its ticket.")]
    public IList<int> MinimumResetsPerLevel { get; set; } = new List<int> { 0, 30, 75, 150, 220, 300, 360 };

    /// <summary>
    /// Gets or sets a value indicating whether the level of Devil Square is chosen by the resets.
    /// </summary>
    [Display(Name = "Devil Square level by resets")]
    public bool DevilSquareByResets { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the level of Blood Castle is chosen by the resets.
    /// </summary>
    [Display(Name = "Blood Castle level by resets")]
    public bool BloodCastleByResets { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the level of Chaos Castle is chosen by the resets.
    /// </summary>
    [Display(Name = "Chaos Castle level by resets")]
    public bool ChaosCastleByResets { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether talking to Charon (Devil Square) or to the Messenger of Archangel (Blood Castle)
    /// enters the event directly with a ticket of any level, instead of opening the window of the game client, which asks
    /// for the level and only accepts a ticket of the chosen level.
    /// </summary>
    [Display(Name = "Direct entry at the NPC", Description = "Talking to Charon (Devil Square) or to the Messenger of Archangel (Blood Castle) enters the event of the resets directly, with a ticket of any level, instead of opening the window of the game client, which asks for a level.")]
    public bool DirectEntryAtNpc { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a Lost Kalima Map of any level opens the Kalima of the resets of the player who drops it.
    /// </summary>
    [Display(Name = "Kalima level by resets", Description = "A Lost Kalima Map of any level opens the Kalima of the resets of the player who drops it.")]
    public bool KalimaByResets { get; set; } = true;

    /// <summary>
    /// Gets or sets the experience multiplier of Devil Square when the game starts. It rises evenly up to
    /// <see cref="DevilSquareEndExperienceMultiplier"/> at the end of the game duration.
    /// </summary>
    [Display(Name = "Devil Square: experience multiplier at the start", Description = "Multiplies the experience of the kills in Devil Square. It rises evenly up to the multiplier at the end of the game. 1 = normal experience.")]
    public double DevilSquareStartExperienceMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the experience multiplier of Devil Square at the end of the game duration.
    /// </summary>
    [Display(Name = "Devil Square: experience multiplier at the end")]
    public double DevilSquareEndExperienceMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets a value indicating whether the monsters of Devil Square drop no items and no money.
    /// </summary>
    [Display(Name = "Devil Square: no drops", Description = "The monsters of Devil Square drop no items and no money. The rewards of the score table stay.")]
    public bool DevilSquareWithoutDrops { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the monsters of Chaos Castle give no experience.
    /// </summary>
    [Display(Name = "Chaos Castle: monsters without experience")]
    public bool ChaosCastleWithoutExperience { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether each killed monster of Chaos Castle drops one of the <see cref="ChaosCastleJewels"/>,
    /// instead of the few jewels of the original Chaos Castle.
    /// </summary>
    [Display(Name = "Chaos Castle: one jewel per kill", Description = "Each killed monster of Chaos Castle drops one jewel of the table, chosen by its weight, instead of the few jewels of the original Chaos Castle.")]
    public bool ChaosCastleJewelPerKill { get; set; } = true;

    /// <summary>
    /// Gets or sets the jewels which the monsters of Chaos Castle drop, see <see cref="ChaosCastleJewelPerKill"/>.
    /// </summary>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(Name = "Chaos Castle: jewels", Description = "The jewels which the monsters of Chaos Castle drop (item group and number), with their weights: a jewel with weight 30 drops three times as often as one with weight 10.")]
    public ICollection<EventJewelDrop> ChaosCastleJewels { get; set; } = CreateDefaultJewels();

    /// <summary>
    /// Gets or sets a value indicating whether the player who delivers the weapon in Blood Castle gets the item of the
    /// <see cref="BloodCastleRewards"/> of the level. It replaces the item reward of the original Blood Castle (a Jewel of Chaos for the party).
    /// </summary>
    [Display(Name = "Blood Castle: item reward of the table", Description = "The player who delivers the weapon to the Archangel gets the item of the table for the level of the castle. It replaces the item reward of the original Blood Castle (a Jewel of Chaos for the party). Experience and zen rewards stay.")]
    public bool BloodCastleItemRewards { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the reward item of Blood Castle is one which the class of the winner can use.
    /// </summary>
    [Display(Name = "Blood Castle: item of the class of the winner")]
    public bool BloodCastleRewardOfOwnClass { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of excellent options of the excellent reward items of Blood Castle.
    /// </summary>
    [Display(Name = "Blood Castle: minimum excellent options")]
    public int BloodCastleMinimumExcellentOptions { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of tiers into which the ancient armor pieces and weapons are split by their drop level,
    /// see <see cref="BloodCastleReward.AncientTier"/>.
    /// </summary>
    [Display(Name = "Blood Castle: ancient tiers", Description = "Into how many tiers the ancient armor pieces and weapons are split by their drop level (lowest first).")]
    public int BloodCastleAncientTierCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the reward item of each level of Blood Castle.
    /// </summary>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(Name = "Blood Castle: rewards", Description = "The reward item of each level of Blood Castle.")]
    public ICollection<BloodCastleReward> BloodCastleRewards { get; set; } = CreateDefaultBloodCastleRewards();

    /// <summary>
    /// Gets the event level (1 or more) of a number of resets.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <returns>The level, starting at 1.</returns>
    public int GetLevelOfResets(int resets)
    {
        var level = this.MinimumResetsPerLevel?.Count(minimum => resets >= minimum) ?? 0;
        return Math.Max(level, 1);
    }

    private static ICollection<EventJewelDrop> CreateDefaultJewels() => new List<EventJewelDrop>
    {
        new() { Name = "Jewel of Bless", Group = 14, Number = 13, Weight = 35 },
        new() { Name = "Jewel of Soul", Group = 14, Number = 14, Weight = 35 },
        new() { Name = "Jewel of Life", Group = 14, Number = 16, Weight = 10 },
        new() { Name = "Jewel of Chaos", Group = 12, Number = 15, Weight = 12 },
        new() { Name = "Jewel of Creation", Group = 14, Number = 22, Weight = 8 },
    };

    private static ICollection<BloodCastleReward> CreateDefaultBloodCastleRewards() => new List<BloodCastleReward>
    {
        new() { GameLevel = 1, Kind = BloodCastleRewardKind.DungeonExcellentItem, DungeonNumber = 1 },
        new() { GameLevel = 2, Kind = BloodCastleRewardKind.DungeonExcellentItem, DungeonNumber = 2 },
        new() { GameLevel = 3, Kind = BloodCastleRewardKind.DungeonExcellentItem, DungeonNumber = 3 },
        new() { GameLevel = 4, Kind = BloodCastleRewardKind.AncientArmorOrWeapon, AncientTier = 1 },
        new() { GameLevel = 5, Kind = BloodCastleRewardKind.AncientArmorOrWeapon, AncientTier = 2 },
        new() { GameLevel = 6, Kind = BloodCastleRewardKind.AncientArmorOrWeapon, AncientTier = 3 },
        new() { GameLevel = 7, Kind = BloodCastleRewardKind.AncientJewelry },
        new() { GameLevel = 8, Kind = BloodCastleRewardKind.AncientJewelry },
    };
}
