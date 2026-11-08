// <copyright file="SkillQuestsDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the skill quests (tier 1).
/// </summary>
[TestFixture]
internal class SkillQuestsDataTest
{
    /// <summary>
    /// Tests that a new database has the quests of the five NPCs, with their kills and rewards,
    /// and that the scrolls and orbs of their skills don't drop and aren't sold.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasSkillQuestsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);

        AssertQuests(gameConfiguration);
    }

    /// <summary>
    /// Tests that applying the update twice to a new database doesn't add the quests again.
    /// </summary>
    [Test]
    public async Task UpdateDoesNotDuplicateQuestsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        var update = new SkillQuestsUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        }

        AssertQuests(gameConfiguration);
    }

    private static void AssertQuests(GameConfiguration gameConfiguration)
    {
        var quests = gameConfiguration.Monsters.SelectMany(m => m.Quests).Where(q => q.IsSkillQuest()).ToList();
        Assert.That(quests.Select(q => q.QuestGiver!.Number), Is.EquivalentTo(new short[] { 251, 254, 242, 226, 243 }));
        foreach (var quest in quests)
        {
            Assert.That(quest.RequiredMonsterKills.Select(r => r.Monster!.Number), Is.EquivalentTo(new short[] { 2, 421, 27 }), quest.Name.ToString());
            Assert.That(quest.RequiredMonsterKills.All(r => r.MinimumNumber == 100), Is.True);
            Assert.That(quest.Rewards.All(r => r.RewardType == QuestRewardType.Skill && r.SkillReward is not null), Is.True);
        }

        var pasi = quests.Single(q => q.QuestGiver!.Number == 254);
        Assert.That(pasi.Rewards.Select(r => (int)r.SkillReward!.Number), Is.EquivalentTo(new[] { 4, 7, 1, 11, 3, 2 }));

        var questSkills = quests.SelectMany(q => q.Rewards).Select(r => r.SkillReward!.Number).ToHashSet();
        var droppingSkillItems = gameConfiguration.Items
            .Where(i => i.Group is 12 or 15 && i.Skill is not null && !(i.Group == 12 && i.Number == 11))
            .Where(i => questSkills.Contains(i.Skill!.Number) && i.DropsFromMonsters)
            .Select(i => i.Name.ToString())
            .ToList();
        Assert.That(droppingSkillItems, Is.Empty);

        var soldSkillItems = gameConfiguration.Monsters
            .Select(m => m.MerchantStore)
            .OfType<DataModel.Entities.ItemStorage>()
            .SelectMany(s => s.Items)
            .Where(i => i.Definition is { Group: 12 or 15, Skill: not null } d && questSkills.Contains(d.Number == 11 && d.Group == 12 ? (short)(d.Skill!.Number + i.Level) : d.Skill!.Number))
            .Select(i => i.Definition!.Name.ToString())
            .ToList();
        Assert.That(soldSkillItems, Is.Empty);
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
