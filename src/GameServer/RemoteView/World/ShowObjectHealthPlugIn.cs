// <copyright file="ShowObjectHealthPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IShowObjectHealthPlugIn"/> which sends the remaining health
/// of observed objects with the <see cref="ObjectHealthStatus"/> packet.
/// </summary>
/// <remarks>
/// The original season 6 client has no health bars for monsters and ignores this packet.
/// The client mod camera3d.dll of the PeroniaMU client draws a health bar above the object.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowObjectHealthPlugIn_Name), Description = nameof(PlugInResources.ShowObjectHealthPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("624defb3-51a8-4ea2-b7e9-9fa4ed522fc7")]
[MinimumClient(6, 3, ClientLanguage.English)]
public class ShowObjectHealthPlugIn : IShowObjectHealthPlugIn
{
    /// <summary>
    /// The value of <see cref="ObjectHealthStatus.HealthStatus"/> for full health.
    /// </summary>
    private const float FullHealthStatus = 250;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowObjectHealthPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowObjectHealthPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowObjectHealthAsync(IAttackable target, float healthRatio)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        // A living object never shows an empty bar.
        var healthStatus = (byte)Math.Clamp(Math.Ceiling(healthRatio * FullHealthStatus), 1, FullHealthStatus);
        await connection.SendObjectHealthStatusAsync(target.GetId(this._player), healthStatus).ConfigureAwait(false);
    }
}
