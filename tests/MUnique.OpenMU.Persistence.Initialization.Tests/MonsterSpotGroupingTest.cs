// <copyright file="MonsterSpotGroupingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the monsters of the hunting maps are grouped into spots by <see cref="MonsterSpotGrouping"/>,
/// in a new database and by the <see cref="GroupMonsterSpotsUpdatePlugIn"/>.
/// </summary>
[TestFixture]
internal class MonsterSpotGroupingTest
{
    private const short LorenciaNumber = 0;
    private const short BullFighterNumber = 0;
    private const short HoundNumber = 1;
    private const short RedDragonNumber = 44;
    private const short IronStickTrapNumber = 101;

    /// <summary>
    /// Tests that the monsters of each hunting map of a new database stand in spots of 5 or 7 monsters,
    /// which lie on spawnable ground, at most one in each square of the map.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasSpotsOnAllHuntingMapsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        foreach (var mapNumber in MonsterSpotGrouping.HuntingMapNumbers)
        {
            var map = gameConfiguration.Maps.Single(m => m.Number == mapNumber && m.Discriminator == 0);
            var spots = GetSpots(map);
            Assert.That(spots, Is.Not.Empty, $"Spots of map {map.Number}");
            AssertSpots(map, spots);
        }
    }

    /// <summary>
    /// Tests that the update replaces the automatic spawns of the regular monsters by spots in their area,
    /// keeps the spawns of bosses, traps, guards and other triggers, and that applying it twice changes nothing.
    /// </summary>
    [Test]
    public async Task UpdateGroupsTheOriginalSpawnsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        var lorencia = gameConfiguration.Maps.Single(m => m.Number == LorenciaNumber);
        foreach (var spawn in GetSpots(lorencia).Concat(lorencia.MonsterSpawns.Where(s => s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)).Distinct().ToList())
        {
            lorencia.MonsterSpawns.Remove(spawn);
        }

        var west = AddSpawn(context, lorencia, GetMonster(gameConfiguration, BullFighterNumber), 8, 94, 11, 244, 45);
        var east = AddSpawn(context, lorencia, GetMonster(gameConfiguration, HoundNumber), 180, 226, 90, 244, 40);
        var boss = AddSpawn(context, lorencia, GetMonster(gameConfiguration, RedDragonNumber), 60, 60, 60, 60, 1);
        var trap = AddSpawn(context, lorencia, GetMonster(gameConfiguration, IronStickTrapNumber), 8, 94, 11, 244, 10);
        var wandering = AddSpawn(context, lorencia, GetMonster(gameConfiguration, HoundNumber), 30, 30, 30, 30, 5);
        wandering.SpawnTrigger = SpawnTrigger.Wandering;
        var kept = lorencia.MonsterSpawns.Except([west, east]).ToList();

        var update = new GroupMonsterSpotsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        var spots = GetSpots(lorencia);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(lorencia.MonsterSpawns, Does.Not.Contain(west));
            Assert.That(lorencia.MonsterSpawns, Does.Not.Contain(east));
            Assert.That(lorencia.MonsterSpawns, Is.SupersetOf(kept));
            Assert.That(kept, Does.Contain(boss).And.Contain(trap).And.Contain(wandering));
            Assert.That(GetSpots(lorencia), Is.EquivalentTo(spots));

            var westSpots = spots.Where(s => s.MonsterDefinition == west.MonsterDefinition).ToList();
            var eastSpots = spots.Where(s => s.MonsterDefinition == east.MonsterDefinition).ToList();
            Assert.That(westSpots, Has.Count.GreaterThan(10));
            Assert.That(eastSpots, Has.Count.GreaterThan(5));
            Assert.That(westSpots.Select(s => (int)s.X1), Is.All.LessThan(100));
            Assert.That(eastSpots.Select(s => (int)s.X1), Is.All.GreaterThan(160));
        });

        AssertSpots(lorencia, spots);
    }

    private static void AssertSpots(GameMapDefinition map, IReadOnlyList<MonsterSpawnArea> spots)
    {
        var terrain = new GameMapTerrain(map);
        Assert.Multiple(() =>
        {
            Assert.That(spots.Select(s => s.Quantity), Is.All.EqualTo(MonsterSpotGrouping.GroupSize).Or.EqualTo(MonsterSpotGrouping.HotspotSize), $"Quantities of map {map.Number}");
            Assert.That(spots.Select(s => s.X2 - s.X1 + 1), Is.All.EqualTo(MonsterSpotGrouping.SpotSize), $"Widths of map {map.Number}");
            Assert.That(spots.Select(s => s.Y2 - s.Y1 + 1), Is.All.EqualTo(MonsterSpotGrouping.SpotSize), $"Heights of map {map.Number}");
            Assert.That(spots.Select(s => (s.X1 / MonsterSpotGrouping.SpotDistance, s.Y1 / MonsterSpotGrouping.SpotDistance)), Is.Unique, $"Squares of map {map.Number}");
            Assert.That(spots.Select(s => s.GetId()), Is.Unique, $"Ids of map {map.Number}");
            if (spots.Count >= 10)
            {
                Assert.That(spots.Where(s => s.Quantity == MonsterSpotGrouping.HotspotSize), Is.Not.Empty, $"Hotspots of map {map.Number}");
            }

            foreach (var spot in spots)
            {
                for (var x = spot.X1; x <= spot.X2; x++)
                {
                    for (var y = spot.Y1; y <= spot.Y2; y++)
                    {
                        Assert.That(terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y], Is.True, $"Coordinate {x}/{y} of {spot}");
                    }
                }
            }
        });
    }

    private static List<MonsterSpawnArea> GetSpots(GameMapDefinition map)
    {
        var spotIds = Enumerable.Range(MonsterSpotGrouping.FirstSpotNumber, 1000)
            .Select(number => GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, (short)number))
            .ToHashSet();
        return map.MonsterSpawns.Where(s => spotIds.Contains(s.GetId())).ToList();
    }

    private static MonsterSpawnArea AddSpawn(IContext context, GameMapDefinition map, MonsterDefinition monster, byte x1, byte x2, byte y1, byte y2, short quantity)
    {
        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.GameMap = map;
        spawn.MonsterDefinition = monster;
        spawn.X1 = x1;
        spawn.X2 = x2;
        spawn.Y1 = y1;
        spawn.Y2 = y2;
        spawn.Quantity = quantity;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        map.MonsterSpawns.Add(spawn);
        return spawn;
    }

    private static MonsterDefinition GetMonster(GameConfiguration gameConfiguration, short number)
    {
        return gameConfiguration.Monsters.Single(m => m.Number == number);
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}
