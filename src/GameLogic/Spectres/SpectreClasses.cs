// <copyright file="SpectreClasses.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Bots;

/// <summary>
/// What distinguishes the kinds of spectres: their character class, names and default build.
/// </summary>
internal static class SpectreClasses
{
    /// <summary>
    /// The number of kinds of spectres.
    /// </summary>
    public const int KindCount = 7;

    /// <summary>
    /// Gets the stats on which a spectre spends its level up points, in the order of the percentages.
    /// Only the lord spends points on leadership (see <see cref="HasLeadership"/>).
    /// </summary>
    public static AttributeDefinition[] StatAttributes { get; } = [Stats.BaseStrength, Stats.BaseAgility, Stats.BaseVitality, Stats.BaseEnergy, Stats.BaseLeadership];

    /// <summary>
    /// Gets all kinds of spectres.
    /// </summary>
    public static IReadOnlyList<SpectreKind> AllKinds { get; } = Enum.GetValues<SpectreKind>();

    /// <summary>
    /// Gets the name of a kind of spectres, as shown to the players.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string GetName(SpectreKind kind) => kind switch
    {
        SpectreKind.Warrior => "guerrero",
        SpectreKind.Mage => "mago",
        SpectreKind.Elf => "elfa",
        SpectreKind.Gladiator => "gladiador",
        SpectreKind.Lord => "lord",
        SpectreKind.Summoner => "invocadora",
        _ => "peleador",
    };

    /// <summary>
    /// Tries to get the kind of spectres by its name or by its letter, as used by the chat command.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c>, if the text names a kind.</returns>
    public static bool TryParse(string text, out SpectreKind kind)
    {
        foreach (var candidate in AllKinds)
        {
            if (string.Equals(text, GetName(candidate), StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, GetLetter(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// Gets the letter of a kind, which is used by the chat command and in the names of the spectres.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The letter.</returns>
    public static string GetLetter(SpectreKind kind) => kind switch
    {
        SpectreKind.Warrior => "G",
        SpectreKind.Mage => "M",
        SpectreKind.Elf => "E",
        SpectreKind.Gladiator => "D",
        SpectreKind.Lord => "L",
        SpectreKind.Summoner => "I",
        _ => "P",
    };

    /// <summary>
    /// Gets the number of the character class of a kind of spectres.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number of the character class.</returns>
    public static byte GetClassNumber(SpectreKind kind) => kind switch
    {
        SpectreKind.Warrior => BotClassNumbers.DarkKnightNumber,
        SpectreKind.Mage => BotClassNumbers.DarkWizardNumber,
        SpectreKind.Elf => BotClassNumbers.FairyElfNumber,
        SpectreKind.Gladiator => BotClassNumbers.MagicGladiatorNumber,
        SpectreKind.Lord => BotClassNumbers.DarkLordNumber,
        SpectreKind.Summoner => BotClassNumbers.SummonerNumber,
        _ => BotClassNumbers.RageFighterNumber,
    };

    /// <summary>
    /// Determines whether a kind of spectres spends points on leadership, too.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c>, if it spends points on leadership.</returns>
    public static bool HasLeadership(SpectreKind kind) => kind == SpectreKind.Lord;

    /// <summary>
    /// Gets the percentages of strength, agility, vitality, energy and leadership by which a new spectre spends its points.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The percentages.</returns>
    public static byte[] GetDefaultStatPercentages(SpectreKind kind) => kind switch
    {
        SpectreKind.Warrior => [60, 25, 15, 0, 0],
        SpectreKind.Mage => [0, 20, 10, 70, 0],
        SpectreKind.Elf => [15, 45, 10, 30, 0],
        SpectreKind.Gladiator => [45, 20, 10, 25, 0],
        SpectreKind.Lord => [35, 20, 10, 15, 20],
        SpectreKind.Summoner => [0, 20, 10, 70, 0],
        _ => [50, 20, 30, 0, 0],
    };

    /// <summary>
    /// Gets the slot of the spectre character of an owner character in the hidden account of the spectres.
    /// The warrior, mage and elf keep the slots of the first version (three per owner character).
    /// </summary>
    /// <param name="ownerCharacterSlot">The slot of the owner character.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The slot.</returns>
    public static byte GetCharacterSlot(byte ownerCharacterSlot, SpectreKind kind)
        => (byte)kind < 3
            ? (byte)((ownerCharacterSlot * 3) + (byte)kind)
            : (byte)(64 + (ownerCharacterSlot * 4) + ((byte)kind - 3));
}
