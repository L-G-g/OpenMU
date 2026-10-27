// <copyright file="IShowSpectresPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// A view plugin which shows the spectres to a player, for the spectre window of the client.
/// </summary>
public interface IShowSpectresPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows that an object in the view of the player is a spectre, e.g. so that the client draws it transparent.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <returns>The task.</returns>
    ValueTask ShowSpectreAsync(SpectrePlayer spectre);

    /// <summary>
    /// Shows the spectres of the player.
    /// </summary>
    /// <param name="entries">The spectres, one for each kind.</param>
    /// <returns>The task.</returns>
    ValueTask ShowSpectrePanelAsync(IReadOnlyList<SpectrePanelEntry> entries);
}
