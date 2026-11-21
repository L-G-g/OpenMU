// <copyright file="GameContextLookup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Finds the game context of a plugin manager, for plugins which are called without one,
/// e.g. when a monster is added to a map.
/// </summary>
public static class GameContextLookup
{
    private static readonly ConditionalWeakTable<PlugInManager, IGameContext> GameContexts = new();

    /// <summary>
    /// Registers the game context of a plugin manager.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    /// <param name="gameContext">The game context.</param>
    public static void Register(PlugInManager plugInManager, IGameContext gameContext)
    {
        GameContexts.AddOrUpdate(plugInManager, gameContext);
    }

    /// <summary>
    /// Gets the game context of a plugin manager.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    /// <returns>The game context, or <c>null</c> if the plugin manager doesn't belong to a game context.</returns>
    public static IGameContext? Get(PlugInManager? plugInManager)
    {
        return plugInManager is not null && GameContexts.TryGetValue(plugInManager, out var gameContext) ? gameContext : null;
    }
}
