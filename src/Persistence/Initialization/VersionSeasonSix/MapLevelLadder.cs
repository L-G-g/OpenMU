// <copyright file="MapLevelLadder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameServer.MessageHandler.Quests;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// Puts the hunting maps on a ladder of levels which don't overlap, and lets each zone of the ladder drop only
/// the best sets it can give.
/// </summary>
/// <remarks>
/// This is a custom feature. In the original game the monster levels of the maps overlap a lot (e.g. the monsters of Elbeland
/// go up to level 48), and a monster drops any item whose drop level is up to 12 levels below its own level,
/// so higher maps keep dropping the sets of the maps before. Here:
/// <list type="bullet">
///   <item>Each zone of <see cref="Zones"/> has its own range of monster levels, from Lorencia, Noria and Elbeland (1 to 19)
///         up to Raklion (140 to 148). The monsters of <see cref="Monsters"/> keep their order of strength, and their stats
///         are taken from the stats of the original monsters of the same rank.</item>
///   <item>A monster which also spawns outside the zone of its entry (e.g. in Devil Square) spawns there as a copy
///         with its original level and stats. The Skeleton Warrior spawns in Lorencia and Dungeon, so Dungeon gets a copy
///         with the stats of its own zone.</item>
///   <item>Each zone drops, for each character class and kind of item, only the best set it can give
///         (see <see cref="SpecialItemType.ZoneItem"/>). Swamp of Calmness and Raklion also drop the socket items.</item>
///   <item>The drops which depend on the monster level (e.g. the Symbol of Kundun) are moved to the new levels.</item>
///   <item>The warps and gates require the level of the place of their map on the ladder, and the warps are free.</item>
/// </list>
/// </remarks>
internal static class MapLevelLadder
{
    /// <summary>
    /// The offset which is added to the number of a monster to get the number from which the ids of its copy
    /// for another zone of the ladder are derived, e.g. the Skeleton Warrior of Dungeon.
    /// It's above <see cref="EliteQuestMonsters.IdNumberOffset"/>.
    /// </summary>
    internal const short LadderCopyIdNumberOffset = 2000;

    /// <summary>
    /// The offset which is added to the number of a monster to get the number from which the ids of its copy
    /// with the original level and stats are derived, which spawns outside of the ladder (e.g. in Devil Square).
    /// </summary>
    internal const short ClassicCopyIdNumberOffset = 3000;

    /// <summary>
    /// The number from which the ids of the drop item groups of the zones are derived.
    /// </summary>
    internal const short ZoneDropItemGroupIdNumber = 4000;

    /// <summary>
    /// The chance of the drop item group of the items of a zone. It's the chance of the common drop item group for random items,
    /// which it replaces.
    /// </summary>
    internal const double ZoneItemDropChance = 0.3;

    /// <summary>
    /// The chance of the drop item group of the excellent items of a zone. It's the chance of the common drop item group
    /// for excellent items, which it replaces.
    /// </summary>
    internal const double ZoneExcellentItemDropChance = 0.0001;

    /// <summary>
    /// The chance of the drop item group of the socket items of a zone.
    /// </summary>
    internal const double ZoneSocketItemDropChance = 0.01;

    /// <summary>
    /// The item level of the weapons, shields and armor pieces dropped by the monsters with the highest level of a zone.
    /// </summary>
    internal const byte MaximumZoneItemLevel = 3;

    private const short CommonRandomItemDropItemGroupNumber = 2;
    private const short CommonExcellentItemDropItemGroupNumber = 3;

    /// <summary>
    /// Gets the zones of the ladder: the range of the monster levels, the numbers of the maps,
    /// and if it drops socket items. The danger level of a zone is its index plus one.
    /// </summary>
    internal static IReadOnlyList<(byte MinimumLevel, byte MaximumLevel, byte[] MapNumbers, bool DropsSocketItems)> Zones { get; } =
    [
        (1, 19, [0, 3, 51], false), // Lorencia, Noria, Elbeland
        (20, 29, [2], false), // Devias
        (30, 39, [1], false), // Dungeon
        (40, 49, [4], false), // Lost Tower
        (50, 59, [7], false), // Atlans
        (60, 69, [8], false), // Tarkan
        (70, 79, [33], false), // Aida
        (80, 89, [10], false), // Icarus
        (90, 99, [37], false), // Kanturu Ruins
        (100, 109, [38], false), // Kanturu Relics
        (110, 119, [80, 81], false), // Karutan 1 and 2
        (120, 129, [63], false), // Vulcanus
        (130, 139, [56], true), // Swamp of Calmness
        (140, 148, [57], true), // Raklion
    ];

