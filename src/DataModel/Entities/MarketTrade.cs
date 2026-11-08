// <copyright file="MarketTrade.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A completed trade of the item market of the web portal. It's the history of the market,
/// from which the price statistics are calculated.
/// </summary>
[AggregateRoot]
public class MarketTrade
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the order which was taken by the trade.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the traded item.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account of the seller.
    /// </summary>
    public Guid SellerAccountId { get; set; }

    /// <summary>
    /// Gets or sets the shown name of the seller.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the account of the buyer.
    /// </summary>
    public Guid BuyerAccountId { get; set; }

    /// <summary>
    /// Gets or sets the shown name of the buyer.
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

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
    /// Gets or sets the item level.
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets the number of excellent options.
    /// </summary>
    public byte ExcellentOptionCount { get; set; }

    /// <summary>
    /// Gets or sets the options of the sold item, one per line, like <see cref="MarketOrder.Description"/>.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the price in WCoin (C) which the buyer paid.
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// Gets or sets the commission in WCoin (C) which the market kept; the seller got the price minus the commission.
    /// </summary>
    public int Commission { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the trade.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
