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
            ? (ushort)this._player.Attributes![Stats.MaximumMana]
            : attribute == Stats.BaseVitality
                ? (ushort)this._player.Attributes![Stats.MaximumHealth]
                : default;
        var updatedMaximumShield = (ushort)this._player.Attributes![Stats.MaximumShield];
        var updatedMaximumAbility = (ushort)this._player.Attributes[Stats.MaximumAbility];

        if (addedPoints == 0)
        {
            await connection.SendCharacterStatIncreaseResponseAsync(false, statType, updatedDependentMaximumStat, updatedMaximumShield, updatedMaximumAbility).ConfigureAwait(false);
            return;
        }

        for (var i = 0; i < addedPoints; i++)
        {
            await connection.SendCharacterStatIncreaseResponseAsync(true, statType, updatedDependentMaximumStat, updatedMaximumShield, updatedMaximumAbility).ConfigureAwait(false);
        }
    }
}
