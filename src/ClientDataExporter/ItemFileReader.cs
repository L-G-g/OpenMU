// <copyright file="ItemFileReader.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ClientDataExporter;

using System.Buffers.Binary;
using System.IO;

/// <summary>
/// Reads the item definitions of the client's <c>Data\Local\item.bmd</c>.
/// </summary>
/// <remarks>
/// The file has a slot for each of the 16 groups with 512 numbers, also for the
/// unused ones. Its record layout changed between client versions, so only the
/// known layouts are accepted, identified by their record size.
/// </remarks>
public static class ItemFileReader
{
    private const int GroupCount = 16;
    private const int NumbersPerGroup = 512;
    private const ushort ChecksumKey = 0xE2F1;

    /// <summary>
    /// The record size of the layout of Season 6 Episode 3 (client 1.04d), with a 30 byte name.
    /// </summary>
    private const int SeasonSixRecordSize = 84;

    private const int NameLength = 30;
    private const int ClassCount = 7;
    private const int ResistanceCount = 8;

    /// <summary>
    /// Reads the defined items of the file. Empty slots are skipped.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="nameEncoding">The encoding of the item names, which depends on the language of the file.</param>
    /// <returns>The defined items, ordered by group and number.</returns>
    /// <exception cref="InvalidDataException">The file is damaged or has an unknown layout.</exception>
    public static IReadOnlyList<ItemRecord> Read(string path, Encoding nameEncoding)
    {
        var (records, recordSize) = BmdFile.ReadRecords(path, GroupCount * NumbersPerGroup, ChecksumKey);
        if (recordSize != SeasonSixRecordSize)
        {
            throw new InvalidDataException($"{path} has records of {recordSize} bytes. Only the layout of Season 6 Episode 3 ({SeasonSixRecordSize} bytes) is known yet.");
        }

        var result = new List<ItemRecord>();
        for (var index = 0; index < GroupCount * NumbersPerGroup; index++)
        {
            var record = records.AsSpan(index * recordSize, recordSize);
            var name = ReadName(record[..NameLength], nameEncoding);
            if (name.Length == 0)
            {
                continue;
            }

            result.Add(ParseSeasonSixRecord(index / NumbersPerGroup, index % NumbersPerGroup, name, record));
        }

        return result;
    }

    /// <remarks>
    /// The client reads the record directly into a C struct with the default alignment,
    /// which is why there are padding bytes before the 16 and 32 bit values.
    /// </remarks>
    private static ItemRecord ParseSeasonSixRecord(int group, int number, string name, ReadOnlySpan<byte> record)
    {
        return new ItemRecord(
            Group: group,
            Number: number,
            Name: name,
            TwoHanded: record[30] != 0,
            DropLevel: ReadUInt16(record, 32),
            ItemSlot: record[34],
            SkillNumber: ReadUInt16(record, 36),
            Width: record[38],
            Height: record[39],
            MinimumDamage: record[40],
            MaximumDamage: record[41],
            DefenseRate: record[42],
            Defense: record[43],
            MagicDefense: record[44],
            AttackSpeed: record[45],
            WalkSpeed: record[46],
            Durability: record[47],
            MagicDurability: record[48],
            MagicPower: record[49],
            RequiredStrength: ReadUInt16(record, 50),
            RequiredAgility: ReadUInt16(record, 52),
            RequiredEnergy: ReadUInt16(record, 54),
            RequiredVitality: ReadUInt16(record, 56),
            RequiredLeadership: ReadUInt16(record, 58),
            RequiredLevel: ReadUInt16(record, 60),
            Value: record[62],
            Zen: BinaryPrimitives.ReadInt32LittleEndian(record[64..]),
            AttackType: record[68],
            ClassRequirements: record.Slice(69, ClassCount).ToArray(),
            Resistances: record.Slice(69 + ClassCount, ResistanceCount).ToArray());
    }

    private static int ReadUInt16(ReadOnlySpan<byte> record, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(record[offset..]);
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        var end = bytes.IndexOf((byte)0);
        if (end >= 0)
        {
            bytes = bytes[..end];
        }

        return encoding.GetString(bytes).Trim();
    }
}
