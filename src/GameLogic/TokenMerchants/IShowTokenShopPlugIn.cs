// <copyright file="IShowTokenShopPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Interface of a view whose implementation informs the game client about the opened tab of a token merchant:
/// what the shop of the game client doesn't know.
/// </summary>
public interface IShowTokenShopPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the data of the opened tab of a token merchant.
    /// </summary>
    /// <param name="tab">The index of the opened tab.</param>
    /// <param name="tokens">The tokens of the player.</param>
    /// <param name="tokenName">The name of the token.</param>
    /// <param name="prices">The slot, width, height and price in tokens of each item of the tab.</param>
    ValueTask ShowTokenShopAsync(byte tab, uint tokens, string tokenName, IReadOnlyList<(byte Slot, byte Width, byte Height, ushort Price)> prices);
}
