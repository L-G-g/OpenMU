// <copyright file="MiniGameType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Defines the type of a mini game.
/// </summary>
public enum MiniGameType
{
    /// <summary>
    /// Undefined mini game type.
    /// </summary>
    Undefined,

    /// <summary>
    /// The devil square event.
    /// </summary>
    DevilSquare,

    /// <summary>
    /// The blood castle event.
    /// </summary>
    BloodCastle,

    /// <summary>
    /// The chaos castle event.
    /// </summary>
    ChaosCastle,

    /// <summary>
    /// The illusion temple event.
    /// </summary>
    IllusionTemple,

    /// <summary>
    /// The doppelganger event.
    /// </summary>
    Doppelganger,

    /// <summary>
    /// The Kanturu Refinery Tower event.
    /// </summary>
    Kanturu,

    /// <summary>
    /// The imperial guardian event (fortress of the imperial guardian).
    /// </summary>
    ImperialGuardian,

    /// <summary>
    /// A dungeon: an instance of a hunting map for a party, with stronger monsters and a boss.
    /// </summary>
    Dungeon,

    /// <summary>
    /// The spectre hub: an instance for one player, where the player duels the abandoned spectres to unlock and evolve them.
    /// </summary>
    SpectreHub,
}