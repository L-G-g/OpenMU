// <copyright file="MapDifficultyLadderOrderTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Tests that the default multipliers of the <see cref="MapDifficultyScaler"/> keep the order of the <see cref="MapLevelLadder"/>:
/// each stat of each monster of a map is lower than the one of the weakest monster of the next map.
/// </summary>
[TestFixture]
internal class MapDifficultyLadderOrderTest
{
    /// <summary>
    /// The numbers of the bosses of the ladder. They appear in invasions, so they may be stronger than the next map.
    /// </summary>
    private static readonly short[] BossNumbers = [25, 18, 38, 49, 59, 63, 309, 77];

    private static readonly string[] StatNames = ["health", "damage", "defense", "attack rate", "defense rate"];

    /// <summary>
    /// Tests that no monster of a map is stronger than the weakest monster of the next map.
    /// </summary>
    [Test]
    public void MapsAreBelowTheNextMap()
    {
        var configuration = new MapDifficultyScalerConfiguration();
        var zones = MapLevelLadder.Monsters
            .Where(m => !BossNumbers.Contains(m.Number))
            .GroupBy(m => m.Zone)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(m => GetStats(configuration, m)).ToList())
            .ToList();

        Assert.Multiple(() =>
        {
            for (var zone = 0; zone < zones.Count - 1; zone++)
            {
                for (var stat = 0; stat < StatNames.Length; stat++)
                {
                    var strongest = zones[zone].Max(s => s[stat]);
                    var weakest = zones[zone + 1].Min(s => s[stat]);
                    Assert.That(strongest, Is.LessThan(weakest), $"{StatNames[stat]} of zone {zone + 1} and {zone + 2}");
                }
            }
        });
    }

    private static double[] GetStats(MapDifficultyScalerConfiguration configuration, MapLevelLadder.LadderMonster monster)
    {
        var mapNumber = MapLevelLadder.Zones[monster.Zone - 1].MapNumbers[0];
        var multipliers = configuration.Monsters.FirstOrDefault(m => m.Number == monster.Number)
            ?? configuration.Maps.FirstOrDefault(m => m.Number == mapNumber)
            ?? new MapDifficultyScalerConfiguration.DifficultyMultipliers();

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
