// <copyright file="StatIncreaseResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IStatIncreaseResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.StatIncreaseResultPlugIn_Name), Description = nameof(PlugInResources.StatIncreaseResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ce603b3c-cf25-426f-9cb9-5cc367843de8")]
public class StatIncreaseResultPlugIn : IStatIncreaseResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatIncreaseResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public StatIncreaseResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    /// <remarks>
    /// The response packet of older clients can only express one added point. When
    /// multiple points were added at once (e.g. by the <c>/add</c> chat command), we
    /// send one response per point, so that the client adds them one by one.
    /// The previous workaround re-sent the character information and warped the
    /// player, which made the client of season 6 jump to the start of the map.
    /// </remarks>
    public async ValueTask StatIncreaseResultAsync(AttributeDefinition attribute, ushort addedPoints)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        var statType = attribute.GetStatType();
        var updatedDependentMaximumStat = attribute == Stats.BaseEnergy
            ? this._player.Attributes![Stats.MaximumMana].ToUInt16Clamped()
            : attribute == Stats.BaseVitality
                ? this._player.Attributes![Stats.MaximumHealth].ToUInt16Clamped()
                : default;
        var updatedMaximumShield = this._player.Attributes![Stats.MaximumShield].ToUInt16Clamped();
        var updatedMaximumAbility = this._player.Attributes[Stats.MaximumAbility].ToUInt16Clamped();

        if (addedPoints == 0)
        {
            await connection.SendCharacterStatIncreaseResponseAsync(false, statType, updatedDependentMaximumStat, updatedMaximumShield, updatedMaximumAbility).ConfigureAwait(false);
            return;
        }

        for (var i = 0; i < addedPoints; i++)
        {
            await connection.SendCharacterStatIncreaseResponseAsync(true, statType, updatedDependentMaximumStat, updatedMaximumShield, updatedMaximumAbility).ConfigureAwait(false);
        }

        await this.ResendLevelUpPointsIfClientRanOutAsync(addedPoints).ConfigureAwait(false);
    }

    /// <summary>
    /// The client only knows up to <see cref="ushort.MaxValue"/> level up points and counts them
    /// down by itself when points are added. With more points, it would show 0 while the character
    /// still has points left. Whenever the points fall below a multiple of <see cref="ushort.MaxValue"/>,
    /// the client has at most that many points left, so we send it the current amount again.
    /// The level update packet is the only one of these clients which sets the points without
    /// warping the character, but it plays the level up effect, so we send it as rarely as possible.
    /// </summary>
    private async ValueTask ResendLevelUpPointsIfClientRanOutAsync(ushort addedPoints)
    {
        if (this._player.SelectedCharacter is not { } selectedCharacter
            || this._player.Attributes is not { } charStats
            || this._player.Connection is not { } connection)
        {
            return;
        }

        var pointsAfter = selectedCharacter.LevelUpPoints;
        var pointsBefore = pointsAfter + addedPoints;
        if (pointsBefore <= ushort.MaxValue
            || (pointsBefore - 1) / ushort.MaxValue == (pointsAfter - 1) / ushort.MaxValue)
        {
            return;
        }

        await connection.SendCharacterLevelUpdateAsync(
            (ushort)charStats[Stats.Level],
            (ushort)Math.Clamp(pointsAfter, 0, ushort.MaxValue),
            charStats[Stats.MaximumHealth].ToUInt16Clamped(),
            charStats[Stats.MaximumMana].ToUInt16Clamped(),
            charStats[Stats.MaximumShield].ToUInt16Clamped(),
            charStats[Stats.MaximumAbility].ToUInt16Clamped(),
            (ushort)selectedCharacter.UsedFruitPoints,
            selectedCharacter.GetMaximumFruitPoints(),
            (ushort)selectedCharacter.UsedNegFruitPoints,
            selectedCharacter.GetMaximumFruitPoints()).ConfigureAwait(false);
    }
}