    /// <summary>
    /// Gets the monsters of the ladder: the zone (index plus one), the number of the monster, and its new level and stats.
    /// The first entry of a monster changes the monster itself, a second one in another zone changes a copy for this zone.
    /// </summary>
    internal static IReadOnlyList<LadderMonster> Monsters { get; } =
    [
        // Lorencia
        new(1, 3, 1, 30, 4, 7, 1, 8, 1), // Spider, level 2 before
        new(1, 2, 4, 60, 10, 13, 3, 10, 3), // Budge Dragon, level 4 before
        new(1, 0, 6, 119, 19, 23, 8, 10, 8), // Bull Fighter, level 6 before
        new(1, 1, 9, 188, 31, 36, 12, 13, 12), // Hound, level 9 before
        new(1, 4, 11, 253, 41, 46, 14, 28, 14), // Elite Bull Fighter, level 12 before
        new(1, 6, 14, 393, 56, 61, 18, 43, 18), // Lich, level 14 before
        new(1, 7, 16, 589, 74, 79, 25, 67, 25), // Giant, level 17 before
        new(1, 14, 19, 716, 85, 90, 30, 85, 30), // Skeleton Warrior, level 19 before

        // Noria
        new(1, 26, 1, 45, 7, 10, 2, 10, 2), // Goblin, level 3 before
        new(1, 27, 4, 79, 13, 17, 4, 10, 4), // Chain Scorpion, level 5 before
        new(1, 33, 6, 139, 22, 27, 9, 10, 9), // Elite Goblin, level 8 before
        new(1, 28, 9, 218, 36, 41, 13, 18, 13), // Beetle Monster, level 10 before
        new(1, 29, 11, 291, 45, 50, 15, 32, 15), // Hunter, level 13 before
        new(1, 30, 14, 457, 61, 67, 20, 49, 20), // Forest Monster, level 15 before
        new(1, 31, 16, 524, 68, 74, 22, 61, 22), // Agon, level 16 before
        new(1, 32, 19, 692, 84, 89, 29, 79, 29), // Stone Golem, level 18 before

        // Elvenland
        new(1, 418, 1, 60, 10, 13, 3, 10, 3), // Strange Rabbit, level 4 before
        new(1, 421, 4, 99, 16, 20, 6, 10, 6), // Polluted Butterfly, level 13 before
        new(1, 419, 6, 163, 26, 31, 10, 10, 10), // Hideous Rabbit, level 19 before
        new(1, 420, 9, 229, 37, 42, 13, 23, 13), // Werewolf421, level 24 before
        new(1, 422, 11, 335, 50, 56, 16, 38, 16), // Cursed Lich, level 30 before
        new(1, 423, 14, 513, 67, 71, 22, 55, 22), // Totem Golem, level 36 before
        new(1, 424, 16, 642, 79, 84, 27, 73, 27), // Grizzly, level 43 before
        new(1, 425, 19, 744, 89, 94, 31, 92, 31), // Captain Grizzly, level 48 before

        // Devias
        new(2, 24, 20, 790, 94, 99, 33, 99, 33), // Worm, level 20 before
        new(2, 22, 22, 840, 99, 104, 33, 108, 33), // Ice Monster, level 22 before
        new(2, 23, 23, 890, 104, 109, 35, 118, 35), // Hommerd, level 24 before
        new(2, 21, 25, 900, 105, 110, 37, 124, 37), // Assassin, level 26 before
        new(2, 19, 26, 978, 109, 114, 39, 129, 39), // Yeti, level 30 before
        new(2, 20, 28, 1077, 114, 119, 44, 138, 41), // Elite Yeti, level 36 before
        new(2, 25, 29, 3590, 147, 157, 79, 199, 72), // Ice Queen (boss), level 52 before

        // Dungeon
        new(3, 14, 30, 1176, 119, 124, 49, 148, 43), // Skeleton Warrior, level 19 before
        new(3, 12, 31, 1200, 120, 125, 50, 158, 45), // Larva, level 25 before
        new(3, 11, 32, 1547, 129, 134, 59, 177, 48), // Ghost, level 32 before
        new(3, 17, 32, 1349, 124, 129, 54, 167, 46), // Cyclops, level 28 before
        new(3, 15, 33, 1746, 130, 139, 64, 187, 50), // Skeleton Archer, level 34 before
        new(3, 5, 34, 1944, 130, 140, 65, 197, 51), // Hell Hound, level 38 before
        new(3, 13, 35, 2285, 134, 144, 65, 207, 54), // Hell Spider, level 40 before
        new(3, 9, 36, 2470, 138, 145, 70, 218, 60), // Thunder Lich, level 44 before
        new(3, 16, 36, 2400, 135, 145, 69, 214, 57), // Elite Skeleton, level 42 before
        new(3, 8, 37, 2706, 140, 148, 73, 223, 63), // Poison Bull, level 46 before
        new(3, 10, 38, 2800, 143, 150, 75, 228, 65), // Dark Knight, level 48 before
        new(3, 18, 39, 5600, 157, 169, 94, 261, 76), // Gorgon (boss), level 55 before

        // LostTower
        new(4, 36, 40, 2934, 147, 152, 77, 230, 66), // Shadow, level 47 before
        new(4, 39, 41, 3000, 149, 154, 79, 233, 69), // Poison Shadow, level 50 before
        new(4, 34, 43, 3131, 150, 155, 80, 238, 70), // Cursed Wizard, level 54 before
        new(4, 41, 44, 3394, 153, 158, 83, 246, 72), // Death Cow, level 57 before
        new(4, 37, 45, 3820, 158, 166, 91, 263, 77), // Devil, level 60 before
        new(4, 40, 47, 4316, 166, 176, 104, 279, 83), // Death Knight, level 62 before
        new(4, 35, 48, 4812, 176, 189, 113, 294, 87), // Death Gorgon, level 64 before
        new(4, 38, 49, 7218, 194, 216, 139, 323, 96), // Balrog (boss), level 66 before

        // Atlans
        new(5, 45, 50, 5308, 186, 198, 118, 306, 90), // Bahamut, level 43 before
        new(5, 46, 52, 5804, 196, 206, 126, 316, 93), // Vepar, level 45 before
        new(5, 47, 53, 6600, 206, 222, 142, 326, 96), // Valkyrie, level 46 before
        new(5, 51, 55, 7592, 222, 248, 162, 336, 105), // Great Bahamut, level 66 before
        new(5, 52, 56, 8584, 236, 266, 176, 346, 113), // Silver Valkyrie, level 68 before
        new(5, 48, 58, 9576, 245, 271, 184, 357, 118), // Lizard King, level 70 before
        new(5, 49, 59, 20216, 270, 311, 204, 439, 130), // Hydra (boss), level 74 before

        // Tarkan
        new(6, 62, 60, 10000, 249, 277, 189, 364, 120), // Mutant, level 72 before
        new(6, 60, 62, 10560, 253, 281, 193, 376, 123), // Bloody Wolf, level 76 before
        new(6, 57, 64, 11276, 257, 286, 196, 388, 126), // Iron Wheel, level 80 before
        new(6, 58, 65, 12588, 259, 292, 199, 397, 129), // Tantallos, level 83 before
        new(6, 61, 67, 13768, 260, 297, 200, 406, 135), // Beam Knight, level 84 before
        new(6, 59, 68, 20652, 354, 412, 291, 447, 148), // Zaikan (boss), level 90 before
        new(6, 63, 69, 22029, 409, 454, 305, 447, 156), // Death Beam Knight (boss), level 93 before

        // Aida
        new(7, 308, 70, 14528, 265, 311, 205, 421, 142), // Death Tree, level 72 before
        new(7, 307, 71, 15520, 275, 321, 213, 438, 147), // Forest Orc, level 74 before
        new(7, 306, 72, 16512, 284, 326, 218, 453, 155), // Death Rider, level 78 before
        new(7, 305, 73, 17504, 294, 335, 221, 468, 162), // Blue Golem, level 84 before
        new(7, 304, 74, 18496, 302, 342, 226, 482, 165), // Witch Queen, level 94 before
        new(7, 549, 75, 19488, 307, 347, 230, 495, 169), // Bloody Orc, level 114 before
        new(7, 550, 76, 20960, 315, 355, 240, 503, 170), // Bloody Death Rider, level 115 before
        new(7, 551, 77, 22000, 327, 362, 250, 518, 172), // Bloody Golem, level 117 before
        new(7, 552, 78, 22464, 335, 367, 259, 535, 176), // Bloody Witch Queen, level 120 before
        new(7, 309, 79, 33696, 369, 404, 285, 588, 194), // Hell Maine (boss), level 98 before

        // Icarus
        new(8, 69, 80, 23912, 335, 377, 272, 554, 183), // Alquamos, level 75 before
        new(8, 71, 81, 25000, 348, 389, 276, 578, 192), // Mega Crust, level 78 before
        new(8, 70, 83, 25880, 369, 408, 278, 602, 197), // Queen Rainer, level 82 before
        new(8, 73, 84, 27864, 389, 432, 281, 627, 204), // Drakan, level 86 before
        new(8, 74, 85, 30272, 408, 442, 292, 640, 214), // Alpha Crust, level 92 before
        new(8, 72, 87, 33040, 415, 458, 309, 664, 220), // Phantom Knight, level 96 before
        new(8, 75, 88, 35520, 443, 490, 332, 694, 222), // Great Drakan, level 100 before
        new(8, 77, 89, 67488, 647, 700, 402, 781, 255), // Dark Phoenix (boss), level 108 before

        // KanturuRuins
        new(9, 351, 90, 37000, 472, 505, 358, 706, 227), // Splinter Wolf, level 80 before
        new(9, 352, 91, 37588, 479, 507, 364, 720, 230), // Iron Rider, level 82 before
        new(9, 353, 92, 38500, 487, 522, 370, 729, 232), // Satyros, level 85 before
        new(9, 354, 92, 38876, 501, 546, 372, 738, 237), // Blade Hunter, level 88 before
        new(9, 355, 93, 39684, 527, 562, 382, 750, 240), // Kentauros, level 93 before
        new(9, 356, 94, 40360, 542, 577, 404, 761, 243), // Gigantis, level 98 before
        new(9, 350, 95, 41352, 547, 584, 422, 787, 249), // Berserker, level 100 before
        new(9, 357, 96, 42344, 552, 590, 427, 803, 252), // Genocider, level 105 before
        new(9, 553, 97, 43460, 555, 593, 430, 810, 255), // Berserker Warrior, level 123 before
        new(9, 554, 97, 45725, 557, 600, 434, 817, 260), // Kentauros Warrior, level 126 before
        new(9, 555, 98, 48500, 563, 603, 444, 835, 272), // Gigantis Warrior, level 128 before
        new(9, 556, 99, 48968, 570, 613, 448, 850, 280), // Genocider Warrior, level 129 before

        // KanturuRelics
        new(10, 359, 100, 50000, 576, 622, 457, 866, 290), // Twin Tale, level 117 before
        new(10, 358, 104, 50000, 591, 628, 463, 886, 291), // Persona, level 118 before
        new(10, 360, 109, 50576, 596, 638, 470, 900, 294), // Dreadfear, level 119 before

        // Karutan
        new(11, 569, 110, 52840, 600, 646, 471, 900, 296), // Venomous Chain Scorpion, level 99 before
        new(11, 570, 111, 55816, 605, 651, 476, 904, 301), // Bone Scorpion, level 103 before
        new(11, 571, 113, 58528, 621, 658, 483, 924, 304), // Orcus, level 105 before
        new(11, 572, 114, 60512, 631, 668, 491, 950, 308), // Gollock, level 108 before
        new(11, 573, 115, 62744, 636, 676, 496, 954, 317), // Crypta, level 111 before
        new(11, 574, 116, 65720, 642, 685, 504, 966, 326), // Crypos, level 114 before
        new(11, 575, 118, 68000, 655, 702, 516, 973, 332), // Condra, level 117 before
        new(11, 576, 119, 68000, 671, 713, 518, 987, 340), // Narcondra, level 120 before

        // Vulcanus
        new(12, 480, 120, 68432, 677, 727, 522, 1000, 345), // Zombie Fighter, level 90 before
        new(12, 482, 121, 70416, 689, 738, 531, 1003, 365), // Resurrected Gladiator, level 93 before
        new(12, 484, 122, 73000, 707, 751, 535, 1016, 381), // Ash Slaughterer, level 100 before
        new(12, 486, 122, 77192, 716, 758, 537, 1032, 410), // Blood Assassin, level 107 before
        new(12, 487, 123, 78920, 723, 774, 543, 1080, 426), // Cruel Blood Assassin, level 109 before
        new(12, 489, 124, 83176, 738, 794, 548, 1084, 430), // Burning Lava Giant, level 111 before
        new(12, 490, 125, 84286, 758, 818, 562, 1100, 432), // Ruthless Lava Giant, level 113 before
        new(12, 481, 126, 7662364, 1023, 1143, 618, 1309, 1042), // Zombie Fighter481 (boss), level 120 before
        new(12, 483, 127, 786159, 1035, 1155, 618, 1331, 1048), // Resurrected Gladiator483 (boss), level 121 before
        new(12, 485, 127, 796886, 1047, 1167, 618, 1353, 1055), // Ash Slaughterer485 (boss), level 122 before
        new(12, 488, 128, 801483, 1059, 1179, 618, 1375, 1061), // Cruel Blood Assassin488 (boss), level 123 before
        new(12, 491, 129, 812211, 1071, 1191, 618, 1397, 1067), // Ruthless Lava Giant491 (boss), level 124 before

        // SwampOfCalmness
        new(13, 441, 130, 85988, 801, 836, 571, 1114, 447), // Sapi Unus, level 95 before
        new(13, 442, 131, 87576, 831, 888, 577, 1190, 525), // Sapi Duo, level 96 before
        new(13, 444, 132, 88272, 919, 997, 611, 1207, 757), // Shadow Pawn, level 98 before
        new(13, 447, 132, 88000, 847, 991, 589, 1191, 751), // Thunder Napin, level 97 before
        new(13, 445, 133, 90512, 949, 1015, 615, 1252, 774), // Shadow Knight, level 100 before
        new(13, 443, 134, 94120, 979, 1097, 616, 1259, 800), // Sapi Tres, level 102 before
        new(13, 446, 135, 95112, 1060, 1185, 621, 1275, 802), // Shadow Look, level 104 before
        new(13, 448, 136, 98184, 1175, 1230, 631, 1370, 822), // Ghost Napin, level 106 before
        new(13, 449, 137, 118219, 1247, 1432, 641, 1545, 840), // Blaze Napin, level 107 before
        new(13, 557, 137, 134511, 1322, 1970, 651, 1558, 843), // Sapi Queen, level 131 before
        new(13, 558, 138, 185500, 1441, 2017, 661, 1570, 877), // Ice Napin, level 135 before
        new(13, 559, 139, 198824, 1451, 2020, 672, 1580, 903), // Shadow Master, level 137 before

        // Raklion
        new(14, 454, 140, 203960, 1585, 2060, 701, 1659, 907), // Ice Walker, level 102 before
        new(14, 455, 141, 218028, 1594, 2062, 716, 1852, 922), // Giant Mammoth, level 112 before
        new(14, 456, 142, 219052, 1741, 2092, 733, 1943, 960), // Ice Giant, level 122 before
        new(14, 457, 143, 230280, 1743, 2100, 783, 2006, 971), // Coolutin, level 132 before
        new(14, 562, 144, 237160, 1748, 2302, 786, 2145, 990), // Dark Mammoth, level 140 before
        new(14, 458, 145, 242144, 1886, 2318, 821, 2245, 1001), // Iron Knight, level 142 before
        new(14, 563, 146, 248096, 1919, 2361, 850, 2444, 1430), // Dark Giant, level 143 before
        new(14, 564, 147, 254088, 2044, 2394, 860, 2717, 1440), // Dark Coolutin, level 145 before
        new(14, 565, 148, 265000, 2217, 2601, 865, 11400, 1440), // Dark Iron Knight, level 148 before
    ];

