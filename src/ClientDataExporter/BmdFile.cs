// <copyright file="BmdFile.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ClientDataExporter;

using System.Buffers.Binary;
using System.IO;

/// <summary>
/// Reads the table files (<c>*.bmd</c>) of the game client which consist of a fixed
/// number of fixed-size records, followed by a checksum.
/// </summary>
/// <remarks>
/// The records are obfuscated by a XOR with a repeating three byte key, which starts
/// again at every record. The checksum is calculated over the obfuscated records
/// and is keyed differently per file type.
/// </remarks>
public static class BmdFile
{
    private static readonly byte[] XorKey = [0xFC, 0xCF, 0xAB];

    /// <summary>
    /// Reads the records of a table file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="recordCount">The number of records the file contains.</param>
    /// <param name="checksumKey">The checksum key of this file type.</param>
    /// <returns>The decoded records, one after the other, and the size of each record.</returns>
    /// <exception cref="InvalidDataException">The file size doesn't fit the record count, or the checksum doesn't match.</exception>
    public static (byte[] Records, int RecordSize) ReadRecords(string path, int recordCount, ushort checksumKey)
    {
        var content = File.ReadAllBytes(path);
        var recordsLength = content.Length - sizeof(uint);
        if (recordsLength <= 0 || recordsLength % recordCount != 0)
        {
            throw new InvalidDataException($"The size of {path} ({content.Length} bytes) doesn't fit {recordCount} records and a checksum.");
        }

        var records = content.AsSpan(0, recordsLength);
        var expectedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(recordsLength));
        var actualChecksum = CalculateChecksum(records, checksumKey);
        if (actualChecksum != expectedChecksum)
        {
            throw new InvalidDataException($"The checksum of {path} doesn't match. Expected {expectedChecksum:X8}, calculated {actualChecksum:X8}.");
        }

        var recordSize = recordsLength / recordCount;
        for (var offset = 0; offset < recordsLength; offset += recordSize)
        {
            Decode(records.Slice(offset, recordSize));
        }

        return (records.ToArray(), recordSize);
    }

    /// <summary>
    /// Calculates the checksum over the (still obfuscated) records of a table file.
    /// </summary>
    /// <param name="data">The obfuscated records.</param>
    /// <param name="key">The checksum key of the file type.</param>
    /// <returns>The checksum.</returns>
    public static uint CalculateChecksum(ReadOnlySpan<byte> data, ushort key)
    {
        uint result = (uint)key << 9;
        for (var offset = 0; offset + sizeof(uint) <= data.Length; offset += sizeof(uint))
        {
            var block = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
            var blockIndex = offset / sizeof(uint);
            if ((blockIndex + key) % 2 == 0)
            {
                result ^= block;
            }
            else
            {
                result += block;
            }

            if (offset % 16 == 0)
            {
                result ^= (key + result) >> ((blockIndex % 8) + 1);
            }
        }

        return result;
    }

    /// <summary>
    /// Removes or applies the XOR obfuscation of one record. Applying it twice restores the original.
    /// </summary>
    /// <param name="record">The record.</param>
    public static void Decode(Span<byte> record)
    {
        for (var i = 0; i < record.Length; i++)
        {
            record[i] ^= XorKey[i % XorKey.Length];
        }
    }
}
