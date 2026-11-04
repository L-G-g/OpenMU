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

    /// <summary>
    /// Shows the story which the guardian of the spectres tells, with the option to enter the spectre hub.
    /// </summary>
    /// <param name="lines">The lines of the story.</param>
    /// <returns>The task.</returns>
    ValueTask ShowGuardianStoryAsync(IReadOnlyList<string> lines);

    /// <summary>
    /// Shows an object with a name which may be longer than the names of the characters, e.g. "Invocadora Recuperada".
    /// </summary>
    /// <param name="obj">The object, a spectre or a rival.</param>
    /// <param name="title">The name.</param>
    /// <returns>The task.</returns>
    ValueTask ShowTitleAsync(IIdentifiable obj, string title);

    /// <summary>
    /// Shows that an object in the view of the player is a rival of its spectre hub, which the player can talk to by clicking on it.
    /// </summary>
    /// <param name="rival">The rival.</param>
    /// <returns>The task.</returns>
    ValueTask ShowRivalAsync(RivalPlayer rival);

    /// <summary>
    /// Shows what a rival of the spectre hub says to the player, in a scene in which the camera looks at the rival,
    /// with the options to duel it (if <paramref name="canDuel"/>) or to come back later.
    /// </summary>
    /// <param name="rival">The rival.</param>
    /// <param name="title">The name of the rival, e.g. "Guerrero Olvidado".</param>
    /// <param name="text">What the rival says.</param>
    /// <param name="canDuel">If set to <c>true</c>, the player may duel the rival now.</param>
    /// <param name="reason">Why the player may not duel the rival now, or <c>null</c>.</param>
    /// <returns>The task.</returns>
    ValueTask ShowRivalDialogueAsync(RivalPlayer rival, string title, string text, bool canDuel, string? reason);
}
