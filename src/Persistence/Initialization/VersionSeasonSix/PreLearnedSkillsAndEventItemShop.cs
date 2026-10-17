// <copyright file="PreLearnedSkillsAndEventItemShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;

/// <summary>
/// Changes the data for a game in which the characters know their skills from the start, and buy the items
/// to enter the events at Lumen the Barmaid in Lorencia instead of finding them.
/// </summary>
/// <remarks>
/// This is a custom feature, in the original game the skill items (scrolls, orbs, parchments, ...) and the items to enter
/// the events drop from monsters. Here:
/// <list type="bullet">
///   <item>The requirements of a skill item (e.g. a level or an agility) become requirements of its skill,
///         so that a character which knows the skill from the start can only perform it when it meets them.
///         The skills are added to new characters by <see cref="AddLearnableSkillsForNewCharacters"/>.</item>
///   <item>The skill items don't drop from monsters anymore. They still exist, and are sold by the merchants as before.</item>
///   <item>The items to enter Blood Castle, Devil Square, Illusion Temple and Kalima don't drop from monsters anymore.
///         Lumen the Barmaid sells all of them.</item>
/// </list>
/// </remarks>
internal static class PreLearnedSkillsAndEventItemShop
{
    /// <summary>
    /// The number of Lumen the Barmaid, the merchant in the bar of Lorencia.
    /// </summary>
    internal const short LumenTheBarmaidNumber = 255;

    /// <summary>
    /// Gets the items to enter the events which don't drop from monsters anymore. Lumen sells all of them.
    /// </summary>
    internal static IReadOnlyList<(byte Group, short Number)> EventItemsWithoutDrop { get; } =
    [
        (13, 16), // Scroll of Archangel (Blood Castle)
        (13, 17), // Blood Bone (Blood Castle)
        (14, 17), // Devil's Eye (Devil Square)
        (14, 18), // Devil's Key (Devil Square)
        (13, 49), // Old Scroll (Illusion Temple)
        (13, 50), // Illusion Sorcerer Covenant (Illusion Temple)
        (14, 29), // Symbol of Kundun (Kalima)
    ];

    /// <summary>
    /// Gets the items which are added to the store of Lumen, which already sells the items of the other levels.
    /// The slots are free in her original store.
    /// </summary>
    internal static IReadOnlyList<(byte Slot, byte Group, short Number, byte Level)> AddedLumenItems { get; } =
    [
        (63, 13, 49, 6), // Old Scroll +6 (Illusion Temple 6)
        (67, 13, 50, 6), // Illusion Sorcerer Covenant +6 (Illusion Temple 6), 1x2
        (80, 14, 28, 1), // Lost Map +1 (Kalima 1)
        (81, 14, 28, 2), // Lost Map +2 (Kalima 2)
        (82, 14, 28, 3), // Lost Map +3 (Kalima 3)
        (83, 14, 28, 4), // Lost Map +4 (Kalima 4)
        (84, 14, 28, 5), // Lost Map +5 (Kalima 5)
        (85, 14, 28, 6), // Lost Map +6 (Kalima 6)
        (86, 14, 28, 7), // Lost Map +7 (Kalima 7)
    ];

    /// <summary>
    /// Gets the item requirements which are calculated from the item level and options, and therefore can't become
    /// requirements of a skill. The skills already have their own energy requirement.
    /// </summary>
    private static IReadOnlyList<AttributeDefinition> ItemBasedRequirements { get; } =
    [
        Stats.TotalStrengthRequirementValue,
        Stats.TotalAgilityRequirementValue,
        Stats.TotalEnergyRequirementValue,
        Stats.TotalVitalityRequirementValue,
        Stats.TotalLeadershipRequirementValue,
    ];

    /// <summary>
    /// Applies the changes to the configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var skillItems = gameConfiguration.Items.Where(AddLearnableSkillsForNewCharacters.IsSkillLearningItem).ToList();
        foreach (var skillItem in skillItems)
        {
            AddItemRequirementsToSkill(context, skillItem);
            skillItem.DropsFromMonsters = false;
        }

        RemoveDropsOfEventItems(gameConfiguration);
        AddEventItemsToLumen(context, gameConfiguration);
    }

    /// <summary>
    /// Determines whether the drop item group only drops items to enter the events, which don't drop anymore.
    /// </summary>
    /// <param name="group">The drop item group.</param>
    /// <returns><c>true</c>, if the group only drops items to enter the events.</returns>
    internal static bool IsEventItemDropGroup(DropItemGroup group)
    {
        return group.PossibleItems.Count > 0
               && group.PossibleItems.All(item => EventItemsWithoutDrop.Contains((item.Group, item.Number)));
    }

    private static void AddItemRequirementsToSkill(IContext context, ItemDefinition skillItem)
    {
        if (skillItem.Skill is not { } skill)
        {
            return;
        }

        foreach (var itemRequirement in skillItem.Requirements)
        {
            if (itemRequirement.Attribute is not { } attribute
                || ItemBasedRequirements.Any(a => a.Id == attribute.Id))
            {
                continue;
            }

            if (skill.Requirements.FirstOrDefault(r => r.Attribute?.Id == attribute.Id) is { } skillRequirement)
            {
                skillRequirement.MinimumValue = Math.Max(skillRequirement.MinimumValue, itemRequirement.MinimumValue);
                continue;
            }

            var requirement = context.CreateNew<AttributeRequirement>();
            requirement.Attribute = attribute;
            requirement.MinimumValue = itemRequirement.MinimumValue;
            skill.Requirements.Add(requirement);
        }
    }

    private static void RemoveDropsOfEventItems(GameConfiguration gameConfiguration)
    {
        foreach (var map in gameConfiguration.Maps)
        {
            foreach (var group in map.DropItemGroups.Where(IsEventItemDropGroup).ToList())
            {
                map.DropItemGroups.Remove(group);
            }
        }

        foreach (var monster in gameConfiguration.Monsters)
        {
            foreach (var group in monster.DropItemGroups.Where(IsEventItemDropGroup).ToList())
            {
                monster.DropItemGroups.Remove(group);
            }
        }
    }

    private static void AddEventItemsToLumen(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == LumenTheBarmaidNumber)?.MerchantStore is not { } store)
        {
            return;
        }

        foreach (var (slot, group, number, level) in AddedLumenItems)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } definition
                || store.Items.Any(i => i.ItemSlot == slot || (i.Definition == definition && i.Level == level)))
            {
                continue;
            }

            var item = context.CreateNew<Item>();
            item.Definition = definition;
            item.ItemSlot = slot;
            item.Durability = 1;
            item.Level = level;
            store.Items.Add(item);
        }
    }
}
