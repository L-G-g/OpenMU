// <copyright file="PotionStacks.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Lets the potions stack up to 255 pieces, and lets the merchants which sell potions also sell a full stack of each of them.
/// </summary>
/// <remarks>
/// The number of pieces of a stack is the durability of the item, which is sent to the client as one byte,
/// so 255 is the biggest possible stack. The original stacks were 3 pieces (healing, mana, shield and complex potions,
/// apple and antidote) or 10 pieces (siege potions and the potions of the Halloween and Cherry Blossom events).
/// Items which turn into another item when their stack is full, like the Symbol of Kundun, keep their stack size.
/// A full stack of the most expensive potion, the Potion of Bless, costs 229.500.000 zen, so all of them can be paid
/// with the zen which fits into a trade (999.999.999).
/// </remarks>
internal static class PotionStacks
{
    /// <summary>
    /// The maximum number of pieces of a stack.
    /// </summary>
    internal const byte StackSize = byte.MaxValue;

    private const byte PotionGroup = 14;

    private const byte StoreColumns = 8;

    private const byte StoreSlots = StoreColumns * 15;

    /// <summary>
    /// Gets the numbers of the items of group 14 which stack up to <see cref="StackSize"/>.
    /// </summary>
    internal static IReadOnlyList<short> StackedPotionNumbers { get; } =
    [
        0, // Apple
        1, // Small Healing Potion
        2, // Medium Healing Potion
        3, // Large Healing Potion
        4, // Small Mana Potion
        5, // Medium Mana Potion
        6, // Large Mana Potion
        7, // Potion of Bless / Potion of Soul (siege potions)
        8, // Antidote
        35, // Small Shield Potion
        36, // Medium Shield Potion
        37, // Large Shield Potion
        38, // Small Complex Potion
        39, // Medium Complex Potion
        40, // Large Complex Potion
        46, // Jack O'Lantern Blessings
        47, // Jack O'Lantern Wrath
        48, // Jack O'Lantern Cry
        49, // Jack O'Lantern Food
        50, // Jack O'Lantern Drink
        85, // Cherry Blossom Wine
        86, // Cherry Blossom Rice Cake
        87, // Cherry Blossom Flower Petal
    ];

    /// <summary>
    /// Applies the changes to the configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var potions = gameConfiguration.Items
            .Where(item => item.Group == PotionGroup && StackedPotionNumbers.Contains(item.Number))
            .ToList();
        foreach (var potion in potions)
        {
            potion.Durability = StackSize;
        }

        foreach (var store in gameConfiguration.Monsters.Select(m => m.MerchantStore).OfType<ItemStorage>())
        {
            AddFullStacks(context, store, potions);
        }
    }

    private static void AddFullStacks(IContext context, ItemStorage store, IReadOnlyCollection<ItemDefinition> potions)
    {
        var soldPotions = store.Items
            .Where(item => item.Definition is { } definition && potions.Contains(definition))
            .OrderBy(item => item.ItemSlot)
            .Select(item => (Definition: item.Definition!, item.Level))
            .Distinct()
            .Where(potion => !store.Items.Any(item => item.Definition == potion.Definition && item.Level == potion.Level && item.Durability >= StackSize))
            .ToList();
        if (soldPotions.Count == 0)
        {
            return;
        }

        var occupied = GetOccupiedSlots(store);

        // The full stacks start in a new row below the other items, if there is enough space.
        var lastOccupiedRow = occupied.Select((isOccupied, index) => isOccupied ? index / StoreColumns : -1).Max();
        var firstSlot = (lastOccupiedRow + 1) * StoreColumns;
        if (firstSlot + soldPotions.Count > StoreSlots)
        {
            firstSlot = 0;
        }

        var slot = firstSlot;
        foreach (var (definition, level) in soldPotions)
        {
            while (slot < StoreSlots && occupied[slot])
            {
                slot++;
            }

            if (slot >= StoreSlots)
            {
                // The store is full.
                return;
            }

            var item = context.CreateNew<Item>();
            item.Definition = definition;
            item.ItemSlot = (byte)slot;
            item.Durability = StackSize;
            item.Level = level;
            store.Items.Add(item);
            occupied[slot] = true;
        }
    }

    private static bool[] GetOccupiedSlots(ItemStorage store)
    {
        var occupied = new bool[StoreSlots];
        foreach (var item in store.Items)
        {
            var width = Math.Max((byte)1, item.Definition?.Width ?? 1);
            var height = Math.Max((byte)1, item.Definition?.Height ?? 1);
            var column = item.ItemSlot % StoreColumns;
            var row = item.ItemSlot / StoreColumns;
            for (var y = row; y < row + height; y++)
            {
                for (var x = column; x < column + width && x < StoreColumns; x++)
                {
                    var slot = (y * StoreColumns) + x;
                    if (slot >= 0 && slot < StoreSlots)
                    {
                        occupied[slot] = true;
                    }
                }
            }
        }

        return occupied;
    }
}
