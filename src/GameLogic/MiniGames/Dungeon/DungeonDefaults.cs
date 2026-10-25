// <copyright file="DungeonDefaults.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The defaults of the first dungeon, an instance of Devias with the Ice Queen as boss.
/// The data initialization creates the NPC, the boss and the <see cref="MiniGameDefinition"/> with these values.
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
    /// The game level of the <see cref="MiniGameDefinition"/> of the dungeon.
    /// </summary>
    public const byte GameLevel = 1;

    /// <summary>
    /// Creates the stages of the first dungeon: the Hommerd in the fort of the north-west, after the way of the Worms;
    /// the Elite Yeti in the south-east, after one of the two ways of the Ice Monsters; and the Ice Queen in the north-east,
    /// after the way of the Assassins.
    /// </summary>
    /// <returns>The stages.</returns>
    public static List<DungeonStage> CreateStages() =>
    [
        new()
        {
            BossDesignation = FirstSemiBossDesignation,
            BossX = 37,
            BossY = 27,
            MonsterNumber = WormNumber,
            KillCount = 150,
            MinimumTime = TimeSpan.FromMinutes(1),
            Hint = "Sigan el camino de los Worms hasta el fuerte (37, 27) y maten 150 Worms.",
        },
        new()
        {
            BossDesignation = SecondSemiBossDesignation,
            BossX = 226,
            BossY = 215,
            MonsterNumber = IceMonsterNumber,
            KillCount = 200,
            MinimumTime = TimeSpan.FromMinutes(5),
            Hint = "Vayan por uno de los caminos de Ice Monsters hasta (226, 215) y maten 200.",
        },
        new()
        {
            BossDesignation = BossDesignation,
            BossX = 27,
            BossY = 226,
            MonsterNumber = AssassinNumber,
            KillCount = 250,
            MinimumTime = TimeSpan.FromMinutes(10),
            Hint = "Sigan el camino de los Assassins hasta (27, 226) y maten 250.",
        },
    ];
}
