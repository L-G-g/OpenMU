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
