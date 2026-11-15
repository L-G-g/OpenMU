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
    }

    private static void AssertMemoryData(GameConfiguration gameConfiguration)
    {
        // The seer stands alone on a free tile of the safezone at the entrance of Atlans.
        var seer = gameConfiguration.Monsters.Single(m => StoryModeFeaturePlugIn.GetActor(m) == StoryActor.Seer);
        var atlans = gameConfiguration.Maps.Single(m => m.Number == 7);
        var seerSpawn = atlans.MonsterSpawns.Single(s => s.MonsterDefinition == seer);
        var atlansTerrain = new GameMapTerrain(atlans);
        Assert.That(atlansTerrain.WalkMap[seerSpawn.X1, seerSpawn.Y1], Is.True);
        Assert.That(atlansTerrain.SafezoneMap[seerSpawn.X1, seerSpawn.Y1], Is.True);
        Assert.That(atlans.MonsterSpawns.Where(s => s != seerSpawn).Any(s => s.X1 <= seerSpawn.X1 && s.X2 >= seerSpawn.X1 && s.Y1 <= seerSpawn.Y1 && s.Y2 >= seerSpawn.Y1 && s.MonsterDefinition?.ObjectKind != NpcObjectKind.Monster), Is.False);

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
