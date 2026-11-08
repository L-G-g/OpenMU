// <copyright file="PortalMarketDtos.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

/// <summary>An item definition which can be traded on the market.</summary>
public sealed record PortalMarketCatalogItem(string Key, byte Group, short Number, string Name, string Category, byte Width, byte Height, IReadOnlyList<string> Classes, bool CanHaveExcellent);

/// <summary>An order of the market, as everybody sees it.</summary>
public sealed record PortalMarketOrder(
    Guid Id,
    string Kind,
    string State,
    string OwnerName,
    string OwnerStatus,
    bool IsMine,
    string ItemKey,
    string ItemName,
    string Category,
    byte Level,
    byte ExcellentOptionCount,
    bool HasLuck,
    bool HasSkill,
    byte OptionLevel,
    bool IsAncient,
    byte SocketCount,
    IReadOnlyList<string> Options,
    int Price,
    int Quantity,
    int RemainingQuantity,
    DateTime CreatedAt,
    DateTime ExpiresAt);

/// <summary>The price statistics of an item.</summary>
public sealed record PortalMarketStats(int? Median48h, int? Lowest48h, int Sold48h, IReadOnlyList<PortalMarketPricePoint> Days);

/// <summary>The median price and the number of trades of an item on one day.</summary>
public sealed record PortalMarketPricePoint(DateOnly Day, int Median, int Minimum, int Maximum, int Count);

/// <summary>The page of one item of the market.</summary>
public sealed record PortalMarketItemPage(PortalMarketCatalogItem Item, IReadOnlyList<PortalMarketOrder> Sell, IReadOnlyList<PortalMarketOrder> Buy, PortalMarketStats Stats);

/// <summary>A trade of the market.</summary>
public sealed record PortalMarketTrade(DateTime At, string ItemKey, string ItemName, byte Level, byte ExcellentOptionCount, int Price, string SellerName, string BuyerName);

/// <summary>An item in the market depot of the logged in account.</summary>
public sealed record PortalDepotItem(Guid Id, string ItemKey, string ItemName, byte Level, byte ExcellentOptionCount, bool HasLuck, bool HasSkill, byte OptionLevel, bool IsAncient, byte SocketCount, IReadOnlyList<string> Options, bool IsTradable, Guid? ListedOrderId);

/// <summary>The market state of the logged in account.</summary>
public sealed record PortalMyMarket(
    int Balance,
    int Reserved,
    decimal CommissionPercent,
    int MaximumOrders,
    IReadOnlyList<string> CharacterNames,
    IReadOnlyList<PortalDepotItem> Depot,
    bool DepotReadable,
    IReadOnlyList<PortalMarketOrder> Orders,
    IReadOnlyList<PortalMarketTrade> History);

/// <summary>The request to offer an item of the market depot.</summary>
public sealed class MarketSellRequest
{
    /// <summary>Gets or sets the identifier of the item in the market depot.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the price in WCoin.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets the character name which is shown as seller.</summary>
    public string? OwnerName { get; set; }
}

/// <summary>The request to place a buy order.</summary>
public sealed class MarketBuyOrderRequest
{
    /// <summary>Gets or sets the key of the item definition, "group-number".</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum item level.</summary>
    public byte MinimumLevel { get; set; }

    /// <summary>Gets or sets the minimum number of excellent options.</summary>
    public byte MinimumExcellentOptions { get; set; }

    /// <summary>Gets or sets a value indicating whether luck is required.</summary>
    public bool RequiresLuck { get; set; }

    /// <summary>Gets or sets a value indicating whether a skill is required.</summary>
    public bool RequiresSkill { get; set; }

    /// <summary>Gets or sets the minimum level of the additional option.</summary>
    public byte MinimumOptionLevel { get; set; }

    /// <summary>Gets or sets the price in WCoin for one item.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets the number of requested items.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Gets or sets the character name which is shown as buyer.</summary>
    public string? OwnerName { get; set; }
}

/// <summary>The request to change an own order.</summary>
public sealed class MarketChangeRequest
{
    /// <summary>Gets or sets the new price, if it changes.</summary>
    public int? Price { get; set; }

    /// <summary>Gets or sets a value indicating whether the order is hidden, if it changes.</summary>
    public bool? Hidden { get; set; }
}

/// <summary>The request to sell an item of the market depot to a buy order.</summary>
public sealed class MarketFillRequest
{
    /// <summary>Gets or sets the identifier of the item in the market depot.</summary>
    public Guid ItemId { get; set; }
}
