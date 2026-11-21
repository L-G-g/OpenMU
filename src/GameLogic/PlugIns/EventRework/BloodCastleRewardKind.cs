// <copyright file="BloodCastleRewardKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

/// <summary>
/// The kinds of reward items of Blood Castle.
/// </summary>
public enum BloodCastleRewardKind
{
    /// <summary>
    /// An excellent item which the dungeon of <see cref="BloodCastleReward.DungeonNumber"/> drops
    /// (of the zones of its drop maps).
    /// </summary>
    DungeonExcellentItem,

    /// <summary>
    /// An ancient armor piece, weapon or shield of the <see cref="BloodCastleReward.AncientTier"/>.
    /// </summary>
    AncientArmorOrWeapon,

    /// <summary>
    /// An ancient pendant or ring.
    /// </summary>
    AncientJewelry,
}
