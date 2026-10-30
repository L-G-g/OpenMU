// <copyright file="SpectreEvolution.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The evolutions of the spectres: what a spectre learns when its owner beats the rival of its kind in the spectre hub,
/// and how the rival looks and fights at each evolution.
/// </summary>
/// <remarks>
/// The tier 0 is the rival which unlocks the spectre; the tiers 1 to <see cref="EvolutionCount"/> are the evolutions.
/// When the owner beats the rival of a tier, the spectre learns the skills of that tier, and the rival of the hub becomes
/// the one of the next tier. The rival of a tier already knows the skills of its tier.
/// The first tiers give single target skills, the middle ones area skills (and the pets of the lord), the last ones the buffs.
/// The armor sets and weapons of the rivals follow the zones of the map ladder (Lorencia, Devias, Lost Tower, Atlans, Aida,
/// Icarus, Kanturu Relics and Karutan), excellent from tier 4, with wings; the rival of tier 0 has no equipment.
/// </remarks>
public static class SpectreEvolution
{
    /// <summary>
    /// The number of evolutions, after the unlock.
    /// </summary>
    public const int EvolutionCount = 8;

    /// <summary>
    /// The item group of the pets.
    /// </summary>
    internal const byte PetGroup = 13;

    private const byte HelmGroup = 7;
    private const byte BootsGroup = 11;

    /// <summary>
    /// The skills which a spectre learns at each tier, by kind. Index 0 is the unlock. The buffs come in the last tiers.
    /// </summary>
    private static readonly short[][][] Skills =
    [

        // Warrior: Cyclone, Twisting Slash, Fire Breath (with the Dinorant), Rageful Blow, combo, Strike of Destruction, -, Swell Life.
        [[], [22], [41], [49], [42], [], [232], [], [48]],

        // Mage: Energy Ball, Lightning, Meteorite, Ice, Evil Spirit, Hellfire and Inferno, Ice Storm and Decay, Soul Barrier, Expansion of Wizardry.
        [[17], [3], [2], [7], [9], [10, 14], [39, 38], [16], [233]],

        // Elf: Ice Arrow, Triple Shot, Penetration, Multi-Shot, Heal, Greater Defense, Greater Damage, Infinity Arrow and Recovery.
        [[], [51], [24], [52], [235], [26], [27], [28], [77, 234]],

        // Gladiator: Cyclone, Lightning, Ice, Power Slash, Twisting Slash, Fire Slash, Flame Strike, Gigantic Storm.
        [[], [22], [3], [7], [56], [41], [55], [236], [237]],

        // Lord: Force Wave, Fire Burst, (Dark Raven), Fire Scream, Earthshake (with the Dark Horse), Electric Spike, Chaotic Diseier, Increase Critical Damage.
        [[], [66], [61], [], [78], [62], [65], [238], [64]],

        // Summoner: Fire Ball, Meteorite, Drain Life, Chain Lightning, Explosion, Requiem and Pollution, Lightning Shock, Damage Reflection, Berserker.
        [[4], [2], [214], [215], [223], [224, 225], [230], [217], [218]],

        // Fighter: Killing Blow, Beast Uppercut, Chain Drive, Dragon Roar, Dark Side, Phoenix Shot, Increase Health and Block, Ignore Defense.
        [[], [260], [261], [262], [264], [263], [270], [267, 268], [266]],
    ];

    /// <summary>
    /// The wings of the rivals (group and number), by kind and generation (first, second and third wings);
    /// a group of 0 means that the kind has no wings of that generation.
    /// </summary>
    private static readonly (byte Group, short Number)[][] Wings =
    [
        [(12, 2), (12, 5), (12, 36)], // Satan, Dragon, Storm
        [(12, 1), (12, 4), (12, 37)], // Heaven, Soul, Eternal
        [(12, 0), (12, 3), (12, 38)], // Elf, Spirits, Illusion
        [(12, 2), (12, 6), (12, 39)], // Satan, Darkness, Ruin
        [(0, 0), (13, 30), (12, 40)], // -, Cape of Lord, Cape of Emperor
        [(12, 41), (12, 42), (12, 43)], // Curse, Despair, Dimension
        [(0, 0), (12, 49), (12, 50)], // -, Cape of Fighter, Cape of Overrule
    ];

    /// <summary>
    /// The names of the evolutions, as they follow the name of the kind, for the male and the female kinds.
    /// </summary>
    private static readonly string[][] EvolutionNames =
    [
        ["Olvidado", "Abandonado", "Debilitado", "Convaleciente", "Recuperado", "Fuerte", "Poderoso", "Epico", "Legendario"],
        ["Olvidada", "Abandonada", "Debilitada", "Convaleciente", "Recuperada", "Fuerte", "Poderosa", "Epica", "Legendaria"],
    ];

