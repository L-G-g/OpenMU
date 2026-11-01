// <copyright file="ShowFlightStatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends <c>C1 06 FE [object id, big endian] [1 flying, 0 not]</c> when a player starts or stops flying,
/// or a flying player comes into view. This packet is not part of the original protocol; it's understood
/// by the client mod flight.dll of the PeroniaMU season 6 client. The original client ignores codes above 0xF9.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowFlightStatePlugIn_Name), Description = nameof(PlugInResources.ShowFlightStatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B8E2C1A-7D34-4F6B-9A0E-3C2D1F4B6A87")]
public class ShowFlightStatePlugIn : IShowFlightStatePlugIn
{
    private const byte Code = 0xFE;
    private const int Length = 6;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowFlightStatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowFlightStatePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowFlightStateAsync(Player player, bool isFlying)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var id = player.GetId(this._player);

        int Write()
        {
            var span = connection.Output.GetSpan(Length)[..Length];
            span[0] = 0xC1;
            span[1] = Length;
            span[2] = Code;
            BinaryPrimitives.WriteUInt16BigEndian(span[3..], id);
            span[5] = isFlying ? (byte)1 : (byte)0;
            return Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
