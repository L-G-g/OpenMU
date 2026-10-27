// <copyright file="ShowSpectresViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Spectres;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IShowSpectresPlugIn"/> for espectros.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packets aren't part of the original protocol, and the original client ignores them:
/// <list type="bullet">
///   <item><c>C1 05 FD 01 [object id, 2 bytes big endian]</c>: the object is a spectre; the client draws it transparent.
///         It's repeated every few seconds while the spectre is in view.</item>
///   <item><c>C2 [length, 2 bytes big endian] FD 00</c> and an entry for each kind of spectre (warrior, mage, elf): the
///         state of the spectres of the player, for the spectre window. An entry has <see cref="EntryLength"/> bytes:
///         kind, summoned (0 or 1), name (11 bytes text), level (2 bytes), resets (2 bytes), level up points (4 bytes),
///         strength, agility, vitality and energy (4 bytes each), the four percentages (1 byte each), and then
///         the 12 equipment slots of <see cref="SlotLength"/> bytes: present (0 or 1), item type (2 bytes, group * 512 + number),
///         item level, excellent options, ancient options, the item as the client knows it (12 bytes), and its
///         description (40 bytes text). The numbers of the entries are little endian.</item>
/// </list>
/// The window answers with the chat command /espectro.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowSpectresViewPlugIn_Name), Description = nameof(PlugInResources.ShowSpectresViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C4A1E7B2-3F5D-4E8A-9B6C-2D7F1A3E5B90")]
public class ShowSpectresViewPlugIn : IShowSpectresPlugIn
{
    private const byte Code = 0xFD;
    private const byte PanelSubCode = 0x00;
    private const byte SpectreSubCode = 0x01;
    private const int SpectreLength = 6;
    private const int PanelHeaderLength = 5;
    private const int NameLength = 11;
    private const int ItemDataLength = 12;
    private const int DescriptionLength = 40;
    private const int SlotLength = 6 + ItemDataLength + DescriptionLength;
    private const int EntryHeaderLength = 41;
    private const int EntryLength = EntryHeaderLength + (SpectrePanelEntry.EquipmentSlots * SlotLength);

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowSpectresViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowSpectresViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowSpectreAsync(SpectrePlayer spectre)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var id = spectre.GetId(this._player);

        int Write()
        {
            var span = connection.Output.GetSpan(SpectreLength)[..SpectreLength];
            span[0] = 0xC1;
            span[1] = SpectreLength;
            span[2] = Code;
            span[3] = SpectreSubCode;
            BinaryPrimitives.WriteUInt16BigEndian(span[4..], id);
            return SpectreLength;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowSpectrePanelAsync(IReadOnlyList<SpectrePanelEntry> entries)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var serializer = this._player.ItemSerializer;
        var length = PanelHeaderLength + (entries.Count * EntryLength);

        int Write()
        {
            var span = connection.Output.GetSpan(length)[..length];
            span.Clear();
            span[0] = 0xC2;
            BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)length);
            span[3] = Code;
            span[4] = PanelSubCode;
            var itemData = new byte[Math.Max(serializer.NeededSpace, ItemDataLength)];
            for (var i = 0; i < entries.Count; i++)
            {
                WriteEntry(span.Slice(PanelHeaderLength + (i * EntryLength), EntryLength), entries[i], serializer, itemData);
            }

            return length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static void WriteEntry(Span<byte> span, SpectrePanelEntry entry, IItemSerializer serializer, byte[] itemData)
    {
        span[0] = (byte)entry.Kind;
        span[1] = entry.Summoned ? (byte)1 : (byte)0;
        WriteText(span.Slice(2, NameLength), entry.Name);
        BinaryPrimitives.WriteUInt16LittleEndian(span[13..], (ushort)Math.Clamp(entry.Level, 0, ushort.MaxValue));
        BinaryPrimitives.WriteUInt16LittleEndian(span[15..], (ushort)Math.Clamp(entry.Resets, 0, ushort.MaxValue));
        BinaryPrimitives.WriteInt32LittleEndian(span[17..], entry.LevelUpPoints);
        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[(21 + (i * 4))..], entry.Stats[i]);
            span[37 + i] = entry.Percentages[i];
        }

        for (var slot = 0; slot < SpectrePanelEntry.EquipmentSlots; slot++)
        {
            if (entry.Equipment[slot] is not { Definition: { } definition } item)
            {
                continue;
            }

            var slotSpan = span.Slice(EntryHeaderLength + (slot * SlotLength), SlotLength);
            Array.Clear(itemData);
            serializer.SerializeItem(itemData, item);
            slotSpan[0] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(slotSpan[1..], (ushort)((definition.Group * 512) + definition.Number));
            slotSpan[3] = item.Level;
            slotSpan[4] = (byte)(itemData[3] & 0x3F);
            slotSpan[5] = itemData[4];
            itemData.AsSpan(0, ItemDataLength).CopyTo(slotSpan[6..]);
            WriteText(slotSpan.Slice(6 + ItemDataLength, DescriptionLength), entry.Descriptions[slot]);
        }
    }

    private static void WriteText(Span<byte> target, string text)
    {
        // The last byte stays 0, so that the text always ends.
        Encoding.Latin1.GetBytes(text.AsSpan(0, Math.Min(text.Length, target.Length - 1)), target);
    }
}
