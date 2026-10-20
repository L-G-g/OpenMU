// <copyright file="LevelRangeExperiencePlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.Experience;

/// <summary>
/// Tests for the <see cref="LevelRangeExperiencePlugIn"/>.
/// </summary>
[TestFixture]
public class LevelRangeExperiencePlugInTest
{
    /// <summary>
    /// Tests that the experience is multiplied inside of the level range, including its limits.
    /// </summary>
    [TestCase(299, false, 100)]
    [TestCase(300, false, 500)]
    [TestCase(399, false, 500)]
    [TestCase(400, false, 100)]
    [TestCase(350, true, 100)]
    public async ValueTask ExperienceIsMultipliedInRangeAsync(int level, bool isMasterExperience, double expectedExperience)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = level;
        var plugIn = new LevelRangeExperiencePlugIn { Configuration = new LevelRangeExperienceConfiguration() };
        var args = new ExperienceCalculationArgs(player, isMasterExperience, 100);

        await plugIn.CalculateExperienceAsync(player, args).ConfigureAwait(false);

        Assert.That(args.Experience, Is.EqualTo(expectedExperience).Within(0.001));
    }
}
