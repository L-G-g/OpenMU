// <copyright file="EventBoardViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.PlugIns.EventRework;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IEventBoardViewPlugIn"/> for eventboard.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packet isn't part of the original protocol, and the original client ignores it. It uses the code 0xFF of the
/// PeroniaMU interface:
/// <c>C2 [length, 2 bytes big endian] FF 50 [count] then per event: [open: 0 or 1] [seconds, 4 bytes big endian] [name]</c>.
/// The name is Latin-1 and ends with a zero. The seconds are until the entrance opens, or, when it's open, until it closes.
/// The client asks for it with the chat command <c>/eventos</c> when the key H opens the board.
/// </remarks>
[PlugIn]
[Display(Name = "Event board view (eventboard.dll)", Description = "Sends the board of the next events to eventboard.dll of the PeroniaMU client (packet C2 FF 50).")]
[Guid("C4A7E2B9-1D6F-4E83-9B5A-3F8D2C7E6B14")]
public class EventBoardViewPlugIn : IEventBoardViewPlugIn
{
    /// <summary>
    /// The code of the packets of the PeroniaMU interface.
    /// </summary>
    internal const byte Code = 0xFF;

    /// <summary>
    /// The sub code of the event board packet.
    /// </summary>
    internal const byte BoardSubCode = 0x50;

    private const int HeaderLength = 6;
    private const int MaximumNameLength = 30;
    private const int MaximumEntries = 20;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventBoardViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public EventBoardViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowEventBoardAsync(IReadOnlyList<EventBoardEntry> entries)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var data = Serialize(entries);

        int Write()
        {
            var span = connection.Output.GetSpan(data.Length)[..data.Length];
            data.CopyTo(span);
            return data.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes the packet of the board.
    /// </summary>
    /// <param name="entries">The events.</param>
    /// <returns>The packet.</returns>
    internal static byte[] Serialize(IReadOnlyList<EventBoardEntry> entries)
    {
        var names = entries.Take(MaximumEntries)
            .Select(e => (Entry: e, Name: Encoding.Latin1.GetBytes(e.Name.Length > MaximumNameLength ? e.Name[..MaximumNameLength] : e.Name)))
            .ToList();
        var length = HeaderLength + names.Sum(n => 5 + n.Name.Length + 1);
        var data = new byte[length];
        data[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(1), (ushort)length);
        data[3] = Code;
        data[4] = BoardSubCode;
        data[5] = (byte)names.Count;
        var offset = HeaderLength;
        foreach (var (entry, name) in names)
        {
            data[offset] = entry.IsOpen ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset + 1), Math.Max(entry.Seconds, 0));
            offset += 5;
            name.CopyTo(data, offset);
            offset += name.Length + 1;
        }

        return data;
    }
}
