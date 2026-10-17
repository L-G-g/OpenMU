// <copyright file="PreLearnedSkillsAndEventItemShopTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the season 6 data has the changes of <see cref="PreLearnedSkillsAndEventItemShop"/>,
/// and that <see cref="PreLearnedSkillsAndEventItemShopUpdatePlugIn"/> applies them to existing databases.
/// </summary>
[TestFixture]
internal class PreLearnedSkillsAndEventItemShopTest
{
    private const byte OrbGroup = 12;
    private const short OrbOfIceArrowNumber = 18;
    private const int IceArrowAgilityRequirement = 258;

    /// <summary>
    /// Tests that a new season 6 database has the changes.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasChangesAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertChanges(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update applies the changes to a database without them,
    /// and that applying it twice doesn't add anything twice.
    /// </summary>
    [Test]
    public async Task UpdateAppliesChangesAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(gameConfiguration);
        Assert.That(GetLumenStore(gameConfiguration).Items.Any(i => i.Definition!.Group == 14 && i.Definition.Number == 28), Is.False);

        var update = new PreLearnedSkillsAndEventItemShopUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertChanges(gameConfiguration);

        var lumenItemCount = GetLumenStore(gameConfiguration).Items.Count;
        var skillRequirementCount = gameConfiguration.Skills.Sum(s => s.Requirements.Count);
        var mapDropGroupCount = gameConfiguration.Maps.Sum(m => m.DropItemGroups.Count);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertChanges(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(GetLumenStore(gameConfiguration).Items, Has.Count.EqualTo(lumenItemCount));
            Assert.That(gameConfiguration.Skills.Sum(s => s.Requirements.Count), Is.EqualTo(skillRequirementCount));
            Assert.That(gameConfiguration.Maps.Sum(m => m.DropItemGroups.Count), Is.EqualTo(mapDropGroupCount));
        });
    }

    private static void AssertChanges(GameConfiguration gameConfiguration)
    {
        var lumenStore = GetLumenStore(gameConfiguration);
        var orbOfIceArrow = gameConfiguration.Items.Single(i => i.Group == OrbGroup && i.Number == OrbOfIceArrowNumber);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.Items.Where(AddLearnableSkillsForNewCharacters.IsSkillLearningItem), Is.Not.Empty);
            Assert.That(gameConfiguration.Items.Where(AddLearnableSkillsForNewCharacters.IsSkillLearningItem).Where(i => i.DropsFromMonsters), Is.Empty);
            Assert.That(
                orbOfIceArrow.Skill!.Requirements.Where(r => r.Attribute == Stats.TotalAgility).Select(r => r.MinimumValue),
                Is.EqualTo(new[] { IceArrowAgilityRequirement }));

            Assert.That(gameConfiguration.Maps.SelectMany(m => m.DropItemGroups).Where(PreLearnedSkillsAndEventItemShop.IsEventItemDropGroup), Is.Empty);
            Assert.That(gameConfiguration.Monsters.SelectMany(m => m.DropItemGroups).Where(PreLearnedSkillsAndEventItemShop.IsEventItemDropGroup), Is.Empty);

            foreach (var (slot, group, number, level) in PreLearnedSkillsAndEventItemShop.AddedLumenItems)
            {
                Assert.That(
                    lumenStore.Items.Count(i => i.ItemSlot == slot && i.Definition!.Group == group && i.Definition.Number == number && i.Level == level),
                    Is.EqualTo(1),
                    $"Slot {slot}");
            }

            Assert.That(lumenStore.Items.Select(i => i.ItemSlot).Distinct().Count(), Is.EqualTo(lumenStore.Items.Count));
        });
    }

    /// <summary>
    /// Brings the data into the state before the changes.
    /// </summary>
    private static void RestorePreviousData(GameConfiguration gameConfiguration)
    {
        foreach (var skillItem in gameConfiguration.Items.Where(AddLearnableSkillsForNewCharacters.IsSkillLearningItem))
        {
            skillItem.DropsFromMonsters = true;
        }

        var iceArrow = gameConfiguration.Items.Single(i => i.Group == OrbGroup && i.Number == OrbOfIceArrowNumber).Skill!;
        foreach (var requirement in iceArrow.Requirements.Where(r => r.Attribute == Stats.TotalAgility).ToList())
        {
            iceArrow.Requirements.Remove(requirement);
        }

        var eventItemGroups = gameConfiguration.DropItemGroups.Where(PreLearnedSkillsAndEventItemShop.IsEventItemDropGroup).ToList();
        Assert.That(eventItemGroups, Is.Not.Empty);
        foreach (var map in gameConfiguration.Maps)
        {
            eventItemGroups.ForEach(map.DropItemGroups.Add);
        }

        var lumenStore = GetLumenStore(gameConfiguration);
        foreach (var (slot, _, _, _) in PreLearnedSkillsAndEventItemShop.AddedLumenItems)
        {
            if (lumenStore.Items.FirstOrDefault(i => i.ItemSlot == slot) is { } item)
            {
                lumenStore.Items.Remove(item);
            }
        }
    }

    private static ItemStorage GetLumenStore(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters.Single(m => m.Number == PreLearnedSkillsAndEventItemShop.LumenTheBarmaidNumber).MerchantStore!;
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
