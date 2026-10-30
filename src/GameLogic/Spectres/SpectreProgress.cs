// <copyright file="SpectreProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The progress of a character with its spectres: how often it beat the rival of each kind in the spectre hub.
/// The first victory unlocks the spectre, each further victory an evolution (see <see cref="SpectreEvolution"/>).
/// The victories are kept in an attribute of the owner character per kind, so no change of the database is needed.
/// </summary>
public static class SpectreProgress
{
    /// <summary>
    /// The highest number of victories against the rival of a kind: the unlock and the evolutions.
    /// </summary>
    public const int MaximumVictories = SpectreEvolution.EvolutionCount + 1;

    /// <summary>
    /// Gets the attribute definitions of the victories of a character against the rivals, by kind.
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> VictoryAttributes { get; } =
    [
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A01"), "Spectre victories warrior", "How often the character beat the warrior in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A02"), "Spectre victories mage", "How often the character beat the mage in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A03"), "Spectre victories elf", "How often the character beat the elf in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A04"), "Spectre victories gladiator", "How often the character beat the gladiator in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A05"), "Spectre victories lord", "How often the character beat the lord in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A06"), "Spectre victories summoner", "How often the character beat the summoner in the spectre hub."),
        new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A07"), "Spectre victories fighter", "How often the character beat the fighter in the spectre hub."),
    ];

    /// <summary>
    /// Gets the attribute definition of the pets which a spectre got from its evolutions (see <see cref="SpectreGrantedPet"/>),
    /// as bit mask. Such a pet can't be taken off the spectre.
    /// </summary>
    public static AttributeDefinition GrantedPetsAttribute { get; } = new(new Guid("3A9E2B70-5C1D-4F8E-9B21-6D0C7E8F1A10"), "Spectre granted pets", "The pets which a spectre got from its evolutions, as bit mask.");

    /// <summary>
    /// Gets how often a character beat the rival of a kind.
    /// </summary>
    /// <param name="character">The owner character.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The number of victories, 0 if the spectre is still locked.</returns>
    public static int GetVictories(Character character, SpectreKind kind)
        => (int)GetValue(character, VictoryAttributes[(int)kind]);

    /// <summary>
    /// Determines whether the spectre of a kind is unlocked for a character.
    /// </summary>
    /// <param name="character">The owner character.</param>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c>, if it's unlocked.</returns>
    public static bool IsUnlocked(Character character, SpectreKind kind) => GetVictories(character, kind) > 0;

    /// <summary>
    /// Sets how often a character beat the rival of a kind.
    /// </summary>
    /// <param name="player">The owner.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="victories">The number of victories.</param>
    /// <returns><c>true</c>, if the value could be set.</returns>
    public static bool SetVictories(Player player, SpectreKind kind, int victories)
        => player.SelectedCharacter is { } character
           && SetValue(player.GameContext.Configuration, player.PersistenceContext, character, VictoryAttributes[(int)kind], Math.Clamp(victories, 0, MaximumVictories));

    /// <summary>
    /// Gets the value of an attribute of a character, which isn't part of its attribute system.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="definition">The attribute definition.</param>
    /// <returns>The value, or 0.</returns>
    internal static float GetValue(Character character, AttributeDefinition definition)
        => character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id)?.Value ?? 0;

    /// <summary>
    /// Sets the value of an attribute of a character, which isn't part of its attribute system.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="persistenceContext">The persistence context of the character.</param>
    /// <param name="character">The character.</param>
    /// <param name="definition">The attribute definition.</param>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c>, if the definition exists in the configuration and the value was set.</returns>
    internal static bool SetValue(GameConfiguration configuration, IContext persistenceContext, Character character, AttributeDefinition definition, float value)
    {
        if (character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id) is { } existing)
        {
            existing.Value = value;
            return true;
        }

        if (configuration.Attributes.FirstOrDefault(a => a.Id == definition.Id) is not { } persistentDefinition)
        {
            return false;
        }

        character.Attributes.Add(persistenceContext.CreateNew<StatAttribute>(persistentDefinition, value));
        return true;
    }
}
