// <copyright file="MoneyDropCalculationArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// The arguments for the <see cref="IMoneyDropCalculationPlugIn"/>.
/// </summary>
public sealed class MoneyDropCalculationArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoneyDropCalculationArgs"/> class.
    /// </summary>
    /// <param name="monster">The definition of the killed monster.</param>
    /// <param name="gainedExperience">The experience which all players gained for the kill together.</param>
    /// <param name="money">The calculated amount of money.</param>
    public MoneyDropCalculationArgs(MonsterDefinition monster, int gainedExperience, double money)
    {
        this.Monster = monster;
        this.GainedExperience = gainedExperience;
        this.Money = money;
    }

    /// <summary>
    /// Gets the definition of the killed monster.
    /// </summary>
    public MonsterDefinition Monster { get; }

    /// <summary>
    /// Gets the experience which all players gained for the kill together.
    /// </summary>
    public int GainedExperience { get; }

    /// <summary>
    /// Gets or sets the amount of money of one money drop. It can be modified by the plugins.
    /// </summary>
    public double Money { get; set; }
}
