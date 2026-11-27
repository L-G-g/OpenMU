// <copyright file="StorySceneViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Story;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IStorySceneViewPlugIn"/> for cinematicas.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packet isn't part of the original protocol, and the original client ignores it. It uses the code 0xFF of the
/// PeroniaMU interface:
/// <c>C2 [length, 2 bytes big endian] FF 30 [object id, 2 bytes big endian] [page count] [button count]
/// [title] [note] [pages] [buttons]</c>. The texts are Latin-1 and end with a zero; the note may be empty. A button is
/// <c>[id] [label]</c>. The client shows the pages one after another and the buttons on the last page. A button with
/// an id above 0 is answered with the chat command <c>/historia [id] [object id]</c>; the id 0 just closes the scene.
/// </remarks>
[PlugIn]
[Display(Name = "Story scene view (cinematicas.dll)", Description = "Sends the scenes of the story mode to cinematicas.dll of the PeroniaMU client (packet C2 FF 30).")]
[Guid("5C1E8A34-7B2F-4D69-9E3A-8F4B2C6D1A75")]
public class StorySceneViewPlugIn : IStorySceneViewPlugIn
{
    /// <summary>
    /// The code of the packets of the PeroniaMU interface.
    /// </summary>
    internal const byte Code = 0xFF;

    /// <summary>
    /// The sub code of the scene packet.
    /// </summary>
    internal const byte SceneSubCode = 0x30;

    /// <summary>
    /// The sub code of the packet of a memory.
    /// </summary>
    internal const byte MemorySubCode = 0x31;

    /// <summary>
    /// The sub code of the packet which makes the screen black before a memory.
    /// </summary>
    internal const byte CurtainSubCode = 0x32;

    private const int HeaderLength = 9;
    private const int MaximumTitleLength = 30;
    private const int MaximumNoteLength = 90;
    private const int MaximumPageLength = 600;
    private const int MaximumLabelLength = 24;
    private const int MaximumPages = 40;
    private const int MaximumButtons = 3;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorySceneViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public StorySceneViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowSceneAsync(NonPlayerCharacter npc, StoryScene scene)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var data = Serialize(npc.GetId(this._player), scene);

