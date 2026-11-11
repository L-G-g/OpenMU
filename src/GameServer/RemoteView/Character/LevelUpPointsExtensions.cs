// <copyright file="LevelUpPointsExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Extensions to send the level up points which the original client can't know.
/// </summary>
internal static class LevelUpPointsExtensions
{
    /// <summary>
    /// Sends the real level up points with the <see cref="CharacterLevelUpPoints"/> packet, when the character
    /// has (or had, before adding stat points) more than the original client can know.
    /// The packets of the original protocol carry up to <see cref="ushort.MaxValue"/> points. The client mod
    /// points.dll keeps that many on the client, and shows the real amount; the original client ignores the packet.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="previousLevelUpPoints">The level up points before they changed, if they did.</param>
    public static async ValueTask SendLevelUpPointsAboveClientLimitAsync(this RemotePlayer player, int previousLevelUpPoints = 0)
    {
        if (player.SelectedCharacter is not { } selectedCharacter
            || Math.Max(selectedCharacter.LevelUpPoints, previousLevelUpPoints) <= ushort.MaxValue)
        {
            return;
        }

        await player.Connection.SendCharacterLevelUpPointsAsync((uint)Math.Max(0, selectedCharacter.LevelUpPoints)).ConfigureAwait(false);
    }
}
