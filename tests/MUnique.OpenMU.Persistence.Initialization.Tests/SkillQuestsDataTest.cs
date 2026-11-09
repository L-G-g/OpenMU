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
/// Tests the data of the skill quests.
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
        Assert.That(quests, Has.Count.EqualTo(31));
        Assert.That(quests.Select(q => q.QuestGiver!.Number).Distinct(), Is.EquivalentTo(new short[] { 251, 254, 242, 226, 243, 416, 568 }));
        Assert.That(quests.Select(q => (q.Group, q.Number, q.QualifiedCharacter!.Number)).Distinct().Count(), Is.EqualTo(quests.Count));
        Assert.That(quests.Where(q => q.QuestGiver!.Number == 243).Select(q => q.QualifiedCharacter!.Number).Distinct(), Is.EqualTo(new byte[] { 24 }), "Eo teaches only rage fighters");
        foreach (var quest in quests)
        {
            Assert.That(quest.RequiredMonsterKills, Is.Not.Empty, quest.Name.ToString());
            Assert.That(quest.Rewards, Is.Not.Empty, quest.Name.ToString());
            Assert.That(quest.Rewards.All(r => r.RewardType == QuestRewardType.Skill && r.SkillReward is not null), Is.True);
        }

        foreach (var quest in quests.Where(q => q.Number == 1))
        {
            Assert.That(quest.RequiredMonsterKills.Select(r => r.Monster!.Number), Is.EquivalentTo(new short[] { 2, 421, 27 }), quest.Name.ToString());
            Assert.That(quest.RequiredMonsterKills.All(r => r.MinimumNumber == 30), Is.True);
        }

        var pasi = quests.Single(q => q.QuestGiver!.Number == 254 && q.Number == 1 && q.QualifiedCharacter!.Number == 0);
        Assert.That(pasi.Rewards.Select(r => (int)r.SkillReward!.Number), Is.EquivalentTo(new[] { 4, 7, 11, 2, 17 }));
        var rhea = quests.Where(q => q.QuestGiver!.Number == 416).Select(q => q.Number);
        Assert.That(quests.Where(q => q.QuestGiver!.Number == 254 && q.QualifiedCharacter!.Number == 20).Select(q => q.Number), Is.EqualTo(new short[] { 1 }), "summoners do only the first quest of Pasi");
        Assert.That(rhea, Is.EquivalentTo(new short[] { 2, 3, 4 }));

        int[] Rewards(short npc, short number, byte characterClass) => quests
            .Single(q => q.QuestGiver!.Number == npc && q.Number == number && q.QualifiedCharacter!.Number == characterClass)
            .Rewards.Select(r => (int)r.SkillReward!.Number).ToArray();
        Assert.That(Rewards(251, 3, 4), Is.EquivalentTo(new[] { 48 }), "Hanzo III teaches Swell Life to the knights, without the class change");
        Assert.That(Rewards(251, 4, 4), Does.Contain(42), "Hanzo IV teaches Rageful Blow");
        Assert.That(Rewards(251, 1, 12), Does.Contain(47), "Hanzo I teaches Impale to the magic gladiators");
        Assert.That(Rewards(254, 1, 12), Does.Contain(17), "Pasi I teaches Energy Ball to the magic gladiators");
        Assert.That(Rewards(568, 3, 12), Is.EquivalentTo(new[] { 56, 55, 236 }), "Zyro III teaches Power Slash, Fire Slash and Flame Strike");
        Assert.That(Rewards(251, 1, 4), Does.Contain(47), "Hanzo I teaches Impale to the knights too");
        Assert.That(Rewards(254, 4, 0), Does.Contain(45), "Pasi IV teaches Lance to the soul masters");
        Assert.That(quests.Where(q => q.QualifiedCharacter!.Number == 12 && q.QuestGiver!.Number is 251 or 254).Select(q => ((int)q.QuestGiver!.Number, (int)q.Number)),
            Is.EquivalentTo(new[] { (251, 1), (251, 2), (254, 1), (254, 2), (254, 3) }), "the magic gladiators end Hanzo at II and Pasi at III");

        var weaponsWithSkill = gameConfiguration.Items
            .Where(i => i.Group <= 6 && i.Skill is not null && i.Skill.Number is not (223 or 224 or 225))
            .Select(i => i.Name.ToString())
            .ToList();
        Assert.That(weaponsWithSkill, Is.Empty);
        Assert.That(gameConfiguration.Items.Any(i => i.Group == 5 && i.Skill?.Number == 223), Is.True, "the summoner books keep their skills");

        var questSkills = quests.SelectMany(q => q.Rewards).Select(r => r.SkillReward!.Number).ToHashSet();
        var droppingSkillItems = gameConfiguration.Items
            .Where(i => i.Group is 12 or 15 && i.Skill is not null)
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
