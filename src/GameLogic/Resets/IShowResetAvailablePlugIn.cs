// <copyright file="IShowResetAvailablePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Interface of a view which shows whether the character can do a reset right now,
/// e.g. the reset button of resetbutton.dll in the PeroniaMU client.
/// </summary>
public interface IShowResetAvailablePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows whether the character can do a reset right now.
    /// </summary>
    /// <param name="available">If set to <c>true</c>, the character meets all requirements of the next reset.</param>
    ValueTask ShowResetAvailableAsync(bool available);
}
