// <copyright file="SkillQuestViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="ISkillQuestViewPlugIn"/> for quests.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packets aren't part of the original protocol, and the original client ignores them. They use the code 0xFF of the
/// PeroniaMU interface (sub code 0x00 is the reset button):
/// <list type="bullet">
///   <item><c>C2 [length, 2 bytes big endian] FF 10 [count]</c> and per NPC <c>[object id, 2 bytes big endian] [mark]</c>
///         (1 = quest available, 2 = in progress, 3 = ready to deliver): the marks above the NPCs. They're repeated
///         every few seconds while they're shown; an empty list removes them.</item>
///   <item><c>C2 [length] FF 11 [count]</c> and per line <c>[color] [text, zero terminated]</c>: the tracker of the active
///         quests; an empty list hides it.</item>
///   <item><c>C2 [length] FF 12 [object id, 2 bytes big endian] [action] [title, zero terminated] [count]</c> and the lines
///         like in 0x11: the quest window of the NPC. The action is the button: 0 = none, 1 = accept, 2 = deliver.</item>
/// </list>
/// Colors: 0 = white, 1 = gold, 2 = green, 3 = gray. Texts are Latin-1. The client answers with the chat command /quest.
/// </remarks>
[PlugIn]
[Display(Name = "Skill quest view (quests.dll)", Description = "Sends the marks above the NPCs, the tracker and the quest window of the skill quests to quests.dll of the PeroniaMU client (packets C2 FF 10, 11 and 12).")]
[Guid("E3B7A1C5-9D2F-4A6E-8B4C-1F5D7A9E2C64")]
public class SkillQuestViewPlugIn : ISkillQuestViewPlugIn
{
    private const byte Code = 0xFF;
    private const byte MarksSubCode = 0x10;
    private const byte TrackerSubCode = 0x11;
    private const byte DialogSubCode = 0x12;
    private const int HeaderLength = 5;
    private const int MaximumLineLength = 80;
    private const int MaximumLines = 40;
    private const int MaximumMarks = 60;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillQuestViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public SkillQuestViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowMarksAsync(IReadOnlyList<(NonPlayerCharacter Npc, SkillQuestMark Mark)> marks)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var entries = marks.Take(MaximumMarks).Select(m => (Id: m.Npc.GetId(this._player), m.Mark)).ToList();
        var length = HeaderLength + 1 + (entries.Count * 3);

        int Write()
        {
            var span = connection.Output.GetSpan(length)[..length];
            WriteHeader(span, length, MarksSubCode);
            span[HeaderLength] = (byte)entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = span[(HeaderLength + 1 + (i * 3))..];
                BinaryPrimitives.WriteUInt16BigEndian(entry, entries[i].Id);
                entry[2] = (byte)entries[i].Mark;
            }

            return length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowTrackerAsync(IReadOnlyList<SkillQuestLine> lines)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var encodedLines = EncodeLines(lines);
        var length = HeaderLength + 1 + encodedLines.Sum(l => l.Length);

        int Write()
        {
            var span = connection.Output.GetSpan(length)[..length];
            WriteHeader(span, length, TrackerSubCode);
            span[HeaderLength] = (byte)encodedLines.Count;
            WriteLines(span[(HeaderLength + 1)..], encodedLines);
            return length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowDialogAsync(NonPlayerCharacter npc, SkillQuestDialog dialog)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var id = npc.GetId(this._player);
        var title = Encode(dialog.Title);
        var encodedLines = EncodeLines(dialog.Lines);
        var length = HeaderLength + 3 + title.Length + 1 + 1 + encodedLines.Sum(l => l.Length);

        int Write()
        {
            var span = connection.Output.GetSpan(length)[..length];
            WriteHeader(span, length, DialogSubCode);
            BinaryPrimitives.WriteUInt16BigEndian(span[HeaderLength..], id);
            span[HeaderLength + 2] = (byte)dialog.Action;
            var offset = HeaderLength + 3;
            title.CopyTo(span[offset..]);
            offset += title.Length;
            span[offset++] = 0;
            span[offset++] = (byte)encodedLines.Count;
            WriteLines(span[offset..], encodedLines);
            return length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static void WriteHeader(Span<byte> span, int length, byte subCode)
    {
        span.Clear();
        span[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)length);
        span[3] = Code;
        span[4] = subCode;
    }

    private static byte[] Encode(string text)
    {
        return Encoding.Latin1.GetBytes(text.Length > MaximumLineLength ? text[..MaximumLineLength] : text);
    }

    /// <summary>
    /// Encodes the lines as [color] [text] [0].
    /// </summary>
    private static List<byte[]> EncodeLines(IReadOnlyList<SkillQuestLine> lines)
    {
        return lines.Take(MaximumLines).Select(line =>
        {
            var text = Encode(line.Text);
            var entry = new byte[text.Length + 2];
            entry[0] = (byte)line.Color;
            text.CopyTo(entry, 1);
            return entry;
        }).ToList();
    }

    private static void WriteLines(Span<byte> span, List<byte[]> lines)
    {
        var offset = 0;
        foreach (var line in lines)
        {
            line.CopyTo(span[offset..]);
            offset += line.Length;
        }
    }
}
