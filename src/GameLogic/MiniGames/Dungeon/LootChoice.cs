// <copyright file="LootChoice.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The choice of a player for a rolled item of a dungeon.
/// </summary>
public enum LootChoice
{
    /// <summary>
    /// The player doesn't want the item.
    /// </summary>
    Pass,

    /// <summary>
    /// The player wants the item for another character or to sell it.
    /// </summary>
    Greed,

    /// <summary>
    /// The player needs the item for its own character. Only allowed if the character class can use the item.
    /// </summary>
    Need,
}
