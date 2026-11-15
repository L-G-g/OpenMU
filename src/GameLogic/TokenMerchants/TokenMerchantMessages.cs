// <copyright file="TokenMerchantMessages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

/// <summary>
/// The messages of the token merchants. They are in Spanish without accents,
/// because the game client doesn't show them.
/// </summary>
public static class TokenMerchantMessages
{
    /// <summary>The message when a player gets tokens: amount, token name, total.</summary>
    public const string TokensGainedFormat = "Obtuviste {0} {1}. Tenes {2}.";

    /// <summary>The message when the shop opens: tokens of the player, token name, price of a set piece, price of a weapon.</summary>
    public const string ShopOpenedFormat = "Tenes {0} {1}. Piezas de set: {2}. Armas y escudos: {3}.";

    /// <summary>The message when a tab is shown: class name.</summary>
    public const string TabShownFormat = "Items de {0}. Otra clase: /tab 1 a 7 (DK, DW, ELF, SUM, MG, DL, RF).";

    /// <summary>The message when the player has not enough tokens: price, token name, tokens of the player.</summary>
    public const string NotEnoughTokensFormat = "Necesitas {0} {1}, tenes {2}.";

    /// <summary>The message after a purchase: item name, number of excellent options, remaining tokens, token name.</summary>
    public const string BoughtFormat = "Compraste {0} con {1} opciones excelentes. Te quedan {2} {3}.";

    /// <summary>The message after a purchase of an item with one excellent option.</summary>
    public const string BoughtOneOptionFormat = "Compraste {0} con 1 opcion excelente. Te quedan {2} {3}.";

    /// <summary>The message when the merchant has nothing to sell.</summary>
    public const string NothingToSell = "El mercader no tiene items para vender.";

    /// <summary>The message when no token merchant is opened.</summary>
    public const string NoShopOpened = "Primero habla con un mercader de tokens.";

    /// <summary>The message when the tab doesn't exist.</summary>
    public const string InvalidTab = "Usa /tab 1 a 7 (DK, DW, ELF, SUM, MG, DL, RF).";

    /// <summary>A line of the list of tokens: token name, tokens of the player.</summary>
    public const string TokenLineFormat = "{0}: {1}";

    /// <summary>
    /// Gets the names of the classes of the tabs.
    /// </summary>
    public static IReadOnlyList<string> TabNames { get; } = ["Dark Knight", "Dark Wizard", "Elf", "Summoner", "Magic Gladiator", "Dark Lord", "Rage Fighter"];
}