    /// <summary>
    /// Gets the new level ranges of the drops of the Symbol of Kundun for each of its item levels.
    /// They are moved to the zones, so that each Kalima can be entered with the symbols of its zones.
    /// </summary>
    internal static IReadOnlyList<(byte MinimumLevel, byte MaximumLevel)> SymbolOfKundunDropLevels { get; } =
    [
        (20, 39), // +1
        (40, 59), // +2
        (60, 79), // +3
        (80, 89), // +4
        (90, 99), // +5
        (100, 119), // +6
        (120, 255), // +7
    ];

    /// <summary>
    /// Gets the new level requirements of the warps which were out of order of the ladder.
    /// </summary>
    internal static IReadOnlyDictionary<int, int> WarpLevelRequirements { get; } = new Dictionary<int, int>
    {
        { 28, 180 }, // Kanturu Ruins 1
        { 29, 180 }, // Kanturu Ruins 2
        { 45, 180 }, // Kanturu Ruins 3
        { 46, 240 }, // Karutan 1
        { 47, 250 }, // Karutan 2
        { 42, 260 }, // Vulcanus
        { 33, 270 }, // Swamp of Calmness
    };

    private static IReadOnlyList<AttributeDefinition> MonsterStats { get; } =
    [
        Stats.Level,
        Stats.MaximumHealth,
        Stats.MinimumPhysBaseDmg,
        Stats.MaximumPhysBaseDmg,
        Stats.DefenseBase,
        Stats.AttackRatePvm,
        Stats.DefenseRatePvm,
    ];

