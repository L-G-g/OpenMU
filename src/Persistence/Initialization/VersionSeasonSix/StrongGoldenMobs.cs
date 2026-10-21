// <copyright file="StrongGoldenMobs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Makes the monsters of the golden invasion stronger, lets each of them always drop its Box of Kundun,
/// and lets the Box of Kundun+1 to +5 always give an item instead of money.
/// </summary>
/// <remarks>
/// The health, damage and defense come from the classic monster table (Monster.txt, KOR),
/// which has the golden monsters 2 to 10 times stronger than the original initialization.
/// The level, attack rate and defense rate stay as they are, because the defense rate of the classic
/// table (2000 to 4000) would let low characters hit them only rarely.
/// The Golden Soldier, Golden Vepar and Golden Wheel didn't drop a box at all, so they dropped the items and
/// money of the map instead, and the Golden Budge Dragon dropped a Box of Luck. Each of them gets a Box of Kundun
/// of the level which fits its monster level.
/// The Box of Kundun gave money when its excellent item group didn't hit (80 %), so the item groups get a chance
/// of 100 % and the money groups a chance of 0.
/// </remarks>
internal static class StrongGoldenMobs
{
    /// <summary>
    /// The first number of the ids of the added drop item groups of the boxes. The second number is the number of the monster.
    /// </summary>
    internal const short BoxDropItemGroupIdNumber = 4100;

    private const byte BoxGroup = 14;

    private const short BoxNumber = 11;

    /// <summary>
    /// The item level of the box item which is the Box of Kundun+1. The Box of Kundun+2 is one level above, and so on.
    /// </summary>
    private const byte BoxOfKundunFirstItemLevel = 8;

    /// <summary>
    /// The item level of the box item which is the Box of Kundun+5.
    /// </summary>
    private const byte BoxOfKundunLastItemLevel = 12;

    /// <summary>
    /// Gets the golden monsters with their new health, damage and defense, and the level of the Box of Kundun which they always drop.
    /// </summary>
    internal static IReadOnlyList<GoldenMob> Mobs { get; } =
    [
        new(43, 4400, 240, 250, 180, 1), // Golden Budge Dragon, Lorencia, dropped a Box of Luck before
        new(78, 6200, 260, 300, 200, 1), // Golden Goblin, Noria
        new(54, 7000, 280, 300, 400, 1), // Golden Soldier, Devias, dropped no box before
        new(53, 16000, 340, 360, 750, 2), // Golden Titan, Devias
        new(81, 12600, 380, 400, 1000, 2), // Golden Vepar, Atlans, dropped no box before
        new(83, 30000, 640, 720, 1500, 3), // Golden Wheel, Tarkan, dropped no box before
        new(79, 46000, 800, 1000, 2000, 3), // Golden Dragon, Lorencia, Noria or Devias
        new(80, 50000, 900, 1200, 2000, 4), // Golden Lizard King, Atlans
        new(82, 100000, 1200, 1500, 2500, 5), // Golden Tantallos, Tarkan
    ];

    /// <summary>
    /// Applies the new stats and the drops of the boxes to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var mob in Mobs)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == mob.Number) is not { } monster)
            {
                continue;
            }

            SetValue(context, gameConfiguration, monster, Stats.MaximumHealth, mob.Health);
            SetValue(context, gameConfiguration, monster, Stats.MinimumPhysBaseDmg, mob.MinimumDamage);
            SetValue(context, gameConfiguration, monster, Stats.MaximumPhysBaseDmg, mob.MaximumDamage);
            SetValue(context, gameConfiguration, monster, Stats.DefenseBase, mob.Defense);

            SetBoxOfKundun(context, gameConfiguration, monster, mob.BoxOfKundunLevel);
        }

        MakeBoxesAlwaysGiveItems(gameConfiguration);
    }

    private static void MakeBoxesAlwaysGiveItems(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Items.FirstOrDefault(IsBox) is not { } box)
        {
            return;
        }

        foreach (var group in box.DropItems.Where(g => g.SourceItemLevel is >= BoxOfKundunFirstItemLevel and <= BoxOfKundunLastItemLevel))
        {
            group.Chance = group.ItemType == SpecialItemType.Money ? 0 : 1;
        }
    }

    private static void SetBoxOfKundun(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, byte boxLevel)
    {
        var itemLevel = (byte)(BoxOfKundunFirstItemLevel + boxLevel - 1);
        var description = $"Box of Kundun +{boxLevel}";
        if (monster.DropItemGroups.FirstOrDefault(g => g.Chance >= 1 && g.PossibleItems.Any(IsBox)) is { } existing)
        {
            // The drop item groups of the boxes belong to one monster each, so they can be changed.
            existing.ItemLevel = itemLevel;
            existing.Description = description;
            return;
        }

        if (gameConfiguration.Items.FirstOrDefault(IsBox) is not { } box)
        {
            return;
        }

        var id = GuidHelper.CreateGuid<DropItemGroup>(BoxDropItemGroupIdNumber, monster.Number);
        if (gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id) is not { } group)
        {
            group = context.CreateNew<DropItemGroup>();
            group.SetGuid(id);
            group.Chance = 1;
            group.ItemLevel = itemLevel;
            group.Description = description;
            group.Monster = monster;
            group.PossibleItems.Add(box);
            gameConfiguration.DropItemGroups.Add(group);
        }

        monster.DropItemGroups.Add(group);
    }

    private static bool IsBox(ItemDefinition item) => item.Group == BoxGroup && item.Number == BoxNumber;

    private static void SetValue(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, AttributeDefinition stat, float value)
    {
        if (monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == stat.Id) is { } existing)
        {
            existing.Value = value;
            return;
        }

        var attribute = context.CreateNew<MonsterAttribute>();
        attribute.AttributeDefinition = stat.GetPersistent(gameConfiguration);
        attribute.Value = value;
        monster.Attributes.Add(attribute);
    }

    /// <summary>
    /// A golden monster with its new stats.
    /// </summary>
    /// <param name="Number">The number of the monster.</param>
    /// <param name="Health">The maximum health.</param>
    /// <param name="MinimumDamage">The minimum physical damage.</param>
    /// <param name="MaximumDamage">The maximum physical damage.</param>
    /// <param name="Defense">The defense.</param>
    /// <param name="BoxOfKundunLevel">The level of the Box of Kundun (1 to 5) which it always drops.</param>
    internal record GoldenMob(short Number, float Health, float MinimumDamage, float MaximumDamage, float Defense, byte BoxOfKundunLevel);
}
