// <copyright file="FixItemDataFromClientPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Corrects item definitions of season 6 which differ from the item table (<c>item_eng.bmd</c>)
/// of the original Season 6 Episode 3 client.
/// </summary>
/// <remarks>
/// The corrections:
/// <list type="bullet">
///   <item>Items which the client only lets characters of the second class (or higher) equip
///     were also qualified for the first class, e.g. the Dark Breaker for the Dark Knight.</item>
///   <item>The items of the third classes require level 380.</item>
///   <item>Item number 44 of the armor groups (Eternal Wing set of the client) had the values of another set.</item>
///   <item>Missing vitality requirement of the Phoenix Soul set and energy requirements of some gloves.</item>
///   <item>The Guardian Shield has the defense skill, like the other shields.</item>
/// </list>
/// Version 2 corrects the seed spheres:
/// <list type="bullet">
///   <item>The client only knows the spheres Mono, Di and Tri (12/70 to 12/72) and their seed spheres
///     (12/100 to 12/117). The spheres 4 and 5 (12/73, 12/74) and their seed spheres (12/118 to 12/129)
///     can no longer drop or be used to create seed spheres. They are kept, because characters may
///     already have them.</item>
///   <item>The level of a seed sphere is the level of its seed (the kind of option), so a seed sphere
///     allows the same item levels as its seed.</item>
///   <item>The drop levels of the seed spheres are the ones of the client (1 to 3).</item>
/// </list>
/// </remarks>
[PlugIn]
[Display(Name = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.FixItemDataFromClientPlugIn_Name), Description = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.FixItemDataFromClientPlugIn_Description), ResourceType = typeof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources))]
[Guid("C8587C91-81F0-43B0-ABA2-D01052A6205D")]
public class FixItemDataFromClientPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Item Data (Client Season 6)";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Corrects class restrictions, level and stat requirements, defense and durability of season 6 items, and the seed spheres, so that they match the item table of the original Season 6 Episode 3 client.";

    private const short DefenseSkillNumber = 18;

    private const byte SocketItemGroup = 12;
    private const short FirstSeedNumber = 60;
    private const short FirstSphereNumber = 70;
    private const short FirstSeedSphereNumber = 100;
    private const int SeedTypes = 6;
    private const int ClientSphereLevels = 3;
    private const int OldSphereLevels = 5;

    private static readonly (CharacterClassNumber FirstClass, (byte Group, short Number)[] Items)[] SecondClassItems =
    [
        (CharacterClassNumber.DarkWizard, [
            (5, 9), (5, 11), (5, 12), (5, 13), (5, 31), (5, 33), (6, 15), (6, 20), (7, 18), (7, 22), (7, 30),
            (8, 18), (8, 22), (8, 30), (9, 18), (9, 22), (9, 30), (10, 18), (10, 22), (10, 30), (11, 18), (11,
            22), (11, 30), (12, 4), (15, 14), (15, 16), (15, 17), (15, 18), (15, 28),
        ]),
        (CharacterClassNumber.DarkKnight, [
            (0, 17), (0, 20), (0, 22), (0, 24), (0, 26), (0, 27), (3, 10), (3, 11), (6, 17), (7, 17), (7, 21),
            (7, 29), (8, 17), (8, 21), (8, 29), (9, 17), (9, 21), (9, 29), (10, 17), (10, 21), (10, 29), (11,
            17), (11, 21), (11, 29), (12, 5), (12, 12), (12, 19), (12, 44),
        ]),
        (CharacterClassNumber.FairyElf, [
            (2, 7), (2, 16), (4, 17), (4, 19), (4, 20), (4, 21), (4, 22), (4, 23), (4, 24), (6, 16), (6, 19),
            (7, 19), (7, 24), (7, 31), (8, 19), (8, 24), (8, 31), (9, 19), (9, 24), (9, 31), (10, 19), (10,
            24), (10, 31), (11, 19), (11, 24), (11, 31), (12, 3), (12, 18), (12, 45), (12, 46),
        ]),
        (CharacterClassNumber.Summoner, [
            (5, 18), (5, 19), (5, 20), (5, 34), (7, 42), (7, 43), (7, 44), (8, 42), (8, 43), (8, 44), (9, 42),
            (9, 43), (9, 44), (10, 42), (10, 43), (10, 44), (11, 42), (11, 43), (11, 44), (12, 42), (15, 26),
            (15, 27),
        ]),
    ];

    private static readonly (byte Group, short Number, RequirementKind Kind, int Value)[] RequirementCorrections =
    [
        (2, 16, RequirementKind.Level, 380), // Frost Mace
        (2, 17, RequirementKind.Level, 380), // Absolute Scepter
        (4, 23, RequirementKind.Level, 380), // Stinger Bow
        (6, 17, RequirementKind.Level, 380), // Crimson Glory
        (6, 18, RequirementKind.Level, 380), // Salamander Shield
        (6, 19, RequirementKind.Level, 380), // Frost Barrier
        (6, 20, RequirementKind.Level, 380), // Guardian Shield
        (7, 45, RequirementKind.Level, 380), // Titan Helm
        (7, 46, RequirementKind.Level, 380), // Brave Helm
        (7, 49, RequirementKind.Level, 380), // Seraphim Helm
        (7, 50, RequirementKind.Level, 380), // Faith Helm
        (7, 51, RequirementKind.Level, 380), // Paewang Mask
        (7, 52, RequirementKind.Level, 380), // Hades Helm
        (8, 45, RequirementKind.Level, 380), // Titan Armor
        (8, 46, RequirementKind.Level, 380), // Brave Armor
        (8, 47, RequirementKind.Level, 380), // Destory Armor
        (8, 48, RequirementKind.Level, 380), // Phantom Armor
        (8, 49, RequirementKind.Level, 380), // Seraphim Armor
        (8, 50, RequirementKind.Level, 380), // Faith Armor
        (8, 51, RequirementKind.Level, 380), // Paewang Armor
        (8, 52, RequirementKind.Level, 380), // Hades Armor
        (9, 45, RequirementKind.Level, 380), // Titan Pants
        (9, 46, RequirementKind.Level, 380), // Brave Pants
        (9, 47, RequirementKind.Level, 380), // Destory Pants
        (9, 48, RequirementKind.Level, 380), // Phantom Pants
        (9, 49, RequirementKind.Level, 380), // Seraphim Pants
        (9, 50, RequirementKind.Level, 380), // Faith Pants
        (9, 51, RequirementKind.Level, 380), // Paewang Pants
        (9, 52, RequirementKind.Level, 380), // Hades Pants
        (10, 45, RequirementKind.Level, 380), // Titan Gloves
        (10, 46, RequirementKind.Level, 380), // Brave Gloves
        (10, 47, RequirementKind.Level, 380), // Destroy Gloves
        (10, 48, RequirementKind.Level, 380), // Phantom Gloves
        (10, 49, RequirementKind.Level, 380), // Seraphim Gloves
        (10, 50, RequirementKind.Level, 380), // Faith Gloves
        (10, 51, RequirementKind.Level, 380), // Paewang Gloves
        (10, 52, RequirementKind.Level, 380), // Hades Gloves
        (11, 45, RequirementKind.Level, 380), // Titan Boots
        (11, 46, RequirementKind.Level, 380), // Brave Boots
        (11, 47, RequirementKind.Level, 380), // Destory Boots
        (11, 48, RequirementKind.Level, 380), // Phantom Boots
        (11, 49, RequirementKind.Level, 380), // Seraphim Boots
        (11, 50, RequirementKind.Level, 380), // Faith Boots
        (11, 51, RequirementKind.Level, 380), // Phaewang Boots
        (11, 52, RequirementKind.Level, 380), // Hades Boots
        (7, 44, RequirementKind.Level, 380), // Lilium Helm
        (8, 44, RequirementKind.Level, 380), // Lilium Armor
        (9, 44, RequirementKind.Level, 380), // Lilium Pants
        (10, 44, RequirementKind.Level, 380), // Lilium Gloves
        (11, 44, RequirementKind.Level, 380), // Lilium Boots
        (7, 44, RequirementKind.Strength, 62), // Lilium Helm
        (7, 44, RequirementKind.Agility, 20), // Lilium Helm
        (7, 44, RequirementKind.Energy, 170), // Lilium Helm
        (8, 44, RequirementKind.Strength, 62), // Lilium Armor
        (8, 44, RequirementKind.Agility, 20), // Lilium Armor
        (8, 44, RequirementKind.Energy, 170), // Lilium Armor
        (9, 44, RequirementKind.Strength, 62), // Lilium Pants
        (9, 44, RequirementKind.Agility, 20), // Lilium Pants
        (9, 44, RequirementKind.Energy, 170), // Lilium Pants
        (10, 44, RequirementKind.Strength, 62), // Lilium Gloves
        (10, 44, RequirementKind.Agility, 20), // Lilium Gloves
        (10, 44, RequirementKind.Energy, 170), // Lilium Gloves
        (11, 44, RequirementKind.Strength, 62), // Lilium Boots
        (11, 44, RequirementKind.Agility, 20), // Lilium Boots
        (11, 44, RequirementKind.Energy, 170), // Lilium Boots
        (7, 73, RequirementKind.Vitality, 85), // Phoenix Soul Helmet
        (8, 73, RequirementKind.Vitality, 85), // Phoenix Soul Armor
        (9, 73, RequirementKind.Vitality, 85), // Phoenix Soul Pants
        (11, 73, RequirementKind.Vitality, 85), // Phoenix Soul Boots
        (10, 48, RequirementKind.Energy, 165), // Phantom Gloves
        (10, 50, RequirementKind.Energy, 138), // Faith Gloves
        (10, 52, RequirementKind.Energy, 181), // Hades Gloves
    ];

    private static readonly (byte Group, byte DropLevel, float Defense, byte Durability)[] EternalWingSetCorrections =
    [
        (7, 116, 35, 86), // Lilium Helm
        (8, 131, 49, 86), // Lilium Armor
        (9, 122, 41, 86), // Lilium Pants
        (10, 102, 29, 86), // Lilium Gloves
        (11, 99, 32, 86), // Lilium Boots
    ];

    private enum RequirementKind
    {
        Level,
        Strength,
        Agility,
        Energy,
        Vitality,
    }

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override int Version => 2;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (firstClassNumber, items) in SecondClassItems)
        {
            if (gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == (byte)firstClassNumber) is not { } firstClass)
            {
                continue;
            }

            foreach (var (group, number) in items)
            {
                FindItem(gameConfiguration, group, number)?.QualifiedCharacters.Remove(firstClass);
            }
        }

        foreach (var (group, number, kind, value) in RequirementCorrections)
        {
            if (FindItem(gameConfiguration, group, number) is { } item)
            {
                SetRequirement(context, gameConfiguration, item, GetAttribute(kind), value);
            }
        }

        foreach (var (group, dropLevel, defense, durability) in EternalWingSetCorrections)
        {
            if (FindItem(gameConfiguration, group, 44) is not { } item)
            {
                continue;
            }

            item.DropLevel = dropLevel;
            item.Durability = durability;
            if (item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute == Stats.DefenseBase) is { } defensePowerUp)
            {
                defensePowerUp.BaseValue = defense;
            }

            if (item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute == Stats.AttackSpeedAny) is { } attackSpeedPowerUp)
            {
                item.BasePowerUpAttributes.Remove(attackSpeedPowerUp);
            }
        }

        if (FindItem(gameConfiguration, 6, 20) is { } guardianShield
            && gameConfiguration.Skills.FirstOrDefault(s => s.Number == DefenseSkillNumber) is { } defenseSkill)
        {
            guardianShield.Skill = defenseSkill;
        }

        FixSeedSpheres(gameConfiguration);

        return ValueTask.CompletedTask;
    }

    private static void FixSeedSpheres(GameConfiguration gameConfiguration)
    {
        for (var level = 0; level < OldSphereLevels; level++)
        {
            for (var type = 0; type < SeedTypes; type++)
            {
                if (FindItem(gameConfiguration, SocketItemGroup, (short)(FirstSeedSphereNumber + (level * SeedTypes) + type)) is not { } seedSphere)
                {
                    continue;
                }

                if (level >= ClientSphereLevels)
                {
                    seedSphere.DropsFromMonsters = false;
                    continue;
                }

                seedSphere.DropLevel = (byte)(level + 1);
                if (FindItem(gameConfiguration, SocketItemGroup, (short)(FirstSeedNumber + type)) is { } seed)
                {
                    seedSphere.MaximumItemLevel = seed.MaximumItemLevel;
                }
            }
        }

        // The spheres which the client doesn't know can't be used to create seed spheres anymore.
        var unknownSpheres = new List<ItemDefinition>();
        for (var level = ClientSphereLevels; level < OldSphereLevels; level++)
        {
            if (FindItem(gameConfiguration, SocketItemGroup, (short)(FirstSphereNumber + level)) is { } sphere)
            {
                sphere.DropsFromMonsters = false;
                unknownSpheres.Add(sphere);
            }
        }

        foreach (var requiredItem in gameConfiguration.Monsters
                     .SelectMany(m => m.ItemCraftings)
                     .Select(c => c.SimpleCraftingSettings)
                     .Where(s => s is not null)
                     .SelectMany(s => s!.RequiredItems))
        {
            foreach (var sphere in unknownSpheres)
            {
                requiredItem.PossibleItems.Remove(sphere);
            }
        }
    }

    private static ItemDefinition? FindItem(GameConfiguration gameConfiguration, byte group, short number)
    {
        return gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number);
    }

    private static AttributeDefinition GetAttribute(RequirementKind kind)
    {
        return kind switch
        {
            RequirementKind.Level => Stats.Level,
            RequirementKind.Strength => Stats.TotalStrengthRequirementValue,
            RequirementKind.Agility => Stats.TotalAgilityRequirementValue,
            RequirementKind.Energy => Stats.TotalEnergyRequirementValue,
            RequirementKind.Vitality => Stats.TotalVitalityRequirementValue,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static void SetRequirement(IContext context, GameConfiguration gameConfiguration, ItemDefinition item, AttributeDefinition attribute, int value)
    {
        var requirement = item.Requirements.FirstOrDefault(r => r.Attribute == attribute);
        if (requirement is null)
        {
            requirement = context.CreateNew<AttributeRequirement>();
            requirement.Attribute = attribute.GetPersistent(gameConfiguration);
            item.Requirements.Add(requirement);
        }

        requirement.MinimumValue = value;
    }
}
