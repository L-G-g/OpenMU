// <copyright file="SpectreKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The kinds of spectres which a player can summon.
/// </summary>
public enum SpectreKind : byte
{
    /// <summary>
    /// The warrior, a Dark Knight.
    /// </summary>
    Warrior = 0,

    /// <summary>
    /// The mage, a Dark Wizard.
    /// </summary>
    Mage = 1,

    /// <summary>
    /// The elf, a Fairy Elf.
    /// </summary>
    Elf = 2,

    /// <summary>
    /// The gladiator, a Magic Gladiator.
    /// </summary>
    Gladiator = 3,

    /// <summary>
    /// The lord, a Dark Lord.
    /// </summary>
    Lord = 4,

    /// <summary>
    /// The summoner, a Summoner.
    /// </summary>
    Summoner = 5,

    /// <summary>
    /// The fighter, a Rage Fighter.
    /// </summary>
    Fighter = 6,
}
