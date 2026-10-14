// <copyright file="FixItemDataFromClientTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the season 6 item data matches the item table of the original client,
/// and that <see cref="FixItemDataFromClientPlugIn"/> corrects existing databases.
/// </summary>
[TestFixture]
internal class FixItemDataFromClientTest
{
    /// <summary>
    /// Tests that a new season 6 database has the corrected item data.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasClientItemDataAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertCorrectedData(gameConfiguration);
        Assert.That(
            gameConfiguration.Items.Where(i => i.Group == 12 && (i.Number is 73 or 74 || i.Number >= 118 && i.Number <= 129)),
            Is.Empty,
            "The spheres and seed spheres which the client doesn't know shouldn't be created.");
    }

    /// <summary>
    /// Tests that the update corrects a database with the previous item data,
    /// and that applying it twice doesn't duplicate anything.
    /// </summary>
    [Test]
    public async Task UpdateCorrectsExistingDatabaseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(context, gameConfiguration);

        var update = new FixItemDataFromClientPlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertCorrectedData(gameConfiguration);
        Assert.That(GetItem(gameConfiguration, 2, 16).Requirements.Count(r => r.Attribute == Stats.Level), Is.EqualTo(1));
        Assert.That(GetItem(gameConfiguration, 10, 48).Requirements.Count(r => r.Attribute == Stats.TotalEnergyRequirementValue), Is.EqualTo(1));

        // The spheres which the client doesn't know are kept (characters may have them), but they
        // can't drop or be used to create seed spheres anymore.
        var sphere4 = GetItem(gameConfiguration, 12, 73);
        Assert.Multiple(() =>
        {
            Assert.That(sphere4.DropsFromMonsters, Is.False);
            Assert.That(GetItem(gameConfiguration, 12, 118).DropsFromMonsters, Is.False);
            Assert.That(
                gameConfiguration.Monsters.SelectMany(m => m.ItemCraftings).Select(c => c.SimpleCraftingSettings)
                    .Where(s => s is not null).SelectMany(s => s!.RequiredItems).SelectMany(r => r.PossibleItems),
                Does.Not.Contain(sphere4));
        });
    }

    private static void AssertCorrectedData(GameConfiguration gameConfiguration)
    {
        var darkBreaker = GetItem(gameConfiguration, 0, 17);
        var frostMace = GetItem(gameConfiguration, 2, 16);
        var guardianShield = GetItem(gameConfiguration, 6, 20);
        var helm44 = GetItem(gameConfiguration, 7, 44);
        var gloves44 = GetItem(gameConfiguration, 10, 44);

        Assert.Multiple(() =>
        {
            Assert.That(darkBreaker.QualifiedCharacters.Select(c => (CharacterClassNumber)c.Number), Is.EquivalentTo(new[] { CharacterClassNumber.BladeKnight, CharacterClassNumber.BladeMaster }));
            Assert.That(GetRequirement(frostMace, Stats.Level), Is.EqualTo(380));
            Assert.That(GetRequirement(GetItem(gameConfiguration, 8, 45), Stats.Level), Is.EqualTo(380));
            Assert.That(guardianShield.Skill?.Number, Is.EqualTo(18));

            Assert.That(helm44.DropLevel, Is.EqualTo(116));
            Assert.That(helm44.Durability, Is.EqualTo(86));
            Assert.That(helm44.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.DefenseBase).BaseValue, Is.EqualTo(35));
            Assert.That(GetRequirement(helm44, Stats.TotalStrengthRequirementValue), Is.EqualTo(62));
            Assert.That(GetRequirement(helm44, Stats.TotalEnergyRequirementValue), Is.EqualTo(170));
            Assert.That(gloves44.BasePowerUpAttributes.Any(p => p.TargetAttribute == Stats.AttackSpeedAny), Is.False);

            Assert.That(GetRequirement(GetItem(gameConfiguration, 8, 73), Stats.TotalVitalityRequirementValue), Is.EqualTo(85));
            Assert.That(GetRequirement(GetItem(gameConfiguration, 10, 48), Stats.TotalEnergyRequirementValue), Is.EqualTo(165));

            // A seed sphere keeps the level of its seed, and has the drop level of the client.
            Assert.That(GetItem(gameConfiguration, 12, 100).MaximumItemLevel, Is.EqualTo(GetItem(gameConfiguration, 12, 60).MaximumItemLevel));
            Assert.That(GetItem(gameConfiguration, 12, 100).MaximumItemLevel, Is.GreaterThan(0));
            Assert.That(GetItem(gameConfiguration, 12, 117).MaximumItemLevel, Is.EqualTo(GetItem(gameConfiguration, 12, 65).MaximumItemLevel));
            Assert.That(GetItem(gameConfiguration, 12, 100).DropLevel, Is.EqualTo(1));
            Assert.That(GetItem(gameConfiguration, 12, 112).DropLevel, Is.EqualTo(3));
        });
    }

    /// <summary>
    /// Brings some of the corrected items back into the state of the previous initialization data.
    /// </summary>
    private static void RestorePreviousData(IContext context, GameConfiguration gameConfiguration)
    {
        var darkKnight = gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)CharacterClassNumber.DarkKnight);
        GetItem(gameConfiguration, 0, 17).QualifiedCharacters.Add(darkKnight);

        foreach (var item in new[] { GetItem(gameConfiguration, 2, 16), GetItem(gameConfiguration, 8, 45) })
        {
            item.Requirements.Remove(item.Requirements.Single(r => r.Attribute == Stats.Level));
        }

        GetItem(gameConfiguration, 6, 20).Skill = null;

        var helm44 = GetItem(gameConfiguration, 7, 44);
        helm44.DropLevel = 110;
        helm44.Durability = 80;
        helm44.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.DefenseBase).BaseValue = 50;
        helm44.Requirements.Single(r => r.Attribute == Stats.TotalStrengthRequirementValue).MinimumValue = 80;
        helm44.Requirements.Remove(helm44.Requirements.Single(r => r.Attribute == Stats.TotalEnergyRequirementValue));

        var attackSpeed = context.CreateNew<ItemBasePowerUpDefinition>();
        attackSpeed.TargetAttribute = Stats.AttackSpeedAny.GetPersistent(gameConfiguration);
        attackSpeed.BaseValue = 6;
        GetItem(gameConfiguration, 10, 44).BasePowerUpAttributes.Add(attackSpeed);

        var phoenixArmor = GetItem(gameConfiguration, 8, 73);
        phoenixArmor.Requirements.Remove(phoenixArmor.Requirements.Single(r => r.Attribute == Stats.TotalVitalityRequirementValue));
        var gloves48 = GetItem(gameConfiguration, 10, 48);
        gloves48.Requirements.Remove(gloves48.Requirements.Single(r => r.Attribute == Stats.TotalEnergyRequirementValue));

        foreach (var seedSphere in gameConfiguration.Items.Where(i => i.Group == 12 && i.Number >= 100 && i.Number <= 117))
        {
            seedSphere.DropLevel = (byte)((seedSphere.Number - 100) / 6);
            seedSphere.MaximumItemLevel = 0;
        }

        // The previous data also had a fourth sphere, usable to create seed spheres, and seed spheres
        // of the fourth level which dropped from monsters.
        var sphere4 = context.CreateNew<ItemDefinition>();
        sphere4.Group = 12;
        sphere4.Number = 73;
        gameConfiguration.Items.Add(sphere4);
        var seedSphereCrafting = gameConfiguration.Monsters.SelectMany(m => m.ItemCraftings).Single(c => c.Name == "Seed Sphere Creation");
        seedSphereCrafting.SimpleCraftingSettings!.RequiredItems.Single(r => r.PossibleItems.Any(i => i.Number == 70)).PossibleItems.Add(sphere4);

        var seedSphere4 = context.CreateNew<ItemDefinition>();
        seedSphere4.Group = 12;
        seedSphere4.Number = 118;
        seedSphere4.DropsFromMonsters = true;
        gameConfiguration.Items.Add(seedSphere4);
    }

    private static ItemDefinition GetItem(GameConfiguration gameConfiguration, int group, int number)
    {
        return gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
    }

    private static double GetRequirement(ItemDefinition item, AttributeDefinition attribute)
    {
        return item.Requirements.Single(r => r.Attribute == attribute).MinimumValue;
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
