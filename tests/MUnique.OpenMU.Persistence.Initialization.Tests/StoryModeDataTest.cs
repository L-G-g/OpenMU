// <copyright file="StoryModeDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.GameLogic.Story;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the story mode.
/// </summary>
[TestFixture]
internal class StoryModeDataTest
{
    /// <summary>
    /// Tests that a new database has the attributes and the actors of the story, also after applying the update twice.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasStoryDataAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        AssertData(gameConfiguration);

        var update = new StoryModeUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        }

        var memoryUpdate = new StoryMemoryUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await memoryUpdate.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        }

        AssertData(gameConfiguration);
    }

    /// <summary>
    /// Tests that an old database with the seer in Atlans gets her in Kalima with the second version of the update.
    /// </summary>
    [Test]
    public async Task SeerMovesFromAtlansToKalimaAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        // The state of the first version: the seer only in Atlans.
        var seer = gameConfiguration.Monsters.Single(m => StoryModeFeaturePlugIn.GetActor(m) == StoryActor.Seer);
        foreach (var map in gameConfiguration.Maps)
        {
            foreach (var spawn in map.MonsterSpawns.Where(s => s.MonsterDefinition == seer).ToList())
            {
                map.MonsterSpawns.Remove(spawn);
            }
        }

        var atlans = gameConfiguration.Maps.Single(m => m.Number == 7);
        var old = context.CreateNew<MonsterSpawnArea>();
        old.SetGuid(GuidHelper.CreateGuid<MonsterSpawnArea>(atlans.Number, VersionSeasonSix.StoryMemoryData.SeerIdNumber));
        old.GameMap = atlans;
        old.MonsterDefinition = seer;
        old.X1 = old.X2 = 24;
        old.Y1 = old.Y2 = 20;
        old.Quantity = 1;
        atlans.MonsterSpawns.Add(old);

        await new StoryMemoryUpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        AssertMemoryData(gameConfiguration);
    }

    /// <summary>
    /// Tests the order of the questline with the default configurations: it comes from the requirements.
    /// </summary>
    [Test]
    public async Task QuestlineOrderAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var entries = Questline.Build(gameConfiguration, new StoryConfiguration(), new SkillQuestConfiguration());
        Assert.That(string.Join(' ', entries.Select(e => e.Code)), Is.EqualTo("H1 S1 S2 H2 H3 H4 S3 H5 H6 H7 H8 S4"));

        // Another configuration, another order: the third tier at 3 resets still waits for the Dungeon (step 4).
        var entries2 = Questline.Build(gameConfiguration, new StoryConfiguration { DungeonMinimumResets = 5 }, new SkillQuestConfiguration { Tier3MinimumResets = 3 });
        Assert.That(string.Join(' ', entries2.Select(e => e.Code)), Is.EqualTo("H1 S1 S2 H2 H3 H4 S3 H5 H6 H7 H8 S4"));
        var entries3 = Questline.Build(gameConfiguration, new StoryConfiguration { SebinaMinimumResets = 8 }, new SkillQuestConfiguration());
        Assert.That(string.Join(' ', entries3.Select(e => e.Code)), Is.EqualTo("H1 S1 S2 H2 H3 H4 H5 H6 S3 H7 H8 S4"));
    }

    private static void AssertData(GameConfiguration gameConfiguration)
    {
        Assert.That(StoryProgress.IsAvailable(gameConfiguration), Is.True);

        var actors = gameConfiguration.Monsters.Where(m => StoryModeFeaturePlugIn.GetActor(m) is not null).ToList();
        Assert.That(actors.Select(m => StoryModeFeaturePlugIn.GetActor(m)), Is.EquivalentTo(new[] { StoryActor.Lumen, StoryActor.Stranger, StoryActor.Sebina, StoryActor.Seer }));

        var stranger = actors.Single(m => StoryModeFeaturePlugIn.GetActor(m) == StoryActor.Stranger);
        var lorencia = gameConfiguration.Maps.Single(m => m.Number == 0);
        var spawn = lorencia.MonsterSpawns.Single(s => s.MonsterDefinition == stranger);
        var terrain = new GameMapTerrain(lorencia);
        Assert.That((spawn.X1, spawn.Y1), Is.EqualTo(((byte)141, (byte)208)));
        Assert.That(terrain.WalkMap[spawn.X1, spawn.Y1], Is.True, "the stranger stands on a free tile");
        Assert.That(terrain.SafezoneMap[spawn.X1, spawn.Y1], Is.False, "outside of the town: the plugin protects the players during his scenes");
        Assert.That(lorencia.MonsterSpawns.Where(s => s != spawn).Any(s => s.X1 == spawn.X1 && s.Y1 == spawn.Y1 && s.X2 == s.X1 && s.Y2 == s.Y1), Is.False, "nobody else stands there");

        // Where the player comes back to after the duel.
        for (var x = spawn.X1; x <= spawn.X1 + 1; x++)
        {
            for (var y = spawn.Y1 - 3; y <= spawn.Y1 - 2; y++)
            {
                Assert.That(terrain.WalkMap[x, y], Is.True, $"{x}, {y}");
            }
        }

        AssertMemoryData(gameConfiguration);

        // The actors of the story don't teach skills, so their marks are free for the story.
        Assert.That(actors.SelectMany(m => m.Quests).Any(q => q.IsSkillQuest()), Is.False);

        // The maps which the story opens exist.
        var configuration = new StoryConfiguration();
        foreach (var map in gameConfiguration.Maps)
        {
            var step = configuration.GetUnlockingStep(map.Number);
            Assert.That(step, Is.InRange(0, StoryScript.Steps.Count));
        }

        Assert.That(configuration.GetUnlockingStep(0), Is.Zero);
        Assert.That(configuration.GetUnlockingStep(3), Is.Zero);
        Assert.That(configuration.GetUnlockingStep(51), Is.Zero);
        Assert.That(configuration.GetUnlockingStep(2), Is.EqualTo(StoryScript.Devias));
        Assert.That(configuration.GetUnlockingStep(1), Is.EqualTo(StoryScript.Dungeon));
        Assert.That(configuration.GetUnlockingStep(4), Is.EqualTo(StoryScript.SchoolOfLight));
        Assert.That(configuration.GetUnlockingStep(7), Is.EqualTo(StoryScript.OldWorld));

        // Only Lorencia, Noria and Elbeland are open from the start: every other map opens with step 7 at the latest.
        foreach (var map in gameConfiguration.Maps.Where(m => m.Number is not (0 or 3 or 51)))
        {
            Assert.That(configuration.GetUnlockingStep(map.Number), Is.GreaterThan(0), $"{map.Name} ({map.Number})");
        }

        Assert.That(configuration.GetUnlockingStep(79), Is.EqualTo(StoryScript.OldWorld), "Loren Market");
        Assert.That(configuration.GetUnlockingStep(24), Is.EqualTo(StoryScript.OldWorld), "Kalima 1");
        Assert.That(new StoryConfiguration { OldWorldOpensAllOtherMaps = false }.GetUnlockingStep(79), Is.Zero);
    }

    private static void AssertMemoryData(GameConfiguration gameConfiguration)
    {
        // The seer stands alone on a free tile at the entrance of each level of Kalima, and not in Atlans anymore.
        var seer = gameConfiguration.Monsters.Single(m => StoryModeFeaturePlugIn.GetActor(m) == StoryActor.Seer);
        var atlans = gameConfiguration.Maps.Single(m => m.Number == 7);
        Assert.That(atlans.MonsterSpawns.Any(s => s.MonsterDefinition == seer), Is.False);
        foreach (var mapNumber in StoryMemory.KalimaMapNumbers)
        {
            var kalima = gameConfiguration.Maps.Single(m => m.Number == mapNumber && m.Discriminator == 0);
            var seerSpawn = kalima.MonsterSpawns.Single(s => s.MonsterDefinition == seer);
            var kalimaTerrain = new GameMapTerrain(kalima);
            Assert.That(kalimaTerrain.WalkMap[seerSpawn.X1, seerSpawn.Y1], Is.True, $"Kalima {mapNumber}");
            Assert.That(kalima.MonsterSpawns.Where(s => s != seerSpawn).Any(s => s.X1 <= seerSpawn.X1 && s.X2 >= seerSpawn.X1 && s.Y1 <= seerSpawn.Y1 && s.Y2 >= seerSpawn.Y1), Is.False, $"Kalima {mapNumber}");
            Assert.That(kalima.MonsterSpawns.Any(s => s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster && Math.Abs(s.X1 - seerSpawn.X1) < 10 && Math.Abs(s.Y1 - seerSpawn.Y1) < 10), Is.False, $"no monster next to the seer in Kalima {mapNumber}");
        }

        // The stage: one definition, whose entrance is the spot of the viewer.
        var memory = StoryMemory.Sacrifice;
        var stage = gameConfiguration.MiniGameDefinitions.Single(d => d.Type == MiniGameType.StoryScene);
        Assert.That(stage.Entrance?.Map?.Number, Is.EqualTo(StoryMemory.SacrificeMapNumber));
        Assert.That((stage.Entrance!.X1, stage.Entrance.Y1), Is.EqualTo((memory.ViewerSpot.X, memory.ViewerSpot.Y)));

        // Everybody stands on a free tile, close enough to the viewer to be seen, and the equipment exists for the class.
        var devias = gameConfiguration.Maps.Single(m => m.Number == StoryMemory.SacrificeMapNumber);
        var terrain = new GameMapTerrain(devias);
        Assert.That(terrain.WalkMap[memory.ViewerSpot.X, memory.ViewerSpot.Y], Is.True);
        foreach (var actor in memory.Actors)
        {
            foreach (var point in new[] { actor.Spot, actor.WalksTo ?? actor.Spot })
            {
                Assert.That(terrain.WalkMap[point.X, point.Y], Is.True, $"{actor.Name} at {point}");
                Assert.That(Math.Max(Math.Abs(point.X - memory.ViewerSpot.X), Math.Abs(point.Y - memory.ViewerSpot.Y)), Is.LessThanOrEqualTo(12), $"{actor.Name} at {point}");
            }

            var characterClass = gameConfiguration.CharacterClasses.Single(c => c.Number == (actor.Kind == GameLogic.Spectres.SpectreKind.Lord ? 16 : 0));
            foreach (var (group, number) in actor.Equipment)
            {
                var item = gameConfiguration.Items.SingleOrDefault(i => i.Group == group && i.Number == number);
                Assert.That(item, Is.Not.Null, $"{actor.Name}: item {group} {number}");
                var qualified = false;
                for (var current = characterClass; current is not null; current = current.NextGenerationClass)
                {
                    qualified |= item!.QualifiedCharacters.Contains(current);
                }

                Assert.That(qualified, Is.True, $"{actor.Name}: {item!.Name}");
            }
        }

        // The walk of an actor is a straight line over free tiles (the doors of the hall).
        foreach (var actor in memory.Actors.Where(a => a.WalksTo is not null))
        {
            var target = actor.WalksTo!.Value;
            Assert.That(actor.Spot.X, Is.EqualTo(target.X));
            for (var y = Math.Min(actor.Spot.Y, target.Y); y <= Math.Max(actor.Spot.Y, target.Y); y++)
            {
                Assert.That(terrain.WalkMap[target.X, y], Is.True, $"{actor.Name}: {target.X}, {y}");
            }

            Assert.That(Math.Abs(actor.Spot.Y - target.Y), Is.LessThanOrEqualTo(16));
        }
    }
}