    /// <summary>
    /// The tier at which a kind changes into its second class (e.g. the warrior into a Blade Knight), or 0 if it doesn't.
    /// </summary>
    private static readonly int[] ClassEvolutionTiers = [2, 4, 1, 0, 0, 8, 0];

    /// <summary>
    /// The armor sets of the rivals, as number of the pieces in the item groups of helms to boots, by kind and tier (1 to 8).
    /// </summary>
    private static readonly byte[][] ArmorSets =
    [
        [5, 0, 9, 1, 16, 17, 45, 29], // Leather, Bronze, Plate, Dragon, Black Dragon, Dark Phoenix, Titan, Dragon Knight
        [2, 4, 7, 3, 18, 22, 52, 30], // Pad, Bone, Sphinx, Legendary, Grand Soul, Dark Soul, Hades, Venom Mist
        [10, 11, 13, 14, 19, 24, 49, 31], // Vine, Silk, Spirit, Guardian, Divine, Red Spirit, Seraphim, Sylphid Ray
        [2, 0, 7, 8, 15, 37, 23, 32], // Pad, Bronze, Sphinx, Brass, Storm Crow, Valiant, Hurricane, Volcano
        [5, 0, 25, 26, 27, 38, 51, 33], // Leather, Bronze, Light Plate, Adamantine, Dark Steel, Glorious, Phaewang, Sunlight
        [39, 40, 41, 41, 42, 43, 44, 44], // Mistery, Red Wing, Ancient, Ancient, Black Rose, Aura, Lilium, Lilium
        [5, 6, 8, 9, 59, 60, 61, 73], // Leather, Scale, Brass, Plate, Sacred, Storm Hard, Piercing, Phoenix Soul
    ];

    /// <summary>
    /// The weapons of the rivals (group and number), by kind and tier (1 to 8).
    /// </summary>
    private static readonly (byte Group, short Number)[][] Weapons =
    [
        [(0, 3), (0, 6), (0, 11), (0, 14), (0, 16), (0, 17), (0, 27), (0, 22)], // Katache .. Bone Blade
        [(5, 1), (5, 2), (5, 4), (5, 5), (5, 6), (5, 8), (5, 13), (5, 12)], // Angelic Staff .. Grand Viper Staff
        [(4, 1), (4, 2), (4, 4), (4, 5), (4, 17), (4, 22), (4, 23), (4, 21)], // Bow .. Sylph Wind Bow
        [(0, 3), (0, 6), (0, 11), (0, 14), (0, 31), (0, 18), (0, 28), (0, 23)], // Katache .. Explosion Blade
        [(2, 0), (2, 2), (2, 8), (2, 9), (2, 10), (2, 11), (2, 15), (2, 14)], // Mace .. Soleil Scepter
        [(5, 0), (5, 14), (5, 15), (5, 16), (5, 17), (5, 18), (5, 19), (5, 20)], // Skull Staff .. Eternal Wing Stick
        [(0, 32), (0, 32), (0, 32), (0, 32), (0, 33), (0, 33), (0, 34), (0, 34)], // Sacred, Storm Hard and Piercing Blade Glove
    ];

    /// <summary>
    /// Gets the skills which a spectre learns at a tier.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier, 0 for the unlock.</param>
    /// <returns>The numbers of the skills.</returns>
    public static IReadOnlyList<short> GetSkillsOfTier(SpectreKind kind, int tier)
        => tier is >= 0 and <= EvolutionCount ? Skills[(int)kind][tier] : [];

    /// <summary>
    /// Gets the skills which a spectre knows up to (including) a tier.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="highestTier">The highest tier, or -1 if none.</param>
    /// <returns>The numbers of the skills.</returns>
    public static IEnumerable<short> GetSkillsUpTo(SpectreKind kind, int highestTier)
        => Enumerable.Range(0, Math.Min(highestTier, EvolutionCount) + 1).SelectMany(tier => GetSkillsOfTier(kind, tier));

    /// <summary>
    /// Gets all skills which spectres of a kind can learn.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The numbers of the skills.</returns>
    public static IEnumerable<short> GetAllSkills(SpectreKind kind) => GetSkillsUpTo(kind, EvolutionCount);

    /// <summary>
    /// Determines whether a spectre has its second class at a tier.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="highestTier">The highest tier of the spectre, or -1 if none.</param>
    /// <returns><c>true</c>, if it has the second class.</returns>
    public static bool HasSecondClass(SpectreKind kind, int highestTier)
        => ClassEvolutionTiers[(int)kind] > 0 && highestTier >= ClassEvolutionTiers[(int)kind];

