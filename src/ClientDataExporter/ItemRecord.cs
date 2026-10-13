// <copyright file="ItemRecord.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ClientDataExporter;

/// <summary>
/// One item definition of the client's <c>item.bmd</c>.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number within the group.</param>
/// <param name="Name">The name.</param>
/// <param name="TwoHanded">If set to <c>true</c>, the item is a two-handed weapon.</param>
/// <param name="DropLevel">The level of the item, which decides from which monster level it drops.</param>
/// <param name="ItemSlot">The equipment slot, or 255 if it can't be equipped.</param>
/// <param name="SkillNumber">The number of the skill the item adds.</param>
/// <param name="Width">The width in inventory slots.</param>
/// <param name="Height">The height in inventory slots.</param>
/// <param name="MinimumDamage">The minimum damage.</param>
/// <param name="MaximumDamage">The maximum damage.</param>
/// <param name="DefenseRate">The defense rate (block rate) of a shield.</param>
/// <param name="Defense">The defense.</param>
/// <param name="MagicDefense">The magic defense.</param>
/// <param name="AttackSpeed">The attack speed.</param>
/// <param name="WalkSpeed">The walk speed.</param>
/// <param name="Durability">The durability.</param>
/// <param name="MagicDurability">The magic durability of a staff.</param>
/// <param name="MagicPower">The magic power (wizardry rise) of a staff or book.</param>
/// <param name="RequiredStrength">The required strength.</param>
/// <param name="RequiredAgility">The required agility.</param>
/// <param name="RequiredEnergy">The required energy.</param>
/// <param name="RequiredVitality">The required vitality.</param>
/// <param name="RequiredLeadership">The required leadership.</param>
/// <param name="RequiredLevel">The required character level.</param>
/// <param name="Value">The value which is used for the price calculation of some items.</param>
/// <param name="Zen">The fixed buy price in zen, if it has one.</param>
/// <param name="AttackType">The attack type.</param>
/// <param name="ClassRequirements">The class requirement per character class, in the order DW, DK, Elf, MG, DL, Summoner, RF. 0 means not wearable, otherwise the required class rank.</param>
/// <param name="Resistances">The resistances, in the order ice, poison, lightning, fire, earth, wind, water, and an unused one.</param>
public sealed record ItemRecord(
    int Group,
    int Number,
    string Name,
    bool TwoHanded,
    int DropLevel,
    int ItemSlot,
    int SkillNumber,
    int Width,
    int Height,
    int MinimumDamage,
    int MaximumDamage,
    int DefenseRate,
    int Defense,
    int MagicDefense,
    int AttackSpeed,
    int WalkSpeed,
    int Durability,
    int MagicDurability,
    int MagicPower,
    int RequiredStrength,
    int RequiredAgility,
    int RequiredEnergy,
    int RequiredVitality,
    int RequiredLeadership,
    int RequiredLevel,
    int Value,
    int Zen,
    int AttackType,
    byte[] ClassRequirements,
    byte[] Resistances);
