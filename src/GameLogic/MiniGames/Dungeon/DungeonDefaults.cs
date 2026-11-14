// <copyright file="DungeonDefaults.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The defaults of the dungeons: an instance of Devias with the Ice Queen as boss, one of Tarkan with the Death Beam Knight
/// and one of Kanturu Ruins with the Genocider Warrior. Each has a normal and an extreme version.
/// The data initialization creates the NPCs, the bosses and the <see cref="MiniGameDefinition"/>s with these values.
/// </summary>
public static class DungeonDefaults
{
    /// <summary>
    /// The designation of the NPC at which the party leader enters the dungeon.
    /// </summary>
    public const string NpcDesignation = "Dungeon Guard";

    /// <summary>
    /// The designation of the first semi boss, a stronger Hommerd.
    /// </summary>
    public const string FirstSemiBossDesignation = "Hommerd Boss";

    /// <summary>
    /// The designation of the second semi boss, a stronger Elite Yeti.
    /// </summary>
    public const string SecondSemiBossDesignation = "Elite Yeti Boss";

    /// <summary>
    /// The designation of the final boss, a stronger Ice Queen.
    /// </summary>
    public const string BossDesignation = "Ice Queen Boss";

    /// <summary>
    /// The number of the Worm, the monster of the first stage.
    /// </summary>
    public const short WormNumber = 24;

    /// <summary>
    /// The number of the Ice Monster, the monster of the second stage.
    /// </summary>
    public const short IceMonsterNumber = 22;

    /// <summary>
    /// The number of the Assassin, the monster of the third stage.
    /// </summary>
    public const short AssassinNumber = 21;

    /// <summary>
    /// The game level of the <see cref="MiniGameDefinition"/> of the first dungeon.
    /// </summary>
    public const byte GameLevel = 1;

    /// <summary>
    /// The number of the dungeon of Devias.
    /// </summary>
    public const byte DeviasDungeonNumber = 1;

    /// <summary>
    /// The number of the dungeon of Tarkan.
    /// </summary>
    public const byte TarkanDungeonNumber = 2;

    /// <summary>
    /// The number of the dungeon of Kanturu Ruins.
    /// </summary>
    public const byte KanturuDungeonNumber = 3;

    /// <summary>
    /// What is added to the game level of a dungeon for the <see cref="MiniGameDefinition"/> of its extreme version.
    /// </summary>
    public const byte ExtremeGameLevelOffset = 10;

    /// <summary>
    /// The prefix of the designation of the NPC of the extreme version of a dungeon.
    /// </summary>
    public const string ExtremeNpcPrefix = "Extreme ";

    /// <summary>
    /// The designation of the NPC of the dungeon of Tarkan.
    /// </summary>
    public const string TarkanNpcDesignation = "Tarkan Dungeon Guard";

    /// <summary>
    /// The designation of the NPC of the dungeon of Kanturu Ruins.
    /// </summary>
    public const string KanturuNpcDesignation = "Kanturu Dungeon Guard";

    /// <summary>
    /// The suffix of the designation of a boss of a dungeon, which is a copy of the monster with the designation before it.
    /// </summary>
    public const string BossSuffix = " Boss";

    /// <summary>The number of the Iron Wheel of Tarkan.</summary>
    public const short IronWheelNumber = 57;

    /// <summary>The number of the Tantalos of Tarkan.</summary>
    public const short TantalosNumber = 58;

    /// <summary>The number of the Zaikan of Tarkan.</summary>
    public const short ZaikanNumber = 59;

    /// <summary>The number of the Bloody Wolf of Tarkan.</summary>
    public const short BloodyWolfNumber = 60;

    /// <summary>The number of the Beam Knight of Tarkan.</summary>
    public const short BeamKnightNumber = 61;

    /// <summary>The number of the Mutant of Tarkan.</summary>
    public const short MutantNumber = 62;

    /// <summary>The number of the Death Beam Knight of Tarkan.</summary>
    public const short DeathBeamKnightNumber = 63;

    /// <summary>The number of the Splinter Wolf of Kanturu Ruins.</summary>
    public const short SplinterWolfNumber = 351;

    /// <summary>The number of the Iron Rider of Kanturu Ruins.</summary>
    public const short IronRiderNumber = 352;

    /// <summary>The number of the Satyros of Kanturu Ruins.</summary>
    public const short SatyrosNumber = 353;

