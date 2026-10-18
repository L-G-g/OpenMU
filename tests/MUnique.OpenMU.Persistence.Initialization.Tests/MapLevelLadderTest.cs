// <copyright file="MapLevelLadderTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the season 6 data has the <see cref="MapLevelLadder"/> and the <see cref="EventEntryItemShop"/>,
/// and that <see cref="MapLevelLadderUpdatePlugIn"/> adds them to existing databases.
/// </summary>
[TestFixture]
internal class MapLevelLadderTest
{
    private const short SkeletonWarriorNumber = 14;
    private const short DevilSquareMapNumber = 9;

    /// <summary>
    /// Tests that a new season 6 database has the ladder.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasLadderAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertLadder(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update applies the ladder to a database without it,
    /// and that applying it twice doesn't add anything twice.
    /// </summary>
    [Test]
    public async Task UpdateAppliesLadderAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(gameConfiguration);
        Assert.That(GetCopies(gameConfiguration), Is.Empty);

        var update = new MapLevelLadderUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertLadder(gameConfiguration);

        var monsterCount = gameConfiguration.Monsters.Count;
        var dropGroupCount = gameConfiguration.DropItemGroups.Count;
        var mapDropGroupCount = gameConfiguration.Maps.Sum(m => m.DropItemGroups.Count);
        var lumenItemCount = GetLumenStore(gameConfiguration).Items.Count;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertLadder(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.Monsters, Has.Count.EqualTo(monsterCount));
            Assert.That(gameConfiguration.DropItemGroups, Has.Count.EqualTo(dropGroupCount));
            Assert.That(gameConfiguration.Maps.Sum(m => m.DropItemGroups.Count), Is.EqualTo(mapDropGroupCount));
            Assert.That(GetLumenStore(gameConfiguration).Items, Has.Count.EqualTo(lumenItemCount));
        });
    }

    /// <summary>
    /// Tests that each monster is on the ladder at most twice, once as itself and once as a copy for another zone.
    /// </summary>
    [Test]
    public void MonstersAreInAtMostTwoZones()
    {
        Assert.That(
            MapLevelLadder.Monsters.GroupBy(m => m.Number).Where(g => g.Count() > 2 || g.Select(m => m.Zone).Distinct().Count() != g.Count()),
            Is.Empty);
    }

    private static void AssertLadder(GameConfiguration gameConfiguration)
    {
        var ladderMapNumbers = MapLevelLadder.Zones.SelectMany(z => z.MapNumbers).ToHashSet();
        var reworkedOriginals = MapLevelLadder.Monsters.Select(m => MapLevelLadder.GetMonster(gameConfiguration, m.Number)).ToHashSet();
        var commonGroupIds = new[] { GuidHelper.CreateGuid<DropItemGroup>(2), GuidHelper.CreateGuid<DropItemGroup>(3) };
        Assert.Multiple(() =>
        {
            foreach (var entry in MapLevelLadder.Monsters)
            {
                var zone = MapLevelLadder.Zones[entry.Zone - 1];
                var spawns = gameConfiguration.Maps
                    .Where(map => zone.MapNumbers.Contains((byte)map.Number))
                    .SelectMany(map => map.MonsterSpawns)
                    .Where(spawn => spawn.MonsterDefinition?.Number == entry.Number && spawn.MonsterDefinition.Designation.ValueInNeutralLanguage?.EndsWith("Elite") is not true)
                    .ToList();
                Assert.That(spawns, Is.Not.Empty, $"monster {entry.Number} in zone {entry.Zone}");
                Assert.That(spawns.Select(s => s.MonsterDefinition![Stats.Level]), Is.All.EqualTo((float)entry.Level), $"monster {entry.Number} in zone {entry.Zone}");
                Assert.That(spawns.Select(s => s.MonsterDefinition![Stats.MaximumHealth]), Is.All.EqualTo(entry.Health), $"monster {entry.Number} in zone {entry.Zone}");
                Assert.That(entry.Level, Is.InRange(zone.MinimumLevel, zone.MaximumLevel), $"monster {entry.Number} in zone {entry.Zone}");
            }

            // Outside the ladder, the monsters keep their original level and stats. Only the monsters whose stats didn't change
            // don't need a copy.
            var changedOriginals = reworkedOriginals
                .Where(m => m is not null && MapLevelLadder.GetMonster(gameConfiguration, (short)(m.Number + MapLevelLadder.ClassicCopyIdNumberOffset)) is not null)
                .ToHashSet();
            var outsideSpawns = gameConfiguration.Maps
                .Where(map => !ladderMapNumbers.Contains((byte)map.Number))
                .SelectMany(map => map.MonsterSpawns)
                .ToList();
            Assert.That(outsideSpawns.Where(s => changedOriginals.Contains(s.MonsterDefinition)), Is.Empty);
            var devilSquareSkeleton = gameConfiguration.Maps
                .Where(map => map.Number == DevilSquareMapNumber)
                .SelectMany(map => map.MonsterSpawns)
                .First(s => s.MonsterDefinition?.Number == SkeletonWarriorNumber)
                .MonsterDefinition!;
            Assert.That(devilSquareSkeleton[Stats.Level], Is.EqualTo(19));
            Assert.That(devilSquareSkeleton[Stats.MaximumHealth], Is.EqualTo(525));

            // The elites take the new stats of their originals.
            foreach (var (originalNumber, name, _, _) in EliteQuestMonsters.Elites)
            {
                var original = MapLevelLadder.GetMonster(gameConfiguration, originalNumber)!;
                var elite = MapLevelLadder.GetMonster(gameConfiguration, (short)(originalNumber + EliteQuestMonsters.IdNumberOffset))!;
                Assert.That(elite[Stats.MaximumHealth], Is.EqualTo(original[Stats.MaximumHealth] * EliteQuestMonsters.HealthMultiplier), name);
                Assert.That(elite[Stats.Level], Is.EqualTo(original[Stats.Level]), name);
            }

            foreach (var map in gameConfiguration.Maps.Where(map => ladderMapNumbers.Contains((byte)map.Number)))
            {
                Assert.That(map.DropItemGroups.Where(g => commonGroupIds.Contains(g.GetId())), Is.Empty, map.Name.ValueInNeutralLanguage);
                Assert.That(map.DropItemGroups.Count(g => g.ItemType == SpecialItemType.ZoneItem && g.PossibleItems.Count == 0), Is.EqualTo(1), map.Name.ValueInNeutralLanguage);
                Assert.That(map.DropItemGroups.Count(g => g.ItemType == SpecialItemType.ZoneExcellentItem), Is.EqualTo(1), map.Name.ValueInNeutralLanguage);
            }

            var symbolOfKundunGroups = gameConfiguration.DropItemGroups
                .Where(g => g.PossibleItems.Any(i => i is { Group: 14, Number: 29 }))
                .OrderBy(g => g.ItemLevel)
                .Select(g => (g.MinimumMonsterLevel!.Value, g.MaximumMonsterLevel!.Value));
            Assert.That(symbolOfKundunGroups, Is.EqualTo(MapLevelLadder.SymbolOfKundunDropLevels));

            Assert.That(gameConfiguration.WarpList.Select(w => w.Costs), Is.All.EqualTo(0));
            Assert.That(gameConfiguration.WarpList.Single(w => w.Index == 33).LevelRequirement, Is.EqualTo(270), "Swamp of Calmness");
            var devias = gameConfiguration.Maps.Single(m => m.Number == 2 && m.Discriminator == 0);
            Assert.That(devias.EnterGates.Single(g => g.TargetGate?.Map?.Number == 57).LevelRequirement, Is.EqualTo(280), "Devias to Raklion");

            // The event items don't drop anymore, Lumen sells them.
            var eventGroups = gameConfiguration.Maps.SelectMany(m => m.DropItemGroups)
                .Concat(gameConfiguration.Monsters.SelectMany(m => m.DropItemGroups))
                .Where(EventEntryItemShop.IsEventItemDropGroup);
            Assert.That(eventGroups, Is.Empty);
            Assert.That(
                gameConfiguration.Items.Where(i => EventEntryItemShop.EventItemsWithoutDrop.Contains((i.Group, i.Number))).Select(i => i.DropsFromMonsters),
                Is.All.False);
            var lumenItems = GetLumenStore(gameConfiguration).Items.Select(i => (i.ItemSlot, i.Definition!.Group, i.Definition.Number, i.Level));
            Assert.That(EventEntryItemShop.AddedLumenItems.Select(i => (i.Slot, i.Group, i.Number, i.Level)), Is.SubsetOf(lumenItems));
        });
    }

    /// <summary>
    /// Brings the data into the state before the ladder existed, as far as the tests need it.
    /// </summary>
    private static void RestorePreviousData(GameConfiguration gameConfiguration)
    {
        foreach (var copy in GetCopies(gameConfiguration))
        {
            var original = MapLevelLadder.GetMonster(gameConfiguration, copy.Number)!;
            foreach (var spawn in gameConfiguration.Maps.SelectMany(m => m.MonsterSpawns).Where(s => s.MonsterDefinition == copy))
            {
                spawn.MonsterDefinition = original;
            }

            if (copy.GetId() == GuidHelper.CreateGuid<MonsterDefinition>((short)(copy.Number + MapLevelLadder.ClassicCopyIdNumberOffset)))
            {
                foreach (var attribute in copy.Attributes)
                {
                    original.Attributes.Single(a => a.AttributeDefinition == attribute.AttributeDefinition).Value = attribute.Value;
                }
            }

            gameConfiguration.Monsters.Remove(copy);
        }

        var commonGroups = new[] { 2, 3 }.Select(n => gameConfiguration.DropItemGroups.Single(g => g.GetId() == GuidHelper.CreateGuid<DropItemGroup>((short)n))).ToList();
        foreach (var zoneGroup in gameConfiguration.DropItemGroups.Where(g => g.ItemType is SpecialItemType.ZoneItem or SpecialItemType.ZoneExcellentItem).ToList())
        {
            foreach (var map in gameConfiguration.Maps.Where(m => m.DropItemGroups.Contains(zoneGroup)))
            {
                map.DropItemGroups.Remove(zoneGroup);
                commonGroups.Where(g => !map.DropItemGroups.Contains(g)).ToList().ForEach(map.DropItemGroups.Add);
            }

            gameConfiguration.DropItemGroups.Remove(zoneGroup);
        }

        var store = GetLumenStore(gameConfiguration);
        foreach (var item in store.Items.Where(i => EventEntryItemShop.AddedLumenItems.Any(added => added.Slot == i.ItemSlot)).ToList())
        {
            store.Items.Remove(item);
        }

        gameConfiguration.WarpList.ToList().ForEach(w => w.Costs = 2000);
    }

    private static List<MonsterDefinition> GetCopies(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters
            .Where(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>((short)(m.Number + MapLevelLadder.ClassicCopyIdNumberOffset))
                        || m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>((short)(m.Number + MapLevelLadder.LadderCopyIdNumberOffset)))
            .ToList();
    }

    private static ItemStorage GetLumenStore(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters.Single(m => m.Number == EventEntryItemShop.LumenTheBarmaidNumber).MerchantStore!;
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
