// <copyright file="MonsterLevelMoneyDropPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.MoneyDrop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Calculates the money of a money drop by the level of the killed monster, instead of by the experience
/// which the players gained for the kill: money = multiplier × level ^ exponent.
/// </summary>
/// <remarks>
/// So the money doesn't depend on the level of the players, the experience rate or the experience multiplier
/// of the map. The money rate of the players (e.g. the excellent option for more money) still applies, and
/// a party still shares the money by the experience of each member.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MonsterLevelMoneyDropPlugIn_Name), Description = nameof(PlugInResources.MonsterLevelMoneyDropPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0CE56F61-45C4-413D-9385-7A56DD0E37BE")]
public class MonsterLevelMoneyDropPlugIn : IMoneyDropCalculationPlugIn, ISupportCustomConfiguration<MonsterLevelMoneyDropConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public MonsterLevelMoneyDropConfiguration? Configuration { get; set; }

    /// <summary>
    /// Calculates the money of a money drop of a monster of the specified level.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="monsterLevel">The level of the monster.</param>
    /// <returns>The money of a money drop.</returns>
    public static double CalculateMoney(MonsterLevelMoneyDropConfiguration configuration, double monsterLevel)
    {
        var money = configuration.Multiplier * Math.Pow(Math.Max(monsterLevel, 0), configuration.Exponent);
        return Math.Max(money, configuration.MinimumMoney);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new MonsterLevelMoneyDropConfiguration();
    }

    /// <inheritdoc />
    public ValueTask CalculateMoneyDropAsync(Player killer, MoneyDropCalculationArgs args)
    {
        if (this.Configuration is { } configuration)
        {
            args.Money = CalculateMoney(configuration, args.Monster[Stats.Level]);
        }

        return ValueTask.CompletedTask;
    }
}
