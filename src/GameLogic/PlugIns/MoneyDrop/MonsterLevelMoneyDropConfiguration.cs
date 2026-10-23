// <copyright file="MonsterLevelMoneyDropConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.MoneyDrop;

/// <summary>
/// Configuration for the <see cref="MonsterLevelMoneyDropPlugIn"/>.
/// </summary>
/// <remarks>
/// The default values are chosen so that 40 minutes of hunting in Raklion (monster level 140 to 148, about
/// 1,470 kills per hour, half of them drop money) give about 333 million zen, or 1,000 million with
/// five excellent items with more money.
/// </remarks>
public class MonsterLevelMoneyDropConfiguration
{
    /// <summary>
    /// Gets or sets the multiplier of the formula: money = multiplier × level ^ exponent.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_Multiplier_Name), Description = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_Multiplier_Description))]
    public double Multiplier { get; set; } = 0.019;

    /// <summary>
    /// Gets or sets the exponent of the formula: money = multiplier × level ^ exponent.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_Exponent_Name), Description = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_Exponent_Description))]
    public double Exponent { get; set; } = 3.5;

    /// <summary>
    /// Gets or sets the minimum amount of money of a money drop.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_MinimumMoney_Name), Description = nameof(PlugInResources.MonsterLevelMoneyDropConfiguration_MinimumMoney_Description))]
    public int MinimumMoney { get; set; } = 1;
}
