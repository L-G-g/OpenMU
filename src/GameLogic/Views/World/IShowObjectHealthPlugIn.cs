// <copyright file="IShowObjectHealthPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about the remaining health of an observed
/// non-player object, e.g. to show a health bar above a monster.
/// </summary>
public interface IShowObjectHealthPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the remaining health of an object.
    /// </summary>
    /// <param name="target">The object.</param>
    /// <param name="healthRatio">The remaining health, between 0 (dead) and 1 (full health).</param>
    ValueTask ShowObjectHealthAsync(IAttackable target, float healthRatio);
}
