// <copyright file="MarketOrderState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The state of a <see cref="MarketOrder"/>.
/// </summary>
public enum MarketOrderState
{
    /// <summary>
    /// The order is open and shown on the market.
    /// </summary>
    Active,

    /// <summary>
    /// The order is open, but hidden by its owner. A hidden sell order still locks its item.
    /// </summary>
    Hidden,

    /// <summary>
    /// The order was completed by trades.
    /// </summary>
    Completed,

    /// <summary>
    /// The order was cancelled by its owner.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The order expired.
    /// </summary>
    Expired,
}