    /// <summary>
    /// Determines whether the warrior can do combos at a tier.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="highestTier">The highest tier of the spectre, or -1 if none.</param>
    /// <returns><c>true</c>, if it can do combos.</returns>
    public static bool HasCombo(SpectreKind kind, int highestTier) => kind == SpectreKind.Warrior && highestTier >= 5;

    /// <summary>
    /// Gets the pets which a spectre has at a tier.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="highestTier">The highest tier of the spectre, or -1 if none.</param>
    /// <returns>The pets.</returns>
    public static SpectreGrantedPet GetPets(SpectreKind kind, int highestTier)
    {
        var pets = SpectreGrantedPet.None;
        if (kind == SpectreKind.Lord && highestTier >= 3)
        {
            pets |= SpectreGrantedPet.DarkRaven;
        }

        if (kind == SpectreKind.Lord && highestTier >= 5)
        {
            pets |= SpectreGrantedPet.DarkHorse;
        }

        if (kind == SpectreKind.Warrior && highestTier >= 3)
        {
            pets |= SpectreGrantedPet.Dinorant;
        }

        return pets;
    }

    /// <summary>
    /// Gets the item number of a pet in the item group <see cref="PetGroup"/>.
    /// </summary>
    /// <param name="pet">The pet.</param>
    /// <returns>The item number.</returns>
    public static short GetPetItemNumber(SpectreGrantedPet pet) => pet switch
    {
        SpectreGrantedPet.DarkRaven => 5,
        SpectreGrantedPet.DarkHorse => 4,
        _ => 3,
    };

    /// <summary>
    /// Gets the equipment slot of a pet.
    /// </summary>
    /// <param name="pet">The pet.</param>
    /// <returns>The slot.</returns>
    public static byte GetPetSlot(SpectreGrantedPet pet) => pet == SpectreGrantedPet.DarkRaven ? InventoryConstants.RightHandSlot : InventoryConstants.PetSlot;

    /// <summary>
    /// Gets the items of the rival at a tier: the pieces of its armor set and its weapon, as group and number.
    /// The pieces which don't exist (e.g. the helm of the gladiator) are left out by the caller.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier, 0 for the rival without equipment.</param>
    /// <returns>The items.</returns>
    public static IEnumerable<(byte Group, short Number)> GetRivalEquipment(SpectreKind kind, int tier)
    {
        if (tier < 1 || tier > EvolutionCount)
        {
            yield break;
        }

        var setNumber = ArmorSets[(int)kind][tier - 1];
        for (var group = HelmGroup; group <= BootsGroup; group++)
        {
            yield return (group, setNumber);
        }

        yield return Weapons[(int)kind][tier - 1];
    }

    /// <summary>
    /// Gets the wings of the rival at a tier: the first wings from tier 1, the second from tier 4 and the third from tier 7.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The group and number of the wings, or <c>null</c> if the rival has no wings at this tier.</returns>
    public static (byte Group, short Number)? GetRivalWings(SpectreKind kind, int tier)
    {
        if (tier < 1 || tier > EvolutionCount)
        {
            return null;
        }

        var wings = Wings[(int)kind][(tier - 1) / 3];
        return wings.Group == 0 ? null : wings;
    }

    /// <summary>
    /// Determines whether the armor set and the weapon of the rival at a tier are excellent.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <returns><c>true</c>, if they are excellent.</returns>
    public static bool IsRivalEquipmentExcellent(int tier) => tier >= 4;

    /// <summary>
    /// Gets the name of a spectre of a kind at a tier, e.g. "Invocadora Recuperada".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The name.</returns>
    public static string GetTitle(SpectreKind kind, int tier)
        => $"{SpectreHub.GetRivalName(kind)} {EvolutionNames[IsFemale(kind) ? 1 : 0][Math.Clamp(tier, 0, EvolutionCount)]}";

    /// <summary>
    /// Gets the name of a spectre of a kind at a tier with its article, e.g. "la Elfa Olvidada".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The name with its article.</returns>
    public static string GetTitleWithArticle(SpectreKind kind, int tier) => (IsFemale(kind) ? "la " : "el ") + GetTitle(kind, tier);

    /// <summary>
    /// Gets the item level of the equipment of the rival at a tier.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <returns>The item level.</returns>
    public static byte GetRivalItemLevel(int tier) => tier switch
    {
        <= 3 => (byte)Math.Max(tier, 0),
        <= 6 => 9,
        7 => 13,
        _ => 15,
    };

    private static bool IsFemale(SpectreKind kind) => kind is SpectreKind.Elf or SpectreKind.Summoner;
}
