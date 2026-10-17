// <copyright file="UnlockAllCharacterClassesPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.UnlockCharacterClass;

/// <summary>
/// Tests for the <see cref="UnlockAllCharacterClassesPlugIn"/>.
/// </summary>
[TestFixture]
public class UnlockAllCharacterClassesPlugInTest
{
    /// <summary>
    /// Tests that all creatable classes with a creation flag get unlocked when the player returns to the character selection,
    /// and that classes which can't be created or have no flag stay locked.
    /// </summary>
    [Test]
    public async Task CreatableClassesGetUnlockedAtCharacterSelectionAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var darkLord = new CharacterClass { Number = 16, CanGetCreated = true, CreationAllowedFlag = 2 };
        var summoner = new CharacterClass { Number = 20, CanGetCreated = true, CreationAllowedFlag = 1 };
        var darkKnight = new CharacterClass { Number = 4, CanGetCreated = true };
        var bladeKnight = new CharacterClass { Number = 6, CreationAllowedFlag = 0 };
        var lockedFlagged = new CharacterClass { Number = 30, CreationAllowedFlag = 8 };
        player.GameContext.Configuration.CharacterClasses.Add(darkLord);
        player.GameContext.Configuration.CharacterClasses.Add(summoner);
        player.GameContext.Configuration.CharacterClasses.Add(darkKnight);
        player.GameContext.Configuration.CharacterClasses.Add(bladeKnight);
        player.GameContext.Configuration.CharacterClasses.Add(lockedFlagged);
        player.Account!.UnlockedCharacterClasses.Add(summoner);
        player.GameContext.PlugInManager.RegisterPlugIn<IPlayerStateChangedPlugIn, UnlockAllCharacterClassesPlugIn>();

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That(player.Account.UnlockedCharacterClasses, Is.EquivalentTo(new[] { summoner }));
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false), Is.True);

        Assert.That(player.Account.UnlockedCharacterClasses, Is.EquivalentTo(new[] { summoner, darkLord }));
    }
}
