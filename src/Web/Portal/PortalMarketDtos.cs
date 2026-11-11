// <copyright file="PortalMarketDtos.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

/// <summary>An item definition which can be traded on the market.</summary>
public sealed record PortalMarketCatalogItem(string Key, byte Group, short Number, string Name, string Category, byte Width, byte Height, IReadOnlyList<string> Classes, bool CanHaveExcellent, IReadOnlyList<string> ExcellentOptions, string? Image);

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
    DateTime ExpiresAt,
    string? Image);

/// <summary>The price statistics of an item.</summary>
public sealed record PortalMarketStats(int? Median48h, int? Lowest48h, int Sold48h, IReadOnlyList<PortalMarketPricePoint> Days);

/// <summary>The median price and the number of trades of an item on one day.</summary>
public sealed record PortalMarketPricePoint(DateOnly Day, int Median, int Minimum, int Maximum, int Count);

/// <summary>The page of one item of the market.</summary>
public sealed record PortalMarketItemPage(PortalMarketCatalogItem Item, IReadOnlyList<PortalMarketOrder> Sell, IReadOnlyList<PortalMarketOrder> Buy, PortalMarketStats Stats);

/// <summary>A trade of the market.</summary>
public sealed record PortalMarketTrade(DateTime At, string ItemKey, string ItemName, byte Level, byte ExcellentOptionCount, int Price, string SellerName, string BuyerName, string? Image);

/// <summary>An item in the market depot of the logged in account.</summary>
public sealed record PortalDepotItem(Guid Id, string ItemKey, string ItemName, byte Level, byte ExcellentOptionCount, bool HasLuck, bool HasSkill, byte OptionLevel, bool IsAncient, byte SocketCount, IReadOnlyList<string> Options, bool IsTradable, Guid? ListedOrderId, string? Image);

/// <summary>A sale or an offer of an item, compared to a wanted item.</summary>
/// <param name="Kind">"sold" or "selling".</param>
/// <param name="At">When it was sold or offered.</param>
/// <param name="Level">The level.</param>
/// <param name="ExcellentOptionCount">The number of excellent options.</param>
/// <param name="Options">The options, one per line.</param>
/// <param name="Price">The price.</param>
/// <param name="OptionDistance">How many options differ from the wanted item.</param>
/// <param name="LevelDistance">How many levels it differs from the wanted item.</param>
/// <param name="IsExact">If set to <c>true</c>, it's exactly like the wanted item.</param>
public sealed record PortalMarketReference(string Kind, DateTime At, byte Level, byte ExcellentOptionCount, IReadOnlyList<string> Options, int Price, int OptionDistance, int LevelDistance, bool IsExact);

/// <summary>The sales and offers which are the most like a wanted item.</summary>
public sealed record PortalMarketPriceGuide(string ItemKey, int? SuggestedPrice, bool HasExact, IReadOnlyList<PortalMarketReference> References);

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

    /// <summary>Gets or sets the excellent options which the item must have, by their market names.</summary>
    public List<string>? ExcellentOptions { get; set; }

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

/// <summary>A wanted item, to compare prices.</summary>
public sealed class MarketPriceQuery
{
    /// <summary>Gets or sets the level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the excellent options, by their market names.</summary>
    public List<string>? ExcellentOptions { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets the level of the additional option (1 = +4).</summary>
    public byte OptionLevel { get; set; }
}
