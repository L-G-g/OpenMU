// <copyright file="MonsterAttributeScalerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests for the <see cref="MonsterAttributeScaler"/>.
/// </summary>
[TestFixture]
public class MonsterAttributeScalerTest
{
    /// <summary>
    /// Tests that a configured percentage results in the expected multiplier, including negative percentages which weaken monsters.
    /// </summary>
    /// <param name="percentage">The configured percentage.</param>
    /// <param name="expectedMultiplier">The expected multiplier.</param>
    [TestCase(0f, 1.0f)]
    [TestCase(25f, 1.25f)]
    [TestCase(20f, 1.2f)]
    [TestCase(-20f, 0.8f)]
    [TestCase(-99f, 0.01f)]
    [TestCase(-100f, 0.01f)]
    [TestCase(-150f, 0.01f)]
    public void MultiplierFromPercentage(float percentage, float expectedMultiplier)
    {
        Assert.That(MonsterAttributeScaler.GetMultiplier(percentage), Is.EqualTo(expectedMultiplier).Within(0.0001f));
    }
}
