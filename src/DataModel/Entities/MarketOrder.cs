// <copyright file="MarketOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An order of the item market of the web portal: an offer to sell one item of the market depot,
/// or a request to buy items which meet some minimum requirements.
/// </summary>
/// <remarks>
/// Like the coin grants, it's a separate aggregate which refers to its account by identifier,
/// so that the portal can add it without loading the account.
/// </remarks>
[AggregateRoot]
public class MarketOrder
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account which placed the order.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the name which is shown on the market for the account, usually the name of a character.
    /// </summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind of the order.
    /// </summary>
    public MarketOrderKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the state of the order.
    /// </summary>
    public MarketOrderState State { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the offered item in the market depot of the seller.
    /// Only set for a sell order.
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Gets or sets the group of the item definition.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the item definition within its group.
    /// </summary>
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the name of the item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item level. For a buy order, it's the minimum level.
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets the number of excellent options. For a buy order, it's the minimum number.
    /// </summary>
    public byte ExcellentOptionCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck. For a buy order, it's required.
    /// </summary>
    public bool HasLuck { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has a skill. For a buy order, it's required.
    /// </summary>
    public bool HasSkill { get; set; }

    /// <summary>
    /// Gets or sets the level of the additional option (e.g. 3 for +12). For a buy order, it's the minimum level.
    /// </summary>
    public byte OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is an ancient item.
    /// </summary>
    public bool IsAncient { get; set; }

    /// <summary>
    /// Gets or sets the number of sockets.
    /// </summary>
    public byte SocketCount { get; set; }

    /// <summary>
    /// Gets or sets the description of the item options, one per line, as the client shows them.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the price in WCoin (C) for one item.
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// Gets or sets the number of requested items. It's always 1 for a sell order.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the number of items which are still requested or offered.
    /// </summary>
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// Gets or sets the timestamp at which the order was placed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp at which the order expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp at which the order was closed; <c>null</c>, if it's still open.
    /// </summary>
    public DateTime? ClosedAt { get; set; }
}
