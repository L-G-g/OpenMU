// <copyright file="UnlockAllCharacterClassesPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.UnlockCharacterClass;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Unlocks every creatable character class (e.g. Magic Gladiator, Dark Lord, Summoner and Rage Fighter)
/// for an account as soon as it logged in, so that they can be created without reaching a level first.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UnlockAllCharacterClassesPlugIn_Name), Description = nameof(PlugInResources.UnlockAllCharacterClassesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0B6C2E5F-3B7A-4D6E-9F1C-8A2D4E6F7B91")]
public class UnlockAllCharacterClassesPlugIn : IPlayerStateChangedPlugIn, IDisabledByDefault
{
    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        // The character list, including the unlock flags, is sent after the authentication.
        // Bots are excluded for the same reason as in UnlockCharacterAtLevelBase.
        if (currentState != PlayerState.Authenticated || player.Account is not { IsBot: false } account)
        {
            return ValueTask.CompletedTask;
        }

        var lockedClasses = player.GameContext.Configuration.CharacterClasses
            .Where(c => c is { CanGetCreated: true, CreationAllowedFlag: > 0 })
            .Where(c => account.UnlockedCharacterClasses.All(u => u.Number != c.Number))
            .ToList();
        foreach (var characterClass in lockedClasses)
        {
            account.UnlockedCharacterClasses.Add(characterClass);
        }

        return ValueTask.CompletedTask;
    }
}
