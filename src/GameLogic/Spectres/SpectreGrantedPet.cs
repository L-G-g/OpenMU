// <copyright file="SpectreGrantedPet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The pets which a spectre gets with an evolution (see <see cref="SpectreEvolution"/>). They're bits of <see cref="SpectreProgress.GrantedPetsAttribute"/>.
/// </summary>
[Flags]
public enum SpectreGrantedPet
{
    /// <summary>
    /// No pet.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Dark Raven of the lord, in the other hand.
    /// </summary>
    DarkRaven = 1,

    /// <summary>
    /// The Dark Horse of the lord, in the pet slot.
    /// </summary>
    DarkHorse = 2,

    /// <summary>
    /// The Dinorant of the warrior, in the pet slot.
    /// </summary>
    Dinorant = 4,
}
