// <copyright file="StoryProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The progress of a character in the story mode. It's kept in the attributes <see cref="Stats.StoryProgress"/> and
/// <see cref="Stats.StoryFlags"/> of the character, which the update "Story Mode" adds to the game configuration.
/// </summary>
public static class StoryProgress
{
    /// <summary>
    /// Determines whether the game configuration has the attributes of the story mode.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns><c>true</c>, if the story mode can keep the progress of the characters.</returns>
    public static bool IsAvailable(GameConfiguration configuration)
        => configuration.Attributes.Any(a => a.Id == Stats.StoryProgress.Id)
           && configuration.Attributes.Any(a => a.Id == Stats.StoryFlags.Id);

    /// <summary>
    /// Gets the number of the last step which the character finished.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The number of the last finished step; 0, if it didn't start the story.</returns>
    public static int GetFinishedStep(Character character) => (int)GetValue(character, Stats.StoryProgress);

    /// <summary>
    /// Gets the flags of the character.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The flags.</returns>
    public static StoryFlags GetFlags(Character character) => (StoryFlags)(int)GetValue(character, Stats.StoryFlags);

    /// <summary>
    /// Sets the number of the last step which the character of the player finished.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="step">The number of the step.</param>
    /// <returns><c>true</c>, if it was set.</returns>
    public static bool SetFinishedStep(Player player, int step) => SetValue(player, Stats.StoryProgress, step);

    /// <summary>
    /// Adds a flag to the character of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="flag">The flag.</param>
    /// <returns><c>true</c>, if the character has the flag now.</returns>
    public static bool AddFlag(Player player, StoryFlags flag)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return false;
        }

        var flags = GetFlags(character);
        return flags.HasFlag(flag) || SetValue(player, Stats.StoryFlags, (int)(flags | flag));
    }

    /// <summary>
    /// Sets the flags of the character of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="flags">The flags.</param>
    /// <returns><c>true</c>, if they were set.</returns>
    public static bool SetFlags(Player player, StoryFlags flags) => SetValue(player, Stats.StoryFlags, (int)flags);

    private static float GetValue(Character character, AttributeDefinition definition)
        => character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id)?.Value ?? 0;

    private static bool SetValue(Player player, AttributeDefinition definition, int value)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return false;
        }

        var stat = character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id);
        if (stat is null)
        {
            // The definition of the game configuration, not the static one: it's the one which is saved.
            if (player.GameContext.Configuration.Attributes.FirstOrDefault(a => a.Id == definition.Id) is not { } persistentDefinition)
            {
                return false;
            }

            stat = player.PersistenceContext.CreateNew<StatAttribute>(persistentDefinition, 0);
            character.Attributes.Add(stat);
        }

        stat.Value = value;
        return true;
    }
}
