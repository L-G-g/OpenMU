// <copyright file="DefaultDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using Nito.AsyncEx;

/// <summary>
/// The default drop generator.
/// </summary>
public class DefaultDropGenerator : IDropGenerator
{
    /// <summary>
    /// The amount of money which is dropped at least, and added to the gained experience.
    /// </summary>
    private const int BaseMoneyDrop = 7;
    private const int DropLevelMaxGap = 12;
    private const int SkillDropChancePercent = 50;

    private const byte DefaultMaxItemOptionLevelDrop = 3;
    private const byte MinItemOptionLevelDrop = 1;
    private const byte MaxItemOptionLevelDrop = 4;
    private const byte ShieldGroup = 6;

    /// <summary>
    /// A re-usable list of drop item groups.
    /// </summary>
    private readonly List<DropItemGroup> _chanceDropGroups = new(64);
    private readonly List<DropItemGroup> _guaranteedDropGroups = new(16);

    private readonly AsyncLock _lock = new();
    private readonly IRandomizer _randomizer;
    private readonly IList<ItemDefinition> _ancientItems;
    private readonly IList<ItemDefinition> _droppableItems;
    private readonly ICollection<DropItemGroup> _dropItemGroups;
    private readonly ICollection<GameMapDefinition> _maps;
    private readonly Dictionary<DropItemGroup, int> _strongestMonsterLevelPerZoneGroup = new();
    private readonly IList<ItemDefinition>?[] _droppableItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _droppableSocketItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _zoneEquipmentPerMaximumLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _zoneExcellentEquipmentPerMaximumLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _otherItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _otherExcellentItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];

    private readonly byte _maxItemOptionLevelDrop;
    private readonly byte _excellentItemDropLevelDelta;

    private IList<byte>? _zoneMaximumLevels;
    private HashSet<(CharacterClass Family, ItemDefinition Item)>? _ownWeapons;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDropGenerator" /> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="randomizer">The randomizer.</param>
    public DefaultDropGenerator(GameConfiguration config, IRandomizer randomizer)
    {
        this._excellentItemDropLevelDelta = config.ExcellentItemDropLevelDelta;
        this._randomizer = randomizer;
        this._maxItemOptionLevelDrop = IsValidOptionLevelDrop(config.MaximumItemOptionLevelDrop)
            ? config.MaximumItemOptionLevelDrop
            : DefaultMaxItemOptionLevelDrop;
        this._droppableItems = config.Items.Where(i => i.DropsFromMonsters).ToList();
        this._dropItemGroups = config.DropItemGroups;
        this._maps = config.Maps ?? [];
        this._ancientItems = this._droppableItems.Where(
            i => i.PossibleItemSetGroups.Any(
                g => g.Options?.PossibleOptions.Any(
                    o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false))
            .ToList();
    }

    /// <summary>
    /// The kinds of equipment which are ranked separately for the zone drops.
    /// </summary>
    private enum EquipmentKind
    {
        /// <summary>
        /// The pieces of the armor sets.
        /// </summary>
        ArmorSet,

        /// <summary>
        /// The shields.
        /// </summary>
        Shield,

        /// <summary>
        /// The own weapons of a family of character classes (see <see cref="GetOwnWeapons"/>).
        /// </summary>
        Weapon,

        /// <summary>
        /// The weapons which a family of character classes can use, but which aren't its own.
        /// They drop only in the zones before the ones of its own weapons.
        /// </summary>
        SharedWeapon,
    }

    /// <inheritdoc/>
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var character = player.SelectedCharacter;
        var map = player.CurrentMap?.Definition;
        if (map is null || character is null)
        {
            return ([], null);
        }

        using var l = await this._lock.LockAsync().ConfigureAwait(false);
        this._guaranteedDropGroups.Clear();
        this._chanceDropGroups.Clear();

        if (monster.ObjectKind == NpcObjectKind.Destructible)
        {
            this.PartitionDropGroups(monster.DropItemGroups ?? []);
        }
        else
        {
            var questItemGroups = (await GetQuestItemGroupsAsync(player).ConfigureAwait(false) ?? []).ToList();
            var soloQuestItemGroups = player.Party is null ? character.GetQuestDropItemGroups().ToList() : [];
            this.PartitionDropGroups(monster.DropItemGroups?.Where(group => IsDropOfNeededQuestItemsOrOther(group, soloQuestItemGroups)) ?? []);
            this.PartitionDropGroups(character.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(map.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(questItemGroups, monster);
        }

        uint money = 0;
        var (droppedItems, moneyResult) = this.GenerateDrops(monster, gainedExperience);
        if (moneyResult > 0)
        {
            money = moneyResult;
        }

        this._guaranteedDropGroups.Clear();
        this._chanceDropGroups.Clear();
        return (droppedItems ?? Enumerable.Empty<Item>(), money > 0 ? money : null);
    }

    /// <inheritdoc/>
    public Item? GenerateItemDrop(DropItemGroup selectedGroup)
    {
        return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems);
    }

    /// <inheritdoc/>
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups)
    {
        var group = this.SelectRandomGroup(groups.OrderBy(group => group.Chance), 1.0);
        if (group is null)
        {
            return (null, null, ItemDropEffect.Undefined);
        }

        var dropEffect = ItemDropEffect.Undefined;
        if (group is ItemDropItemGroup itemDropItemGroup)
        {
            dropEffect = itemDropItemGroup.DropEffect;

            if (group.ItemType == SpecialItemType.Money)
            {
                return (null, (uint)itemDropItemGroup.MoneyAmount, dropEffect);
            }
        }

        return (this.GenerateItemDrop(group), null, dropEffect);
    }

    /// <summary>
    /// Gets a random item.
    /// </summary>
    /// <param name="monsterLevel">The monster level.</param>
    /// <param name="isSocketItem">If set to <c>true</c>, it selects only socket items.</param>
    /// <returns>A random item.</returns>
    protected Item? GenerateRandomItem(int monsterLevel, bool isSocketItem)
    {
        var possible = this.GetPossibleList(monsterLevel, isSocketItem);
        var item = this.GenerateRandomItem(possible);
        if (item is null)
        {
            return null;
        }

        item.Level = GetItemLevelByMonsterLevel(item.Definition!, monsterLevel);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Applies random options to the item.
    /// </summary>
    /// <param name="item">The item.</param>
    protected void ApplyRandomOptions(Item item)
    {
        foreach (var option in item.Definition!.PossibleItemOptions.Where(o =>
            o.AddsRandomly &&
            !o.PossibleOptions.Any(po => object.Equals(po.OptionType, ItemOptionTypes.Excellent))))
        {
            this.ApplyOption(item, option);
        }

        if (item.Definition.MaximumSockets > 0)
        {
            item.SocketCount = this._randomizer.NextInt(1, item.Definition.MaximumSockets + 1);
        }

        if (item.CanHaveSkill())
        {
            item.HasSkill = this._randomizer.NextRandomBool(SkillDropChancePercent);
        }
    }

    /// <summary>
    /// Gets a random excellent item.
    /// </summary>
    /// <param name="monsterLevel">The monster level, if it's a monster drop.</param>
    /// <param name="possibleItems">The possible items, if the drop is from an item box (e.g. box of kundun).</param>
    /// <returns>A random excellent item.</returns>
    protected Item? GenerateRandomExcellentItem(int monsterLevel = 0, ICollection<ItemDefinition>? possibleItems = null)
    {
        if (monsterLevel < this._excellentItemDropLevelDelta && possibleItems is null)
        {
            return null;
        }

        var possible = possibleItems ?? this.GetPossibleList(monsterLevel - this._excellentItemDropLevelDelta);
        var item = this.GenerateRandomItem(possible);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every excellent item got skill

        this.AddRandomExcOptions(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Gets a random ancient item.
    /// </summary>
    /// <returns>A random ancient item.</returns>
    protected Item? GenerateRandomAncient()
    {
        var item = this.GenerateRandomItem(this._ancientItems);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every ancient item got skill

        this.ApplyRandomAncientOption(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    private static byte GetItemLevelByMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        return Math.Min((byte)((monsterLevel - itemDefinition.DropLevel) / 3), itemDefinition.MaximumItemLevel);
    }

    private static async ValueTask<IEnumerable<DropItemGroup>> GetQuestItemGroupsAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return [];
        }

        if (player.Party is { } party)
        {
            return await party.GetQuestDropItemGroupsAsync(player).ConfigureAwait(false);
        }

        return character.GetQuestDropItemGroups();
    }

    /// <summary>
    /// Determines whether a drop item group of a monster may drop for the player.
    /// </summary>
    /// <remarks>
    /// A group which only contains quest items (e.g. the quest items which the elite monsters of the
    /// first class change quest drop) only drops when a quest of the player requires one of its
    /// items with the same item level, and only when the player isn't in a party: these monsters are meant to be
    /// hunted alone. Other players couldn't pick the items up anyway.
    /// </remarks>
    private static bool IsDropOfNeededQuestItemsOrOther(DropItemGroup group, IReadOnlyCollection<DropItemGroup> questItemGroups)
    {
        if (group.PossibleItems.Count == 0 || group.PossibleItems.Any(item => !item.IsQuestItem))
        {
            return true;
        }

        return questItemGroups.Any(questGroup =>
            (questGroup.ItemLevel ?? 0) == (group.ItemLevel ?? 0)
            && questGroup.PossibleItems.Any(group.PossibleItems.Contains));
    }

    private static bool IsGroupRelevant(MonsterDefinition monsterDefinition, DropItemGroup group)
    {
        if (group.MinimumMonsterLevel.HasValue && monsterDefinition[Stats.Level] < group.MinimumMonsterLevel)
        {
            return false;
        }

        if (group.MaximumMonsterLevel.HasValue && monsterDefinition[Stats.Level] > group.MaximumMonsterLevel)
        {
            return false;
        }

        if (group.Monster is { } monster && !monster.Equals(monsterDefinition))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidOptionLevelDrop(byte value)
        => value is >= MinItemOptionLevelDrop and <= MaxItemOptionLevelDrop;

    private static bool CanDropAtMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        if (itemDefinition.DropLevel > monsterLevel)
        {
            return false;
        }

        return itemDefinition.MaximumDropLevel is not { } maxDropLevel || monsterLevel <= maxDropLevel;
    }

    /// <summary>
    /// Gets the item level of a weapon, shield or armor piece of a zone, which grows from 0 at the minimum level of the zone
    /// up to the maximum item level at its maximum level.
    /// </summary>
    private static byte GetZoneItemLevel(int monsterLevel, int minimumLevel, int maximumLevel, byte maximumItemLevel)
    {
        if (maximumLevel <= minimumLevel)
        {
            return maximumItemLevel;
        }

        var level = (monsterLevel - minimumLevel) * (maximumItemLevel + 1) / (maximumLevel - minimumLevel + 1);
        return (byte)Math.Clamp(level, 0, maximumItemLevel);
    }

    private static bool IsEquipment(ItemDefinition item)
    {
        return item.ItemSlot?.ItemSlots.Any(slot => slot >= InventoryConstants.LeftHandSlot && slot <= InventoryConstants.BootsSlot) ?? false;
    }

    private static bool HasExcellentOptions(ItemDefinition item)
    {
        return item.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => object.Equals(p.OptionType, ItemOptionTypes.Excellent)));
    }

    private static bool IsArmorPiece(ItemDefinition item)
    {
        return item.ItemSlot?.ItemSlots.Any(slot => slot >= InventoryConstants.HelmSlot && slot <= InventoryConstants.BootsSlot) ?? false;
    }

    /// <summary>
    /// Gets the kind of a weapon, shield or armor piece. The armor pieces are ranked as sets, the shields on their own,
    /// and all own weapons of a character class together (see <see cref="GetOwnWeapons"/>),
    /// so that a weapon type with few items doesn't drop in every zone.
    /// </summary>
    private static EquipmentKind GetEquipmentKind(ItemDefinition item)
    {
        if (IsArmorPiece(item))
        {
            return EquipmentKind.ArmorSet;
        }

        return item.Group == ShieldGroup ? EquipmentKind.Shield : EquipmentKind.Weapon;
    }

    /// <summary>
    /// Gets the kind of a weapon, shield or armor piece for a family of character classes.
    /// </summary>
    private static EquipmentKind GetEquipmentKind(ItemDefinition item, CharacterClass family, HashSet<(CharacterClass Family, ItemDefinition Item)> ownWeapons)
    {
        var kind = GetEquipmentKind(item);
        return kind == EquipmentKind.Weapon && !ownWeapons.Contains((family, item)) ? EquipmentKind.SharedWeapon : kind;
    }

    /// <summary>
    /// Gets the family of a character class, which is the class of its highest generation (e.g. the Blade Master for the Dark Knight).
    /// </summary>
    private static CharacterClass GetFamily(CharacterClass characterClass)
    {
        var family = characterClass;
        for (var generation = 0; family.NextGenerationClass is { } next && generation < 10; generation++)
        {
            family = next;
        }

        return family;
    }

    /// <summary>
    /// Gets the key of the rank of an item: the pieces of an armor set share their number, the other items are ranked alone.
    /// </summary>
    private static int GetRankKey(ItemDefinition item)
    {
        return IsArmorPiece(item) ? item.Number : (item.Group << 9) + item.Number;
    }

    /// <summary>
    /// Gets the ranks of the items of one kind, from the weakest to the strongest. A rank contains the items with the same number,
    /// which are the pieces of an armor set, and is ordered by the drop level of the armor, or of its lowest piece.
    /// </summary>
    private static List<IGrouping<int, ItemDefinition>> GetRanks(IEnumerable<ItemDefinition> items)
    {
        return items
            .GroupBy(GetRankKey)
            .OrderBy(GetRankLevel)
            .ThenBy(rank => rank.Key)
            .ToList();
    }

    private static int GetRankLevel(IGrouping<int, ItemDefinition> rank)
    {
        return rank.FirstOrDefault(item => item.ItemSlot?.ItemSlots.Contains(InventoryConstants.ArmorSlot) ?? false)?.DropLevel
               ?? rank.Min(item => item.DropLevel);
    }

    /// <summary>
    /// Gets the ranks which the zone with the specified index drops, when the ranks are spread evenly over all zones,
    /// from the weakest one in the first zone to the strongest one in the last zone.
    /// An armor set kind gives exactly one set per zone; if there are more sets than zones, some are skipped,
    /// and if there are less, neighboring zones drop the same set. The weapons and shields are split into
    /// consecutive parts, one per zone, or one item per zone if there are less items than zones.
    /// </summary>
    private static IEnumerable<IGrouping<int, ItemDefinition>> GetZoneRanks(EquipmentKind kind, List<IGrouping<int, ItemDefinition>> ranks, int zoneIndex, int zoneCount)
    {
        if (kind != EquipmentKind.ArmorSet)
        {
            var first = DivideRounded(zoneIndex * ranks.Count, zoneCount);
            var end = DivideRounded((zoneIndex + 1) * ranks.Count, zoneCount);
            if (end > first)
            {
                return ranks.GetRange(first, end - first);
            }
        }

        var index = zoneCount > 1 ? DivideRounded(zoneIndex * (ranks.Count - 1), zoneCount - 1) : ranks.Count - 1;
        return [ranks[index]];
    }

    /// <summary>
    /// Gets the ranks which the zone with the specified index drops for a kind of equipment of a family of character classes.
    /// The own weapons of a family start in the first zone whose maximum level reaches the drop level of its weakest one,
    /// and the zones before drop the weaker weapons which it shares with other families. This way, a family with few own
    /// weapons (e.g. the gloves of the Rage Fighter) doesn't drop a strong weapon in the first zones.
    /// </summary>
    private static IEnumerable<IGrouping<int, ItemDefinition>> GetZoneRanks(
        EquipmentKind kind,
        List<IGrouping<int, ItemDefinition>> ranks,
        List<IGrouping<int, ItemDefinition>>? sharedWeapons,
        IList<byte> zoneMaximumLevels,
        int zoneIndex)
    {
        var zoneCount = zoneMaximumLevels.Count;
        if (kind == EquipmentKind.Weapon && sharedWeapons is not null)
        {
            var firstLevel = GetRankLevel(ranks[0]);
            var start = zoneMaximumLevels.Count(level => level < firstLevel);
            var weaker = sharedWeapons.Where(rank => GetRankLevel(rank) < firstLevel).ToList();
            if (start > 0 && start < zoneCount && weaker.Count > 0)
            {
                return zoneIndex < start
                    ? GetZoneRanks(kind, weaker, zoneIndex, start)
                    : GetZoneRanks(kind, ranks, zoneIndex - start, zoneCount - start);
            }
        }

        return GetZoneRanks(kind, ranks, zoneIndex, zoneCount);
    }

    private static int DivideRounded(int dividend, int divisor)
    {
        return ((2 * dividend) + divisor) / (2 * divisor);
    }

    private (IList<Item>? Items, uint Money) GenerateDrops(MonsterDefinition monster, int gainedExperience)
    {
        uint money = 0;
        List<Item>? droppedItems = null;
        var remainingDrops = monster.NumberOfMaximumItemDrops;

        // Guaranteed groups.
        foreach (var group in this._guaranteedDropGroups)
        {
            if (remainingDrops <= 0)
            {
                break;
            }

            var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, out var droppedMoney);
            if (item is not null)
            {
                droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                droppedItems.Add(item);
            }

            if (droppedMoney is not null)
            {
                money += droppedMoney.Value;
            }

            remainingDrops--;
        }

        // Chance based groups.
        if (remainingDrops > 0 && this._chanceDropGroups.Count > 0)
        {
            double totalChance = 0;
            foreach (var group in this._chanceDropGroups)
            {
                totalChance += group.Chance;
            }

            for (int i = 0; i < remainingDrops; i++)
            {
                var group = this.SelectRandomGroup(this._chanceDropGroups, totalChance);
                if (group is null)
                {
                    continue;
                }

                var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, out var droppedMoney);
                if (item is not null)
                {
                    droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                    droppedItems.Add(item);
                }

                if (droppedMoney is not null)
                {
                    money += droppedMoney.Value;
                }
            }
        }

        return (droppedItems, money);
    }

    private void PartitionDropGroups(IEnumerable<DropItemGroup> groups, MonsterDefinition? monster = null)
    {
        foreach (var group in groups)
        {
            if (group.Chance <= 0 || (monster is not null && !IsGroupRelevant(monster, group)))
            {
                // A group without chance never drops, e.g. a quest item which only drops from specific monsters.
                continue;
            }

            if (group.Chance >= 1.0)
            {
                this._guaranteedDropGroups.Add(group);
            }
            else
            {
                this._chanceDropGroups.Add(group);
            }
        }
    }

    private Item? GenerateItemDrop(DropItemGroup selectedGroup, ICollection<ItemDefinition> possibleItems)
    {
        var item = selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(possibleItems: possibleItems),
            _ => this.GenerateRandomItem(possibleItems),
        };

        if (item is null)
        {
            return null;
        }

        if (item.Durability == 0)
        {
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
        }

        if (selectedGroup is ItemDropItemGroup itemDropItemGroup)
        {
            item.Level = (byte)this._randomizer.NextInt(itemDropItemGroup.MinimumLevel, itemDropItemGroup.MaximumLevel + 1);
        }
        else if (selectedGroup.ItemLevel is { } itemLevel)
        {
            item.Level = itemLevel;
        }
        else
        {
            // no level defined, so it stays at 0.
        }

        item.Level = Math.Min(item.Level, item.Definition!.MaximumItemLevel);

        return item;
    }

    private void ApplyOption(Item item, ItemOptionDefinition option)
    {
        for (int i = 0; i < option.MaximumOptionsPerItem; i++)
        {
            if (this._randomizer.NextRandomBool(option.AddChance))
            {
                var remainingOptions = option.PossibleOptions.Where(possibleOption => item.ItemOptions.All(link => link.ItemOption != possibleOption));
                var newOption = remainingOptions.SelectRandom(this._randomizer);
                if (newOption is null)
                {
                    break;
                }

                var itemOptionLink = new ItemOptionLink
                {
                    ItemOption = newOption,
                    Level = newOption.LevelDependentOptions
                        .Select(ldo => ldo.Level)
                        .Concat(newOption.LevelDependentOptions.Count > 0 ? [1] : []) // For base def/dmg opts level 1 is not an ItemOptionOfLevel entry
                        .Distinct()
                        .Where(l => l <= this._maxItemOptionLevelDrop)
                        .DefaultIfEmpty(0)
                        .SelectRandom(),
                };
                item.ItemOptions.Add(itemOptionLink);
            }
        }
    }

    private Item? GenerateRandomItem(ICollection<ItemDefinition>? possibleItems)
    {
        if (possibleItems is null || possibleItems.Count == 0)
        {
            return null;
        }

        return this.CreateRandomItem(possibleItems.ElementAt(this._randomizer.NextInt(0, possibleItems.Count)));
    }

    private Item CreateRandomItem(ItemDefinition definition)
    {
        var item = new TemporaryItem
        {
            Definition = definition,
        };

        this.ApplyRandomOptions(item);

        return item;
    }

    private void ApplyRandomAncientOption(Item item)
    {
        var ancientSet = item.Definition?.PossibleItemSetGroups
            .Where(g => g!.Options?.PossibleOptions.Any(o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false)
            .SelectRandom(this._randomizer);
        if (ancientSet is null)
        {
            return;
        }

        var itemOfSet = ancientSet.Items.First(i => object.Equals(i.ItemDefinition, item.Definition));
        item.ItemSetGroups.Add(itemOfSet);

        // For example: +5str or +10str.
        if (itemOfSet.BonusOption is { } bonusOption)
        {
            var bonusOptionLink = new ItemOptionLink();
            bonusOptionLink.ItemOption = bonusOption;
            bonusOptionLink.Level = bonusOption.LevelDependentOptions.Select(o => o.Level).SelectRandom();
            item.ItemOptions.Add(bonusOptionLink);
        }
    }

    private void AddRandomExcOptions(Item item)
    {
        var excellentOptions = item.Definition!.PossibleItemOptions.FirstOrDefault(
            o => o.PossibleOptions.Any(p => object.Equals(p.OptionType, ItemOptionTypes.Excellent)));

        if (excellentOptions is null)
        {
            return;
        }

        var existingOptionCount = item.ItemOptions.Count(o => object.Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));

        for (int i = existingOptionCount; i < excellentOptions.MaximumOptionsPerItem; i++)
        {
            if (i == 0)
            {
                // The first option is always added without a chance
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                    existingOptionCount++;
                }

                continue;
            }

            if (this._randomizer.NextRandomBool(excellentOptions.AddChance))
            {
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                while (item.ItemOptions.Any(o => object.Equals(o.ItemOption, newOption)))
                {
                    newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                }

                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                }
            }
        }
    }

    private Item? GenerateItemDropOrMoney(MonsterDefinition monster, DropItemGroup selectedGroup, int gainedExperience, out uint? droppedMoney)
    {
        droppedMoney = null;

        if (selectedGroup.ItemType is SpecialItemType.ZoneItem or SpecialItemType.ZoneExcellentItem)
        {
            return this.GenerateZoneItem(monster, selectedGroup);
        }

        if (selectedGroup.PossibleItems?.Count > 0)
        {
            return this.GenerateItemFromGroup(monster, selectedGroup);
        }

        var item = this.GenerateSpecialItem(monster, selectedGroup);
        if (item is null && selectedGroup.ItemType == SpecialItemType.Money)
        {
            droppedMoney = (uint)(gainedExperience + BaseMoneyDrop);
        }

        return item;
    }

    private Item? GenerateItemFromGroup(MonsterDefinition monster, DropItemGroup selectedGroup)
    {
        var isDropSpecificForMonster = monster.DropItemGroups.Contains(selectedGroup);
        if (isDropSpecificForMonster)
        {
            return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems!);
        }

        var monsterLevel = (int)monster[Stats.Level];
        var isJewel = selectedGroup.ItemType == SpecialItemType.Jewel;

        var filteredPossibleItems = selectedGroup.PossibleItems!
            .Where(it => CanDropAtMonsterLevel(it, monsterLevel)
                         && (isJewel || it.DropLevel == 0 || it.DropLevel > monsterLevel - DropLevelMaxGap))
            .ToList();

        return this.GenerateItemDrop(selectedGroup, filteredPossibleItems);
    }

    /// <summary>
    /// Generates an item of a <see cref="SpecialItemType.ZoneItem"/> or <see cref="SpecialItemType.ZoneExcellentItem"/> group.
    /// </summary>
    /// <remarks>
    /// The weapons, shields and armor pieces come from <see cref="GetZoneEquipment"/>, or from the possible items of the group,
    /// and the other items (e.g. potions, jewelry) are chosen by the monster level as usual.
    /// </remarks>
    private Item? GenerateZoneItem(MonsterDefinition monster, DropItemGroup group)
    {
        var monsterLevel = (int)monster[Stats.Level];
        var minimumLevel = group.MinimumMonsterLevel ?? 0;
        var maximumLevel = group.MaximumMonsterLevel ?? byte.MaxValue;
        var isExcellent = group.ItemType == SpecialItemType.ZoneExcellentItem;

        var equipment = group.PossibleItems.Count > 0 ? group.PossibleItems : this.GetZoneEquipment(maximumLevel, isExcellent);
        var others = group.PossibleItems.Count > 0 ? null : this.GetOtherItems(monsterLevel, isExcellent);
        var count = equipment.Count + (others?.Count ?? 0);
        if (count == 0)
        {
            return null;
        }

        var index = this._randomizer.NextInt(0, count);
        var isEquipment = index < equipment.Count;
        var definition = isEquipment ? equipment.ElementAt(index) : others![index - equipment.Count];
        var item = this.CreateRandomItem(definition);
        if (isExcellent)
        {
            item.HasSkill = item.CanHaveSkill(); // every excellent item got skill
            this.AddRandomExcOptions(item);
        }
        else
        {
            item.Level = isEquipment
                ? GetZoneItemLevel(monsterLevel, minimumLevel, this.GetStrongestMonsterLevel(group, minimumLevel, maximumLevel), group.ItemLevel ?? 0)
                : GetItemLevelByMonsterLevel(definition, monsterLevel);
            item.Level = Math.Min(item.Level, definition.MaximumItemLevel);
        }

        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Gets the level of the strongest monster in the level range of a zone group which spawns on the maps of the group,
    /// so that it drops the maximum item level even if the strongest monsters of the range don't spawn there,
    /// e.g. because the monsters are grouped into spots.
    /// </summary>
    private int GetStrongestMonsterLevel(DropItemGroup group, int minimumLevel, int maximumLevel)
    {
        if (this._strongestMonsterLevelPerZoneGroup.TryGetValue(group, out var level))
        {
            return level;
        }

        level = this._maps
            .Where(map => map.DropItemGroups.Contains(group))
            .SelectMany(map => map.MonsterSpawns)
            .Select(spawn => (int)(spawn.MonsterDefinition?[Stats.Level] ?? 0))
            .Where(monsterLevel => monsterLevel >= minimumLevel && monsterLevel <= maximumLevel)
            .DefaultIfEmpty(maximumLevel)
            .Max();
        this._strongestMonsterLevelPerZoneGroup[group] = level;
        return level;
    }

    /// <summary>
    /// Gets the weapons, shields and armor pieces which a zone drops. For each family of character classes (see <see cref="GetFamily"/>)
    /// and each kind of item (armor sets, shields and own weapons), the items are ordered by their drop level and spread evenly
    /// over the zones, which are ordered by their maximum level.
    /// The pieces of an armor set are ranked together by the drop level of its armor, so they drop in the same zone.
    /// Socket items are not included, they can be added to a zone with a group which has them as possible items.
    /// </summary>
    private IList<ItemDefinition> GetZoneEquipment(byte maximumLevel, bool isExcellent)
    {
        var cache = isExcellent ? this._zoneExcellentEquipmentPerMaximumLevel : this._zoneEquipmentPerMaximumLevel;
        if (cache[maximumLevel] is { } cached)
        {
            return cached;
        }

        var zoneMaximumLevels = this.GetZoneMaximumLevels(maximumLevel);
        var zoneIndex = zoneMaximumLevels.IndexOf(maximumLevel);
        var ownWeapons = this._ownWeapons ??= this.GetOwnWeapons();
        var ranksOfFamilies = this._droppableItems
            .Where(item => IsEquipment(item) && item.MaximumSockets == 0)
            .SelectMany(item => item.QualifiedCharacters.Select(GetFamily).Distinct()
                .Select(family => (Family: family, Kind: GetEquipmentKind(item, family, ownWeapons), Item: item)))
            .GroupBy(entry => (entry.Family, entry.Kind), entry => entry.Item)
            .ToDictionary(familyKind => familyKind.Key, GetRanks);
        return cache[maximumLevel] = ranksOfFamilies
            .Where(entry => entry.Key.Kind != EquipmentKind.SharedWeapon)
            .SelectMany(entry => GetZoneRanks(
                entry.Key.Kind,
                entry.Value,
                ranksOfFamilies.GetValueOrDefault((entry.Key.Family, EquipmentKind.SharedWeapon)),
                zoneMaximumLevels,
                zoneIndex))
            .SelectMany(rank => rank)
            .Where(item => !isExcellent || HasExcellentOptions(item))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Gets the weapons which belong to each family of character classes (e.g. the Dark Knight, Blade Knight and Blade Master),
    /// so that each class drops only its own weapons in the zones: the ones of the item groups in which the family has
    /// weapons which only it can use (e.g. the staffs of the Dark Wizard), without the weapons which every family can use.
    /// A family without such weapons gets all weapons which it can use.
    /// </summary>
    private HashSet<(CharacterClass Family, ItemDefinition Item)> GetOwnWeapons()
    {
        var weapons = this._droppableItems
            .Where(item => IsEquipment(item) && item.MaximumSockets == 0 && GetEquipmentKind(item) == EquipmentKind.Weapon)
            .Select(item => (Item: item, Families: item.QualifiedCharacters.Select(GetFamily).ToHashSet()))
            .ToList();
        var familyCount = weapons.SelectMany(weapon => weapon.Families).Distinct().Count();
        var result = new HashSet<(CharacterClass Family, ItemDefinition Item)>();
        foreach (var family in weapons.SelectMany(weapon => weapon.Families).Distinct())
        {
            var usable = weapons.Where(weapon => weapon.Families.Contains(family)).ToList();
            var ownGroups = usable.Where(weapon => weapon.Families.Count == 1).Select(weapon => weapon.Item.Group).ToHashSet();
            var own = usable
                .Where(weapon => ownGroups.Contains(weapon.Item.Group) && (familyCount == 1 || weapon.Families.Count < familyCount))
                .ToList();
            foreach (var weapon in own.Count > 0 ? own : usable)
            {
                result.Add((family, weapon.Item));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the maximum levels of all zones in ascending order, including the specified one. The zones are the ones of all
    /// <see cref="SpecialItemType.ZoneItem"/> and <see cref="SpecialItemType.ZoneExcellentItem"/> groups without possible items.
    /// </summary>
    private IList<byte> GetZoneMaximumLevels(byte maximumLevel)
    {
        this._zoneMaximumLevels ??= this._dropItemGroups
            .Where(group => group.ItemType is SpecialItemType.ZoneItem or SpecialItemType.ZoneExcellentItem && group.PossibleItems.Count == 0)
            .Select(group => group.MaximumMonsterLevel ?? byte.MaxValue)
            .Distinct()
            .Order()
            .ToList();
        return this._zoneMaximumLevels.Contains(maximumLevel)
            ? this._zoneMaximumLevels
            : this._zoneMaximumLevels.Append(maximumLevel).Order().ToList();
    }

    /// <summary>
    /// Gets the items which aren't weapons, shields or armor pieces, which drop from a monster of a zone as usual.
    /// </summary>
    private IList<ItemDefinition> GetOtherItems(int monsterLevel, bool isExcellent)
    {
        var level = isExcellent ? monsterLevel - this._excellentItemDropLevelDelta : monsterLevel;
        if (this.GetPossibleList(level) is not { } possible)
        {
            return [];
        }

        var cache = isExcellent ? this._otherExcellentItemsPerMonsterLevel : this._otherItemsPerMonsterLevel;
        return cache[level] ??= possible
            .Where(item => !IsEquipment(item) && (!isExcellent || HasExcellentOptions(item)))
            .ToList();
    }

    private Item? GenerateSpecialItem(MonsterDefinition monster, DropItemGroup selectedGroup)
    {
        var monsterLevel = (int)monster[Stats.Level];
        return selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(monsterLevel),
            SpecialItemType.RandomItem => this.GenerateRandomItem(monsterLevel, false),
            SpecialItemType.SocketItem => this.GenerateRandomItem(monsterLevel, true),
            _ => null,
        };
    }

    private DropItemGroup? SelectRandomGroup(IEnumerable<DropItemGroup> groups, double totalChance)
    {
        var remainingThreshold = this._randomizer.NextDouble();
        if (totalChance > 1.0)
        {
            remainingThreshold *= totalChance;
        }

        foreach (var group in groups)
        {
            if (remainingThreshold > group.Chance)
            {
                remainingThreshold -= group.Chance;
            }
            else
            {
                return group;
            }
        }

        return null;
    }

    private IList<ItemDefinition>? GetPossibleList(int monsterLevel, bool isSocketItem = false)
    {
        if (monsterLevel is < byte.MinValue or > byte.MaxValue)
        {
            return null;
        }

        var cache = isSocketItem ? this._droppableSocketItemsPerMonsterLevel : this._droppableItemsPerMonsterLevel;
        return cache[monsterLevel]
            ??= (from it in this._droppableItems
                 where CanDropAtMonsterLevel(it, monsterLevel)
                       && (it.DropLevel > monsterLevel - DropLevelMaxGap)
                       && (!isSocketItem || it.MaximumSockets > 0)
                 select it).ToList();
    }
}
