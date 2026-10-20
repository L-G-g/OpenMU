// <copyright file="EventEntryItemShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Changes the data for a game in which the items to enter the events are bought at Lumen the Barmaid in Lorencia
/// instead of found.
/// </summary>
/// <remarks>
/// This is a custom feature, in the original game the items to enter Blood Castle, Devil Square, Illusion Temple,
/// Doppelganger and Imperial Guardian drop from monsters. Here they don't drop anymore, and Lumen the Barmaid sells all of them.
/// The Symbol of Kundun to enter Kalima still drops, see <see cref="MapLevelLadder.SymbolOfKundunDropLevels"/>.
/// </remarks>
internal static class EventEntryItemShop
{
    /// <summary>
    /// The number of Lumen the Barmaid, the merchant in the bar of Lorencia.
    /// </summary>
    internal const short LumenTheBarmaidNumber = 255;

    /// <summary>
    /// Gets the items to enter the events which don't drop from monsters anymore. Lumen sells all of them.
    /// </summary>
    internal static IReadOnlyList<(byte Group, short Number)> EventItemsWithoutDrop { get; } =
    [
        (13, 16), // Scroll of Archangel (Blood Castle)
        (13, 17), // Blood Bone (Blood Castle)
        (14, 17), // Devil's Eye (Devil Square)
        (14, 18), // Devil's Key (Devil Square)
        (13, 49), // Old Scroll (Illusion Temple)
        (13, 50), // Illusion Sorcerer Covenant (Illusion Temple)
        (14, 110), // Sign of Dimensions (Doppelganger)
        (14, 101), // Suspicious Scrap of Paper (Imperial Guardian)
    ];

    /// <summary>
    /// Gets the items which are added to the store of Lumen, which already sells the items of the other levels.
    /// The slots are free in her original store.
    /// </summary>
    internal static IReadOnlyList<(byte Slot, byte Group, short Number, byte Level)> AddedLumenItems { get; } =
    [
        (63, 13, 49, 6), // Old Scroll +6 (Illusion Temple 6)
        (67, 13, 50, 6), // Illusion Sorcerer Covenant +6 (Illusion Temple 6), 1x2
        (87, 14, 111, 0), // Mirror of Dimensions (Doppelganger)
        (88, 14, 102, 0), // Gaion's Order (Imperial Guardian)
    ];

    /// <summary>
    /// Applies the changes to the configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var item in gameConfiguration.Items.Where(item => EventItemsWithoutDrop.Contains((item.Group, item.Number))))
        {
            item.DropsFromMonsters = false;
        }

        RemoveDropsOfEventItems(gameConfiguration);
        AddEventItemsToLumen(context, gameConfiguration);
    }

    /// <summary>
    /// Determines whether the drop item group only drops items to enter the events, which don't drop anymore.
    /// </summary>
    /// <param name="group">The drop item group.</param>
    /// <returns><c>true</c>, if the group only drops items to enter the events.</returns>
    internal static bool IsEventItemDropGroup(DropItemGroup group)
    {
        return group.PossibleItems.Count > 0
               && group.PossibleItems.All(item => EventItemsWithoutDrop.Contains((item.Group, item.Number)));
    }

    private static void RemoveDropsOfEventItems(GameConfiguration gameConfiguration)
    {
        foreach (var map in gameConfiguration.Maps)
        {
            foreach (var group in map.DropItemGroups.Where(IsEventItemDropGroup).ToList())
            {
                map.DropItemGroups.Remove(group);
            }
        }

        foreach (var monster in gameConfiguration.Monsters)
        {
            foreach (var group in monster.DropItemGroups.Where(IsEventItemDropGroup).ToList())
            {
                monster.DropItemGroups.Remove(group);
            }
        }
    }

    private static void AddEventItemsToLumen(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == LumenTheBarmaidNumber)?.MerchantStore is not { } store)
        {
            return;
        }

        foreach (var (slot, group, number, level) in AddedLumenItems)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } definition
                || store.Items.Any(i => i.ItemSlot == slot || (i.Definition == definition && i.Level == level)))
            {
                continue;
            }

            var item = context.CreateNew<Item>();
            item.Definition = definition;
            item.ItemSlot = slot;
            item.Durability = 1;
            item.Level = level;
            store.Items.Add(item);
        }
    }
}
