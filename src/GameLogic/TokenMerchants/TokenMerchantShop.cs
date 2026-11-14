// <copyright file="TokenMerchantShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// The shop of the token merchants (see <see cref="TokenMerchantFeaturePlugIn"/>). Each merchant has a tab for each
/// character class, with the best armor set, shield and weapons of its dungeon for the class. The items are not saved
/// in the configuration: they are the ones which the zones of the drop maps of the dungeon give
/// (<see cref="DefaultDropGenerator.GetBestZoneEquipmentAsync"/>), so they follow the dungeon when it changes.
/// </summary>
/// <remarks>
/// The game client only knows shops with one list of items and prices in zen. The server shows the list of the
/// current tab of the player, starting with the tab of its own class, and charges tokens instead of zen.
/// </remarks>
public class TokenMerchantShop
{
    /// <summary>
    /// The number of tabs, one for each character class.
    /// </summary>
    public const int TabCount = 7;

    private const int StoreRows = 15;

    /// <summary>
    /// The class of each tab, as the number of the character class of the first generation divided by 4:
    /// Dark Knight, Dark Wizard, Elf, Summoner, Magic Gladiator, Dark Lord, Rage Fighter.
    /// </summary>
    private static readonly int[] ClassIndexOfTab = [1, 0, 2, 5, 3, 4, 6];

    private static readonly ConditionalWeakTable<Player, ShopState> OpenedShops = new();

    private volatile Dictionary<byte, IReadOnlyList<Item>[]> _tabsOfDungeons = new();

    /// <summary>
    /// Gets the tab of a character class.
    /// </summary>
    /// <param name="characterClass">The character class.</param>
    /// <returns>The index of the tab.</returns>
    public static int GetTabOfClass(CharacterClass? characterClass)
    {
        var tab = Array.IndexOf(ClassIndexOfTab, (characterClass?.Number ?? 4) / 4);
        return tab < 0 ? 0 : tab;
    }

    /// <summary>
    /// Gets the price of an item of a merchant, in tokens.
    /// </summary>
    /// <param name="merchant">The merchant.</param>
    /// <param name="definition">The definition of the item.</param>
    /// <returns>The price in tokens.</returns>
    public static int GetPrice(TokenMerchantSettings merchant, ItemDefinition definition)
    {
        var isSetPiece = definition.ItemSlot?.ItemSlots.Any(slot => slot >= InventoryConstants.HelmSlot && slot <= InventoryConstants.BootsSlot) ?? false;
        return Math.Max(0, isSetPiece ? merchant.SetPiecePrice : merchant.WeaponPrice);
    }

    /// <summary>
    /// Creates the item which a token merchant shows in its shop: excellent with all excellent options, with skill and optionally with luck.
    /// A bought item gets its excellent options like a dropped one (<see cref="DefaultDropGenerator.GenerateExcellentItemAsync"/>).
    /// </summary>
    /// <param name="definition">The definition of the item.</param>
    /// <param name="withLuck">If set to <c>true</c>, the item has luck.</param>
    /// <returns>The created item, which is not persistent.</returns>
    public static TemporaryItem CreateShownItem(ItemDefinition definition, bool withLuck)
    {
        var item = new TemporaryItem { Definition = definition };
        item.HasSkill = item.CanHaveSkill();
        foreach (var option in definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions))
        {
            if (option.OptionType == ItemOptionTypes.Excellent || (withLuck && option.OptionType == ItemOptionTypes.Luck))
            {
                item.ItemOptions.Add(new ItemOptionLink { ItemOption = option });
            }
        }