        int Write()
        {
            var span = connection.Output.GetSpan(data.Length)[..data.Length];
            data.CopyTo(span);
            return data.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowCurtainAsync()
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var span = connection.Output.GetSpan(4)[..4];
            span[0] = 0xC1;
            span[1] = 4;
            span[2] = Code;
            span[3] = CurtainSubCode;
            return 4;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowMemoryAsync(StoryMemory memory)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var data = SerializeMemory(memory);

        int Write()
        {
            var span = connection.Output.GetSpan(data.Length)[..data.Length];
            data.CopyTo(span);
            return data.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes a memory to the packet C2 FF 31.
    /// </summary>
    /// <param name="memory">The memory.</param>
    /// <returns>The packet.</returns>
    /// <remarks>
    /// After the header: the tile at which the camera looks (x, y), the height of its ground, the yaw, the tilt in tenths
    /// of a degree and the distance (four little endian shorts), the number of lines, and the texts (Latin-1, each ends
    /// with 0): the sound which replaces the music of the map (may be empty), the label of the last button and, for each line, a flag (1: the client tells the server when it shows
    /// the line, with <c>/historia linea</c>), the name of the actor and what it says. If lines change the sound, the
    /// number of them follows, and for each: the index of the line, 1 if the sound loops (0: it plays once), and the sound.
    /// </remarks>
    internal static byte[] SerializeMemory(StoryMemory memory)
    {
        const int memoryHeaderLength = 16;
        var sound = Encode(memory.Sound ?? string.Empty, MaximumNoteLength);
        var endLabel = Encode(memory.EndLabel, MaximumLabelLength);
        var lines = memory.Lines.Take(MaximumPages).Select((line, index) => (
            // The client also tells the last line, which finishes the step of the memory.
            Notify: index > 0 && (index == Math.Min(memory.Lines.Count, MaximumPages) - 1 || memory.Actors.Any(a => a.EntersAtLine == index) || (memory.Cues?.Any(c => c.Line == index) ?? false)),
            Title: Encode(memory.Actors[line.Actor].Title, MaximumTitleLength),
            Text: Encode(line.Text, MaximumPageLength))).ToList();
        var lineSounds = memory.Lines.Take(MaximumPages)
            .Select((line, index) => (Index: index, line.Sound, line.LoopsSound))
            .Where(l => l.Index > 0 && l.Sound is not null)
            .Select(l => (Index: (byte)l.Index, Loops: l.LoopsSound, Path: Encode(l.Sound!, MaximumNoteLength)))
            .ToList();
        var length = memoryHeaderLength + sound.Length + 1 + endLabel.Length + 1 + lines.Sum(l => 1 + l.Title.Length + 1 + l.Text.Length + 1)
            + (lineSounds.Count > 0 ? 1 + lineSounds.Sum(l => 2 + l.Path.Length + 1) : 0);
        var data = new byte[length];
        data[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(1), (ushort)length);
        data[3] = Code;
        data[4] = MemorySubCode;
        data[5] = memory.Camera.Target.X;
        data[6] = memory.Camera.Target.Y;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(7), memory.Camera.Height);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(9), memory.Camera.Yaw);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(11), (short)Math.Round(memory.Camera.Tilt * 10));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(13), memory.Camera.Distance);
        data[15] = (byte)lines.Count;
        var offset = memoryHeaderLength;
        sound.CopyTo(data, offset);
        offset += sound.Length + 1;
        endLabel.CopyTo(data, offset);
        offset += endLabel.Length + 1;
        foreach (var (notify, title, text) in lines)
        {
            data[offset++] = notify ? (byte)1 : (byte)0;
            title.CopyTo(data, offset);
            offset += title.Length + 1;
            text.CopyTo(data, offset);
            offset += text.Length + 1;
        }

        if (lineSounds.Count > 0)
        {
            // Older clients stop reading after the lines.
            data[offset++] = (byte)lineSounds.Count;
            foreach (var (index, loops, path) in lineSounds)
            {
                data[offset++] = index;
                data[offset++] = loops ? (byte)1 : (byte)0;
                path.CopyTo(data, offset);
                offset += path.Length + 1;
            }
        }

        return data;
    }

    /// <summary>
    /// Serializes the packet of a scene.
    /// </summary>
    /// <param name="objectId">The id of the NPC.</param>
    /// <param name="scene">The scene.</param>
    /// <returns>The packet.</returns>
    internal static byte[] Serialize(ushort objectId, StoryScene scene)
    {
        var title = Encode(scene.Title, MaximumTitleLength);
        var note = Encode(scene.Note ?? string.Empty, MaximumNoteLength);
        var pages = scene.Pages.Take(MaximumPages).Select(page => Encode(page, MaximumPageLength)).ToList();
        var buttons = scene.Buttons.Take(MaximumButtons).Select(button => (button.Id, Label: Encode(button.Label, MaximumLabelLength))).ToList();
        var length = HeaderLength + title.Length + 1 + note.Length + 1 + pages.Sum(p => p.Length + 1) + buttons.Sum(b => b.Label.Length + 2);
        var data = new byte[length];
        data[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(1), (ushort)length);
        data[3] = Code;
        data[4] = SceneSubCode;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(5), objectId);
        data[7] = (byte)pages.Count;
        data[8] = (byte)buttons.Count;
        var offset = HeaderLength;
        foreach (var text in new[] { title, note }.Concat(pages))
        {
            text.CopyTo(data, offset);
            offset += text.Length + 1;
        }

        foreach (var (id, label) in buttons)
        {
            data[offset++] = id;
            label.CopyTo(data, offset);
            offset += label.Length + 1;
        }

        return data;
    }

    private static byte[] Encode(string text, int maximumLength)
    {
        return Encoding.Latin1.GetBytes(text.Length > maximumLength ? text[..maximumLength] : text);
    }
}
