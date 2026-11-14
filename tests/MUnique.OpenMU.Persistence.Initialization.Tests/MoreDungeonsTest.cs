// <copyright file="MoreDungeonsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the data of the dungeons of Tarkan and Kanturu Ruins and of the extreme versions of the dungeons.
/// </summary>
[TestFixture]
internal class MoreDungeonsTest
{
    private const byte ArmorGroup = 8;

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
    /// Tests that a new database has the data of all dungeons, also after applying the update twice.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasDungeonsAsync()
    {
        this.AssertData();
        var monsterCount = this._gameConfiguration.Monsters.Count;
        var spawnCount = this._gameConfiguration.Maps.Sum(map => map.MonsterSpawns.Count);
        var update = new MoreDungeonsUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(this._contextProvider.CreateNewContext(), this._gameConfiguration).ConfigureAwait(false);
        }

        this.AssertData();
        Assert.That(this._gameConfiguration.Monsters, Has.Count.EqualTo(monsterCount));
        Assert.That(this._gameConfiguration.Maps.Sum(map => map.MonsterSpawns.Count), Is.EqualTo(spawnCount));
    }

    /// <summary>
    /// Tests that the update adds the stages of the new dungeons to a configuration which has only the stages of Devias, and keeps these.
    /// </summary>
    [Test]
    public void UpdateAddsMissingStagesToSavedConfiguration()
    {
        var plugInConfiguration = this._gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(DungeonFeaturePlugIn).GUID);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = new PlugInConfiguration { TypeId = typeof(DungeonFeaturePlugIn).GUID };
            this._gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        // Like a configuration of the first version: it has no dungeons, and the stages have no dungeon number.
        plugInConfiguration.CustomConfiguration = """
            {
              "NpcDesignation": "Dungeon Guard", "GameLevel": 1, "EntryX": 165, "EntryY": 41, "EntryCooldown": "12:00:00",
              "Stages": [
                { "BossDesignation": "Hommerd Boss", "BossX": 37, "BossY": 27, "MonsterNumber": 24, "KillCount": 111, "MinimumTime": "00:01:00", "ExcellentDropChance": 0.5, "Hint": "a" },
                { "BossDesignation": "Ice Queen Boss", "BossX": 27, "BossY": 226, "MonsterNumber": 21, "KillCount": 222, "MinimumTime": "00:10:00", "ExcellentDropChance": 1, "Hint": "b" }
              ]
            }
            """;
        MoreDungeons.Apply(this._contextProvider.CreateNewContext(), this._gameConfiguration);
        var configuration = plugInConfiguration.GetConfiguration<DungeonConfiguration>(new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler())!;
        Assert.That(configuration.EntryCooldown, Is.EqualTo(TimeSpan.FromHours(12)));
        Assert.That(configuration.Dungeons, Has.Count.EqualTo(3));
        var devias = configuration.Dungeons.Single(d => d.Number == DungeonDefaults.DeviasDungeonNumber);
        Assert.That(configuration.GetStages(devias).Select(s => s.KillCount), Is.EqualTo(new[] { 111, 222 }));
        foreach (var dungeon in configuration.Dungeons.Where(d => d != devias))
        {
            Assert.That(configuration.GetStages(dungeon), Has.Count.EqualTo(3), dungeon.Name);
        }

        plugInConfiguration.CustomConfiguration = null;
    }

    /// <summary>
    /// Tests that the items of each dungeon are split into a low and a high half per character class,
    /// and that only the high half gets the higher weight.
    /// </summary>
    [Test]
    public async Task DropsAreSplitIntoLowAndHighItemsAsync()
    {
        var generator = new DefaultDropGenerator(this._gameConfiguration, Rand.GetRandomizer());
        foreach (var dungeon in DungeonDefaults.CreateDungeons())
        {
            var maps = dungeon.GetDropMapNumbers();
            Assert.That(maps, Has.Count.EqualTo(4), dungeon.Name);
            var normal = await generator.GetZoneItemWeightsAsync(maps, 1).ConfigureAwait(false);
            var extreme = await generator.GetZoneItemWeightsAsync(maps, 3).ConfigureAwait(false);
            Assert.That(normal, Is.Not.Empty, dungeon.Name);
            Assert.That(normal.Values, Is.All.EqualTo(1.0), "the normal version gives all items the same chance");
            Assert.That(extreme.Keys, Is.EquivalentTo(normal.Keys));
            Assert.That(extreme.Values.Count(weight => weight > 1), Is.GreaterThan(0), dungeon.Name);
            TestContext.Out.WriteLine($"{dungeon.Name}: " + string.Join("; ", extreme.Where(e => e.Key.Group == ArmorGroup || e.Key.Group < 7).OrderBy(e => e.Key.Group).ThenBy(e => e.Key.Number).Select(e => $"{e.Key.Name} {e.Value:0.#}")));


            var item = await generator.GenerateExcellentItemOfZonesAsync(maps, 60, 3, 3).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null);
            Assert.That(item!.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.GreaterThanOrEqualTo(3));
        }

        // The example of the elf in the dungeon of Devias: the Vine set is low and the Silk set is high.
        var devias = await generator.GetZoneItemWeightsAsync(DungeonDefaults.CreateDungeons()[0].GetDropMapNumbers(), 3).ConfigureAwait(false);
        Assert.That(devias.Single(e => e.Key.Group == ArmorGroup && e.Key.Number == 10).Value, Is.EqualTo(1.0), "Vine Armor");
        Assert.That(devias.Single(e => e.Key.Group == ArmorGroup && e.Key.Number == 11).Value, Is.EqualTo(3.0), "Silk Armor");

        // The example of the Dark Knight: with three sets, Leather and Bronze are low and Scale is high.
        Assert.That(devias.Single(e => e.Key.Group == ArmorGroup && e.Key.Number == 5).Value, Is.EqualTo(1.0), "Leather Armor");
        Assert.That(devias.Single(e => e.Key.Group == ArmorGroup && e.Key.Number == 0).Value, Is.EqualTo(1.0), "Bronze Armor");
        Assert.That(devias.Single(e => e.Key.Group == ArmorGroup && e.Key.Number == 6).Value, Is.EqualTo(3.0), "Scale Armor");
    }

    private void AssertData()
    {
        var configuration = new DungeonConfiguration();
        Assert.That(MoreDungeons.Dungeons.Select(d => d.Number), Is.EquivalentTo(configuration.Dungeons.Select(d => d.Number)));
        var npcPositions = new HashSet<(short Map, byte X, byte Y)>();
        foreach (var data in MoreDungeons.Dungeons)
        {
            var dungeon = configuration.Dungeons.Single(d => d.Number == data.Number);
            var map = this._gameConfiguration.Maps.Single(m => m.Number == data.MapNumber && m.Discriminator == 0);
            var terrain = new GameMapTerrain(map);
            Assert.That(this._gameConfiguration.Attributes.Any(a => a.Id == Stats.GetDungeonLastEntry(dungeon.Number).Id), Is.True, dungeon.Name);
            Assert.That(dungeon.GameLevel, Is.Not.EqualTo(dungeon.ExtremeGameLevel));
            foreach (var gameLevel in new[] { dungeon.GameLevel, dungeon.ExtremeGameLevel })
            {
                var definition = this._gameConfiguration.MiniGameDefinitions.Single(d => d.Type == MiniGameType.Dungeon && d.GameLevel == gameLevel);
                Assert.That(definition.Entrance?.Map, Is.SameAs(map), dungeon.Name);
                Assert.That(definition.MapCreationPolicy, Is.EqualTo(MiniGameMapCreationPolicy.OnePerParty));
                Assert.That(configuration.FindByGameLevel(gameLevel)?.Dungeon, Is.SameAs(dungeon));
            }

            foreach (var designation in new[] { dungeon.NpcDesignation, dungeon.ExtremeNpcDesignation })
            {
                var npc = this._gameConfiguration.Monsters.Single(m => m.Designation.ValueInNeutralLanguage == designation);
                var spawn = map.MonsterSpawns.Single(s => s.MonsterDefinition == npc);
                Assert.That(npc.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
                Assert.That(terrain.SafezoneMap[spawn.X1, spawn.Y1], Is.True, $"{designation} stands in the town");
                Assert.That(npcPositions.Add((map.Number, spawn.X1, spawn.Y1)), Is.True, $"{designation} stands alone");
                Assert.That(map.MonsterSpawns.Count(s => s.X1 == spawn.X1 && s.Y1 == spawn.Y1 && s.X2 == s.X1 && s.Y2 == s.Y1), Is.EqualTo(1), $"nobody else stands at the place of {designation}");
            }

            Assert.That(terrain.WalkMap[dungeon.EntryX, dungeon.EntryY], Is.True, $"entry of {dungeon.Name}");
            Assert.That(terrain.SafezoneMap[dungeon.EntryX, dungeon.EntryY], Is.False, $"entry of {dungeon.Name} is outside of the blocked town");

            var waySpawns = map.MonsterSpawns.Where(s => s.SpawnTrigger == SpawnTrigger.AutomaticDuringEvent).ToList();
            foreach (var spawn in waySpawns)
            {
                for (var x = spawn.X1; x <= spawn.X2; x++)
                {
                    for (var y = spawn.Y1; y <= spawn.Y2; y++)
                    {
                        Assert.That(terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y], Is.True, $"spot at {x}, {y} of {dungeon.Name}");
                    }
                }
            }

            var stages = configuration.GetStages(dungeon);
            Assert.That(stages, Has.Count.EqualTo(3), dungeon.Name);
            foreach (var stage in stages)
            {
                var boss = this._gameConfiguration.Monsters.Single(m => m.Designation.ValueInNeutralLanguage == stage.BossDesignation);
                Assert.That(boss.Attributes, Is.Not.Empty, stage.BossDesignation);
                Assert.That(terrain.WalkMap[stage.BossX, stage.BossY], Is.True, $"place of {stage.BossDesignation}");
                Assert.That(terrain.SafezoneMap[stage.BossX, stage.BossY], Is.False, $"place of {stage.BossDesignation}");
                if (stage.EscortCount > 0)
                {
                    Assert.That(this._gameConfiguration.Monsters.Count(m => m.Designation.ValueInNeutralLanguage == stage.EscortDesignation), Is.EqualTo(1), stage.EscortDesignation);
                }

                foreach (var (number, kills) in new[] { (stage.MonsterNumber, stage.KillCount), (stage.SecondMonsterNumber, stage.SecondKillCount) }.Where(e => e.Item1 > 0))
                {
                    var monsters = waySpawns.Where(s => s.MonsterDefinition?.Number == number).Sum(s => s.Quantity);
                    Assert.That(monsters, Is.GreaterThanOrEqualTo(20), $"monsters {number} on the ways of {dungeon.Name}");
                    Assert.That(kills, Is.GreaterThan(0));
                }
            }
        }
    }
}
