// <copyright file="PortalMarketService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Market;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The item market of the portal. Items are offered from the market depot of an account, which is opened
/// at the Mercader in the game, and bought items arrive there. Prices are paid in WCoin (C).
/// </summary>
/// <remarks>
/// <para>
/// The coins only move by coin grants, which the game server applies to the balance of an account; the
/// available balance is the balance plus the pending grants. A buy order reserves its coins with a negative grant
/// when it's placed, and gets them back with a positive grant when it's cancelled or expires.
/// </para>
/// <para>
/// An item moves like in a trade between players: it's removed from the market depot of the seller, detached from
/// its persistence context, attached to the context of the buyer and added to the market depot of the buyer.
/// The account of a player who is online is changed in memory, while holding the persistence lock of the player;
/// an offline account is reserved at the login server, so that nobody logs in while it's changed. A trade is refused
/// while the market depot of the seller or the buyer is open in the game.
/// </para>
/// <para>
/// All changes run one after another under one lock. Offered items are locked by <see cref="MarketLocks"/>, so they
/// can't be moved in the game.
/// </para>
/// </remarks>
public sealed class PortalMarketService : IDisposable
{
    private const int MaximumPrice = 10_000_000;
    private const int MaximumBuyQuantity = 50;
    private static readonly TimeSpan TradeHistoryDuration = TimeSpan.FromDays(120);
    private static readonly TimeSpan WebActivityDuration = TimeSpan.FromMinutes(10);
    private static readonly string[] ClassFamilies = ["DW", "DK", "ELF", "MG", "DL", "SUM", "RF"];

    private readonly IPersistenceContextProvider _persistence;
    private readonly IDataSource<GameConfiguration> _configuration;
    private readonly ILoginServer _loginServer;
    private readonly IFriendServer _friendServer;
    private readonly IServerProvider _serverProvider;
    private readonly PortalSettings _settings;
    private readonly ILogger<PortalMarketService> _logger;
    private readonly SemaphoreSlim _lock = new(1);
    private readonly List<MarketOrder> _orders = [];
    private readonly List<MarketTrade> _trades = [];
    private readonly ConcurrentDictionary<Guid, DateTime> _webActivity = new();
    private readonly ConcurrentDictionary<string, Guid> _accountIds = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _images = PortalMarketImages.FindFileNames();

    private IReadOnlyList<PortalMarketCatalogItem>? _catalog;
    private Timer? _expiryTimer;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalMarketService"/> class.
    /// </summary>
    /// <param name="persistence">The persistence context provider.</param>
    /// <param name="configuration">The game configuration source.</param>
    /// <param name="loginServer">The login server.</param>
    /// <param name="friendServer">The friend server, which knows the online characters.</param>
    /// <param name="serverProvider">The server provider, which knows the game servers and their players.</param>
    /// <param name="settings">The portal settings.</param>
    /// <param name="logger">The logger.</param>
    public PortalMarketService(
        IPersistenceContextProvider persistence,
        IDataSource<GameConfiguration> configuration,
        ILoginServer loginServer,
        IFriendServer friendServer,
        IServerProvider serverProvider,
        PortalSettings settings,
        ILogger<PortalMarketService> logger)
    {
        this._persistence = persistence;
        this._configuration = configuration;
        this._loginServer = loginServer;
        this._friendServer = friendServer;
        this._serverProvider = serverProvider;
        this._settings = settings;
        this._logger = logger;
    }

    private int CommissionOf(int price)
        => (int)Math.Floor(price * Math.Clamp(this._settings.MarketCommissionPercent, 0, 100) / 100m);

    /// <summary>
    /// Loads the orders and the trade history, and locks the offered items.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task which completes when the market is loaded.</returns>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
            var orders = await context.GetAsync<MarketOrder>(cancellationToken).ConfigureAwait(false);
            var trades = await context.GetAsync<MarketTrade>(cancellationToken).ConfigureAwait(false);
            var since = DateTime.UtcNow - TradeHistoryDuration;
            this._orders.Clear();
            this._orders.AddRange(orders.Where(o => IsOpen(o) || o.ClosedAt > since));
            this._trades.Clear();
            this._trades.AddRange(trades.Where(t => t.CreatedAt > since).OrderBy(t => t.CreatedAt));
            foreach (var order in this._orders.Where(o => IsOpen(o) && o.Kind == MarketOrderKind.Sell && o.ItemId is not null))
            {
                MarketLocks.LockItem(order.ItemId!.Value);
            }

            await this.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            this._initialized = true;
            this._expiryTimer ??= new Timer(_ => this.OnExpiryTimer(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            this._logger.LogInformation("Market: loaded {orders} open orders and {trades} trades.", this._orders.Count(IsOpen), this._trades.Count);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Market: couldn't load the orders; the market is closed until the next start.");
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._expiryTimer?.Dispose();
        this._lock.Dispose();
    }

    /// <summary>
    /// Gets the item definitions which can be traded.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The catalog.</returns>
    public async ValueTask<IReadOnlyList<PortalMarketCatalogItem>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (this._catalog is { } catalog)
        {
            return catalog;
        }

        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        catalog = config.Items
            .Where(d => d.IsTradable && !string.IsNullOrWhiteSpace(Neutral(d.Name)))
            .OrderBy(d => d.Group).ThenBy(d => d.Number)
            .SelectMany(this.ToCatalogItems)
            .ToList();
        this._catalog = catalog;
        return catalog;
    }