        // After the options, because the durability depends on them.
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Places the items into the grid of a shop, row by row.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The items which fit into the shop, with their slots.</returns>
    public static List<Item> PlaceItems(IEnumerable<Item> items)
    {
        var used = new bool[StoreRows, InventoryConstants.RowSize];
        var result = new List<Item>();
        foreach (var item in items)
        {
            var width = Math.Max((byte)1, item.Definition!.Width);
            var height = Math.Max((byte)1, item.Definition.Height);
            if (FindFreeSlot(used, width, height) is not var (row, column))
            {
                continue;
            }

            for (var r = row; r < row + height; r++)
            {
                for (var c = column; c < column + width; c++)
                {
                    used[r, c] = true;
                }
            }

            item.ItemSlot = (byte)((row * InventoryConstants.RowSize) + column);
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Gets the tab which the player has opened at a token merchant.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The merchant and the index of the tab, or <c>null</c> if the player has no token merchant opened.</returns>
    public static (TokenMerchantSettings Merchant, int Tab)? GetOpenedTab(Player player)
    {
        return player.OpenedNpc is { } npc
               && OpenedShops.TryGetValue(player, out var state)
               && state.Merchant.NpcDesignation == npc.Definition.Designation.ValueInNeutralLanguage
            ? (state.Merchant, state.Tab)
            : null;
    }

    /// <summary>
    /// Forgets the items of the merchants, so that they are created again, e.g. after a change of the configuration.
    /// </summary>
    public void Reset() => this._tabsOfDungeons = new();

    /// <summary>
    /// Gets the items of the tabs of a merchant.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="merchant">The merchant.</param>
    /// <returns>The items of each tab.</returns>
    public async ValueTask<IReadOnlyList<Item>[]> GetTabsAsync(IGameContext gameContext, TokenMerchantConfiguration configuration, TokenMerchantSettings merchant)
    {
        var tabsOfDungeons = this._tabsOfDungeons;
        if (tabsOfDungeons.TryGetValue(merchant.Dungeon, out var tabs))
        {
            return tabs;
        }

        tabs = Enumerable.Range(0, TabCount).Select(_ => (IReadOnlyList<Item>)[]).ToArray();
        if (gameContext.DropGenerator is DefaultDropGenerator dropGenerator
            && DungeonFeaturePlugIn.GetConfiguration(gameContext)?.Dungeons.FirstOrDefault(d => d.Number == merchant.Dungeon) is { } dungeon)
        {
            var equipment = await dropGenerator.GetBestZoneEquipmentAsync(dungeon.GetDropMapNumbers()).ConfigureAwait(false);
            for (var tab = 0; tab < TabCount; tab++)
            {
                if (equipment.TryGetValue(ClassIndexOfTab[tab], out var definitions))
                {
                    // The shown items have all excellent options; a bought item gets random ones.
                    tabs[tab] = PlaceItems(definitions.Select(definition => CreateShownItem(definition, configuration.ItemsHaveLuck)));
                }
            }
        }

        this._tabsOfDungeons = new Dictionary<byte, IReadOnlyList<Item>[]>(tabsOfDungeons) { [merchant.Dungeon] = tabs };
        return tabs;
    }

    /// <summary>
    /// Opens the shop of a merchant for a player, with the tab of the class of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="merchant">The merchant.</param>
    public async ValueTask OpenAsync(Player player, TokenMerchantConfiguration configuration, TokenMerchantSettings merchant)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var state = OpenedShops.GetOrCreateValue(player);
        state.Merchant = merchant;
        state.Tab = GetTabOfClass(character.CharacterClass);
        await player.InvokeViewPlugInAsync<IOpenNpcWindowPlugIn>(p => p.OpenNpcWindowAsync(NpcWindow.Merchant)).ConfigureAwait(false);
        await this.ShowTabAsync(player, configuration, state, false).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(string.Format(
            TokenMerchantMessages.ShopOpenedFormat,
            TokenWallet.GetTokens(character, merchant.Dungeon),
            merchant.TokenName,
            Math.Max(0, merchant.SetPiecePrice),
            Math.Max(0, merchant.WeaponPrice))).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows another tab of the opened shop to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="tab">The index of the tab.</param>
    public async ValueTask SwitchTabAsync(Player player, TokenMerchantConfiguration configuration, int tab)
    {
        if (GetOpenedTab(player) is null || !OpenedShops.TryGetValue(player, out var state))
        {
            await player.ShowBlueMessageAsync(TokenMerchantMessages.NoShopOpened).ConfigureAwait(false);
            return;
        }

        if (tab < 0 || tab >= TabCount)
        {
            await player.ShowBlueMessageAsync(TokenMerchantMessages.InvalidTab).ConfigureAwait(false);
            return;
        }

        state.Tab = tab;
        await this.ShowTabAsync(player, configuration, state, true).ConfigureAwait(false);
    }

    /// <summary>
    /// Buys the item of a slot of the opened tab for the tokens of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="slot">The slot of the item in the shop.</param>
    public async ValueTask BuyAsync(Player player, TokenMerchantConfiguration configuration, byte slot)
    {
        if (GetOpenedTab(player) is not var (merchant, tab)
            || player.SelectedCharacter is not { } character
            || player.Inventory is not { } inventory
            || (await this.GetTabsAsync(player.GameContext, configuration, merchant).ConfigureAwait(false))[tab].FirstOrDefault(i => i.ItemSlot == slot) is not { Definition: { } definition } storeItem)
        {
            await FailAsync(player, null).ConfigureAwait(false);
            return;
        }

        if (player.GameContext.DropGenerator is not DefaultDropGenerator dropGenerator)
        {
            await FailAsync(player, null).ConfigureAwait(false);
            return;
        }

        if (inventory.CheckInvSpace(storeItem) is not { } toSlot)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            await FailAsync(player, null).ConfigureAwait(false);
            return;
        }

        var price = GetPrice(merchant, definition);
        if (!TokenWallet.TrySpendTokens(player, merchant.Dungeon, price))
        {
            await FailAsync(player, string.Format(TokenMerchantMessages.NotEnoughTokensFormat, price, merchant.TokenName, TokenWallet.GetTokens(character, merchant.Dungeon))).ConfigureAwait(false);
            return;
        }

        var newItem = player.PersistenceContext.CreateNew<Item>();
        newItem.AssignValues(await dropGenerator.GenerateExcellentItemAsync(definition, configuration.ItemsHaveLuck).ConfigureAwait(false));
        newItem.ItemSlot = toSlot;
        await player.InvokeViewPlugInAsync<INpcItemBoughtPlugIn>(p => p.NpcItemBoughtAsync(newItem)).ConfigureAwait(false);
        await inventory.AddItemAsync(newItem).ConfigureAwait(false);

        var optionCount = newItem.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent);
        await player.ShowBlueMessageAsync(string.Format(
            optionCount == 1 ? TokenMerchantMessages.BoughtOneOptionFormat : TokenMerchantMessages.BoughtFormat,
            definition.Name.ValueInNeutralLanguage,
            optionCount,
            TokenWallet.GetTokens(character, merchant.Dungeon),
            merchant.TokenName)).ConfigureAwait(false);
        await this.ShowInfoAsync(player, configuration, merchant, tab).ConfigureAwait(false);
    }

