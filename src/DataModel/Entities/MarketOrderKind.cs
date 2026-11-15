// <copyright file="MarketOrderKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The kind of a <see cref="MarketOrder"/>.
/// </summary>
public enum MarketOrderKind
{
    /// <summary>
    /// An item of the market depot is offered for a price.
    /// </summary>
    Sell,

    /// <summary>
    /// Items are requested for a price, which is reserved when the order is placed.
    /// </summary>
    Buy,
}
