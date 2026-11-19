// <copyright file="EventBoardEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

/// <summary>
/// An event of the board of the next events.
/// </summary>
/// <param name="Name">The name of the event, e.g. "Devil Square 5".</param>
/// <param name="Seconds">The seconds until the event opens; while it's open, the seconds until the entrance closes.</param>
/// <param name="IsOpen">If set to <c>true</c>, the entrance of the event is open now.</param>
public sealed record EventBoardEntry(string Name, int Seconds, bool IsOpen);
