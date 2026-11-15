// <copyright file="CharacterSummary.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

/// <summary>
/// A summary of a character, e.g. for the ranking.
/// </summary>
/// <param name="Name">The name of the character.</param>
/// <param name="CharacterClassName">The name of the class of the character; empty when the
/// game configuration which was passed to the player context doesn't know the class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="CharacterClassId">The identifier of the class of the character.</param>
public sealed record CharacterSummary(string Name, string CharacterClassName, int Level, int MasterLevel, int Resets, Guid? CharacterClassId = null);
