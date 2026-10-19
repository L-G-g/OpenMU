// <copyright file="ResetExperienceSchemeTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.Experience;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that the season 6 data has the <see cref="ResetExperienceScheme"/>,
/// and that <see cref="ResetExperienceSchemeUpdatePlugIn"/> applies it to existing databases.
/// </summary>
[TestFixture]
internal class ResetExperienceSchemeTest
{
    private const short ElfSoldierNumber = 257;

    /// <summary>
    /// Tests the required levels of the scheme: 300 for the first 10 resets, then 10 more every 10 resets, up to 400.
    /// </summary>
    [TestCase(1, 300)]
    [TestCase(10, 300)]
    [TestCase(11, 310)]
    [TestCase(20, 310)]
    [TestCase(21, 320)]
    [TestCase(100, 390)]
    [TestCase(101, 400)]
    [TestCase(360, 400)]
    public void RequiredLevels(int resetCount, int expectedLevel)
    {
        var configuration = new ResetConfiguration { RequiredLevel = 300 };
        ResetExperienceScheme.CreateRequiredLevelTiers().ForEach(configuration.RequiredLevelTiers.Add);

        Assert.That(ResetProgressionCalculator.GetRequiredLevel(configuration, resetCount), Is.EqualTo(expectedLevel));
    }

    /// <summary>
    /// Tests that a new season 6 database has the scheme.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasSchemeAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertScheme(gameConfiguration);
        Assert.That(GetResetConfiguration(gameConfiguration, null)!.RequiredLevel, Is.EqualTo(new ResetConfiguration().RequiredLevel));
    }

    /// <summary>
    /// Tests that the update applies the scheme to a database without it and keeps the other reset settings,
    /// and that applying it twice gives the same result.
    /// </summary>
    [Test]
    public async Task UpdateAppliesSchemeAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(context, gameConfiguration);

        var update = new ResetExperienceSchemeUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertScheme(gameConfiguration);
        AssertOtherResetSettingsKept(gameConfiguration);

        var resetConfigurationJson = GetResetPlugInConfiguration(gameConfiguration).CustomConfiguration;
        var globalValueCount = gameConfiguration.GlobalBaseAttributeValues.Count;
        var plugInConfigurationCount = gameConfiguration.PlugInConfigurations.Count;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertScheme(gameConfiguration);
        AssertOtherResetSettingsKept(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(GetResetPlugInConfiguration(gameConfiguration).CustomConfiguration, Is.EqualTo(resetConfigurationJson));
            Assert.That(gameConfiguration.GlobalBaseAttributeValues, Has.Count.EqualTo(globalValueCount));
            Assert.That(gameConfiguration.PlugInConfigurations, Has.Count.EqualTo(plugInConfigurationCount));
        });
    }

    private static void AssertScheme(GameConfiguration gameConfiguration)
    {
        var resetConfiguration = GetResetConfiguration(gameConfiguration, ReferenceHandler.Preserve);
        Assert.Multiple(() =>
        {
            Assert.That(resetConfiguration, Is.Not.Null);
            Assert.That(
                resetConfiguration!.RequiredLevelTiers.Select(t => (t.MinimumResetCount, t.RequiredLevel)),
                Is.EqualTo(ResetExperienceScheme.CreateRequiredLevelTiers().Select(t => (t.MinimumResetCount, t.RequiredLevel))));
            Assert.That(gameConfiguration.Monsters.Single(m => m.Number == ElfSoldierNumber).Buffs, Is.Empty);
            Assert.That(GetGlobalValue(gameConfiguration, Stats.RandomExperienceMinMultiplier), Is.EqualTo(1));
            Assert.That(GetGlobalValue(gameConfiguration, Stats.RandomExperienceMaxMultiplier), Is.EqualTo(1));
            Assert.That(gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(PermanentBuffBelowResetCountPlugIn).GUID).IsActive, Is.True);
            Assert.That(gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(LevelRangeExperiencePlugIn).GUID).IsActive, Is.True);
            Assert.That(
                gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(LevelRangeExperiencePlugIn).GUID).GetConfiguration<LevelRangeExperienceConfiguration>(null)!.Ranges.Single().Multiplier,
                Is.EqualTo(5));
        });
    }

    private static void AssertOtherResetSettingsKept(GameConfiguration gameConfiguration)
    {
        var resetConfiguration = GetResetConfiguration(gameConfiguration, ReferenceHandler.Preserve)!;
        Assert.Multiple(() =>
        {
            Assert.That(resetConfiguration.RequiredLevel, Is.EqualTo(300));
            Assert.That(resetConfiguration.ResetLimit, Is.EqualTo(360));
            Assert.That(resetConfiguration.PointsPerReset, Is.EqualTo(200));
            Assert.That(resetConfiguration.LevelAfterReset, Is.EqualTo(1));
            Assert.That(GetResetPlugInConfiguration(gameConfiguration).IsActive, Is.True);
        });
    }

    /// <summary>
    /// Restores the data like an existing database before the update: the reset feature is configured
    /// by the server owner (serialized with references, like the admin panel does), the random experience
    /// multipliers are 0.8 and 1.2, the Elf Soldier gives its buff and the new plugins aren't configured.
    /// </summary>
    private static void RestorePreviousData(IContext context, GameConfiguration gameConfiguration)
    {
        var resetPlugInConfiguration = GetResetPlugInConfiguration(gameConfiguration);
        resetPlugInConfiguration.IsActive = true;
        resetPlugInConfiguration.SetConfiguration(
            new ResetConfiguration { RequiredLevel = 300, ResetLimit = 360, PointsPerReset = 200, LevelAfterReset = 1 },
            ReferenceHandler.Preserve);

        foreach (var (attribute, value) in new[] { (Stats.RandomExperienceMinMultiplier, 0.8f), (Stats.RandomExperienceMaxMultiplier, 1.2f) })
        {
            var definition = gameConfiguration.Attributes.Single(a => a.Id == attribute.Id);
            gameConfiguration.GlobalBaseAttributeValues.Remove(gameConfiguration.GlobalBaseAttributeValues.Single(v => v.Definition?.Id == attribute.Id));
            gameConfiguration.GlobalBaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(value, definition));
        }

        var buff = context.CreateNew<Buff>();
        buff.MagicEffectDefinition = gameConfiguration.MagicEffects.Single(e => e.Number == 3);
        buff.MaximumLevel = 220;
        gameConfiguration.Monsters.Single(m => m.Number == ElfSoldierNumber).Buffs.Add(buff);

        foreach (var plugInType in new[] { typeof(PermanentBuffBelowResetCountPlugIn), typeof(LevelRangeExperiencePlugIn) })
        {
            gameConfiguration.PlugInConfigurations.Remove(gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == plugInType.GUID));
        }

        Assert.That(GetResetConfiguration(gameConfiguration, ReferenceHandler.Preserve)!.RequiredLevelTiers, Is.Empty);
    }

    private static PlugInConfiguration GetResetPlugInConfiguration(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(ResetFeaturePlugIn).GUID);
    }

    private static ResetConfiguration? GetResetConfiguration(GameConfiguration gameConfiguration, ReferenceHandler? referenceHandler)
    {
        return GetResetPlugInConfiguration(gameConfiguration).GetConfiguration<ResetConfiguration>(referenceHandler);
    }

    private static float GetGlobalValue(GameConfiguration gameConfiguration, AttributeDefinition attribute)
    {
        return gameConfiguration.GlobalBaseAttributeValues.Single(v => v.Definition?.Id == attribute.Id).Value;
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
