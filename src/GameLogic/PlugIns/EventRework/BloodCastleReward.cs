// <copyright file="BloodCastleReward.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

/// <summary>
/// The reward item of one level of Blood Castle, see <see cref="EventReworkConfiguration.BloodCastleRewards"/>.
/// </summary>
public class BloodCastleReward
{
    /// <summary>
    /// Gets or sets the level of Blood Castle.
    /// </summary>
    [Display(Name = "Blood Castle level")]
    public int GameLevel { get; set; }

    /// <summary>
    /// Gets or sets the kind of the reward item.
    /// </summary>
    [Display(Name = "Kind")]
    public BloodCastleRewardKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the number of the dungeon (1 = Devias, 2 = Tarkan, 3 = Kanturu), for <see cref="BloodCastleRewardKind.DungeonExcellentItem"/>.
    /// </summary>
    [Display(Name = "Dungeon number", Description = "For an excellent item of a dungeon: 1 = Devias, 2 = Tarkan, 3 = Kanturu (the numbers of the dungeon plugin).")]
    public byte DungeonNumber { get; set; }

    /// <summary>
    /// Gets or sets the tier of the ancient armor pieces and weapons by their drop level (1 = lowest), for
    /// <see cref="BloodCastleRewardKind.AncientArmorOrWeapon"/>. 0 means all of them.
    /// </summary>
    [Display(Name = "Ancient tier", Description = "For an ancient armor piece or weapon: the tier by drop level, 1 = lowest. 0 = all of them.")]
    public int AncientTier { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"BC {this.GameLevel}: {this.Kind}";
}
