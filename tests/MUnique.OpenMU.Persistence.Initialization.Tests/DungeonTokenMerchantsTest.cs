// <copyright file="DungeonTokenMerchantsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.TokenMerchants;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the tokens of the dungeons and of their merchants, and the items which the merchants sell.
/// </summary>
[TestFixture]
internal class DungeonTokenMerchantsTest
{
    private GameConfiguration _gameConfiguration = null!;
    private InMemoryPersistenceContextProvider _contextProvider = null!;

    /// <summary>
    /// Creates the data of a new database.
    /// </summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        this._contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(this._contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = this._contextProvider.CreateNewContext();
        this._gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// Tests that a new database has the token attributes and the merchants, also after applying the update twice.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasMerchantsAsync()
    {
        this.AssertData();
        var monsterCount = this._gameConfiguration.Monsters.Count;
        var spawnCount = this._gameConfiguration.Maps.Sum(map => map.MonsterSpawns.Count);
        var attributeCount = this._gameConfiguration.Attributes.Count;
        var update = new DungeonTokenMerchantsUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(this._contextProvider.CreateNewContext(), this._gameConfiguration).ConfigureAwait(false);
        }

        this.AssertData();
        Assert.That(this._gameConfiguration.Monsters, Has.Count.EqualTo(monsterCount));
        Assert.That(this._gameConfiguration.Maps.Sum(map => map.MonsterSpawns.Count), Is.EqualTo(spawnCount));
        Assert.That(this._gameConfiguration.Attributes, Has.Count.EqualTo(attributeCount));
    }

    /// <summary>
    /// Tests that each merchant sells a complete armor set and at least one weapon for each character class,
    /// that the items fit into the shop without overlapping, and the prices.
    /// </summary>
    [Test]
    public async Task MerchantsSellBestItemsOfEachClassAsync()
    {
        var generator = new DefaultDropGenerator(this._gameConfiguration, Rand.GetRandomizer());
        var dungeons = DungeonDefaults.CreateDungeons();
        var previousSets = new Dictionary<int, int>();
        foreach (var merchant in TokenMerchantConfiguration.CreateDefaultMerchants())
        {
            var dungeon = dungeons.Single(d => d.Number == merchant.Dungeon);
            var equipment = await generator.GetBestZoneEquipmentAsync(dungeon.GetDropMapNumbers()).ConfigureAwait(false);
            Assert.That(equipment.Keys, Is.EquivalentTo(Enumerable.Range(0, TokenMerchantShop.TabCount)), merchant.TokenName);
            foreach (var (classIndex, definitions) in equipment.OrderBy(e => e.Key))
            {
                TestContext.Out.WriteLine($"{merchant.TokenName} class {classIndex}: " + string.Join(", ", definitions.Select(d => d.Name.ValueInNeutralLanguage)));
                var setPieces = definitions.Where(d => TokenMerchantShop.GetPrice(merchant, d) == merchant.SetPiecePrice).ToList();
                var others = definitions.Except(setPieces).ToList();
                Assert.That(setPieces.Select(d => d.Number).Distinct().Count(), Is.EqualTo(1), "one armor set");

                // The Magic Gladiator has no helm and the Rage Fighter no gloves.
                Assert.That(setPieces, Has.Count.EqualTo(classIndex is 3 or 6 ? 4 : 5), $"pieces of class {classIndex}");
                Assert.That(others.Any(d => d.Group < 6), Is.True, $"a weapon for class {classIndex}");
                Assert.That(others.GroupBy(d => d.Group).All(g => g.Count() == 1), Is.True, "one item of each weapon group and one shield");
                Assert.That(definitions.All(d => d.QualifiedCharacters.Any(c => c.Number / 4 == classIndex)), Is.True);

                // The set of a later dungeon is a better one.
                var setLevel = setPieces.Max(d => d.DropLevel);
                Assert.That(setLevel, Is.GreaterThan(previousSets.GetValueOrDefault(classIndex)), $"set of class {classIndex}");
                previousSets[classIndex] = setLevel;

                var shown = TokenMerchantShop.PlaceItems(definitions.Select(d => TokenMerchantShop.CreateShownItem(d, true)));
                Assert.That(shown, Has.Count.EqualTo(definitions.Count), "all items fit into the shop");
                var cells = shown.SelectMany(item => Enumerable.Range(0, item.Definition!.Height).SelectMany(r => Enumerable.Range(0, item.Definition.Width).Select(c => (Row: (item.ItemSlot / 8) + r, Column: (item.ItemSlot % 8) + c)))).ToList();
                Assert.That(cells, Is.Unique);
                Assert.That(cells.All(cell => cell.Column < 8 && cell.Row < 15), Is.True);
                Assert.That(shown.All(item => item.IsExcellent() && item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck)), Is.True);
            }
        }
    }

    /// <summary>
    /// Tests that a bought item is a usual excellent item with luck: at least one excellent option, and mostly not all of them.
    /// </summary>
    [Test]
    public async Task BoughtItemsAreExcellentWithLuckAsync()
    {
        var generator = new DefaultDropGenerator(this._gameConfiguration, Rand.GetRandomizer());
        var armor = this._gameConfiguration.Items.First(i => i.Group == 8 && i.Number == 6);
        var counts = new int[7];
        for (var i = 0; i < 300; i++)
        {
            var item = await generator.GenerateExcellentItemAsync(armor, true).ConfigureAwait(false);
            Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck), Is.EqualTo(1));
            Assert.That(item.Level, Is.Zero);
            Assert.That(item.Durability, Is.GreaterThan(0));
            counts[item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent)]++;
        }

        TestContext.Out.WriteLine("Excellent options of 300 items: " + string.Join(", ", counts.Select((count, options) => $"{options}: {count}")));
        Assert.That(counts[0], Is.Zero);
        Assert.That(counts[1], Is.GreaterThan(counts[2]));
    }

    private void AssertData()
    {
        foreach (var merchant in TokenMerchantConfiguration.CreateDefaultMerchants())
        {
            Assert.That(this._gameConfiguration.Attributes.Count(a => a.Id == Stats.GetDungeonTokens(merchant.Dungeon).Id), Is.EqualTo(1), merchant.TokenName);
            var npc = this._gameConfiguration.Monsters.Single(m => m.Designation.ValueInNeutralLanguage == merchant.NpcDesignation);
            Assert.That(npc.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
            var dungeonMap = MoreDungeons.Dungeons.Single(d => d.Number == merchant.Dungeon).MapNumber;
            var map = this._gameConfiguration.Maps.Single(m => m.Number == dungeonMap && m.Discriminator == 0);
            var spawn = map.MonsterSpawns.Single(s => s.MonsterDefinition == npc);
            var terrain = new GameMapTerrain(map);
            if (merchant.NpcDesignation == TokenMerchantConfiguration.DeviasNpcDesignation)
            {
                // Zienna stands in her shop since ever.
                continue;
            }

            Assert.That(terrain.SafezoneMap[spawn.X1, spawn.Y1], Is.True, $"{merchant.NpcDesignation} stands in the town");
            Assert.That(terrain.WalkMap[spawn.X1, spawn.Y1], Is.True, $"{merchant.NpcDesignation} stands on the ground");
            Assert.That(map.MonsterSpawns.Count(s => s.X1 == spawn.X1 && s.Y1 == spawn.Y1 && s.X2 == s.X1 && s.Y2 == s.Y1), Is.EqualTo(1), $"nobody else stands at the place of {merchant.NpcDesignation}");
            Assert.That(
                map.MonsterSpawns.Count(s => s.MonsterDefinition?.ObjectKind != NpcObjectKind.Monster && Math.Abs(s.X1 - spawn.X1) <= 8 && Math.Abs(s.Y1 - spawn.Y1) <= 8 && s.MonsterDefinition?.Designation.ValueInNeutralLanguage.Contains("Dungeon Guard") == true),
                Is.GreaterThan(0),
                $"{merchant.NpcDesignation} stands near the guards of its dungeon");
        }

        Assert.That(this._gameConfiguration.Monsters.Single(m => m.Designation.ValueInNeutralLanguage == TokenMerchantConfiguration.TarkanNpcDesignation).Number, Is.EqualTo(DungeonTokenMerchants.HaroldNumber));
        Assert.That(this._gameConfiguration.Monsters.Single(m => m.Designation.ValueInNeutralLanguage == TokenMerchantConfiguration.KanturuNpcDesignation).Number, Is.EqualTo(DungeonTokenMerchants.BoloNumber));
    }
}
