// <copyright file="MarketServiceTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Market;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Web.Portal;

/// <summary>
/// Tests for the <see cref="PortalMarketService"/>, with offline accounts.
/// </summary>
[TestFixture]
public class MarketServiceTest
{
    private InMemoryPersistenceContextProvider _persistence = null!;
    private GameConfiguration _config = null!;
    private PortalMarketService _market = null!;
    private Guid _itemId;
    private Guid _sellerId;
    private Guid _buyerId;

    /// <summary>
    /// Sets up a seller with a jewel in the market depot and a buyer with 100 coins.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._persistence = new InMemoryPersistenceContextProvider();
        using (var context = this._persistence.CreateNewContext())
        {
            this._config = context.CreateNew<GameConfiguration>();
            var jewel = context.CreateNew<ItemDefinition>();
            jewel.Name = "Jewel of Bless";
            jewel.Group = 14;
            jewel.Number = 13;
            jewel.Width = 1;
            jewel.Height = 1;
            this._config.Items.Add(jewel);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var context = this._persistence.CreateNewPlayerContext(this._config))
        {
            var seller = CreateAccount(context, "seller", "Seller");
            seller.MarketDepot = context.CreateNew<ItemStorage>();
            var item = context.CreateNew<Item>();
            item.Definition = this._config.Items.First();
            item.Durability = 1;
            item.ItemSlot = 0;
            seller.MarketDepot.Items.Add(item);
            this._itemId = item.GetId();
            this._sellerId = seller.GetId();

            var buyer = CreateAccount(context, "buyer", "Buyer");
            this._buyerId = buyer.GetId();
            var grant = context.CreateNew<CashShopCoinGrant>();
            grant.AccountId = this._buyerId;
            grant.CoinType = CashShopCoinType.WCoinC;
            grant.Amount = 100;
            grant.CreatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(this._config));
        var serverProvider = new Mock<IServerProvider>();
        serverProvider.Setup(s => s.Servers).Returns(new List<IManageableServer>());
        this._market = new PortalMarketService(
            this._persistence,
            configSource.Object,
            new LoginServer.LoginServer(),
            new Mock<IFriendServer>().Object,
            serverProvider.Object,
            new PortalSettings { MarketCommissionPercent = 10 },
            NullLogger<PortalMarketService>.Instance);
        await this._market.InitializeAsync(default).ConfigureAwait(false);
    }

    /// <summary>
    /// Unlocks the item, which is a static lock.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        MarketLocks.UnlockItem(this._itemId);
        this._market.Dispose();
    }

    /// <summary>
    /// Tests that an offered item is locked, and that buying it moves it into the depot of the buyer and moves the coins.
    /// </summary>
    [Test]
    public async Task BuyingMovesTheItemAndTheCoinsAsync()
    {
        Assert.That(await this._market.SellAsync("seller", new MarketSellRequest { ItemId = this._itemId, Price = 50 }, default).ConfigureAwait(false), Is.Null);
        Assert.That(MarketLocks.IsItemListed(this._itemId), Is.True);
        var order = (await this._market.GetOrdersAsync(MarketOrderKind.Sell, null, default).ConfigureAwait(false)).Single();
        Assert.That(order.ItemName, Is.EqualTo("Jewel of Bless"));

        Assert.That(await this._market.BuyAsync("buyer", order.Id, default).ConfigureAwait(false), Is.Null);

        Assert.That(MarketLocks.IsItemListed(this._itemId), Is.False);
        using var context = this._persistence.CreateNewPlayerContext(this._config);
        var seller = await context.GetAccountByLoginNameAsync("seller").ConfigureAwait(false);
        var buyer = await context.GetAccountByLoginNameAsync("buyer").ConfigureAwait(false);
        Assert.That(seller!.MarketDepot!.Items, Is.Empty);
        Assert.That(buyer!.MarketDepot!.Items.Select(i => i.GetId()), Is.EqualTo(new[] { this._itemId }));
        var buyerGrants = await context.GetPendingCashShopCoinGrantsAsync(this._buyerId).ConfigureAwait(false);
        var sellerGrants = await context.GetPendingCashShopCoinGrantsAsync(this._sellerId).ConfigureAwait(false);
        Assert.That(buyerGrants.Sum(g => g.Amount), Is.EqualTo(50));
        Assert.That(sellerGrants.Sum(g => g.Amount), Is.EqualTo(45));
        Assert.That((await this._market.GetOrdersAsync(MarketOrderKind.Sell, null, default).ConfigureAwait(false)), Is.Empty);
    }

    /// <summary>
    /// Tests that a buyer can't pay more than the available coins.
    /// </summary>
    [Test]
    public async Task BuyingNeedsEnoughCoinsAsync()
    {
        await this._market.SellAsync("seller", new MarketSellRequest { ItemId = this._itemId, Price = 150 }, default).ConfigureAwait(false);
        var order = (await this._market.GetOrdersAsync(MarketOrderKind.Sell, null, default).ConfigureAwait(false)).Single();

        Assert.That(await this._market.BuyAsync("buyer", order.Id, default).ConfigureAwait(false), Is.EqualTo("not_enough_coins"));
        Assert.That(MarketLocks.IsItemListed(this._itemId), Is.True);
    }

    /// <summary>
    /// Tests that a buy order reserves its coins, that selling to it moves the item, and that cancelling gives back the rest.
    /// </summary>
    [Test]
    public async Task BuyOrderReservesFillsAndRefundsAsync()
    {
        var request = new MarketBuyOrderRequest { ItemKey = "14-13", Price = 30, Quantity = 2 };
        Assert.That(await this._market.PlaceBuyOrderAsync("buyer", request, default).ConfigureAwait(false), Is.Null);
        var mine = await this._market.GetMyMarketAsync("buyer", default).ConfigureAwait(false);
        Assert.That(mine!.Balance, Is.EqualTo(40));
        var order = mine.Orders.Single();

        Assert.That(await this._market.FillAsync("seller", order.Id, new MarketFillRequest { ItemId = this._itemId }, default).ConfigureAwait(false), Is.Null);
        Assert.That(await this._market.CancelAsync("buyer", order.Id, default).ConfigureAwait(false), Is.Null);

        using var context = this._persistence.CreateNewPlayerContext(this._config);
        var buyer = await context.GetAccountByLoginNameAsync("buyer").ConfigureAwait(false);
        Assert.That(buyer!.MarketDepot!.Items.Select(i => i.GetId()), Is.EqualTo(new[] { this._itemId }));
        var buyerGrants = await context.GetPendingCashShopCoinGrantsAsync(this._buyerId).ConfigureAwait(false);
        var sellerGrants = await context.GetPendingCashShopCoinGrantsAsync(this._sellerId).ConfigureAwait(false);
        Assert.That(buyerGrants.Sum(g => g.Amount), Is.EqualTo(70), "100 - 60 reserved + 30 refunded");
        Assert.That(sellerGrants.Sum(g => g.Amount), Is.EqualTo(27));
    }

    /// <summary>
    /// Tests that the Horns of Fenrir are different items by their option, and the boxes of the Box of Luck by their level.
    /// </summary>
    [Test]
    public void VariantsAreDifferentItems()
    {
        using var context = this._persistence.CreateNewContext();
        var fenrirDefinition = context.CreateNew<ItemDefinition>();
        fenrirDefinition.Name = "Horn of Fenrir";
        fenrirDefinition.Group = 13;
        fenrirDefinition.Number = 37;
        var gold = context.CreateNew<Item>();
        gold.Definition = fenrirDefinition;
        var link = context.CreateNew<ItemOptionLink>();
        link.ItemOption = context.CreateNew<IncreasableItemOption>();
        link.ItemOption.OptionType = ItemOptionTypes.GoldFenrir;
        gold.ItemOptions.Add(link);
        var red = context.CreateNew<Item>();
        red.Definition = fenrirDefinition;
        red.HasSkill = true;
        Assert.That(PortalMarketItems.Identify(gold), Is.EqualTo(("13-37-dorado", "Fenrir dorado (Illusion)", (byte)0)));
        Assert.That(PortalMarketItems.Identify(red), Is.EqualTo(("13-37-rojo", "Fenrir rojo", (byte)0)));
        Assert.That(PortalMarketItems.KeyOf(13, 37, "Fenrir dorado (Illusion)"), Is.EqualTo("13-37-dorado"));

        var boxDefinition = context.CreateNew<ItemDefinition>();
        boxDefinition.Name = "Box of Luck";
        boxDefinition.Group = 14;
        boxDefinition.Number = 11;
        var box = context.CreateNew<Item>();
        box.Definition = boxDefinition;
        box.Level = 10;
        Assert.That(PortalMarketItems.Identify(box), Is.EqualTo(("14-11-10", "Box of Kundun +3", (byte)0)));
        Assert.That(PortalMarketItems.KeyOf(14, 11, "Box of Kundun +3"), Is.EqualTo("14-11-10"));
    }

    /// <summary>
    /// Tests that a buy order which asks for an excellent option isn't filled by an item without it.
    /// </summary>
    [Test]
    public async Task BuyOrderNeedsTheExcellentOptionsAsync()
    {
        using (var context = this._persistence.CreateNewContext())
        {
            var definition = context.CreateNew<ItemOptionDefinition>();
            var option = context.CreateNew<IncreasableItemOption>();
            option.OptionType = ItemOptionTypes.Excellent;
            option.PowerUpDefinition = context.CreateNew<PowerUpDefinition>();
            option.PowerUpDefinition.TargetAttribute = new AttributeSystem.AttributeDefinition(Guid.NewGuid(), "Maximum Health", string.Empty);
            definition.PossibleOptions.Add(option);
            this._config.Items.First().PossibleItemOptions.Add(definition);
        }

        await this.ReloadAsync().ConfigureAwait(false);
        var request = new MarketBuyOrderRequest { ItemKey = "14-13", Price = 30, ExcellentOptions = ["Vida máx. +4%"] };
        Assert.That(await this._market.PlaceBuyOrderAsync("buyer", request, default).ConfigureAwait(false), Is.Null);
        var order = (await this._market.GetMyMarketAsync("buyer", default).ConfigureAwait(false))!.Orders.Single();
        Assert.That(order.Options, Is.EqualTo(new[] { "Exc: Vida máx. +4%" }));

        Assert.That(await this._market.FillAsync("seller", order.Id, new MarketFillRequest { ItemId = this._itemId }, default).ConfigureAwait(false), Is.Not.Null);
        Assert.That(await this._market.PlaceBuyOrderAsync("buyer", new MarketBuyOrderRequest { ItemKey = "14-13", Price = 1, ExcellentOptions = ["Daño +2%"] }, default).ConfigureAwait(false), Is.EqualTo("invalid_options"));
    }

    /// <summary>
    /// Tests that the price guide shows the nearest sale when there's none exactly like the wanted item.
    /// </summary>
    [Test]
    public async Task PriceGuideFindsTheNearestSaleAsync()
    {
        await this._market.SellAsync("seller", new MarketSellRequest { ItemId = this._itemId, Price = 50 }, default).ConfigureAwait(false);
        var order = (await this._market.GetOrdersAsync(MarketOrderKind.Sell, null, default).ConfigureAwait(false)).Single();
        await this._market.BuyAsync("buyer", order.Id, default).ConfigureAwait(false);

        var exact = await this._market.GetPriceGuideAsync("14-13", new MarketPriceQuery(), default).ConfigureAwait(false);
        Assert.That(exact!.HasExact, Is.True);
        Assert.That(exact.SuggestedPrice, Is.EqualTo(50));

        var other = await this._market.GetPriceGuideAsync("14-13", new MarketPriceQuery { Level = 3, Luck = true }, default).ConfigureAwait(false);
        Assert.That(other!.HasExact, Is.False);
        Assert.That(other.SuggestedPrice, Is.EqualTo(50));
        Assert.That(other.References.Single().OptionDistance, Is.EqualTo(1));
        Assert.That(other.References.Single().LevelDistance, Is.EqualTo(3));
    }

    private async Task ReloadAsync()
    {
        this._market.Dispose();
        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(this._config));
        var serverProvider = new Mock<IServerProvider>();
        serverProvider.Setup(s => s.Servers).Returns(new List<IManageableServer>());
        this._market = new PortalMarketService(
            this._persistence,
            configSource.Object,
            new LoginServer.LoginServer(),
            new Mock<IFriendServer>().Object,
            serverProvider.Object,
            new PortalSettings { MarketCommissionPercent = 10 },
            NullLogger<PortalMarketService>.Instance);
        await this._market.InitializeAsync(default).ConfigureAwait(false);
    }

    private static Account CreateAccount(IContext context, string loginName, string characterName)
    {
        var account = context.CreateNew<Account>();
        account.LoginName = loginName;
        var character = context.CreateNew<Character>();
        character.Name = characterName;
        account.Characters.Add(character);
        return account;
    }
}
