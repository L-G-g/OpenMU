// <copyright file="MapDropsAndInvasionsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.MapInvasions;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the <see cref="MapDropsAndInvasions"/> of season 6 and its <see cref="MapDropsAndInvasionsUpdatePlugIn"/>.
/// </summary>
[TestFixture]
internal class MapDropsAndInvasionsTest
{
    /// <summary>
    /// Tests that a new season 6 database has the drops and invasions.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasMapDropsAndInvasionsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertMapDropsAndInvasions(gameConfiguration);
    }

    /// <summary>
    /// Tests that applying the update again doesn't add anything twice.
    /// </summary>
    [Test]
    public async Task UpdateCanBeAppliedTwiceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        var dropGroupCount = gameConfiguration.DropItemGroups.Count;
        var monsterDropGroupCount = gameConfiguration.Monsters.Sum(m => m.DropItemGroups.Count);

        await new MapDropsAndInvasionsUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertMapDropsAndInvasions(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.DropItemGroups, Has.Count.EqualTo(dropGroupCount));
            Assert.That(gameConfiguration.Monsters.Sum(m => m.DropItemGroups.Count), Is.EqualTo(monsterDropGroupCount));
        });
    }

    private static void AssertMapDropsAndInvasions(GameConfiguration gameConfiguration)
    {
        var chaos = gameConfiguration.Items.First(i => i.Group == 12 && i.Number == 15);
        var gemstone = gameConfiguration.Items.First(i => i.Group == 14 && i.Number == 41);
        var guardian = gameConfiguration.Items.First(i => i.Group == 14 && i.Number == 31);
        var feather = gameConfiguration.Items.First(i => i.Group == 13 && i.Number == 14);
        var monoSphere = gameConfiguration.Items.First(i => i.Group == 12 && i.Number == 70);
        var lorencia = gameConfiguration.Maps.First(m => m.Number == 0);
        var kanturu = gameConfiguration.Maps.First(m => m.Number == 37);
        var relics = gameConfiguration.Maps.First(m => m.Number == 38);
        var crywolf = gameConfiguration.Maps.First(m => m.Number == CrywolfFortress.Number);
        var swamp = gameConfiguration.Maps.First(m => m.Number == 56);
        var movedItems = new[] { chaos, gemstone, guardian, feather, monoSphere };

        static IEnumerable<MonsterDefinition> Monsters(GameMapDefinition map) => map.MonsterSpawns
            .Where(s => s.SpawnTrigger == SpawnTrigger.Automatic && s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
            .Select(s => s.MonsterDefinition!)
            .Distinct();

        Assert.Multiple(() =>
        {
            // The moved items don't drop from any map group anymore.
            Assert.That(gameConfiguration.Maps.SelectMany(m => m.DropItemGroups).SelectMany(g => g.PossibleItems).Intersect(movedItems), Is.Empty);
            Assert.That(gameConfiguration.Items.Where(i => i.Group == 12 && i.Number is >= 70 and <= 74 && i.DropsFromMonsters), Is.Empty);

            // Every monster of the maps drops the item of the map, with the expected chance.
            Assert.That(Monsters(lorencia), Is.Not.Empty);
            Assert.That(Monsters(lorencia).All(m => m.DropItemGroups.Any(g => g.PossibleItems.Contains(chaos) && Math.Abs(g.Chance - (1.0 / 1250)) < 1e-9)), Is.True);
            Assert.That(Monsters(kanturu).All(m => m.DropItemGroups.Any(g => g.PossibleItems.Contains(gemstone))), Is.True);
            Assert.That(Monsters(relics).All(m => m.DropItemGroups.Any(g => g.PossibleItems.Contains(guardian))), Is.True);
            Assert.That(Monsters(swamp).All(m => m.DropItemGroups.Any(g => g.PossibleItems.Count == 4 && g.PossibleItems.Contains(chaos))), Is.True);

            // Crywolf: the Fenrir materials drop from some monsters only, and Soram drops none.
            Assert.That(crywolf.DropItemGroups.Where(MapLevelLadder.IsFenrirMaterialGroup), Is.Empty);
            Assert.That(MapLevelLadder.GetOriginalMonster(gameConfiguration, 316)!.DropItemGroups.Count(MapLevelLadder.IsFenrirMaterialGroup), Is.EqualTo(1));
            Assert.That(MapLevelLadder.GetOriginalMonster(gameConfiguration, 317)!.DropItemGroups.Where(MapLevelLadder.IsFenrirMaterialGroup), Is.Empty);

            // The invasion monsters don't spawn on their invaded maps anymore.
            foreach (var (mapNumber, monsterNumber, _) in MapDropsAndInvasions.Invasions)
            {
                var map = gameConfiguration.Maps.First(m => m.Number == mapNumber && m.Discriminator == 0);
                Assert.That(map.MonsterSpawns.Where(s => s.MonsterDefinition?.Number == monsterNumber && s.SpawnTrigger == SpawnTrigger.Automatic), Is.Empty, $"map {mapNumber}, monster {monsterNumber}");
            }

            var plugIn = gameConfiguration.PlugInConfigurations.First(c => c.TypeId == typeof(MapInvasionsPlugIn).GUID);
            Assert.That(plugIn.IsActive, Is.True);
            Assert.That(plugIn.CustomConfiguration, Does.Contain("Invasions"));
            Assert.That(gameConfiguration.PlugInConfigurations.First(c => c.TypeId == typeof(RedDragonInvasionPlugIn).GUID).IsActive, Is.False);
            Assert.That(gameConfiguration.PlugInConfigurations.First(c => c.TypeId == typeof(WhiteWizardInvasionPlugIn).GUID).IsActive, Is.False);
        });
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
