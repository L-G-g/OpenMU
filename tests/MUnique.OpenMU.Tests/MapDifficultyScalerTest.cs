// <copyright file="MapDifficultyScalerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests for the <see cref="MapDifficultyScaler"/>.
/// </summary>
[TestFixture]
public class MapDifficultyScalerTest
{
    private const short RaklionNumber = 57;
    private const short LorenciaNumber = 0;
    private const short GoldenBudgeDragonNumber = 43;
    private const short SpiderNumber = 3;

    /// <summary>
    /// Tests that the multipliers of a configured monster are used instead of the ones of its map.
    /// </summary>
    [Test]
    public void MonsterReplacesMap()
    {
        var configuration = new MapDifficultyScalerConfiguration();
        configuration.Maps.Add(new MapDifficultyScalerConfiguration.DifficultyMultipliers { Number = LorenciaNumber, Health = 2 });

        Assert.That(MapDifficultyScaler.GetKey(configuration, LorenciaNumber, GoldenBudgeDragonNumber), Is.EqualTo((true, GoldenBudgeDragonNumber)));
        Assert.That(MapDifficultyScaler.GetKey(configuration, LorenciaNumber, SpiderNumber), Is.EqualTo((false, LorenciaNumber)));
    }

    /// <summary>
    /// Tests that monsters of maps which aren't configured stay unchanged.
    /// </summary>
    [Test]
    public void UnconfiguredMapIsIgnored()
    {
        var configuration = new MapDifficultyScalerConfiguration();

        Assert.That(MapDifficultyScaler.GetKey(configuration, LorenciaNumber, SpiderNumber), Is.Null);
    }

    /// <summary>
    /// Tests that the default configuration makes Raklion harder and keeps all multipliers at 1 or above.
    /// </summary>
    [Test]
    public void DefaultsMakeMonstersStronger()
    {
        var configuration = new MapDifficultyScalerConfiguration();
        var raklion = configuration.Maps.Single(m => m.Number == RaklionNumber);

        Assert.That(raklion.Health, Is.GreaterThan(1));
        Assert.That(raklion.Defense, Is.GreaterThan(1));
        Assert.That(configuration.Maps.Concat(configuration.Monsters).SelectMany(m => new[] { m.Health, m.Damage, m.Defense, m.AttackRate, m.DefenseRate }), Has.All.GreaterThanOrEqualTo(1));
        Assert.That(configuration.Maps.Select(m => m.Number), Is.Unique);
        Assert.That(configuration.Monsters.Select(m => m.Number), Is.Unique);
    }

    /// <summary>
    /// Tests that the multipliers are applied to the stats and follow a change of the configured values.
    /// </summary>
    [Test]
    public void MultipliersAreAppliedAndUpdated()
    {
        var entry = new MapDifficultyScalerConfiguration.DifficultyMultipliers { Number = RaklionNumber, Health = 3, Damage = 2 };
        var multipliers = new MapDifficultyScaler.Multipliers(entry);
        var attributes = new AttributeSystem(
            [],
            [new ConstValueAttribute(1000, Stats.MaximumHealth), new ConstValueAttribute(100, Stats.MaximumPhysBaseDmg)],
            []);

        multipliers.AddTo(attributes);
        Assert.That(attributes[Stats.MaximumHealth], Is.EqualTo(3000));
        Assert.That(attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(200));

        entry.Health = 4;
        multipliers.SetValues(entry);
        Assert.That(attributes[Stats.MaximumHealth], Is.EqualTo(4000));

        multipliers.RemoveFrom(attributes);
        Assert.That(attributes[Stats.MaximumHealth], Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that a multiplier of zero or less, or a removed entry, changes nothing.
    /// </summary>
    [Test]
    public void InvalidOrMissingValuesChangeNothing()
    {
        var multipliers = new MapDifficultyScaler.Multipliers(new MapDifficultyScalerConfiguration.DifficultyMultipliers { Health = 0, Damage = -2 });
        Assert.That(multipliers.Health, Is.EqualTo(1));
        Assert.That(multipliers.Damage, Is.EqualTo(1));

        multipliers.SetValues(null);
        Assert.That(multipliers.Health, Is.EqualTo(1));
    }
}