    /// <summary>
    /// Gets the open orders of a kind, which aren't hidden.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="loginName">The login name of the viewer, if logged in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The orders, newest first.</returns>
    public async ValueTask<IReadOnlyList<PortalMarketOrder>> GetOrdersAsync(MarketOrderKind kind, string? loginName, CancellationToken cancellationToken)
    {
        var viewer = loginName is null ? (Guid?)null : await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
        var orders = await this.SnapshotAsync(o => o.State == MarketOrderState.Active && o.Kind == kind, cancellationToken).ConfigureAwait(false);
        var result = new List<PortalMarketOrder>(orders.Count);
        foreach (var order in orders.OrderByDescending(o => o.CreatedAt))
        {
            result.Add(await this.ToDtoAsync(order, viewer).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>
    /// Gets the page of an item: its open orders and its price statistics.
    /// </summary>
    /// <param name="key">The key of the item definition, "group-number".</param>
    /// <param name="loginName">The login name of the viewer, if logged in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, or <c>null</c> if the item doesn't exist.</returns>
    public async ValueTask<PortalMarketItemPage?> GetItemPageAsync(string key, string? loginName, CancellationToken cancellationToken)
    {
        var item = (await this.GetCatalogAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(c => c.Key == key);
        if (item is null)
        {
            return null;
        }

        var viewer = loginName is null ? (Guid?)null : await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
        var orders = await this.SnapshotAsync(o => o.State == MarketOrderState.Active && o.ItemGroup == item.Group && o.ItemNumber == item.Number && KeyOf(o) == key, cancellationToken).ConfigureAwait(false);
        var sell = new List<PortalMarketOrder>();
        var buy = new List<PortalMarketOrder>();
        foreach (var order in orders)
        {
            var dto = await this.ToDtoAsync(order, viewer).ConfigureAwait(false);
            (order.Kind == MarketOrderKind.Sell ? sell : buy).Add(dto);
        }

        sell = sell.OrderByDescending(o => o.OwnerStatus == "game").ThenBy(o => o.Price).ToList();
        buy = buy.OrderByDescending(o => o.Price).ToList();

        List<MarketTrade> trades;
        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            trades = this._trades.Where(t => t.ItemGroup == item.Group && t.ItemNumber == item.Number && KeyOf(t) == key).ToList();
        }
        finally
        {
            this._lock.Release();
        }

        return new PortalMarketItemPage(item, sell, buy, CalculateStats(trades));
    }

    /// <summary>
    /// Gets the latest trades.
    /// </summary>
    /// <param name="count">The maximum number of trades.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trades, newest first.</returns>
    public async ValueTask<IReadOnlyList<PortalMarketTrade>> GetLatestTradesAsync(int count, CancellationToken cancellationToken)
    {
        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return this._trades.AsEnumerable().Reverse().Take(Math.Clamp(count, 1, 100)).Select(ToTradeDto).ToList();
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <summary>
    /// Gets the market state of an account: its balance, its market depot, its orders and its trades.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state, or <c>null</c> if the account doesn't exist.</returns>
    public async ValueTask<PortalMyMarket?> GetMyMarketAsync(string loginName, CancellationToken cancellationToken)
    {
        var accountId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (accountId is not { } id)
        {
            return null;
        }

        this._webActivity[id] = DateTime.UtcNow;
        var orders = await this.SnapshotAsync(o => o.AccountId == id && (IsOpen(o) || o.ClosedAt > DateTime.UtcNow.AddDays(-30)), cancellationToken).ConfigureAwait(false);
        var listed = orders.Where(o => IsOpen(o) && o.ItemId is not null).ToDictionary(o => o.ItemId!.Value, o => o.Id);

        var depot = new List<PortalDepotItem>();
        var names = new List<string>();
        var balance = 0;
        var depotReadable = true;
        await using (var session = await this.OpenSessionAsync(loginName, false, cancellationToken).ConfigureAwait(false))
        {
            if (session is null)
            {
                return null;
            }

            await session.RunAsync(async () =>
            {
                names.AddRange(session.Account.Characters.OrderBy(c => c.CharacterSlot).Select(c => c.Name));
                foreach (var item in session.Account.MarketDepot?.Items.OrderBy(i => i.ItemSlot) ?? Enumerable.Empty<Item>())
                {
                    var info = Describe(item);
                    depot.Add(new PortalDepotItem(
                        item.GetId(),
                        info.Key,
                        info.Name,
                        info.Level,
                        info.ExcellentOptionCount,
                        info.HasLuck,
                        item.HasSkill,
                        info.OptionLevel,
                        info.IsAncient,
                        (byte)item.SocketCount,
                        info.Options,
                        item.Definition?.IsTradable ?? false,
                        listed.TryGetValue(item.GetId(), out var orderId) ? orderId : null,
                        item.Definition is { } d ? this.ImageOf(d.Group, d.Number, item.Level, info.ExcellentOptionCount > 0, info.IsAncient) : null));
                }

                balance = await this.GetBalanceAsync(session, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        var dtos = new List<PortalMarketOrder>();
        foreach (var order in orders.OrderByDescending(o => o.CreatedAt))
        {
            dtos.Add(await this.ToDtoAsync(order, id).ConfigureAwait(false));
        }

        List<PortalMarketTrade> history;
        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            history = this._trades.Where(t => t.SellerAccountId == id || t.BuyerAccountId == id).Reverse().Take(50).Select(ToTradeDto).ToList();
        }
        finally
        {
            this._lock.Release();
        }

        var reserved = orders.Where(o => IsOpen(o) && o.Kind == MarketOrderKind.Buy).Sum(o => o.Price * o.RemainingQuantity);
        return new PortalMyMarket(balance, reserved, this._settings.MarketCommissionPercent, this._settings.MarketMaximumOrders, names, depot, depotReadable, dtos, history);
    }

    /// <summary>
    /// Gets the sales and offers of an item which are the most like a wanted item: first the ones with the nearest
    /// options, then the ones with the nearest level. So there's a price for an item even if nobody sold one exactly like it.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="query">The wanted item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The price guide, or <c>null</c> if the item doesn't exist.</returns>
    public async ValueTask<PortalMarketPriceGuide?> GetPriceGuideAsync(string key, MarketPriceQuery query, CancellationToken cancellationToken)
    {
        var item = (await this.GetCatalogAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(c => c.Key == key);
        if (item is null)
        {
            return null;
        }

        var wanted = new HashSet<string>(query.ExcellentOptions ?? []);
        var references = new List<(PortalMarketReference Reference, DateTime At)>();

        PortalMarketReference Compare(string kind, DateTime at, byte level, byte excellentCount, string[] lines, int price)
        {
            var excellent = ExcellentLines(lines);

            // Older sales which don't know their options are compared by the number of excellent options.
            var excellentDistance = excellent.Length == 0 && excellentCount > 0
                ? Math.Abs(excellentCount - wanted.Count)
                : wanted.Count + excellent.Length - (2 * excellent.Count(wanted.Contains));
            var optionLevel = lines.Select(l => l.StartsWith("Opción +", StringComparison.Ordinal) && int.TryParse(l["Opción +".Length..], out var o) ? o / 4 : 0).DefaultIfEmpty(0).Max();
            var distance = excellentDistance
                           + (lines.Contains("Luck") != query.Luck ? 1 : 0)
                           + (lines.Contains("Skill") != query.Skill ? 1 : 0)
                           + (optionLevel != query.OptionLevel ? 1 : 0);
            var levelDistance = Math.Abs(level - query.Level);
            return new PortalMarketReference(kind, at, level, excellentCount, lines, price, distance, levelDistance, distance == 0 && levelDistance == 0);
        }

        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var trade in this._trades.Where(t => t.ItemGroup == item.Group && t.ItemNumber == item.Number && KeyOf(t) == key))
            {
                var description = trade.Description ?? this._orders.FirstOrDefault(o => o.Id == trade.OrderId && o.Kind == MarketOrderKind.Sell)?.Description;
                references.Add((Compare("sold", trade.CreatedAt, trade.Level, trade.ExcellentOptionCount, OptionLines(description), trade.Price), trade.CreatedAt));
            }

            foreach (var order in this._orders.Where(o => o.State == MarketOrderState.Active && o.Kind == MarketOrderKind.Sell && o.ItemGroup == item.Group && o.ItemNumber == item.Number && KeyOf(o) == key))
            {
                references.Add((Compare("selling", order.CreatedAt, order.Level, order.ExcellentOptionCount, OptionLines(order.Description), order.Price), order.CreatedAt));
            }
        }
        finally
        {
            this._lock.Release();
        }

        var sorted = references
            .OrderBy(r => r.Reference.OptionDistance)
            .ThenBy(r => r.Reference.LevelDistance)
            .ThenBy(r => r.Reference.Kind == "sold" ? 0 : 1)
            .ThenByDescending(r => r.At)
            .Select(r => r.Reference)
            .ToList();

        // The suggested price is the median of the nearest sales.
        var nearestSold = sorted.Where(r => r.Kind == "sold").ToList();
        int? suggested = null;
        if (nearestSold.Count > 0)
        {
            var best = nearestSold[0];
            var prices = nearestSold.Where(r => r.OptionDistance == best.OptionDistance && r.LevelDistance == best.LevelDistance).Select(r => r.Price).Order().ToList();
            suggested = prices[prices.Count / 2];
        }

        return new PortalMarketPriceGuide(item.Key, suggested, sorted.Any(r => r.IsExact), sorted.Take(10).ToList());
    }

    /// <summary>
    /// Offers an item of the market depot for a price.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the item was offered; otherwise, the error code.</returns>
    public async ValueTask<string?> SellAsync(string loginName, MarketSellRequest request, CancellationToken cancellationToken)
    {
        if (request.Price is < 1 or > MaximumPrice)
        {
            return "invalid_price";
        }

        return await this.WriteAsync(async () =>
        {
            var accountId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            if (accountId is not { } id)
            {
                return "unauthorized";
            }

            if (this.CountOpenOrders(id) >= this._settings.MarketMaximumOrders)
            {
                return "too_many_orders";
            }

            if (MarketLocks.IsItemListed(request.ItemId))
            {
                return "item_already_listed";
            }

            // Locked first, so that it can't be moved in the game while it's checked.
            MarketLocks.LockItem(request.ItemId);
            var keepLock = false;
            try
            {
                await using var session = await this.OpenSessionAsync(loginName, false, cancellationToken).ConfigureAwait(false);
                if (session is null)
                {
                    return "unauthorized";
                }

                string? error = null;
                ItemInfo? info = null;
                string? ownerName = null;
                await session.RunAsync(() =>
                {
                    ownerName = ChooseOwnerName(session.Account, request.OwnerName);
                    var item = session.Account.MarketDepot?.Items.FirstOrDefault(i => i.GetId() == request.ItemId);
                    if (item is null)
                    {
                        error = "item_not_in_depot";
                    }
                    else if (item.Definition is not { IsTradable: true })
                    {
                        error = "item_not_tradable";
                    }
                    else
                    {
                        info = Describe(item);
                    }

                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);

                if (error is not null || info is null)
                {
                    return error ?? "item_not_in_depot";
                }

                if (ownerName is null)
                {
                    return "no_character";
                }

                using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
                var order = context.CreateNew<MarketOrder>();
                order.AccountId = id;
                order.OwnerName = ownerName;
                order.Kind = MarketOrderKind.Sell;
                order.State = MarketOrderState.Active;
                order.ItemId = request.ItemId;
                info.ApplyTo(order);
                order.Price = request.Price;
                order.Quantity = 1;
                order.RemainingQuantity = 1;
                order.CreatedAt = DateTime.UtcNow;
                order.ExpiresAt = order.CreatedAt.AddDays(this._settings.MarketOrderDays);
                if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
                {
                    return "save_failed";
                }

                this._orders.Add(order);
                keepLock = true;
                this._logger.LogInformation("Market: {account} offers {item} for {price} WC (order {order}).", loginName, order.ItemName, order.Price, order.Id);
                return null;
            }
            finally
            {
                if (!keepLock)
                {
                    MarketLocks.UnlockItem(request.ItemId);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Places a buy order and reserves its coins.
    /// </summary>
    /// <param name="loginName">The login name of the buyer.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the order was placed; otherwise, the error code.</returns>
    public async ValueTask<string?> PlaceBuyOrderAsync(string loginName, MarketBuyOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Price is < 1 or > MaximumPrice)
        {
            return "invalid_price";
        }

        if (request.Quantity is < 1 or > MaximumBuyQuantity || (long)request.Price * request.Quantity > MaximumPrice)
        {
            return "invalid_quantity";
        }

        var catalogItem = (await this.GetCatalogAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(c => c.Key == request.ItemKey);
        if (catalogItem is null)
        {
            return "unknown_item";
        }

        var wantedExcellent = (request.ExcellentOptions ?? []).Distinct().ToList();
        if (wantedExcellent.Any(e => !catalogItem.ExcellentOptions.Contains(e)))
        {
            return "invalid_options";
        }

        return await this.WriteAsync(async () =>
        {
            var accountId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            if (accountId is not { } id)
            {
                return "unauthorized";
            }

            if (this.CountOpenOrders(id) >= this._settings.MarketMaximumOrders)
            {
                return "too_many_orders";
            }

            await using var session = await this.OpenSessionAsync(loginName, true, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return "account_busy";
            }

            var total = request.Price * request.Quantity;
            return await session.RunAsync(async () =>
            {
                if (ChooseOwnerName(session.Account, request.OwnerName) is not { } ownerName)
                {
                    return "no_character";
                }

                if (await this.GetBalanceAsync(session, cancellationToken).ConfigureAwait(false) < total)
                {
                    return "not_enough_coins";
                }

                using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
                var order = context.CreateNew<MarketOrder>();
                order.AccountId = id;
                order.OwnerName = ownerName;
                order.Kind = MarketOrderKind.Buy;
                order.State = MarketOrderState.Active;
                order.ItemGroup = catalogItem.Group;
                order.ItemNumber = catalogItem.Number;
                order.ItemName = catalogItem.Name;
                order.Level = Math.Min(request.MinimumLevel, (byte)15);
                order.ExcellentOptionCount = (byte)Math.Min(Math.Max(request.MinimumExcellentOptions, wantedExcellent.Count), 6);
                order.Description = string.Join('\n', wantedExcellent.Select(e => PortalMarketItems.ExcellentPrefix + e));
                order.HasLuck = request.RequiresLuck;
                order.HasSkill = request.RequiresSkill;
                order.OptionLevel = Math.Min(request.MinimumOptionLevel, (byte)7);
                order.Price = request.Price;
                order.Quantity = request.Quantity;
                order.RemainingQuantity = request.Quantity;
                order.CreatedAt = DateTime.UtcNow;
                order.ExpiresAt = order.CreatedAt.AddDays(this._settings.MarketOrderDays);
                AddGrant(context, id, -total, $"Mercado: reserva del pedido de {order.ItemName}", $"market:{order.Id}:reserve");
                if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
                {
                    return "save_failed";
                }

                this._orders.Add(order);
                this._logger.LogInformation("Market: {account} wants {quantity}x {item} for {price} WC each (order {order}).", loginName, order.Quantity, order.ItemName, order.Price, order.Id);
                return (string?)null;
            }).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Buys the item of a sell order.
    /// </summary>
    /// <param name="loginName">The login name of the buyer.</param>
    /// <param name="orderId">The identifier of the sell order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the item was bought; otherwise, the error code.</returns>
    public async ValueTask<string?> BuyAsync(string loginName, Guid orderId, CancellationToken cancellationToken)
    {
        return await this.WriteAsync(async () =>
        {
            var buyerId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            var order = this._orders.FirstOrDefault(o => o.Id == orderId);
            if (buyerId is not { } id)
            {
                return "unauthorized";
            }

            if (order is not { Kind: MarketOrderKind.Sell, State: MarketOrderState.Active, ItemId: { } itemId })
            {
                return "order_not_found";
            }

            if (order.AccountId == id)
            {
                return "own_order";
            }

            var sellerLogin = await this.GetLoginNameAsync(order.AccountId, cancellationToken).ConfigureAwait(false);
            if (sellerLogin is null)
            {
                return "order_not_found";
            }

            return await this.TradeAsync(order, sellerLogin, loginName, id, itemId, true, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sells an item of the market depot to a buy order.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="orderId">The identifier of the buy order.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the item was sold; otherwise, the error code.</returns>
    public async ValueTask<string?> FillAsync(string loginName, Guid orderId, MarketFillRequest request, CancellationToken cancellationToken)
    {
        return await this.WriteAsync(async () =>
        {
            var sellerId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            var order = this._orders.FirstOrDefault(o => o.Id == orderId);
            if (sellerId is not { } id)
            {
                return "unauthorized";
            }

            if (order is not { Kind: MarketOrderKind.Buy, State: MarketOrderState.Active } || order.RemainingQuantity < 1)
            {
                return "order_not_found";
            }

            if (order.AccountId == id)
            {
                return "own_order";
            }

            if (MarketLocks.IsItemListed(request.ItemId))
            {
                return "item_already_listed";
            }

            var buyerLogin = await this.GetLoginNameAsync(order.AccountId, cancellationToken).ConfigureAwait(false);
            if (buyerLogin is null)
            {
                return "order_not_found";
            }

            MarketLocks.LockItem(request.ItemId);
            try
            {
                return await this.TradeAsync(order, loginName, buyerLogin, order.AccountId, request.ItemId, false, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                MarketLocks.UnlockItem(request.ItemId);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels an own order. A sell order unlocks its item, a buy order gets its reserved coins back.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="orderId">The identifier of the order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the order was cancelled; otherwise, the error code.</returns>
    public async ValueTask<string?> CancelAsync(string loginName, Guid orderId, CancellationToken cancellationToken)
    {
        return await this.WriteAsync(async () =>
        {
            var accountId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            var order = this._orders.FirstOrDefault(o => o.Id == orderId);
            if (order is null || order.AccountId != accountId || !IsOpen(order))
            {
                return "order_not_found";
            }

            return await this.CloseAsync(order, MarketOrderState.Cancelled, cancellationToken).ConfigureAwait(false) ? null : "save_failed";
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the price of an own order, or hides or shows it.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="orderId">The identifier of the order.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the order was changed; otherwise, the error code.</returns>
    public async ValueTask<string?> ChangeAsync(string loginName, Guid orderId, MarketChangeRequest request, CancellationToken cancellationToken)
    {
        if (request.Price is < 1 or > MaximumPrice)
        {
            return "invalid_price";
        }

        return await this.WriteAsync(async () =>
        {
            var accountId = await this.GetAccountIdAsync(loginName, cancellationToken).ConfigureAwait(false);
            var order = this._orders.FirstOrDefault(o => o.Id == orderId);
            if (order is null || order.AccountId != accountId || !IsOpen(order))
            {
                return "order_not_found";
            }

            var newPrice = request.Price ?? order.Price;
            var delta = order.Kind == MarketOrderKind.Buy ? (newPrice - order.Price) * order.RemainingQuantity : 0;
            if (delta != 0 && (long)newPrice * order.RemainingQuantity > MaximumPrice)
            {
                return "invalid_price";
            }

            AccountSession? session = null;
            try
            {
                if (delta > 0)
                {
                    session = await this.OpenSessionAsync(loginName, true, cancellationToken).ConfigureAwait(false);
                    if (session is null)
                    {
                        return "account_busy";
                    }
                }

                async ValueTask<string?> ApplyAsync()
                {
                    if (delta > 0 && await this.GetBalanceAsync(session!, cancellationToken).ConfigureAwait(false) < delta)
                    {
                        return "not_enough_coins";
                    }

                    using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
                    context.Attach(order);
                    var oldPrice = order.Price;
                    var oldState = order.State;
                    order.Price = newPrice;
                    if (request.Hidden is { } hidden)
                    {
                        order.State = hidden ? MarketOrderState.Hidden : MarketOrderState.Active;
                    }

                    if (delta != 0)
                    {
                        AddGrant(context, order.AccountId, -delta, $"Mercado: cambio de precio del pedido de {order.ItemName}", $"market:{order.Id}:price:{DateTime.UtcNow.Ticks}");
                    }

                    if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
                    {
                        order.Price = oldPrice;
                        order.State = oldState;
                        return "save_failed";
                    }

                    return null;
                }

                return session is null ? await ApplyAsync().ConfigureAwait(false) : await session.RunAsync(ApplyAsync).ConfigureAwait(false);
            }
            finally
            {
                if (session is not null)
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsOpen(MarketOrder order) => order.State is MarketOrderState.Active or MarketOrderState.Hidden;

    private static string? Neutral(LocalizedString value) => value.ValueInNeutralLanguage;

    private static string? Neutral(string? value) => string.IsNullOrEmpty(value) ? null : new LocalizedString(value).ValueInNeutralLanguage;

    private static string KeyOf(MarketOrder order) => PortalMarketItems.KeyOf(order.ItemGroup, order.ItemNumber, order.ItemName, order.Level);

    private static string KeyOf(MarketTrade trade) => PortalMarketItems.KeyOf(trade.ItemGroup, trade.ItemNumber, trade.ItemName, trade.Level);

    private static string[] OptionLines(string? description)
        => (description ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(PortalMarketItems.TranslateOptionLine).ToArray();

    private static string[] ExcellentLines(IEnumerable<string> lines)
        => lines.Where(l => l.StartsWith(PortalMarketItems.ExcellentPrefix, StringComparison.Ordinal)).Select(l => l[PortalMarketItems.ExcellentPrefix.Length..]).ToArray();

    private IEnumerable<PortalMarketCatalogItem> ToCatalogItems(ItemDefinition definition)
    {
        var classes = definition.QualifiedCharacters
            .Select(c => c.Number / 4)
            .Where(f => f >= 0 && f < ClassFamilies.Length)
            .Distinct()
            .Order()
            .Select(f => ClassFamilies[f])
            .ToList();
        var excellent = definition.PossibleItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(p => p.OptionType == ItemOptionTypes.Excellent)
            .Select(p => Neutral(p.PowerUpDefinition?.TargetAttribute?.Designation))
            .ToList();
        IReadOnlyList<string> excellentOptions = excellent.Count == 0
            ? []
            : excellent.Any(d => d == "Maximum Health") ? PortalMarketItems.DefenseExcellentOptions : PortalMarketItems.AttackExcellentOptions;
        var category = PortalMarketItems.CategoryOf(definition);
        foreach (var (key, name, imageLevel) in PortalMarketItems.VariantsOf(definition.Group, definition.Number, Neutral(definition.Name) ?? string.Empty))
        {
            yield return new PortalMarketCatalogItem(
                key,
                definition.Group,
                definition.Number,
                name,
                category,
                definition.Width,
                definition.Height,
                classes,
                excellentOptions.Count > 0,
                excellentOptions,
                this.ImageOf(definition.Group, definition.Number, imageLevel, false, false));
        }
    }

    /// <summary>
    /// Gets the url of the picture of an item, if there is one.
    /// </summary>
    private string? ImageOf(byte group, short number, byte level, bool excellent, bool ancient)
    {
        var name = PortalMarketItems.ImageNames(group, number, level, excellent, ancient).FirstOrDefault(this._images.Contains);
        return name is null ? null : PortalMarketImages.UrlPrefix + name;
    }

    private static ItemInfo Describe(Item item)
    {
        var definition = item.Definition!;
        var options = new List<string>();
        byte excellent = 0;
        var luck = false;
        byte optionLevel = 0;
        if (item.HasSkill)
        {
            options.Add("Skill");
        }

        foreach (var link in item.ItemOptions.OrderBy(l => l.Index))
        {
            var type = link.ItemOption?.OptionType;
            var target = Neutral(link.ItemOption?.PowerUpDefinition?.TargetAttribute?.Designation)
                ?? Neutral(link.ItemOption?.LevelDependentOptions?.FirstOrDefault()?.PowerUpDefinition?.TargetAttribute?.Designation);
            if (type == ItemOptionTypes.BlackFenrir || type == ItemOptionTypes.BlueFenrir || type == ItemOptionTypes.GoldFenrir)
            {
                // The name of the Horn of Fenrir says it.
            }
            else if (type == ItemOptionTypes.Luck)
            {
                luck = true;
                options.Add("Luck");
            }
            else if (type == ItemOptionTypes.Option)
            {
                optionLevel = (byte)Math.Clamp(link.Level, 0, 255);
                options.Add($"Opción +{link.Level * 4}");
            }
            else if (type == ItemOptionTypes.Excellent)
            {
                excellent++;
                options.Add(PortalMarketItems.ExcellentPrefix + PortalMarketItems.ExcellentName(target));
            }
            else if (type == ItemOptionTypes.Wing)
            {
                options.Add($"Alas: {target ?? "opción"}");
            }
            else if (type == ItemOptionTypes.HarmonyOption)
            {
                options.Add($"Harmony: {target ?? "opción"} (nivel {link.Level})");
            }
            else if (type == ItemOptionTypes.GuardianOption)
            {
                options.Add("Opción 380");
            }
            else if (type == ItemOptionTypes.SocketOption)
            {
                options.Add($"Socket: {target ?? "opción"}");
            }
        }

        var ancientSet = item.ItemSetGroups.FirstOrDefault(s => s.AncientSetDiscriminator != 0)?.ItemSetGroup;
        if (ancientSet is not null)
        {
            options.Add($"Ancient: {Neutral(ancientSet.Name)}");
        }

        if (item.SocketCount > 0)
        {
            options.Add($"{item.SocketCount} sockets");
        }

        var (key, name, level) = PortalMarketItems.Identify(item);
        return new ItemInfo(
            key,
            definition.Group,
            definition.Number,
            name,
            level,
            excellent,
            luck,
            item.HasSkill,
            optionLevel,
            ancientSet is not null,
            (byte)Math.Clamp(item.SocketCount, 0, 255),
            options);
    }

    private static bool Meets(ItemInfo info, MarketOrder buyOrder)
        => info.Key == KeyOf(buyOrder)
           && ExcellentLines(OptionLines(buyOrder.Description)).All(e => info.Options.Contains(PortalMarketItems.ExcellentPrefix + e))
           && info.Level >= buyOrder.Level
           && info.ExcellentOptionCount >= buyOrder.ExcellentOptionCount
           && (info.HasLuck || !buyOrder.HasLuck)
           && (info.HasSkill || !buyOrder.HasSkill)
           && info.OptionLevel >= buyOrder.OptionLevel;

    private static string? ChooseOwnerName(Account account, string? requested)
    {
        var names = account.Characters.OrderBy(c => c.CharacterSlot).Select(c => c.Name).ToList();
        return requested is not null && names.Contains(requested) ? requested : names.FirstOrDefault();
    }

    private PortalMarketTrade ToTradeDto(MarketTrade t)
        => new(t.CreatedAt, KeyOf(t), t.ItemName, t.Level, t.ExcellentOptionCount, t.Price, t.SellerName, t.BuyerName, this.ImageOf(t.ItemGroup, t.ItemNumber, LevelOfImage(t.ItemGroup, t.ItemNumber, t.ItemName, t.Level), t.ExcellentOptionCount > 0, false));

    /// <summary>
    /// Gets the level for the picture: the level of the variant, if the item is a different item by its level.
    /// </summary>
    private static byte LevelOfImage(byte group, short number, string itemName, byte level)
    {
        var key = PortalMarketItems.KeyOf(group, number, itemName, level);
        var parts = key.Split('-');
        return parts.Length == 3 && byte.TryParse(parts[2], out var variantLevel) ? variantLevel : level;
    }

    private static PortalMarketStats CalculateStats(IReadOnlyList<MarketTrade> trades)
    {
        static int Median(IEnumerable<int> values)
        {
            var sorted = values.Order().ToList();
            return sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
        }

        var now = DateTime.UtcNow;
        var recent = trades.Where(t => t.CreatedAt > now.AddHours(-48)).Select(t => t.Price).ToList();
        var days = trades
            .Where(t => t.CreatedAt > now.AddDays(-90))
            .GroupBy(t => DateOnly.FromDateTime(t.CreatedAt))
            .OrderBy(g => g.Key)
            .Select(g => new PortalMarketPricePoint(g.Key, Median(g.Select(t => t.Price)), g.Min(t => t.Price), g.Max(t => t.Price), g.Count()))
            .ToList();
        return new PortalMarketStats(recent.Count > 0 ? Median(recent) : null, recent.Count > 0 ? recent.Min() : null, recent.Count, days);
    }

    private static void AddGrant(IContext context, Guid accountId, int amount, string reason, string reference)
    {
        var grant = context.CreateNew<CashShopCoinGrant>();
        grant.AccountId = accountId;
        grant.CoinType = CashShopCoinType.WCoinC;
        grant.Amount = amount;
        grant.Reason = reason;
        grant.GrantedBy = "Mercado";
        grant.Reference = reference;
        grant.CreatedAt = DateTime.UtcNow;
    }

    private int CountOpenOrders(Guid accountId) => this._orders.Count(o => o.AccountId == accountId && IsOpen(o));

    private async ValueTask<string?> TradeAsync(MarketOrder order, string sellerLogin, string buyerLogin, Guid buyerId, Guid itemId, bool chargeBuyer, CancellationToken cancellationToken)
    {
        await using var seller = await this.OpenSessionAsync(sellerLogin, true, cancellationToken).ConfigureAwait(false);
        if (seller is null)
        {
            return chargeBuyer ? "seller_busy" : "account_busy";
        }

        await using var buyer = await this.OpenSessionAsync(buyerLogin, true, cancellationToken).ConfigureAwait(false);
        if (buyer is null)
        {
            return chargeBuyer ? "account_busy" : "buyer_busy";
        }

        // 1. The item must still be in the market depot of the seller.
        Item? item = null;
        ItemInfo? info = null;
        string? sellerName = null;
        var sellerError = await seller.RunAsync(() =>
        {
            if (seller.Player?.IsMarketDepotOpen() ?? false)
            {
                return ValueTask.FromResult<string?>(chargeBuyer ? "seller_depot_open" : "depot_open");
            }

            item = seller.Account.MarketDepot?.Items.FirstOrDefault(i => i.GetId() == itemId);
            if (item?.Definition is not { IsTradable: true })
            {
                return ValueTask.FromResult<string?>("item_not_in_depot");
            }

            info = Describe(item);
            sellerName = chargeBuyer ? order.OwnerName : ChooseOwnerName(seller.Account, null);
            if (!chargeBuyer && !Meets(info, order))
            {
                return ValueTask.FromResult<string?>("item_does_not_meet_order");
            }

            return ValueTask.FromResult<string?>(null);
        }).ConfigureAwait(false);

        if (sellerError == "item_not_in_depot" && chargeBuyer)
        {
            // The item is gone, e.g. because the market was closed while it was moved. The order can't be fulfilled.
            await this.CloseAsync(order, MarketOrderState.Cancelled, cancellationToken).ConfigureAwait(false);
            return "order_not_found";
        }

        if (sellerError is not null)
        {
            return sellerError;
        }

        var price = order.Price;
        var commission = this.CommissionOf(price);
        MarketTrade? trade = null;
        var saved = false;
        (int, MarketOrderState, DateTime?)? restoreOrder = null;

        // 2. The buyer needs space and coins; then the trade is saved, before anything moves.
        var buyerError = await buyer.RunAsync(async () =>
        {
            if (buyer.Player?.IsMarketDepotOpen() ?? false)
            {
                return chargeBuyer ? "depot_open" : "buyer_depot_open";
            }

            var space = buyer.Account.MarketDepot is { } depot
                ? new Storage(InventoryConstants.WarehouseSize, depot).CheckInvSpace(item!)
                : (byte?)0;
            if (space is null)
            {
                return chargeBuyer ? "depot_full" : "buyer_depot_full";
            }

            if (chargeBuyer && await this.GetBalanceAsync(buyer, cancellationToken).ConfigureAwait(false) < price)
            {
                return "not_enough_coins";
            }

            using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
            context.Attach(order);
            var number = order.Quantity - order.RemainingQuantity + 1;
            restoreOrder = (order.RemainingQuantity, order.State, order.ClosedAt);
            order.RemainingQuantity--;
            if (order.RemainingQuantity <= 0)
            {
                order.State = MarketOrderState.Completed;
                order.ClosedAt = DateTime.UtcNow;
            }

            trade = context.CreateNew<MarketTrade>();
            trade.OrderId = order.Id;
            trade.ItemId = itemId;
            trade.SellerAccountId = seller.AccountId;
            trade.SellerName = sellerName ?? string.Empty;
            trade.BuyerAccountId = buyerId;
            trade.BuyerName = chargeBuyer ? ChooseOwnerName(buyer.Account, null) ?? string.Empty : order.OwnerName;
            trade.ItemGroup = info!.Group;
            trade.ItemNumber = info.Number;
            trade.ItemName = info.Name;
            trade.Level = info.Level;
            trade.ExcellentOptionCount = info.ExcellentOptionCount;
            trade.Description = string.Join('\n', info.Options);
            trade.Price = price;
            trade.Commission = commission;
            trade.CreatedAt = DateTime.UtcNow;
            if (chargeBuyer)
            {
                AddGrant(context, buyerId, -price, $"Mercado: compra de {info.Name}", $"market:{order.Id}:{number}:buyer");
            }

            AddGrant(context, seller.AccountId, price - commission, $"Mercado: venta de {info.Name}", $"market:{order.Id}:{number}:seller");
            try
            {
                saved = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Market: the trade of order {order} couldn't be saved.", order.Id);
            }

            return saved ? null : "save_failed";
        }).ConfigureAwait(false);

        if (buyerError is not null)
        {
            if (!saved && restoreOrder is { } restore)
            {
                (order.RemainingQuantity, order.State, order.ClosedAt) = restore;
            }

            return buyerError;
        }

        // 3. The item moves from the depot of the seller to the depot of the buyer.
        try
        {
            await seller.RunAsync(async () =>
            {
                await new Storage(InventoryConstants.WarehouseSize, seller.Account.MarketDepot!).RemoveItemAsync(item!).ConfigureAwait(false);
                seller.Context.Detach(item!);
            }).ConfigureAwait(false);

            await buyer.RunAsync(async () =>
            {
                buyer.Account.MarketDepot ??= buyer.Context.CreateNew<ItemStorage>();
                buyer.Context.Attach(item!);
                if (!await new Storage(InventoryConstants.WarehouseSize, buyer.Account.MarketDepot).AddItemAsync(item!).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The market depot of the buyer has no space anymore.");
                }

                if (!await buyer.SaveAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The market depot of the buyer couldn't be saved.");
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Market: the item {item} of trade {trade} couldn't be moved from {seller} to {buyer}; the trade is reverted. The item stays in the database in the depot of the seller.", itemId, trade!.Id, sellerLogin, buyerLogin);
            await this.RevertTradeAsync(order, trade, chargeBuyer, cancellationToken).ConfigureAwait(false);
            return "trade_failed";
        }

        this._trades.Add(trade!);
        if (order.Kind == MarketOrderKind.Sell)
        {
            MarketLocks.UnlockItem(itemId);
        }

        this._logger.LogInformation("Market: {seller} sold {item} to {buyer} for {price} WC (commission {commission}, order {order}).", sellerLogin, info!.Name, buyerLogin, price, commission, order.Id);
        await NotifyAsync(seller.Player, $"Mercado: vendiste {info.Name} por {price - commission} WC.").ConfigureAwait(false);
        await NotifyAsync(buyer.Player, $"Mercado: compraste {info.Name}. Está en tu depósito de la Mercader.").ConfigureAwait(false);
        return null;
    }

    private async ValueTask RevertTradeAsync(MarketOrder order, MarketTrade trade, bool chargeBuyer, CancellationToken cancellationToken)
    {
        try
        {
            using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
            context.Attach(order);
            context.Attach(trade);
            order.RemainingQuantity++;
            order.State = MarketOrderState.Active;
            order.ClosedAt = null;
            if (chargeBuyer)
            {
                AddGrant(context, trade.BuyerAccountId, trade.Price, $"Mercado: devolución de la compra fallida de {trade.ItemName}", $"market:{trade.Id}:revert:buyer");
            }

            AddGrant(context, trade.SellerAccountId, trade.Commission - trade.Price, $"Mercado: anulación de la venta fallida de {trade.ItemName}", $"market:{trade.Id}:revert:seller");
            await context.DeleteAsync(trade).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogCritical(ex, "Market: the failed trade {trade} couldn't be reverted. Check the coin grants with the references market:{order}:*.", trade.Id, order.Id);
        }
    }

    private static async ValueTask NotifyAsync(Player? player, string message)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
        }
        catch
        {
            // The player may have just logged out.
        }
    }

    private async ValueTask<bool> CloseAsync(MarketOrder order, MarketOrderState state, CancellationToken cancellationToken)
    {
        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        context.Attach(order);
        var oldState = order.State;
        order.State = state;
        order.ClosedAt = DateTime.UtcNow;
        if (order.Kind == MarketOrderKind.Buy && order.RemainingQuantity > 0)
        {
            var reason = state == MarketOrderState.Expired ? "venció" : "se canceló";
            AddGrant(context, order.AccountId, order.Price * order.RemainingQuantity, $"Mercado: devolución, {reason} el pedido de {order.ItemName}", $"market:{order.Id}:refund");
        }

        if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
        {
            order.State = oldState;
            order.ClosedAt = null;
            return false;
        }

        if (order.Kind == MarketOrderKind.Sell && order.ItemId is { } itemId)
        {
            MarketLocks.UnlockItem(itemId);
        }

        this._logger.LogInformation("Market: order {order} ({item}) is {state}.", order.Id, order.ItemName, state);
        return true;
    }

    private void OnExpiryTimer()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await this.WriteAsync(async () =>
                {
                    var now = DateTime.UtcNow;
                    foreach (var order in this._orders.Where(o => IsOpen(o) && o.ExpiresAt <= now).ToList())
                    {
                        await this.CloseAsync(order, MarketOrderState.Expired, default).ConfigureAwait(false);
                    }

                    return null;
                }, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Market: expiring the orders failed.");
            }
        });
    }

    private async ValueTask<string?> WriteAsync(Func<ValueTask<string?>> operation, CancellationToken cancellationToken)
    {
        if (!this._initialized)
        {
            return "market_closed";
        }

        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Market: unexpected error.");
            return "save_failed";
        }
        finally
        {
            this._lock.Release();
        }
    }

    private async ValueTask<List<MarketOrder>> SnapshotAsync(Func<MarketOrder, bool> predicate, CancellationToken cancellationToken)
    {
        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return this._orders.Where(predicate).ToList();
        }
        finally
        {
            this._lock.Release();
        }
    }

    private async ValueTask<PortalMarketOrder> ToDtoAsync(MarketOrder order, Guid? viewer)
    {
        var status = "offline";
        if (await this._friendServer.GetOnlineServerIdAsync(order.OwnerName).ConfigureAwait(false) is not null)
        {
            status = "game";
        }
        else if (this._webActivity.TryGetValue(order.AccountId, out var lastSeen) && DateTime.UtcNow - lastSeen < WebActivityDuration)
        {
            status = "web";
        }

        var options = OptionLines(order.Description);
        var key = KeyOf(order);
        var category = this._catalog?.FirstOrDefault(c => c.Key == key)?.Category ?? "Otros";
        return new PortalMarketOrder(
            order.Id,
            order.Kind == MarketOrderKind.Sell ? "sell" : "buy",
            order.State.ToString().ToLowerInvariant(),
            order.OwnerName,
            status,
            viewer == order.AccountId,
            key,
            order.ItemName,
            category,
            order.Level,
            order.ExcellentOptionCount,
            order.HasLuck,
            order.HasSkill,
            order.OptionLevel,
            order.IsAncient,
            order.SocketCount,
            options,
            order.Price,
            order.Quantity,
            order.RemainingQuantity,
            order.CreatedAt,
            order.ExpiresAt,
            this.ImageOf(order.ItemGroup, order.ItemNumber, LevelOfImage(order.ItemGroup, order.ItemNumber, order.ItemName, order.Level), order.ExcellentOptionCount > 0, order.IsAncient));
    }

    private async ValueTask<IPlayerContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return this._persistence.CreateNewPlayerContext(config);
    }

    private async ValueTask<Guid?> GetAccountIdAsync(string loginName, CancellationToken cancellationToken)
    {
        if (this._accountIds.TryGetValue(loginName, out var id))
        {
            return id;
        }

        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return null;
        }

        id = account.GetId();
        this._accountIds[account.LoginName] = id;
        return id;
    }

    private async ValueTask<string?> GetLoginNameAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (this._accountIds.FirstOrDefault(p => p.Value == accountId) is { Key: { } known })
        {
            return known;
        }

        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var account = await context.GetByIdAsync<Account>(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        this._accountIds[account.LoginName] = accountId;
        return account.LoginName;
    }

    /// <summary>
    /// Gets the available coins: the balance plus the pending grants. For a player who is online, it must be called
    /// while holding its persistence lock, so that the pending grants aren't applied meanwhile.
    /// </summary>
    private async ValueTask<int> GetBalanceAsync(AccountSession session, CancellationToken cancellationToken)
    {
        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var pending = await context.GetPendingCashShopCoinGrantsAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
        var balance = (long)session.Account.WCoinC + pending.Where(g => g.CoinType == CashShopCoinType.WCoinC).Sum(g => (long)g.Amount);
        return (int)Math.Clamp(balance, 0, int.MaxValue);
    }

    private async ValueTask<Player?> FindPlayerAsync(string loginName)
    {
        foreach (var server in this._serverProvider.Servers.OfType<IGameServer>())
        {
            if (server is not IGameServerContextProvider { Context: { } context })
            {
                continue;
            }

            foreach (var player in await context.GetPlayersAsync().ConfigureAwait(false))
            {
                if (player.Account is { } account && string.Equals(account.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Opens the account: the account of the player in memory, if it's online; otherwise, the account from the database.
    /// An exclusive session marks the account as busy for the market and reserves an offline account at the login server.
    /// </summary>
    private async ValueTask<AccountSession?> OpenSessionAsync(string loginName, bool exclusive, CancellationToken cancellationToken)
    {
        if (exclusive && !MarketLocks.TryMarkAccountBusy(loginName))
        {
            return null;
        }

        var session = new AccountSession(this._loginServer, loginName, exclusive);
        try
        {
            if (await this.FindPlayerAsync(loginName).ConfigureAwait(false) is { Account: { } onlineAccount } player)
            {
                session.UsePlayer(player, onlineAccount);
                return session;
            }

            if (exclusive)
            {
                var reservationId = RandomNumberGenerator.GetInt32(256, int.MaxValue);
                if (!await this._loginServer.TryReserveAccountAsync(loginName, reservationId).ConfigureAwait(false))
                {
                    // It just logged in, or another administrative write holds it.
                    if (await this.FindPlayerAsync(loginName).ConfigureAwait(false) is { Account: { } account } justLoggedIn)
                    {
                        session.UsePlayer(justLoggedIn, account);
                        return session;
                    }

                    await session.DisposeAsync().ConfigureAwait(false);
                    return null;
                }

                session.ReservationId = reservationId;
            }

            var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
            var offlineAccount = await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
            if (offlineAccount is null)
            {
                context.Dispose();
                await session.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            session.UseContext(context, offlineAccount);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The item data which the market keeps with an order.
    /// </summary>
    private sealed record ItemInfo(
        string Key,
        byte Group,
        short Number,
        string Name,
        byte Level,
        byte ExcellentOptionCount,
        bool HasLuck,
        bool HasSkill,
        byte OptionLevel,
        bool IsAncient,
        byte SocketCount,
        IReadOnlyList<string> Options)
    {
        public void ApplyTo(MarketOrder order)
        {
            order.ItemGroup = this.Group;
            order.ItemNumber = this.Number;
            order.ItemName = this.Name;
            order.Level = this.Level;
            order.ExcellentOptionCount = this.ExcellentOptionCount;
            order.HasLuck = this.HasLuck;
            order.HasSkill = this.HasSkill;
            order.OptionLevel = this.OptionLevel;
            order.IsAncient = this.IsAncient;
            order.SocketCount = this.SocketCount;
            order.Description = string.Join('\n', this.Options);
        }
    }

    /// <summary>
    /// An account which the market reads or changes.
    /// </summary>
    private sealed class AccountSession : IAsyncDisposable
    {
        private readonly ILoginServer _loginServer;
        private readonly bool _exclusive;
        private IContext? _ownContext;

        public AccountSession(ILoginServer loginServer, string loginName, bool exclusive)
        {
            this._loginServer = loginServer;
            this.LoginName = loginName;
            this._exclusive = exclusive;
        }

        public string LoginName { get; }

        public Player? Player { get; private set; }

        public Account Account { get; private set; } = null!;

        public Guid AccountId => this.Account.GetId();

        public int ReservationId { get; set; }

        public IContext Context => this.Player?.PersistenceContext ?? this._ownContext!;

        public void UsePlayer(Player player, Account account)
        {
            this.Player = player;
            this.Account = account;
        }

        public void UseContext(IContext context, Account account)
        {
            this._ownContext = context;
            this.Account = account;
        }

        public ValueTask<T> RunAsync<T>(Func<ValueTask<T>> operation)
            => this.Player is { } player ? player.RunPersistenceExclusiveAsync(operation) : operation();

        public ValueTask RunAsync(Func<ValueTask> operation)
            => this.Player is { } player ? player.RunPersistenceExclusiveAsync(operation) : operation();

        public async ValueTask<bool> SaveAsync(CancellationToken cancellationToken)
            => this.Player is { } player
                ? await player.SaveProgressAsync(cancellationToken).ConfigureAwait(false)
                : await this._ownContext!.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        public async ValueTask DisposeAsync()
        {
            this._ownContext?.Dispose();
            this._ownContext = null;
            if (this.ReservationId != 0)
            {
                await this._loginServer.ReleaseAccountReservationAsync(this.LoginName, this.ReservationId).ConfigureAwait(false);
                this.ReservationId = 0;
            }

            if (this._exclusive)
            {
                MarketLocks.ReleaseAccount(this.LoginName);
            }
        }
    }
}
