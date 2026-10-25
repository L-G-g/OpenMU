// <copyright file="IShowLootRollPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Interface of a view which shows the rolls of need, greed or pass for the drops of a dungeon,
/// e.g. the window of loot.dll in the PeroniaMU client. Without it, the players choose with chat commands.
/// </summary>
public interface IShowLootRollPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a roll which waits for the choice of the player.
    /// </summary>
    /// <param name="rollId">The id of the roll, as it's used by the chat commands.</param>
    /// <param name="item">The rolled item.</param>
    /// <param name="description">The description of the item, e.g. "Silk Helm +7 (exc 3 opc)".</param>
    /// <param name="duration">The time the player has to choose.</param>
    /// <param name="canNeed">If set to <c>true</c>, the player can choose need.</param>
    ValueTask ShowLootRollAsync(int rollId, Item item, string description, TimeSpan duration, bool canNeed);

    /// <summary>
    /// Closes a roll, because the player chose or it ended.
    /// </summary>
    /// <param name="rollId">The id of the roll.</param>
    ValueTask CloseLootRollAsync(int rollId);
}
