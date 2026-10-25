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
    /// The designation of the boss.
    /// </summary>
    public const string BossDesignation = "Ice Queen Boss";

    /// <summary>
    /// The game level of the <see cref="MiniGameDefinition"/> of the dungeon.
    /// </summary>
    public const byte GameLevel = 1;
}
