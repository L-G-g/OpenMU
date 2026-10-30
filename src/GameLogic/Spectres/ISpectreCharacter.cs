// <copyright file="ISpectreCharacter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// A character of the spectre system: a spectre of a player or a rival of the spectre hub. It gets its skills from
/// the evolutions (see <see cref="SpectreEvolution"/>), so it may use them without the usual requirements of level and stats.
/// </summary>
public interface ISpectreCharacter
{
    /// <summary>
    /// Gets the kind of the spectre.
    /// </summary>
    SpectreKind Kind { get; }
}
