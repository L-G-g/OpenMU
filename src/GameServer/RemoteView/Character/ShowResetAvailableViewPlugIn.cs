// <copyright file="ShowResetAvailableViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IShowResetAvailablePlugIn"/> for the reset button of resetbutton.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packet isn't part of the original protocol, and the original client ignores it:
/// <c>C1 05 FF 00 [available: 0 or 1]</c>. The code 0xFF is meant for the windows and buttons of the PeroniaMU
/// interface; the sub code 0x00 is the reset button. The button answers with the chat command /reset.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowResetAvailableViewPlugIn_Name), Description = nameof(PlugInResources.ShowResetAvailableViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A4D2F6B1-7C3E-4B58-9E1A-6F0C2D8B5E47")]
public class ShowResetAvailableViewPlugIn : IShowResetAvailablePlugIn
{
    private const byte Code = 0xFF;
    private const byte ResetButtonSubCode = 0x00;
    private const int Length = 5;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowResetAvailableViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowResetAvailableViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowResetAvailableAsync(bool available)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var span = connection.Output.GetSpan(Length)[..Length];
            span[0] = 0xC1;
            span[1] = Length;
            span[2] = Code;
            span[3] = ResetButtonSubCode;
            span[4] = available ? (byte)1 : (byte)0;
            return Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
