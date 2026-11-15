// <copyright file="SpectreGrowth.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Brings the character of a spectre or a rival to a tier of <see cref="SpectreEvolution"/>: its class, its learned skills,
/// its combo ability and the pets it got. It's done on the stored character before it enters the game, because the
/// learned skills of a character in the game can't be removed.
/// </summary>
internal static class SpectreGrowth
{
    /// <summary>
    /// Brings a character to a tier.
    /// </summary>
    /// <param name="context">The persistence context of the character.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="character">The character.</param>
    /// <param name="kind">The kind of spectre.</param>
    /// <param name="highestTier">The highest tier which the character reached, or -1 if none.</param>
    /// <returns>The pets which couldn't be given, because their slot is used by another item.</returns>
    public static async ValueTask<SpectreGrantedPet> ApplyAsync(IContext context, GameConfiguration configuration, Character character, SpectreKind kind, int highestTier)
    {
        SetClass(configuration, character, kind, highestTier);
        await SetSkillsAsync(context, configuration, character, kind, highestTier).ConfigureAwait(false);
        SetCombo(context, configuration, character, SpectreEvolution.HasCombo(kind, highestTier));
        return GivePets(context, configuration, character, kind, highestTier);
    }

    /// <summary>
    /// Determines whether an item of a spectre is a pet which it got from an evolution, so it can't be taken off.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if the item is a granted pet.</returns>
    public static bool IsGrantedPet(SpectrePlayer spectre, Item item)
    {
        if (spectre.SelectedCharacter is not { } character || item.Definition is not { Group: SpectreEvolution.PetGroup } definition)
        {
            return false;
        }

        var granted = (SpectreGrantedPet)(int)SpectreProgress.GetValue(character, SpectreProgress.GrantedPetsAttribute);
        return Enum.GetValues<SpectreGrantedPet>()
            .Any(pet => pet != SpectreGrantedPet.None
                        && granted.HasFlag(pet)
                        && SpectreEvolution.GetPetItemNumber(pet) == definition.Number
                        && SpectreEvolution.GetPetSlot(pet) == item.ItemSlot);
    }

    /// <summary>
    /// Creates an item for the equipment of a character.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="definition">The item definition.</param>
    /// <param name="level">The item level.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The item.</returns>
    public static Item CreateItem(IContext context, ItemDefinition definition, byte level, byte slot)
    {
        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = Math.Min(level, definition.MaximumItemLevel);
        item.Durability = Math.Max((byte)1, definition.Durability);
        item.ItemSlot = slot;
        return item;
    }

    private static void SetClass(GameConfiguration configuration, Character character, SpectreKind kind, int highestTier)
    {
        var baseClass = configuration.CharacterClasses.FirstOrDefault(c => c.Number == SpectreClasses.GetClassNumber(kind));
        if (baseClass is null)
        {
            return;
        }

        var targetClass = SpectreEvolution.HasSecondClass(kind, highestTier) && baseClass.NextGenerationClass is { } secondClass
            ? secondClass
            : baseClass;
        if (character.CharacterClass != targetClass && character.CharacterClass?.IsMasterClass != true)
        {
            character.CharacterClass = targetClass;
        }
    }

    private static async ValueTask SetSkillsAsync(IContext context, GameConfiguration configuration, Character character, SpectreKind kind, int highestTier)
    {
        var wanted = SpectreEvolution.GetSkillsUpTo(kind, highestTier).ToHashSet();
        foreach (var entry in character.LearnedSkills.ToList())
        {
            if (entry.Skill is { } skill && skill.MasterDefinition is null && !wanted.Contains(skill.Number))
            {
                character.LearnedSkills.Remove(entry);
                await context.DeleteAsync(entry).ConfigureAwait(false);
            }
        }

        foreach (var number in wanted)
        {
            if (character.LearnedSkills.Any(entry => entry.Skill?.Number == number)
                || configuration.Skills.FirstOrDefault(s => s.Number == number) is not { } skill)
            {
                continue;
            }

            var skillEntry = context.CreateNew<SkillEntry>();
            skillEntry.Skill = skill;
            skillEntry.Level = 0;
            character.LearnedSkills.Add(skillEntry);
        }
    }

    private static void SetCombo(IContext context, GameConfiguration configuration, Character character, bool hasCombo)
    {
        var existing = character.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.IsSkillComboAvailable.Id);
        if (existing is not null)
        {
            existing.Value = hasCombo ? 1 : 0;
        }
        else if (hasCombo && configuration.Attributes.FirstOrDefault(a => a.Id == Stats.IsSkillComboAvailable.Id) is { } definition)
        {
            character.Attributes.Add(context.CreateNew<StatAttribute>(definition, 1));
        }
    }

    private static SpectreGrantedPet GivePets(IContext context, GameConfiguration configuration, Character character, SpectreKind kind, int highestTier)
    {
        var granted = (SpectreGrantedPet)(int)SpectreProgress.GetValue(character, SpectreProgress.GrantedPetsAttribute);
        var blocked = SpectreGrantedPet.None;
        if (character.Inventory is not { } inventory)
        {
            return blocked;
        }

        foreach (var pet in Enum.GetValues<SpectreGrantedPet>())
        {
            if (pet == SpectreGrantedPet.None
                || !SpectreEvolution.GetPets(kind, highestTier).HasFlag(pet)
                || configuration.Items.FirstOrDefault(i => i.Group == SpectreEvolution.PetGroup && i.Number == SpectreEvolution.GetPetItemNumber(pet)) is not { } definition)
            {
                continue;
            }

            var slot = SpectreEvolution.GetPetSlot(pet);
            var current = inventory.Items.FirstOrDefault(i => i.ItemSlot == slot);
            if (current?.Definition == definition && granted.HasFlag(pet))
            {
                continue;
            }

            if (current is not null)
            {
                blocked |= pet;
                continue;
            }

            inventory.Items.Add(CreateItem(context, definition, 1, slot));
            granted |= pet;
        }

        SpectreProgress.SetValue(configuration, context, character, SpectreProgress.GrantedPetsAttribute, (int)granted);
        return blocked;
    }
}