    /// <summary>The number of the Blade Hunter of Kanturu Ruins.</summary>
    public const short BladeHunterNumber = 354;

    /// <summary>The number of the Berserker Warrior of Kanturu Ruins.</summary>
    public const short BerserkerWarriorNumber = 553;

    /// <summary>The number of the Kentauros Warrior of Kanturu Ruins.</summary>
    public const short KentaurosWarriorNumber = 554;

    /// <summary>The number of the Gigantis Warrior of Kanturu Ruins.</summary>
    public const short GigantisWarriorNumber = 555;

    /// <summary>The number of the Genocider Warrior of Kanturu Ruins.</summary>
    public const short GenociderWarriorNumber = 556;

    /// <summary>The designation of the first semi boss of Tarkan, a stronger Mutant.</summary>
    public const string MutantBossDesignation = "Mutant" + BossSuffix;

    /// <summary>The designation of the second semi boss of Tarkan, a stronger Beam Knight.</summary>
    public const string BeamKnightBossDesignation = "Beam Knight" + BossSuffix;

    /// <summary>The designation of the boss of Tarkan, a stronger Death Beam Knight.</summary>
    public const string DeathBeamKnightBossDesignation = "Death Beam Knight" + BossSuffix;

    /// <summary>The designation of the escorts of the boss of Tarkan, stronger Zaikans.</summary>
    public const string ZaikanBossDesignation = "Zaikan" + BossSuffix;

    /// <summary>The designation of the semi boss of Kanturu Ruins, a stronger Satyros.</summary>
    public const string SatyrosBossDesignation = "Satyros" + BossSuffix;

    /// <summary>The designation of the first boss of Kanturu Ruins, a stronger Kentauros Warrior.</summary>
    public const string KentaurosWarriorBossDesignation = "Kentauros Warrior" + BossSuffix;

    /// <summary>The designation of the second boss of Kanturu Ruins, a stronger Genocider Warrior.</summary>
    public const string GenociderWarriorBossDesignation = "Genocider Warrior" + BossSuffix;

    /// <summary>
    /// Creates the dungeons: Devias with the items of Lorencia, Noria, Elbeland and Devias; Tarkan with the ones of
    /// Dungeon, Lost Tower, Atlans and Tarkan; and Kanturu Ruins with the ones of Aida, Icarus, Kanturu Ruins and Kanturu Relics.
    /// </summary>
    /// <returns>The dungeons.</returns>
    public static List<DungeonSettings> CreateDungeons() =>
    [
        CreateDungeon(DeviasDungeonNumber, "Dungeon de Devias", NpcDesignation, 165, 41, "0,3,51,2"),
        CreateDungeon(TarkanDungeonNumber, "Dungeon de Tarkan", TarkanNpcDesignation, 151, 58, "1,4,7,8"),
        CreateDungeon(KanturuDungeonNumber, "Dungeon de Kanturu", KanturuNpcDesignation, 46, 234, "33,10,37,38"),
    ];


