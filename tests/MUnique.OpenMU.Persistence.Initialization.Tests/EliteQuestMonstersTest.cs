// <copyright file="EliteQuestMonstersTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the elite monsters of the first class change quest are in the season 6 data,
/// and that <see cref="AddEliteQuestMonstersUpdatePlugIn"/> adds them to existing databases.
/// </summary>
[TestFixture]
internal class EliteQuestMonstersTest
{
    private const short SebinaThePriestessNumber = 235;

    /// <summary>
    /// Tests that a new season 6 database has the elite monsters.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasEliteMonstersAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertEliteData(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the elite monsters to a database without them,
    /// and that applying it twice doesn't add anything twice.
    /// </summary>
    [Test]
    public async Task UpdateAddsEliteMonstersAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(gameConfiguration);
        Assert.That(GetElites(gameConfiguration), Is.Empty);

        var update = new AddEliteQuestMonstersUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertEliteData(gameConfiguration);

        var monsterCount = gameConfiguration.Monsters.Count;
        var dropGroupCount = gameConfiguration.DropItemGroups.Count;
        var spawnCount = gameConfiguration.Maps.Sum(m => m.MonsterSpawns.Count);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertEliteData(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.Monsters, Has.Count.EqualTo(monsterCount));
            Assert.That(gameConfiguration.DropItemGroups, Has.Count.EqualTo(dropGroupCount));
            Assert.That(gameConfiguration.Maps.Sum(m => m.MonsterSpawns.Count), Is.EqualTo(spawnCount));
        });
    }

    private static void AssertEliteData(GameConfiguration gameConfiguration)
    {
        var elites = GetElites(gameConfiguration);
        var quests = GetQuests(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(elites, Has.Count.EqualTo(EliteQuestMonsters.Elites.Count));
            Assert.That(quests.Select(q => q.Number).Distinct(), Is.EquivalentTo(new short[] { 0, 1 }));
            Assert.That(quests.SelectMany(q => q.RequiredItems).Select(r => r.DropItemGroup!.Chance), Is.All.EqualTo(0));

            foreach (var (originalNumber, name, mapNumber, questNumber) in EliteQuestMonsters.Elites)
            {
                var elite = elites.Single(e => e.Designation.ValueInNeutralLanguage == name);
                var original = gameConfiguration.Monsters.Single(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(originalNumber));
                Assert.That(elite.Number, Is.EqualTo(originalNumber), name);
                Assert.That(elite.RespawnDelay, Is.EqualTo(EliteQuestMonsters.RespawnDelay), name);
                Assert.That(GetValue(elite, Stats.MaximumHealth), Is.EqualTo(GetValue(original, Stats.MaximumHealth) * EliteQuestMonsters.HealthMultiplier), name);
                Assert.That(GetValue(elite, Stats.MaximumPhysBaseDmg), Is.EqualTo(GetValue(original, Stats.MaximumPhysBaseDmg) * EliteQuestMonsters.CombatMultiplier), name);
                Assert.That(GetValue(elite, Stats.Level), Is.EqualTo(GetValue(original, Stats.Level)), name);

                var map = gameConfiguration.Maps.Single(m => m.Number == mapNumber && m.Discriminator == 0);
                var spawns = map.MonsterSpawns.Where(s => s.MonsterDefinition == elite).ToList();
                Assert.That(spawns, Has.Count.EqualTo(EliteQuestMonsters.SpotsPerMap), name);
                Assert.That(spawns.Select(s => s.Quantity), Is.All.EqualTo(1), name);
                Assert.That(spawns.Select(s => (s.X1, s.Y1)).Distinct().Count(), Is.EqualTo(EliteQuestMonsters.SpotsPerMap), name);

                var requiredItems = quests.Where(q => q.Number == questNumber).SelectMany(q => q.RequiredItems).Select(r => r.Item).Distinct().ToList();
                Assert.That(elite.DropItemGroups.Where(g => g.Chance >= 1).SelectMany(g => g.PossibleItems), Is.EquivalentTo(requiredItems), name);
            }

            // The normal monsters stay as they are.
            Assert.That(gameConfiguration.Monsters.Count(m => m.Number == 36), Is.EqualTo(2));
            Assert.That(gameConfiguration.Monsters.Single(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(36)).DropItemGroups, Is.Empty);
        });
    }

    /// <summary>
    /// Brings the data into the state before the elite monsters existed.
    /// </summary>
    private static void RestorePreviousData(GameConfiguration gameConfiguration)
    {
        foreach (var elite in GetElites(gameConfiguration))
        {
            foreach (var map in gameConfiguration.Maps)
            {
                foreach (var spawn in map.MonsterSpawns.Where(s => s.MonsterDefinition == elite).ToList())
                {
                    map.MonsterSpawns.Remove(spawn);
                }
            }

            foreach (var group in elite.DropItemGroups.ToList())
            {
                gameConfiguration.DropItemGroups.Remove(group);
            }

            gameConfiguration.Monsters.Remove(elite);
        }

        foreach (var quest in GetQuests(gameConfiguration))
        {
            foreach (var group in quest.RequiredItems.Select(r => r.DropItemGroup!))
            {
                group.Chance = 10.0 / 10000.0;
                group.MinimumMonsterLevel = quest.Number == 0 ? (byte)45 : (byte)62;
                group.MaximumMonsterLevel = quest.Number == 0 ? (byte)60 : (byte)76;
            }
        }
    }

    private static List<MonsterDefinition> GetElites(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters
            .Where(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>((short)(m.Number + EliteQuestMonsters.IdNumberOffset)))
            .ToList();
    }

    private static List<QuestDefinition> GetQuests(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters
            .Where(m => m.Number == SebinaThePriestessNumber)
            .SelectMany(m => m.Quests)
            .Where(q => q.Group == 0 && q.Number is 0 or 1)
            .ToList();
    }

    private static float GetValue(MonsterDefinition monster, AttributeSystem.AttributeDefinition attribute)
    {
        return monster.Attributes.Single(a => a.AttributeDefinition == attribute).Value;
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
