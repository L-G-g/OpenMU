// <copyright file="SpectrePanelEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The state of a spectre of a player, as the spectre window of the client shows it.
/// </summary>
/// <param name="Kind">The kind of the spectre.</param>
/// <param name="Summoned">If set to <c>true</c>, the spectre is summoned; otherwise, the other values are empty.</param>
/// <param name="Name">The name.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The resets.</param>
/// <param name="LevelUpPoints">The level up points which aren't spent yet.</param>
/// <param name="Stats">The base strength, agility, vitality and energy.</param>
/// <param name="Percentages">The percentages by which the spectre spends its points on strength, agility, vitality and energy.</param>
/// <param name="Equipment">The equipped items, by equipment slot (0 to 11).</param>
/// <param name="Descriptions">The descriptions of the equipped items, by equipment slot.</param>
public sealed record SpectrePanelEntry(
    SpectreKind Kind,
    bool Summoned,
    string Name,
    int Level,
    int Resets,
    int LevelUpPoints,
    IReadOnlyList<int> Stats,
    IReadOnlyList<byte> Percentages,
    IReadOnlyList<Item?> Equipment,
    IReadOnlyList<string> Descriptions)
{
    /// <summary>
    /// The number of equipment slots.
    /// </summary>
    public const int EquipmentSlots = 12;

    /// <summary>
    /// Creates the entry of a spectre which isn't summoned.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The entry.</returns>
    public static SpectrePanelEntry NotSummoned(SpectreKind kind)
        => new(kind, false, string.Empty, 0, 0, 0, [0, 0, 0, 0], [0, 0, 0, 0], new Item?[EquipmentSlots], new string[EquipmentSlots]);
}
