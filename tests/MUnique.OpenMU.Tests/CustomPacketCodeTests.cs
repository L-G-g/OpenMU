// <copyright file="CustomPacketCodeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Reflection;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Tests that the server to client packets which aren't part of the original protocol don't share their codes.
/// </summary>
/// <remarks>
/// The original season 6 client ignores the codes above <see cref="LastOriginalCode"/>, so the client mods
/// (healthbars.dll, resets.dll, loot.dll, espectros.dll, flight.dll, points.dll ...) use them. Each mod hooks the
/// receiving of packets and takes the packets with its code (and sub code) away from the others, so two packets
/// with the same code and without different sub codes break one of the mods.
/// </remarks>
[TestFixture]
public class CustomPacketCodeTests
{
    private const byte LastOriginalCode = 0xF9;

    /// <summary>
    /// Tests that each custom code is used by one packet, or by packets with different sub codes.
    /// </summary>
    [Test]
    public void CustomPacketCodesAreUnique()
    {
        var packets = GetGeneratedCustomPackets().Concat(GetViewPlugInCustomPackets()).ToList();
        Assert.That(packets, Is.Not.Empty);

        var conflicts = new List<string>();
        foreach (var group in packets.GroupBy(p => p.Code))
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var (a, b) = (list[i], list[j]);
                    if (a.SubCodes.Count == 0 || b.SubCodes.Count == 0 || a.SubCodes.Overlaps(b.SubCodes))
                    {
                        conflicts.Add($"0x{group.Key:X2}: {a.Owner} and {b.Owner}");
                    }
                }
            }
        }

        Assert.That(conflicts, Is.Empty, "Custom packets share a code: " + string.Join("; ", conflicts));
    }

    private static IEnumerable<CustomPacket> GetGeneratedCustomPackets()
    {
        var types = typeof(CharacterResetCount).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CharacterResetCount).Namespace && t.IsValueType && !t.Name.EndsWith("Ref", StringComparison.Ordinal));
        foreach (var type in types)
        {
            if (GetStaticByte(type, "Code") is not { } code || code <= LastOriginalCode)
            {
                continue;
            }

            var subCodes = new HashSet<byte>();
            if (GetStaticByte(type, "SubCode") is { } subCode)
            {
                subCodes.Add(subCode);
            }

            yield return new CustomPacket(type.Name, code, subCodes);
        }
    }

    private static IEnumerable<CustomPacket> GetViewPlugInCustomPackets()
    {
        var types = typeof(RemotePlayer).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(typeof(RemotePlayer).Namespace!, StringComparison.Ordinal) ?? false);
        foreach (var type in types)
        {
            var constants = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(byte))
                .ToList();
            if (constants.FirstOrDefault(f => f.Name == "Code")?.GetRawConstantValue() is not byte code || code <= LastOriginalCode)
            {
                continue;
            }

            var subCodes = constants.Where(f => f.Name.EndsWith("SubCode", StringComparison.Ordinal))
                .Select(f => (byte)f.GetRawConstantValue()!)
                .ToHashSet();
            yield return new CustomPacket(type.Name, code, subCodes);
        }
    }

    private static byte? GetStaticByte(Type type, string propertyName)
    {
        return type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as byte?;
    }

    private sealed record CustomPacket(string Owner, byte Code, HashSet<byte> SubCodes);
}
