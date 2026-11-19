// <copyright file="IEventBoardViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// A view plugin which shows the board of the next events, for eventboard.dll of the PeroniaMU client.
/// </summary>
public interface IEventBoardViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the board of the next events.
    /// </summary>
    /// <param name="entries">The events, ordered by their start.</param>
    /// <returns>The task.</returns>
    ValueTask ShowEventBoardAsync(IReadOnlyList<EventBoardEntry> entries);
}
