// <copyright file="MonsterLevelMoneyDropPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.MoneyDrop;

/// <summary>
/// Tests for the <see cref="MonsterLevelMoneyDropPlugIn"/>.
/// </summary>
[TestFixture]
public class MonsterLevelMoneyDropPlugInTest
{
    /// <summary>
    /// Tests the money of the default configuration: 0.019 × level ^ 3.5, at least 1.
    /// </summary>
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(10, 60.08)]
    [TestCase(50, 16_793)]
    [TestCase(148, 749_324)]
    public void MoneyByMonsterLevel(int monsterLevel, double expectedMoney)
    {
        var money = MonsterLevelMoneyDropPlugIn.CalculateMoney(new MonsterLevelMoneyDropConfiguration(), monsterLevel);

        Assert.That(money, Is.EqualTo(expectedMoney).Within(expectedMoney * 0.001));
    }

    /// <summary>
    /// Tests that the plugin replaces the money of the experience by the money of the monster level.
    /// </summary>
    [Test]
    public async ValueTask PlugInReplacesMoneyOfExperienceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = new MonsterLevelMoneyDropPlugIn { Configuration = new MonsterLevelMoneyDropConfiguration() };
        var args = new MoneyDropCalculationArgs(CreateMonster(148), 1_000_000, 1_000_007);

        await plugIn.CalculateMoneyDropAsync(player, args).ConfigureAwait(false);

        Assert.That(args.Money, Is.EqualTo(749_324).Within(1));
    }

    /// <summary>
    /// Tests that the drop generator drops the money of the experience without the plugin,
    /// and the money of the monster level with it.
    /// </summary>
    /// <param name="withPlugIn">If set to <c>true</c>, the plugin is active.</param>
    /// <param name="expectedMoney">The expected money.</param>
    [TestCase(false, 1_007u)]
    [TestCase(true, 749_324u)]
    public async ValueTask DropGeneratorUsesPlugInAsync(bool withPlugIn, uint expectedMoney)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        if (withPlugIn)
        {
            player.GameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IMoneyDropCalculationPlugIn>(
                new MonsterLevelMoneyDropPlugIn { Configuration = new MonsterLevelMoneyDropConfiguration() });
        }

        var monster = CreateMonster(148);
        var moneyGroup = new Mock<DropItemGroup>();
        moneyGroup.SetupAllProperties();
        moneyGroup.Object.Chance = 1;
        moneyGroup.Object.ItemType = SpecialItemType.Money;
        moneyGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition>());
        monster.DropItemGroups.Add(moneyGroup.Object);

        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextDouble()).Returns(0.5);
        var config = new Mock<GameConfiguration>();
        config.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        config.Setup(c => c.DropItemGroups).Returns(new List<DropItemGroup>());
        config.Setup(c => c.Maps).Returns(new List<GameMapDefinition>());
        var generator = new DefaultDropGenerator(config.Object, randomizer.Object);

        var (_, money) = await generator.GenerateItemDropsAsync(monster, 1_000, player).ConfigureAwait(false);

        Assert.That(money, Is.EqualTo(expectedMoney));
    }

    private static MonsterDefinition CreateMonster(byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = 1;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }
}
