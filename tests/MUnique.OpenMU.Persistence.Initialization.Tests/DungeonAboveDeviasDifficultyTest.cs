// <copyright file="DungeonAboveDeviasDifficultyTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using DifficultyMultipliers = MUnique.OpenMU.GameLogic.PlugIns.MapDifficultyScalerConfiguration.DifficultyMultipliers;

/// <summary>
/// Tests that, with the default multipliers of the <see cref="MapDifficultyScaler"/>, the monsters of Devias are weaker
/// than the ones of Dungeon, the next map of the <see cref="MapLevelLadder"/>,
/// and that <see cref="DungeonAboveDeviasDifficultyUpdatePlugIn"/> adds the multipliers to an existing configuration.
/// </summary>
[TestFixture]
internal class DungeonAboveDeviasDifficultyTest
{
    private const byte DeviasZone = 2;
    private const byte DungeonZone = 3;
    private const short DeviasMapNumber = 2;
    private const short DungeonMapNumber = 1;
    private const short IceQueenNumber = 25;
    private const short GorgonNumber = 18;

    /// <summary>
    /// Tests that each stat of each monster of Dungeon is at least the one of the strongest monster of Devias,
    /// and that the Gorgon is stronger than the Ice Queen.
    /// </summary>
    [Test]
    public void DungeonIsAboveDevias()
    {
        var configuration = new MapDifficultyScalerConfiguration();
        var devias = MapLevelLadder.Monsters.Where(m => m.Zone == DeviasZone && m.Number != IceQueenNumber).Select(m => GetStats(configuration, m, DeviasMapNumber)).ToList();
        var dungeon = MapLevelLadder.Monsters.Where(m => m.Zone == DungeonZone && m.Number != GorgonNumber).Select(m => GetStats(configuration, m, DungeonMapNumber)).ToList();
        var iceQueen = GetStats(configuration, MapLevelLadder.Monsters.Single(m => m.Zone == DeviasZone && m.Number == IceQueenNumber), DeviasMapNumber);
        var gorgon = GetStats(configuration, MapLevelLadder.Monsters.Single(m => m.Zone == DungeonZone && m.Number == GorgonNumber), DungeonMapNumber);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < iceQueen.Length; i++)
            {
                var stat = i;
                Assert.That(dungeon.Min(s => s[stat]), Is.GreaterThan(devias.Max(s => s[stat])), $"stat {stat}");
                Assert.That(gorgon[stat], Is.GreaterThan(iceQueen[stat]), $"boss stat {stat}");
            }
        });
    }

    /// <summary>
    /// Tests that the update adds the multipliers of the monsters of Dungeon to an existing configuration once,
    /// and keeps the other multipliers.
    /// </summary>
    [Test]
    public async Task UpdateAddsMultipliersAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var scalerConfiguration = new MapDifficultyScalerConfiguration();
        scalerConfiguration.Monsters = scalerConfiguration.Monsters.Where(m => m.MapNumber != DungeonMapNumber).ToList();
        var otherMonsters = scalerConfiguration.Monsters.Count;
        var scalerPlugIn = gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(MapDifficultyScaler).GUID);
        scalerPlugIn.SetConfiguration(scalerConfiguration, new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler());

        var update = new DungeonAboveDeviasDifficultyUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        var monsters = scalerPlugIn.GetConfiguration<MapDifficultyScalerConfiguration>(new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler())!.Monsters;
        var expected = MapDifficultyScalerConfiguration.CreateDungeonAboveDeviasMonsters();
        Assert.Multiple(() =>
        {
            Assert.That(monsters, Has.Count.EqualTo(otherMonsters + expected.Count));
            Assert.That(
                monsters.Where(m => m.MapNumber == DungeonMapNumber).Select(m => (m.Number, m.Health, m.Damage, m.Defense, m.AttackRate, m.DefenseRate)),
                Is.EquivalentTo(expected.Select(m => (m.Number, m.Health, m.Damage, m.Defense, m.AttackRate, m.DefenseRate))));
        });
    }

    private static double[] GetStats(MapDifficultyScalerConfiguration configuration, MapLevelLadder.LadderMonster monster, short mapNumber)
    {
        // The same order as in the plugin: the monster on the map, the monster on all maps, the map.
        var multipliers = configuration.Monsters.FirstOrDefault(m => m.Number == monster.Number && m.MapNumber == mapNumber)
            ?? configuration.Monsters.FirstOrDefault(m => m.Number == monster.Number && m.MapNumber == DifficultyMultipliers.AllMaps)
            ?? configuration.Maps.FirstOrDefault(m => m.Number == mapNumber)
            ?? new DifficultyMultipliers();

        return
        [
            monster.Health * multipliers.Health,
            (monster.MinimumDamage + monster.MaximumDamage) / 2 * multipliers.Damage,
            monster.Defense * multipliers.Defense,
            monster.AttackRate * multipliers.AttackRate,
            monster.DefenseRate * multipliers.DefenseRate,
        ];
    }
}
