// <copyright file="DropGeneratorTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests the drop generator.
/// </summary>
[TestFixture]
public class DropGeneratorTest
{
    /// <summary>
    /// Tests if the drop fails because the randomizer returns a number which causes a fail.
    /// </summary>
    [Test]
    public async ValueTask TestDropFailAsync()
    {
        var config = this.GetGameConfig();
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(9999));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 0), 0, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();
        Assert.That(item, Is.Null);
    }

    /// <summary>
    /// Tests the drops defined by a monster are getting considered.
    /// </summary>
    [Test]
    public async ValueTask TestItemDropItemByMonsterAsync()
    {
        var config = this.GetGameConfig();
        var monster = this.GetMonster(1, 0);
        monster.DropItemGroups.AddBasicDropItemGroups();
        monster.DropItemGroups.Add(3000, SpecialItemType.RandomItem, true);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);

        // ReSharper disable once PossibleNullReferenceException
        Assert.That(item!.Definition, Is.EqualTo(monster.DropItemGroups.Last().PossibleItems.First()));
    }

    /// <summary>
    /// Tests that items with a maximum drop level are filtered from generic monster drops.
    /// </summary>
    [Test]
    public async ValueTask TestMaximumDropLevelAsync()
    {
        var config = this.GetGameConfig();
        var cappedItem = this.CreateItemDefinition(12, 15, 12, 66);
        var uncappedItem = this.CreateItemDefinition(14, 13, 25);

        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = 1.0;
        dropGroup.Object.ItemType = SpecialItemType.Jewel;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { cappedItem, uncappedItem });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(dropGroup.Object);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 67), 1, player).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(uncappedItem));
    }

    /// <summary>
    /// Tests that a drop item group of a monster with only quest items drops only for a player
    /// whose active quest requires the item.
    /// </summary>
    [Test]
    public async ValueTask TestQuestItemDropOfMonsterOnlyForQuestAsync()
    {
        var config = this.GetGameConfig();
        var questItem = this.CreateItemDefinition(14, 23, 0);
        questItem.IsQuestItem = true;
        var monster = this.GetMonster(1, 47);
        monster.DropItemGroups.Add(this.CreateDropItemGroup(1.0, questItem));
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.9999));

        var (itemsWithoutQuest, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);
        Assert.That(itemsWithoutQuest.Select(i => i.Definition), Does.Not.Contain(questItem));

        // The quest requirement's own group has no chance, so that only the monster drops the item.
        var requirement = new Mock<QuestItemRequirement>();
        requirement.SetupAllProperties();
        requirement.Object.Item = questItem;
        requirement.Object.DropItemGroup = this.CreateDropItemGroup(0, questItem);
        var quest = new Mock<QuestDefinition>();
        quest.Setup(q => q.RequiredItems).Returns(new List<QuestItemRequirement> { requirement.Object });
        Mock.Get(player.SelectedCharacter!).Setup(c => c.QuestStates).Returns(new List<CharacterQuestState> { new() { ActiveQuest = quest.Object } });

        var (itemsWithQuest, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);
        Assert.That(itemsWithQuest.Select(i => i.Definition), Is.EqualTo(new[] { questItem }));
    }

    /// <summary>
    /// Tests that a zone drops, of the weapons of a class, only the one with the highest drop level up to the maximum
    /// level of the zone, with an item level which depends on the place of the monster in the zone.
    /// </summary>
    /// <param name="monsterLevel">The level of the monster.</param>
    /// <param name="expectedItemLevel">The expected item level.</param>
    [TestCase(20, 0)]
    [TestCase(24, 1)]
    [TestCase(29, 3)]
    public async ValueTask TestZoneItemDropAsync(byte monsterLevel, byte expectedItemLevel)
    {
        var config = this.GetGameConfig();
        var characterClass = new CharacterClass();
        var slot = new Mock<ItemSlotType>();
        slot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.LeftHandSlot });
        var swords = new[] { 10, 20, 30 }.Select(dropLevel => this.CreateEquipment(0, (short)dropLevel, (byte)dropLevel, characterClass, slot.Object)).ToList();
        swords.ForEach(config.Items.Add);
        var potion = this.CreateItemDefinition(14, 0, 20);
        potion.DropsFromMonsters = true;
        config.Items.Add(potion);

        // The three swords are spread over the three zones, so the second zone drops the second sword.
        var zoneGroup = this.CreateZoneGroup(20, 29);
        config.DropItemGroups.Add(this.CreateZoneGroup(1, 19));
        config.DropItemGroups.Add(zoneGroup);
        config.DropItemGroups.Add(this.CreateZoneGroup(30, 39));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(zoneGroup);

        // The first possible item is the sword of the zone, the second one is the potion.
        var swordGenerator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (swordDrops, _) = await swordGenerator.GenerateItemDropsAsync(this.GetMonster(1, monsterLevel), 1, player).ConfigureAwait(false);
        var potionGenerator = new DefaultDropGenerator(config, this.GetRandomizer2(1, 0));
        var (potionDrops, _) = await potionGenerator.GenerateItemDropsAsync(this.GetMonster(1, monsterLevel), 1, player).ConfigureAwait(false);

        var sword = swordDrops.Single();
        Assert.Multiple(() =>
        {
            Assert.That(sword.Definition, Is.EqualTo(swords[1]));
            Assert.That(sword.Level, Is.EqualTo(expectedItemLevel));
            Assert.That(potionDrops.Single().Definition, Is.EqualTo(potion));
        });
    }

    /// <summary>
    /// Tests that the pieces of an armor set drop in the same zone, which is given by the drop level of the armor,
    /// even if other pieces of the set have a higher drop level than the ones of the next set.
    /// </summary>
    [Test]
    public async ValueTask TestZoneArmorSetDropAsync()
    {
        var config = this.GetGameConfig();
        var characterClass = new CharacterClass();
        var armorSlot = new Mock<ItemSlotType>();
        armorSlot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.ArmorSlot });
        var glovesSlot = new Mock<ItemSlotType>();
        glovesSlot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.GlovesSlot });
        var firstArmor = this.CreateEquipment(8, 0, 5, characterClass, armorSlot.Object);
        var firstGloves = this.CreateEquipment(10, 0, 25, characterClass, glovesSlot.Object);
        var secondArmor = this.CreateEquipment(8, 1, 15, characterClass, armorSlot.Object);
        var secondGloves = this.CreateEquipment(10, 1, 12, characterClass, glovesSlot.Object);
        new[] { firstArmor, firstGloves, secondArmor, secondGloves }.ToList().ForEach(config.Items.Add);

        var zoneGroup = this.CreateZoneGroup(1, 19);
        config.DropItemGroups.Add(zoneGroup);
        config.DropItemGroups.Add(this.CreateZoneGroup(20, 29));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(zoneGroup);

        var drops = new List<ItemDefinition>();
        foreach (var index in new[] { 0, 1 })
        {
            var generator = new DefaultDropGenerator(config, this.GetRandomizer(index));
            var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 10), 1, player).ConfigureAwait(false);
            drops.Add(items.Single().Definition!);
        }

        Assert.That(drops, Is.EquivalentTo(new[] { firstArmor, firstGloves }));
    }

    /// <summary>
    /// Tests that all weapons of a character class are ranked together, so that a weapon type with only one item
    /// doesn't drop in every zone.
    /// </summary>
    [Test]
    public async ValueTask TestZoneWeaponsAreRankedTogetherAsync()
    {
        var config = this.GetGameConfig();
        var characterClass = new CharacterClass();
        var slot = new Mock<ItemSlotType>();
        slot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.LeftHandSlot });
        var firstSword = this.CreateEquipment(0, 0, 10, characterClass, slot.Object);
        config.Items.Add(firstSword);
        config.Items.Add(this.CreateEquipment(0, 1, 20, characterClass, slot.Object));
        config.Items.Add(this.CreateEquipment(1, 0, 30, characterClass, slot.Object));
        var potion = this.CreateItemDefinition(14, 0, 1);
        potion.DropsFromMonsters = true;
        config.Items.Add(potion);

        var zoneGroup = this.CreateZoneGroup(1, 19);
        config.DropItemGroups.Add(zoneGroup);
        config.DropItemGroups.Add(this.CreateZoneGroup(20, 29));
        config.DropItemGroups.Add(this.CreateZoneGroup(30, 39));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(zoneGroup);

        // The first zone drops only the weakest sword, so the second possible item is the potion and not the axe.
        var swordGenerator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (swordDrops, _) = await swordGenerator.GenerateItemDropsAsync(this.GetMonster(1, 10), 1, player).ConfigureAwait(false);
        var potionGenerator = new DefaultDropGenerator(config, this.GetRandomizer2(1, 0));
        var (potionDrops, _) = await potionGenerator.GenerateItemDropsAsync(this.GetMonster(1, 10), 1, player).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(swordDrops.Single().Definition, Is.EqualTo(firstSword));
            Assert.That(potionDrops.Single().Definition, Is.EqualTo(potion));
        });
    }

    /// <summary>
    /// Tests that a character class drops only its own weapons in a zone, and not a weapon which every class can use.
    /// </summary>
    [Test]
    public async ValueTask TestZoneDropsOnlyOwnWeaponsAsync()
    {
        var config = this.GetGameConfig();
        var wizard = new CharacterClass();
        var knight = new CharacterClass();
        var slot = new Mock<ItemSlotType>();
        slot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.LeftHandSlot });
        var staff = this.CreateEquipment(5, 0, 10, wizard, slot.Object);
        var sword = this.CreateEquipment(0, 0, 10, knight, slot.Object);
        var kris = this.CreateEquipment(0, 1, 1, wizard, slot.Object);
        Mock.Get(kris).Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass> { wizard, knight });
        var potion = this.CreateItemDefinition(14, 0, 1);
        potion.DropsFromMonsters = true;
        new[] { staff, sword, kris, potion }.ToList().ForEach(config.Items.Add);

        var zoneGroup = this.CreateZoneGroup(1, 19);
        config.DropItemGroups.Add(zoneGroup);

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(zoneGroup);

        var drops = new List<ItemDefinition>();
        foreach (var index in new[] { 0, 1, 2 })
        {
            var generator = new DefaultDropGenerator(config, this.GetRandomizer2(index, 0));
            var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 10), 1, player).ConfigureAwait(false);
            drops.Add(items.Single().Definition!);
        }

        Assert.That(drops, Is.EquivalentTo(new[] { staff, sword, potion }));
    }

    /// <summary>
    /// Tests the drops defined by a player are getting considered.
    /// </summary>
    public void TestItemDropItemByPlayer()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests the drops defined by a map are getting considered.
    /// </summary>
    public void TestItemDropItemByMap()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests that ExcellentItemDropLevelDelta property exists and has correct default.
    /// </summary>
    [Test]
    public void TestExcellentItemDropLevelDelta_PropertyExists()
    {
        var config = this.GetGameConfig();
        // The initializer sets default to 25 for backward compatibility
        config.ExcellentItemDropLevelDelta = 25;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(25));

        config.ExcellentItemDropLevelDelta = 0;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(0));

        config.ExcellentItemDropLevelDelta = 50;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(50));
    }

    private MonsterDefinition GetMonster(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }

    private IRandomizer GetRandomizer(int randomValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(randomValue);
        randomizer.Setup(r => r.NextDouble()).Returns(randomValue / 10000.0);
        return randomizer.Object;
    }

    private IRandomizer GetRandomizer2(int integerValue, double doubleValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(integerValue);
        randomizer.Setup(r => r.NextDouble()).Returns(doubleValue);

        return randomizer.Object;
    }

    private GameConfiguration GetGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        gameConfiguration.Setup(c => c.DropItemGroups).Returns(new List<DropItemGroup>());
        return gameConfiguration.Object;
    }

    private DropItemGroup CreateZoneGroup(byte minimumLevel, byte maximumLevel)
    {
        var zoneGroup = new Mock<DropItemGroup>();
        zoneGroup.SetupAllProperties();
        zoneGroup.Object.Chance = 1.0;
        zoneGroup.Object.ItemType = SpecialItemType.ZoneItem;
        zoneGroup.Object.MinimumMonsterLevel = minimumLevel;
        zoneGroup.Object.MaximumMonsterLevel = maximumLevel;
        zoneGroup.Object.ItemLevel = 3;
        zoneGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition>());
        return zoneGroup.Object;
    }

    private DropItemGroup CreateDropItemGroup(double chance, ItemDefinition item)
    {
        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = chance;
        dropGroup.Object.ItemLevel = 0;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { item });
        return dropGroup.Object;
    }

    private ItemDefinition CreateEquipment(byte group, short number, byte dropLevel, CharacterClass characterClass, ItemSlotType slot)
    {
        var itemDefinition = this.CreateItemDefinition(group, number, dropLevel);
        itemDefinition.DropsFromMonsters = true;
        itemDefinition.MaximumItemLevel = 15;
        itemDefinition.ItemSlot = slot;
        Mock.Get(itemDefinition).Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass> { characterClass });
        return itemDefinition;
    }

    private ItemDefinition CreateItemDefinition(byte group, short number, byte dropLevel, byte? maximumDropLevel = null)
    {
        var itemDefinition = new Mock<ItemDefinition>();
        itemDefinition.SetupAllProperties();
        itemDefinition.Object.Group = group;
        itemDefinition.Object.Number = number;
        itemDefinition.Object.DropLevel = dropLevel;
        itemDefinition.Object.MaximumDropLevel = maximumDropLevel;
        itemDefinition.Object.Durability = 1;
        itemDefinition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        itemDefinition.Setup(d => d.PossibleItemSetGroups).Returns(new List<ItemSetGroup>());
        itemDefinition.Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass>());
        return itemDefinition.Object;
    }
}
