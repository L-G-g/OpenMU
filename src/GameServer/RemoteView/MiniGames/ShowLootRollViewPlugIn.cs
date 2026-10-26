// <copyright file="ShowLootRollViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.MiniGames;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IShowLootRollPlugIn"/> for the window of loot.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packets aren't part of the original protocol, and the original client ignores them:
/// <list type="bullet">
///   <item><c>C1 30 FC 00 [roll id, 2 bytes big endian] [seconds] [can need: 0 or 1] [item, 40 bytes text]</c>: a roll started.</item>
///   <item><c>C1 07 FC 01 [roll id, 2 bytes big endian] [won: 0 or 1]</c>: the player chose or the roll ended. When the player
///         won, the item is added to its inventory right after; loot.dll then avoids the "obtained" message of the client,
///         which would name the item which the player picked up last (or the Kris).</item>
/// </list>
/// The window answers with the chat commands /need, /greed and /pass and the roll id.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowLootRollViewPlugIn_Name), Description = nameof(PlugInResources.ShowLootRollViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3B7E9C21-6D4A-4F8E-A1C5-9E2D7B4F6A30")]
public class ShowLootRollViewPlugIn : IShowLootRollPlugIn
{
    private const byte Code = 0xFC;
    private const byte StartedSubCode = 0x00;
    private const byte ClosedSubCode = 0x01;
    private const int DescriptionLength = 40;
    private const int StartedLength = 8 + DescriptionLength;
    private const int ClosedLength = 7;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowLootRollViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowLootRollViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowLootRollAsync(int rollId, Item item, string description, TimeSpan duration, bool canNeed)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var span = connection.Output.GetSpan(StartedLength)[..StartedLength];
            span.Clear();
            WriteHeader(span, StartedLength, StartedSubCode, rollId);
            span[6] = (byte)Math.Clamp(Math.Ceiling(duration.TotalSeconds), 0, byte.MaxValue);
            span[7] = canNeed ? (byte)1 : (byte)0;

            // The last byte stays 0, so that the text always ends.
            Encoding.Latin1.GetBytes(description.AsSpan(0, Math.Min(description.Length, DescriptionLength - 1)), span[8..]);
            return StartedLength;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CloseLootRollAsync(int rollId, bool won)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var span = connection.Output.GetSpan(ClosedLength)[..ClosedLength];
            WriteHeader(span, ClosedLength, ClosedSubCode, rollId);
            span[6] = won ? (byte)1 : (byte)0;
            return ClosedLength;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static void WriteHeader(Span<byte> span, int length, byte subCode, int rollId)
    {
        span[0] = 0xC1;
        span[1] = (byte)length;
        span[2] = Code;
        span[3] = subCode;
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], (ushort)rollId);
    }
}
