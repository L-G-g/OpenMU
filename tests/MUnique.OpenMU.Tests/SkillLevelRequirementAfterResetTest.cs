// <copyright file="SkillLevelRequirementAfterResetTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Tests the <see cref="ResetConfiguration.IgnoreSkillLevelRequirements"/> option at <see cref="Player.TryConsumeForSkillAsync"/>.
/// </summary>
[TestFixture]
public class SkillLevelRequirementAfterResetTest
{
    private const int RequiredLevel = 120;

    /// <summary>
    /// Tests that the level requirement of a skill applies to characters without reset, even if the option is enabled.
    /// </summary>
    [Test]
    public async ValueTask LevelRequirementAppliesWithoutResetAsync()
    {
        var player = await CreatePlayerAsync(resets: 0, ignoreSkillLevelRequirements: true).ConfigureAwait(false);

        Assert.That(await player.TryConsumeForSkillAsync(CreateSkillEntry()).ConfigureAwait(false), Is.False);
    }

    /// <summary>
    /// Tests that the level requirement of a skill applies to characters with reset, if the option is disabled.
    /// </summary>
    [Test]
    public async ValueTask LevelRequirementAppliesAfterResetByDefaultAsync()
    {
        var player = await CreatePlayerAsync(resets: 1, ignoreSkillLevelRequirements: false).ConfigureAwait(false);

        Assert.That(await player.TryConsumeForSkillAsync(CreateSkillEntry()).ConfigureAwait(false), Is.False);
    }

    /// <summary>
    /// Tests that the level requirement of a skill is ignored for characters with reset, if the option is enabled.
    /// </summary>
    [Test]
    public async ValueTask LevelRequirementIsIgnoredAfterResetAsync()
    {
        var player = await CreatePlayerAsync(resets: 1, ignoreSkillLevelRequirements: true).ConfigureAwait(false);

        Assert.That(await player.TryConsumeForSkillAsync(CreateSkillEntry()).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// Tests that other requirements of a skill still apply for characters with reset, if the option is enabled.
    /// </summary>
    [Test]
    public async ValueTask OtherRequirementsApplyAfterResetAsync()
    {
        var player = await CreatePlayerAsync(resets: 1, ignoreSkillLevelRequirements: true).ConfigureAwait(false);
        var skillEntry = CreateSkillEntry();
        skillEntry.Skill!.Requirements.Add(new AttributeRequirement { Attribute = Stats.TotalEnergy, MinimumValue = 100_000 });

        Assert.That(await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false), Is.False);
    }

    private static async ValueTask<Player> CreatePlayerAsync(int resets, bool ignoreSkillLevelRequirements)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 30;
        player.Attributes[Stats.Resets] = resets;
        var configuration = new ResetConfiguration { IgnoreSkillLevelRequirements = ignoreSkillLevelRequirements };
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        return player;
    }

    private static SkillEntry CreateSkillEntry()
    {
        var skill = new TestSkill { Number = 48, Name = "Swell Life" };
        skill.Requirements.Add(new AttributeRequirement { Attribute = Stats.Level, MinimumValue = RequiredLevel });
        return new SkillEntry { Skill = skill };
    }

    private sealed class TestSkill : Skill
    {
        public TestSkill()
        {
            this.QualifiedCharacters = new List<CharacterClass>();
            this.Requirements = new List<AttributeRequirement>();
            this.ConsumeRequirements = new List<AttributeRequirement>();
        }
    }
}
