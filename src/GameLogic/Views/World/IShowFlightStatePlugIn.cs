// <copyright file="IShowFlightStatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about a player which started or stopped flying on its mount.
/// </summary>
public interface IShowFlightStatePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows that the specified player started or stopped flying.
    /// </summary>
    /// <param name="player">The player, which may be the own player.</param>
    /// <param name="isFlying">If set to <c>true</c>, the player is flying.</param>
    ValueTask ShowFlightStateAsync(Player player, bool isFlying);
}
