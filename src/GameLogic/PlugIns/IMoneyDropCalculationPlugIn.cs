// <copyright file="IMoneyDropCalculationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Plugins which modify the amount of money which a killed monster drops.
/// </summary>
[Guid("B36B5534-0D0B-4AAA-9439-8B299C6A5F5B")]
[PlugInPoint("Money drop calculation", "Plugins which modify the amount of money which a killed monster drops.")]
public interface IMoneyDropCalculationPlugIn
{
    /// <summary>
    /// Is called when the amount of money of a money drop has been calculated, before the money rate of the players is applied.
    /// </summary>
    /// <param name="killer">The player which killed the monster.</param>
    /// <param name="args">The arguments, which hold the calculated amount of money.</param>
    ValueTask CalculateMoneyDropAsync(Player killer, MoneyDropCalculationArgs args);
}
