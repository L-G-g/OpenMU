// <copyright file="ResetAvailability.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Checks, without changing anything, whether a character meets the requirements of its next reset.
/// </summary>
/// <remarks>
/// It checks the same as <see cref="ResetCharacterAction"/> does for the chat command /reset:
/// the character is in the game without an opened window, the required level, the reset limit,
/// the required zen and the required reset items.
/// </remarks>
public static class ResetAvailability
{
    /// <summary>
    /// Determines whether the player can do its next reset with the chat command /reset right now.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the reset would succeed.</returns>
    public static bool CanResetNow(Player player)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>()?.Configuration is not { } configuration
            || player.PlayerState.CurrentState != PlayerState.EnteredWorld
            || player.Attributes is not { } attributes
            || player.SelectedCharacter is null)
        {
            return false;
        }

        var progression = ResetProgressionCalculator.Calculate((int)attributes[Stats.Resets], (int)attributes[Stats.PointsPerReset], configuration);
        if (player.Level < progression.RequiredLevel
            || (configuration.ResetLimit > 0 && progression.NextResetCount > configuration.ResetLimit)
            || player.Money < progression.RequiredZen)
        {
            return false;
        }

        if (progression.RequiredItemAmount <= 0 || configuration.RequiredResetItem is not { } requiredItem)
        {
            return true;
        }

        var itemCount = player.Inventory?.Items.Count(item => item.Definition is { } definition
                                                            && definition.Group == requiredItem.Group
                                                            && definition.Number == requiredItem.Number) ?? 0;
        return itemCount >= progression.RequiredItemAmount;
    }
}
