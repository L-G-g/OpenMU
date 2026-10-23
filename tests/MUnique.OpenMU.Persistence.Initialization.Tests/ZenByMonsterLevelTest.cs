// <copyright file="ZenByMonsterLevelTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.MoneyDrop;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that the season 6 data has the changes of <see cref="ZenByMonsterLevel"/>,
/// and that <see cref="ZenByMonsterLevelUpdatePlugIn"/> applies them to existing databases.
/// </summary>
[TestFixture]
internal class ZenByMonsterLevelTest
{
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
    /// Tests that the update applies the changes to a database without them, keeps a configuration of the
    /// plugin which the server owner changed, and that applying it twice gives the same result.
    /// </summary>
    [Test]
    public async Task UpdateAppliesChangesAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        foreach (var option in ZenByMonsterLevel.GetExcellentMoneyOptions(gameConfiguration))
        {
            option.PowerUpDefinition!.Boost!.ConstantValue.Value = 1.4f;
            option.PowerUpDefinition!.Boost!.ConstantValue.AggregateType = AggregateType.Multiplicate;
        }

        gameConfiguration.PlugInConfigurations.Remove(GetPlugInConfiguration(gameConfiguration));

        var update = new ZenByMonsterLevelUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertChanges(gameConfiguration);

        GetPlugInConfiguration(gameConfiguration).SetConfiguration(new MonsterLevelMoneyDropConfiguration { Multiplier = 0.02 }, null);
        var plugInConfigurationCount = gameConfiguration.PlugInConfigurations.Count;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.PlugInConfigurations, Has.Count.EqualTo(plugInConfigurationCount));
            Assert.That(GetPlugInConfiguration(gameConfiguration).GetConfiguration<MonsterLevelMoneyDropConfiguration>(null)!.Multiplier, Is.EqualTo(0.02));
            Assert.That(ZenByMonsterLevel.GetExcellentMoneyOptions(gameConfiguration).Select(o => o.PowerUpDefinition!.Boost!.ConstantValue.Value), Is.All.EqualTo(0.4f));
        });
    }

    private static void AssertChanges(GameConfiguration gameConfiguration)
    {
        var options = ZenByMonsterLevel.GetExcellentMoneyOptions(gameConfiguration).ToList();
        var plugInConfiguration = GetPlugInConfiguration(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(options, Is.Not.Empty);
            Assert.That(options.Select(o => o.PowerUpDefinition!.Boost!.ConstantValue.Value), Is.All.EqualTo(0.4f));
            Assert.That(options.Select(o => o.PowerUpDefinition!.Boost!.ConstantValue.AggregateType), Is.All.EqualTo(AggregateType.AddRaw));
            Assert.That(plugInConfiguration.IsActive, Is.True);
            Assert.That(plugInConfiguration.GetConfiguration<MonsterLevelMoneyDropConfiguration>(null)!.Exponent, Is.EqualTo(3.5));
        });
    }

    private static PlugInConfiguration GetPlugInConfiguration(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterLevelMoneyDropPlugIn).GUID);
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
