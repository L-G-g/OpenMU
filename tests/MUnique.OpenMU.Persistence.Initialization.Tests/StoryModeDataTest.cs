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

        AssertData(gameConfiguration);
    }

    private static void AssertData(GameConfiguration gameConfiguration)
    {
        Assert.That(StoryProgress.IsAvailable(gameConfiguration), Is.True);

        var actors = gameConfiguration.Monsters.Where(m => StoryModeFeaturePlugIn.GetActor(m) is not null).ToList();
        Assert.That(actors.Select(m => StoryModeFeaturePlugIn.GetActor(m)), Is.EquivalentTo(new[] { StoryActor.Lumen, StoryActor.Stranger, StoryActor.Sebina }));

        var stranger = actors.Single(m => StoryModeFeaturePlugIn.GetActor(m) == StoryActor.Stranger);
        var lorencia = gameConfiguration.Maps.Single(m => m.Number == 0);
        var spawn = lorencia.MonsterSpawns.Single(s => s.MonsterDefinition == stranger);
        var terrain = new GameMapTerrain(lorencia);
        Assert.That(terrain.SafezoneMap[spawn.X1, spawn.Y1], Is.True, "the stranger stands in the town");
        Assert.That(lorencia.MonsterSpawns.Where(s => s != spawn).Any(s => s.X1 == spawn.X1 && s.Y1 == spawn.Y1 && s.X2 == s.X1 && s.Y2 == s.Y1), Is.False, "nobody else stands there");

        // Where the player comes back to after the duel.
        for (var x = spawn.X1 + 1; x <= spawn.X1 + 2; x++)
        {
            for (var y = spawn.Y1 + 2; y <= spawn.Y1 + 3; y++)
            {
                Assert.That(terrain.WalkMap[x, y], Is.True, $"{x}, {y}");
            }
        }

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
}