    private static async ValueTask FailAsync(Player player, string? message)
    {
        if (message is not null)
        {
            await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IBuyNpcItemFailedPlugIn>(p => p.BuyNpcItemFailedAsync()).ConfigureAwait(false);
    }

    private static (int Row, int Column)? FindFreeSlot(bool[,] used, int width, int height)
    {
        for (var row = 0; row + height <= StoreRows; row++)
        {
            for (var column = 0; column + width <= InventoryConstants.RowSize; column++)
            {
                var isFree = true;
                for (var r = row; r < row + height && isFree; r++)
                {
                    for (var c = column; c < column + width && isFree; c++)
                    {
                        isFree = !used[r, c];
                    }
                }

                if (isFree)
                {
                    return (row, column);
                }
            }
        }

        return null;
    }

    private async ValueTask ShowTabAsync(Player player, TokenMerchantConfiguration configuration, ShopState state, bool showsTabName)
    {
        var tabs = await this.GetTabsAsync(player.GameContext, configuration, state.Merchant).ConfigureAwait(false);
        var items = tabs[state.Tab];
        await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(p => p.ShowMerchantStoreItemListAsync(items.ToList(), StoreKind.Normal)).ConfigureAwait(false);
        await this.ShowInfoAsync(player, configuration, state.Merchant, state.Tab).ConfigureAwait(false);
        if (tabs.All(tab => tab.Count == 0))
        {
            await player.ShowBlueMessageAsync(TokenMerchantMessages.NothingToSell).ConfigureAwait(false);
        }
        else if (showsTabName)
        {
            await player.ShowBlueMessageAsync(string.Format(TokenMerchantMessages.TabShownFormat, TokenMerchantMessages.TabNames[state.Tab])).ConfigureAwait(false);
        }
        else
        {
            // The message of the opened shop follows.
        }
    }

    /// <summary>
    /// Sends the data of the opened tab which the game client doesn't know (tokens, prices, tabs) to the client of the player.
    /// </summary>
    private async ValueTask ShowInfoAsync(Player player, TokenMerchantConfiguration configuration, TokenMerchantSettings merchant, int tab)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var tabs = await this.GetTabsAsync(player.GameContext, configuration, merchant).ConfigureAwait(false);
        var prices = tabs[tab]
            .Where(item => item.Definition is not null)
            .Select(item => (item.ItemSlot, item.Definition!.Width, item.Definition.Height, Price: (ushort)Math.Min(ushort.MaxValue, GetPrice(merchant, item.Definition))))
            .ToList();
        var tokens = TokenWallet.GetTokens(character, merchant.Dungeon);
        await player.InvokeViewPlugInAsync<IShowTokenShopPlugIn>(p => p.ShowTokenShopAsync((byte)tab, (uint)tokens, merchant.TokenName, prices)).ConfigureAwait(false);
    }

    private sealed class ShopState
    {
        public TokenMerchantSettings Merchant { get; set; } = new();

        public int Tab { get; set; }
    }
}
