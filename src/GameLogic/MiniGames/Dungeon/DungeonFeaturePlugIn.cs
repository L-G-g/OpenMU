// <copyright file="DungeonFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Feature plugin which provides the configuration of the dungeon: an instance of a hunting map for a party,
/// with stronger monsters, more excellent drops, a boss and need, greed or pass for the drops.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DungeonFeaturePlugIn_Name), Description = nameof(PlugInResources.DungeonFeaturePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4E6F5B1C-2D7A-4C3B-8E9F-0A1B2C3D4E51")]
public class DungeonFeaturePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<DungeonConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public DungeonConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new DungeonConfiguration();

    /// <summary>
    /// Gets the configuration of the dungeon of the game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The configuration, or <c>null</c> if the dungeon is not active.</returns>
    internal static DungeonConfiguration? GetConfiguration(IGameContext gameContext)
    {
        return gameContext.FeaturePlugIns.GetPlugIn<DungeonFeaturePlugIn>() is { } plugIn
            ? plugIn.Configuration ??= new DungeonConfiguration()
            : null;
    }
}
