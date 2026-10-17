// <copyright file="AddLearnableSkillsForNewCharacters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds all skills which can be learned with an item (scrolls, orbs, parchments, ...) by the class of a created character
/// or by the classes it can reach with its class change quests.
/// </summary>
/// <remarks>
/// This is a custom feature, in the original game a character has to find or buy the item of each skill.
/// The skills of the classes the character didn't reach yet, and skills whose requirements (e.g. energy) aren't met,
/// are known but can't be performed, see <see cref="Player.IsQualifiedForSkill"/> and <see cref="Skill.Requirements"/>.
/// Master skills are not added, they are learned with master points.
/// </remarks>
[Guid("FCAC501E-5BA7-44B1-A089-BF926BC2BDF8")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddLearnableSkillsForNewCharacters_Name), Description = nameof(PlugInResources.AddLearnableSkillsForNewCharacters_Description), ResourceType = typeof(PlugInResources))]
public class AddLearnableSkillsForNewCharacters : ICharacterCreatedPlugIn, IDisabledByDefault
{
    /// <inheritdoc />
    public void CharacterCreated(Player player, Character createdCharacter)
    {
        if (createdCharacter.CharacterClass is not { } characterClass)
        {
            return;
        }

        var classes = GetClassAndNextGenerations(characterClass);
        foreach (var skill in GetLearnableSkills(player.GameContext.Configuration))
        {
            if (!skill.QualifiedCharacters.Any(classes.Contains)
                || createdCharacter.LearnedSkills.Any(entry => entry.Skill?.Number == skill.Number))
            {
                continue;
            }

            var skillEntry = player.PersistenceContext.CreateNew<SkillEntry>();
            skillEntry.Skill = skill;
            createdCharacter.LearnedSkills.Add(skillEntry);
        }
    }

    /// <summary>
    /// Gets the skills which can be learned by consuming an item.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The skills which can be learned by consuming an item.</returns>
    internal static IEnumerable<Skill> GetLearnableSkills(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Items
            .Where(IsSkillLearningItem)
            .SelectMany(item => GetSkillsOfItem(item, gameConfiguration))
            .Where(skill => skill.MasterDefinition is null)
            .Distinct();
    }

    /// <summary>
    /// Determines whether the item teaches a skill when it's consumed. Weapons and pets also have a skill,
    /// but it's only available while they're equipped.
    /// </summary>
    /// <param name="item">The item definition.</param>
    /// <returns><c>true</c>, if the item teaches a skill when it's consumed.</returns>
    internal static bool IsSkillLearningItem(ItemDefinition item)
    {
        return item.Skill is not null && item.ItemSlot is null;
    }

    private static IEnumerable<Skill> GetSkillsOfItem(ItemDefinition item, GameConfiguration gameConfiguration)
    {
        if (item.Skill is not { } skill)
        {
            yield break;
        }

        if (item.Group != ItemConstants.SummonOrb.Group || item.Number != ItemConstants.SummonOrb.Number)
        {
            yield return skill;
            yield break;
        }

        // The level of the orb of summoning decides which monster it summons, see SummoningOrbConsumeHandlerPlugIn.
        for (var level = 0; level <= item.MaximumItemLevel; level++)
        {
            var skillNumber = skill.Number + level;
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == skillNumber) is { } summonSkill)
            {
                yield return summonSkill;
            }
        }
    }

    private static HashSet<CharacterClass> GetClassAndNextGenerations(CharacterClass characterClass)
    {
        var classes = new HashSet<CharacterClass>();
        var current = characterClass;
        while (current is not null && classes.Add(current))
        {
            current = current.NextGenerationClass;
        }

        return classes;
    }
}
