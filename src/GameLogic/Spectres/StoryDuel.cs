// <copyright file="StoryDuel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// A duel of the story mode (see <see cref="Story.StoryModeFeaturePlugIn"/>) in the <see cref="SpectreDuelContext"/>:
/// instead of a rival of the spectre hub, the player fights a character of the story, e.g. the stranger. The rival is
/// created like the rivals of the hub, but with its own name, level, resets and equipment, and the victory doesn't
/// count for the spectres.
/// </summary>
public sealed class StoryDuel
{
    /// <summary>
    /// Gets the name of the rival, as the client shows it. At most 10 characters.
    /// </summary>
    public required string RivalName { get; init; }

    /// <summary>
    /// Gets the name of the rival with its article, e.g. "el Forastero".
    /// </summary>
    public required string RivalNameWithArticle { get; init; }

    /// <summary>
    /// Gets the kind of the rival, which is its class and how it fights.
    /// </summary>
    public SpectreKind Kind { get; init; } = SpectreKind.Warrior;

    /// <summary>
    /// Gets the level of the rival.
    /// </summary>
    public int Level { get; init; } = 250;

    /// <summary>
    /// Gets the resets of the rival.
    /// </summary>
    public int Resets { get; init; }

    /// <summary>
    /// Gets the tier of <see cref="SpectreEvolution"/> whose skills the rival knows.
    /// </summary>
    public int SkillTier { get; init; }

    /// <summary>
    /// Gets the level of the items of the rival.
    /// </summary>
    public byte ItemLevel { get; init; }

    /// <summary>
    /// Gets the items of the rival, as group and number.
    /// </summary>
    public IReadOnlyList<(byte Group, short Number)> Equipment { get; init; } = [];

    /// <summary>
    /// Gets what happens when the duel ended with a victory (<c>true</c>) or a defeat (<c>false</c>) of the player.
    /// </summary>
    public required Func<Player, bool, ValueTask> FinishedAsync { get; init; }

    /// <summary>
    /// Gets how the player goes back after the duel.
    /// </summary>
    public required Func<Player, ValueTask> ReturnAsync { get; init; }
}
