// <copyright file="MarketLocks.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Market;

using System.Collections.Concurrent;

/// <summary>
/// The locks between the item market of the web portal and the game:
/// items which are offered on the market can't be moved in the game, and the market depot
/// of an account can't be opened while the market changes it.
/// </summary>
public static class MarketLocks
{
    /// <summary>
    /// The message which is shown when a player tries to move an item which is offered on the market.
    /// </summary>
    public const string ItemIsListedMessage = "Este ítem está publicado en el mercado. Cancelá la publicación en la web para sacarlo.";

    /// <summary>
    /// The message which is shown when a player tries to open the market depot while the market changes it.
    /// </summary>
    public const string DepotIsBusyMessage = "El mercado está moviendo ítems de tu depósito. Probá de nuevo en unos segundos.";

    private static readonly ConcurrentDictionary<Guid, byte> ListedItems = new();
    private static readonly ConcurrentDictionary<string, byte> BusyAccounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the item is offered on the market.
    /// </summary>
    /// <param name="itemId">The identifier of the item.</param>
    /// <returns><c>true</c>, if the item is offered on the market.</returns>
    public static bool IsItemListed(Guid itemId) => ListedItems.ContainsKey(itemId);

    /// <summary>
    /// Locks the item, because it's offered on the market.
    /// </summary>
    /// <param name="itemId">The identifier of the item.</param>
    public static void LockItem(Guid itemId) => ListedItems.TryAdd(itemId, 0);

    /// <summary>
    /// Unlocks the item, because it's not offered on the market anymore.
    /// </summary>
    /// <param name="itemId">The identifier of the item.</param>
    public static void UnlockItem(Guid itemId) => ListedItems.TryRemove(itemId, out _);

    /// <summary>
    /// Determines whether the market currently changes the market depot of the account.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <returns><c>true</c>, if the market currently changes the market depot.</returns>
    public static bool IsAccountBusy(string loginName) => BusyAccounts.ContainsKey(loginName);

    /// <summary>
    /// Marks the account as busy, while the market changes its market depot.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <returns><c>true</c>, if the account was marked; <c>false</c>, if it's already busy.</returns>
    public static bool TryMarkAccountBusy(string loginName) => BusyAccounts.TryAdd(loginName, 0);

    /// <summary>
    /// Releases the mark of <see cref="TryMarkAccountBusy"/>.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    public static void ReleaseAccount(string loginName) => BusyAccounts.TryRemove(loginName, out _);
}