    /// <summary>
    /// Creates the stages of the dungeons.
    /// <list type="bullet">
    ///   <item>Devias: the Hommerd in the fort of the north-west, after the way of the Worms; the Elite Yeti in the south-east,
    ///         after one of the two ways of the Ice Monsters; and the Ice Queen in the north-east, after the way of the Assassins.</item>
    ///   <item>Tarkan: the Mutant after the ways of the Bloody Wolves, the Beam Knight after the ways of the Iron Wheels,
    ///         and the Death Beam Knight with three Zaikans after the way of the Tantalos.</item>
    ///   <item>Kanturu Ruins: the Satyros after the ways of the Splinter Wolves and Iron Riders, the Kentauros Warrior after
    ///         the way of the Blade Hunters and Berserker Warriors, and the Genocider Warrior after the way of the Gigantis Warriors.</item>
    /// </list>
    /// </summary>
    /// <returns>The stages.</returns>
    public static List<DungeonStage> CreateStages() =>
    [
        new()
        {
            Dungeon = DeviasDungeonNumber,
            BossDesignation = FirstSemiBossDesignation,
            BossX = 37,
            BossY = 27,
            MonsterNumber = WormNumber,
            KillCount = 150,
            MinimumTime = TimeSpan.FromMinutes(1),
            ExcellentDropChance = 0.5,
            Hint = "Sigan el camino de los Worms hasta el fuerte (37, 27) y maten 150 Worms.",
        },
        new()
        {
            Dungeon = DeviasDungeonNumber,
            BossDesignation = SecondSemiBossDesignation,
            BossX = 226,
            BossY = 215,
            MonsterNumber = IceMonsterNumber,
            KillCount = 200,
            MinimumTime = TimeSpan.FromMinutes(5),
            ExcellentDropChance = 0.5,
            Hint = "Vayan por uno de los caminos de Ice Monsters hasta (226, 215) y maten 200.",
        },
        new()
        {
            Dungeon = DeviasDungeonNumber,
            BossDesignation = BossDesignation,
            BossX = 27,
            BossY = 226,
            MonsterNumber = AssassinNumber,
            KillCount = 250,
            MinimumTime = TimeSpan.FromMinutes(10),
            Hint = "Sigan el camino de los Assassins hasta (27, 226) y maten 250.",
        },
        new()
        {
            Dungeon = TarkanDungeonNumber,
            BossDesignation = MutantBossDesignation,
            BossX = 70,
            BossY = 94,
            MonsterNumber = BloodyWolfNumber,
            KillCount = 100,
            MinimumTime = TimeSpan.FromMinutes(1),
            ExcellentDropChance = 0.5,
            Hint = "Sigan los caminos de los Bloody Wolves hasta (70, 94) y maten 100.",
        },
        new()
        {
            Dungeon = TarkanDungeonNumber,
            BossDesignation = BeamKnightBossDesignation,
            BossX = 73,
            BossY = 182,
            MonsterNumber = IronWheelNumber,
            KillCount = 100,
            MinimumTime = TimeSpan.FromMinutes(5),
            ExcellentDropChance = 0.5,
            Hint = "Vayan por uno de los caminos de Iron Wheels hasta (73, 182) y maten 100.",
        },
        new()
        {
            Dungeon = TarkanDungeonNumber,
            BossDesignation = DeathBeamKnightBossDesignation,
            BossX = 12,
            BossY = 240,
            MonsterNumber = TantalosNumber,
            KillCount = 100,
            MinimumTime = TimeSpan.FromMinutes(10),
            EscortDesignation = ZaikanBossDesignation,
            EscortCount = 3,
            EscortExcellentDropChance = 0.5,
            Hint = "Sigan el camino de los Tantalos hasta (12, 240) y maten 100.",
        },
        new()
        {
            Dungeon = KanturuDungeonNumber,
            BossDesignation = SatyrosBossDesignation,
            BossX = 198,
            BossY = 161,
            MonsterNumber = SplinterWolfNumber,
            KillCount = 75,
            SecondMonsterNumber = IronRiderNumber,
            SecondKillCount = 75,
            MinimumTime = TimeSpan.FromMinutes(1),
            ExcellentDropChance = 0.5,
            Hint = "Sigan los caminos hasta (198, 161) y maten 75 Splinter Wolves y 75 Iron Riders.",
        },
        new()
        {
            Dungeon = KanturuDungeonNumber,
            BossDesignation = KentaurosWarriorBossDesignation,
            BossX = 139,
            BossY = 44,
            MonsterNumber = BladeHunterNumber,
            KillCount = 75,
            SecondMonsterNumber = BerserkerWarriorNumber,
            SecondKillCount = 75,
            MinimumTime = TimeSpan.FromMinutes(5),
            Hint = "Sigan el camino hasta (139, 44) y maten 75 Blade Hunters y 75 Berserker Warriors.",
        },
        new()
        {
            Dungeon = KanturuDungeonNumber,
            BossDesignation = GenociderWarriorBossDesignation,
            BossX = 59,
            BossY = 34,
            MonsterNumber = GigantisWarriorNumber,
            KillCount = 150,
            MinimumTime = TimeSpan.FromMinutes(10),
            Hint = "Sigan el camino hasta (59, 34) y maten 150 Gigantis Warriors.",
        },
    ];

    private static DungeonSettings CreateDungeon(byte number, string name, string npcDesignation, byte entryX, byte entryY, string dropMaps) => new()
    {
        Number = number,
        Name = name,
        NpcDesignation = npcDesignation,
        ExtremeNpcDesignation = ExtremeNpcPrefix + npcDesignation,
        GameLevel = number,
        ExtremeGameLevel = (byte)(number + ExtremeGameLevelOffset),
        EntryX = entryX,
        EntryY = entryY,
        DropMaps = dropMaps,
    };
}
