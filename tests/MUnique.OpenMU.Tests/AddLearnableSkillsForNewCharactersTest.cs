// <copyright file="AddLearnableSkillsForNewCharactersTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;

/// <summary>
/// Tests <see cref="AddLearnableSkillsForNewCharacters"/>, which adds the skills of the class line to a created character,
/// and <see cref="Player.IsQualifiedForSkill"/>, which only allows to perform the skills of the current class.
/// </summary>
[TestFixture]
public class AddLearnableSkillsForNewCharactersTest
{
    private readonly CharacterClass _knight = new() { Number = 4 };
    private readonly CharacterClass _bladeKnight = new() { Number = 6 };
    private readonly CharacterClass _wizard = new() { Number = 0 };

    /// <summary>
    /// Tests that a created knight gets the skills of the knight and the blade knight which are learned with an item,
    /// but not the skills of other classes, of weapons and master skills.
    /// </summary>
    [Test]
    public async ValueTask AddsSkillsOfClassAndNextGenerationAsync()
    {
        var (player, skills) = await this.CreatePlayerWithSkillsAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;

        new AddLearnableSkillsForNewCharacters().CharacterCreated(player, character);

        Assert.That(character.LearnedSkills.Select(entry => entry.Skill), Is.EquivalentTo(new[] { skills.Knight, skills.BladeKnight }));
    }

    /// <summary>
    /// Tests that a skill which the character already knows isn't added twice.
    /// </summary>
    [Test]
    public async ValueTask DoesNotAddKnownSkillAgainAsync()
    {
        var (player, skills) = await this.CreatePlayerWithSkillsAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.LearnedSkills.Add(new SkillEntry { Skill = skills.Knight });

        new AddLearnableSkillsForNewCharacters().CharacterCreated(player, character);

        Assert.That(character.LearnedSkills.Count(entry => entry.Skill == skills.Knight), Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that a knight can't perform the known skill of the blade knight, until it becomes a blade knight.
    /// </summary>
    [Test]
    public async ValueTask SkillOfNextGenerationCanOnlyBePerformedAfterClassChangeAsync()
    {
        var (player, skills) = await this.CreatePlayerWithSkillsAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(player.IsQualifiedForSkill(skills.Knight), Is.True);
            Assert.That(player.IsQualifiedForSkill(skills.BladeKnight), Is.False);
        });
        Assert.That(await player.TryConsumeForSkillAsync(new SkillEntry { Skill = skills.BladeKnight }).ConfigureAwait(false), Is.False);

        player.SelectedCharacter!.CharacterClass = this._bladeKnight;

        Assert.That(player.IsQualifiedForSkill(skills.BladeKnight), Is.True);
        Assert.That(await player.TryConsumeForSkillAsync(new SkillEntry { Skill = skills.BladeKnight }).ConfigureAwait(false), Is.True);
    }

    private async ValueTask<(Player Player, (Skill Knight, Skill BladeKnight) Skills)> CreatePlayerWithSkillsAsync()
    {
        this._knight.NextGenerationClass = this._bladeKnight;
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass = this._knight;

        var knightSkill = CreateSkill(1, this._knight, this._bladeKnight);
        var bladeKnightSkill = CreateSkill(2, this._bladeKnight);
        var wizardSkill = CreateSkill(3, this._wizard);
        var weaponSkill = CreateSkill(4, this._knight, this._bladeKnight);
        var masterSkill = CreateSkill(5, this._knight, this._bladeKnight);
        masterSkill.MasterDefinition = new MasterSkillDefinition();

        var configuration = player.GameContext.Configuration;
        foreach (var skill in new[] { knightSkill, bladeKnightSkill, wizardSkill, weaponSkill, masterSkill })
        {
            configuration.Skills.Add(skill);
            configuration.Items.Add(new TestItemDefinition { Group = 15, Number = skill.Number, Skill = skill });
        }

        configuration.Items.Single(item => item.Skill == weaponSkill).ItemSlot = new ItemSlotType();
        return (player, (knightSkill, bladeKnightSkill));
    }

    private static Skill CreateSkill(short number, params CharacterClass[] qualifiedCharacters)
    {
        var skill = new TestSkill { Number = number, Name = $"Skill {number}" };
        foreach (var characterClass in qualifiedCharacters)
        {
            skill.QualifiedCharacters.Add(characterClass);
        }

        return skill;
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

    private sealed class TestItemDefinition : ItemDefinition
    {
        public TestItemDefinition()
        {
            this.Requirements = new List<AttributeRequirement>();
            this.QualifiedCharacters = new List<CharacterClass>();
        }
    }
}