    /// <summary>
    /// Applies the ladder to the configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var entries in Monsters.GroupBy(m => m.Number))
        {
            ApplyMonster(context, gameConfiguration, entries.ToList());
        }

        UpdateEliteMonsters(gameConfiguration);
        AddZoneDrops(context, gameConfiguration);
        MoveLevelDependentDrops(gameConfiguration);
        UpdateWarps(gameConfiguration);
        UpdateGates(gameConfiguration);
    }

    /// <summary>
    /// Gets the monster whose id was derived from the number.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="idNumber">The number from which the id was derived.</param>
    /// <returns>The monster, if it exists.</returns>
    internal static MonsterDefinition? GetMonster(GameConfiguration gameConfiguration, short idNumber)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(idNumber);
        return gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id);
    }

    private static void ApplyMonster(IContext context, GameConfiguration gameConfiguration, IReadOnlyList<LadderMonster> entries)
    {
        var number = entries[0].Number;
        if (GetMonster(gameConfiguration, number) is not { } original)
        {
            return;
        }

        var mapsOfEntries = entries.SelectMany(e => Zones[e.Zone - 1].MapNumbers).ToHashSet();
        var spawnsOutside = gameConfiguration.Maps
            .Where(map => !mapsOfEntries.Contains((byte)map.Number))
            .SelectMany(map => map.MonsterSpawns)
            .Where(spawn => spawn.MonsterDefinition == original)
            .ToList();
        if (spawnsOutside.Count > 0)
        {
            // The copy takes the original stats, so it can only be created before the original is changed.
            var classicCopy = GetMonster(gameConfiguration, (short)(number + ClassicCopyIdNumberOffset));
            if (classicCopy is null && !HasStats(original, entries[0]))
            {
                classicCopy = CreateCopy(context, gameConfiguration, original, (short)(number + ClassicCopyIdNumberOffset));
            }

            if (classicCopy is not null)
            {
                spawnsOutside.ForEach(spawn => spawn.MonsterDefinition = classicCopy);
            }
        }

        SetStats(context, gameConfiguration, original, number, entries[0]);
        foreach (var entry in entries.Skip(1))
        {
            var idNumber = (short)(number + LadderCopyIdNumberOffset);
            var ladderCopy = GetMonster(gameConfiguration, idNumber) ?? CreateCopy(context, gameConfiguration, original, idNumber);
            SetStats(context, gameConfiguration, ladderCopy, idNumber, entry);
            foreach (var spawn in gameConfiguration.Maps
                         .Where(map => Zones[entry.Zone - 1].MapNumbers.Contains((byte)map.Number))
                         .SelectMany(map => map.MonsterSpawns)
                         .Where(spawn => spawn.MonsterDefinition == original))
            {
                spawn.MonsterDefinition = ladderCopy;
            }
        }
    }

    private static MonsterDefinition CreateCopy(IContext context, GameConfiguration gameConfiguration, MonsterDefinition original, short idNumber)
    {
        var copy = context.CreateNew<MonsterDefinition>();
        copy.SetGuid(idNumber);
        copy.Number = original.Number;
        copy.Designation = original.Designation;
        copy.MoveRange = original.MoveRange;
        copy.AttackRange = original.AttackRange;
        copy.ViewRange = original.ViewRange;
        copy.MoveDelay = original.MoveDelay;
        copy.AttackDelay = original.AttackDelay;
        copy.RespawnDelay = original.RespawnDelay;
        copy.Attribute = original.Attribute;
        copy.NumberOfMaximumItemDrops = original.NumberOfMaximumItemDrops;
        copy.NpcWindow = original.NpcWindow;
        copy.ObjectKind = original.ObjectKind;
        copy.IntelligenceTypeName = original.IntelligenceTypeName;
        copy.AttackSkill = original.AttackSkill;
        foreach (var dropItemGroup in original.DropItemGroups)
        {
            copy.DropItemGroups.Add(dropItemGroup);
        }

        foreach (var originalAttribute in original.Attributes)
        {
            if (originalAttribute.AttributeDefinition is not { } definition)
            {
                continue;
            }

            var attribute = context.CreateNew<MonsterAttribute>();
            attribute.AttributeDefinition = definition;
            attribute.Value = originalAttribute.Value;
            attribute.SetGuid(idNumber, definition.Id.ExtractFirstTwoBytes());
            copy.Attributes.Add(attribute);
        }

        gameConfiguration.Monsters.Add(copy);
        return copy;
    }

    private static float[] GetStatValues(LadderMonster entry)
    {
        return [entry.Level, entry.Health, entry.MinimumDamage, entry.MaximumDamage, entry.Defense, entry.AttackRate, entry.DefenseRate];
    }

    private static bool HasStats(MonsterDefinition monster, LadderMonster entry)
    {
        var values = GetStatValues(entry);
        return MonsterStats.Select((stat, i) => monster.Attributes.Any(a => a.AttributeDefinition?.Id == stat.Id && a.Value == values[i])).All(hasValue => hasValue);
    }

    private static void SetStats(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, short idNumber, LadderMonster entry)
    {
        var values = GetStatValues(entry);
        for (var i = 0; i < MonsterStats.Count; i++)
        {
            SetValue(context, gameConfiguration, monster, idNumber, MonsterStats[i], values[i]);
        }
    }

    private static void SetValue(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, short idNumber, AttributeDefinition stat, float value)
    {
        if (monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == stat.Id) is { } existing)
        {
            existing.Value = value;
            return;
        }

        var attribute = context.CreateNew<MonsterAttribute>();
        attribute.AttributeDefinition = stat.GetPersistent(gameConfiguration);
        attribute.Value = value;
        attribute.SetGuid(idNumber, stat.Id.ExtractFirstTwoBytes());
        monster.Attributes.Add(attribute);
    }

    /// <summary>
    /// Takes the new level and stats of the originals of the elite monsters of <see cref="EliteQuestMonsters"/>.
    /// </summary>
    private static void UpdateEliteMonsters(GameConfiguration gameConfiguration)
    {
        foreach (var (originalNumber, _, _, _) in EliteQuestMonsters.Elites)
        {
            if (GetMonster(gameConfiguration, originalNumber) is not { } original
                || GetMonster(gameConfiguration, (short)(originalNumber + EliteQuestMonsters.IdNumberOffset)) is not { } elite)
            {
                continue;
            }

            foreach (var attribute in elite.Attributes)
            {
                if (attribute.AttributeDefinition is not { } definition
                    || !MonsterStats.Any(s => s.Id == definition.Id)
                    || original.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == definition.Id) is not { } originalAttribute)
                {
                    continue;
                }

                attribute.Value = originalAttribute.Value * EliteQuestMonsters.GetMultiplier(definition);
            }
        }
    }

    /// <summary>
    /// Replaces the common drop item groups for random and excellent items on the maps of each zone by the ones of the zone.
    /// </summary>
    private static void AddZoneDrops(IContext context, GameConfiguration gameConfiguration)
    {
        var commonGroupIds = new[] { CommonRandomItemDropItemGroupNumber, CommonExcellentItemDropItemGroupNumber }
            .Select(GuidHelper.CreateGuid<DropItemGroup>)
            .ToList();
        for (var i = 0; i < Zones.Count; i++)
        {
            var zone = Zones[i];
            var maps = gameConfiguration.Maps.Where(map => zone.MapNumbers.Contains((byte)map.Number)).ToList();
            var mapNames = string.Join(", ", maps.Where(map => map.Discriminator == 0).Select(map => map.Name.ValueInNeutralLanguage));
            var groups = new List<DropItemGroup>
            {
                GetOrCreateZoneGroup(context, gameConfiguration, i, 0, SpecialItemType.ZoneItem, ZoneItemDropChance, $"Items of the zone {mapNames}"),
                GetOrCreateZoneGroup(context, gameConfiguration, i, 1, SpecialItemType.ZoneExcellentItem, ZoneExcellentItemDropChance, $"Excellent items of the zone {mapNames}"),
            };

            if (zone.DropsSocketItems)
            {
                var socketGroup = GetOrCreateZoneGroup(context, gameConfiguration, i, 2, SpecialItemType.ZoneItem, ZoneSocketItemDropChance, $"Socket items of the zone {mapNames}");
                if (socketGroup.PossibleItems.Count == 0)
                {
                    gameConfiguration.Items
                        .Where(item => item.MaximumSockets > 0 && item.DropsFromMonsters && item.DropLevel <= zone.MaximumLevel)
                        .ToList()
                        .ForEach(socketGroup.PossibleItems.Add);
                }

                groups.Add(socketGroup);
            }

            foreach (var map in maps)
            {
                foreach (var commonGroup in map.DropItemGroups.Where(g => commonGroupIds.Contains(g.GetId())).ToList())
                {
                    map.DropItemGroups.Remove(commonGroup);
                }

                foreach (var group in groups.Where(g => !map.DropItemGroups.Contains(g)))
                {
                    map.DropItemGroups.Add(group);
                }
            }
        }
    }

    private static DropItemGroup GetOrCreateZoneGroup(IContext context, GameConfiguration gameConfiguration, int zoneIndex, byte kind, SpecialItemType itemType, double chance, string description)
    {
        var id = GuidHelper.CreateGuid<DropItemGroup>(ZoneDropItemGroupIdNumber, (short)zoneIndex, kind);
        if (gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id) is { } existing)
        {
            return existing;
        }

        var zone = Zones[zoneIndex];
        var group = context.CreateNew<DropItemGroup>();
        group.SetGuid(id);
        group.Description = description;
        group.ItemType = itemType;
        group.Chance = chance;
        group.MinimumMonsterLevel = zone.MinimumLevel;
        group.MaximumMonsterLevel = zone.MaximumLevel;
        group.ItemLevel = itemType == SpecialItemType.ZoneItem ? MaximumZoneItemLevel : (byte)0;
        gameConfiguration.DropItemGroups.Add(group);
        return group;
    }

    /// <summary>
    /// Moves the drops which depend on the monster level to the new levels of the zones where they dropped before.
    /// </summary>
    private static void MoveLevelDependentDrops(GameConfiguration gameConfiguration)
    {
        for (byte level = 1; level <= SymbolOfKundunDropLevels.Count; level++)
        {
            if (GetDropItemGroup(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(14, 29, level)) is { } symbolOfKundun)
            {
                (symbolOfKundun.MinimumMonsterLevel, symbolOfKundun.MaximumMonsterLevel) = SymbolOfKundunDropLevels[level - 1];
            }
        }

        // Loch's Feather and Crest of Monarch drop in Icarus (80 to 89).
        SetMinimumLevel(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(Icarus.Number, 1), 74);
        SetMinimumLevel(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(Icarus.Number, 2), 74);

        // Dark Horse and Dark Raven spirits.
        var spiritNumber = (short)NumberConversionExtensions.MakeWord(13, 31);
        SetMinimumLevel(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(spiritNumber, 0, 1), 100);
        SetMinimumLevel(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(spiritNumber, 1, 1), 92);

        // The items of the quests of Sebina the Priestess which still drop by the monster level.
        // Groups without a level range drop only from specific monsters (see EliteQuestMonsters), they stay as they are.
        var questLevels = new Dictionary<short, (byte MinimumLevel, byte MaximumLevel)>
        {
            { 1, (52, 67) }, // Treasures of Mu
            { 2, (60, 118) }, // Hero Status, Scroll of Emperor +1
            { 3, (70, 118) }, // Secret of the Dark Stone, Broken Sword +1
        };
        var questGroups = gameConfiguration.Monsters
            .SelectMany(m => m.Quests)
            .Where(q => q.Group == QuestConstants.LegacyQuestGroup && questLevels.ContainsKey(q.Number))
            .SelectMany(q => q.RequiredItems.Select(r => (q.Number, r.DropItemGroup)));
        foreach (var (questNumber, dropItemGroup) in questGroups)
        {
            if (dropItemGroup is { MinimumMonsterLevel: not null })
            {
                (dropItemGroup.MinimumMonsterLevel, dropItemGroup.MaximumMonsterLevel) = questLevels[questNumber];
            }
        }
    }

    private static DropItemGroup? GetDropItemGroup(GameConfiguration gameConfiguration, Guid id)
    {
        return gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id);
    }

    private static void SetMinimumLevel(GameConfiguration gameConfiguration, Guid id, byte minimumLevel)
    {
        if (GetDropItemGroup(gameConfiguration, id) is { } group)
        {
            group.MinimumMonsterLevel = minimumLevel;
        }
    }

    private static void UpdateWarps(GameConfiguration gameConfiguration)
    {
        foreach (var warp in gameConfiguration.WarpList)
        {
            warp.Costs = 0;
            if (WarpLevelRequirements.TryGetValue(warp.Index, out var levelRequirement))
            {
                warp.LevelRequirement = levelRequirement;
            }
        }
    }

    /// <summary>
    /// Lets the gates into a map of the ladder require at least the level of the warps to this map,
    /// so that a character can't walk into a map before it can warp there.
    /// </summary>
    private static void UpdateGates(GameConfiguration gameConfiguration)
    {
        var ladderMapNumbers = Zones.SelectMany(z => z.MapNumbers).ToHashSet();
        var warpLevels = gameConfiguration.WarpList
            .Where(warp => warp.Gate?.Map is { } map && ladderMapNumbers.Contains((byte)map.Number))
            .GroupBy(warp => warp.Gate!.Map!)
            .ToDictionary(g => g.Key, g => g.Min(warp => warp.LevelRequirement));
        foreach (var map in gameConfiguration.Maps)
        {
            foreach (var gate in map.EnterGates)
            {
                if (gate.TargetGate?.Map is { } targetMap
                    && targetMap != map
                    && warpLevels.TryGetValue(targetMap, out var warpLevel)
                    && gate.LevelRequirement < warpLevel)
                {
                    gate.LevelRequirement = (short)warpLevel;
                }
            }
        }
    }

    /// <summary>
    /// A monster on the ladder.
    /// </summary>
    /// <param name="Zone">The zone, which is the index in <see cref="Zones"/> plus one.</param>
    /// <param name="Number">The number of the monster.</param>
    /// <param name="Level">The level.</param>
    /// <param name="Health">The maximum health.</param>
    /// <param name="MinimumDamage">The minimum physical base damage.</param>
    /// <param name="MaximumDamage">The maximum physical base damage.</param>
    /// <param name="Defense">The base defense.</param>
    /// <param name="AttackRate">The attack rate against monsters.</param>
    /// <param name="DefenseRate">The defense rate against monsters.</param>
    internal readonly record struct LadderMonster(byte Zone, short Number, byte Level, float Health, float MinimumDamage, float MaximumDamage, float Defense, float AttackRate, float DefenseRate);
}
