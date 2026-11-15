// <copyright file="ShowTokenShopViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.NPC;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.TokenMerchants;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IShowTokenShopPlugIn"/> for the tabs of tokenshop.dll in the PeroniaMU season 6 client.
/// </summary>
/// <remarks>
/// The packet isn't part of the original protocol, and the original client ignores it:
/// <c>C2 [length, 2 bytes big endian] FF 40 [tab] [tokens, 4 bytes big endian] [token name, 20 bytes, zero terminated] [count]</c>
/// and per item <c>[slot] [width] [height] [price, 2 bytes big endian]</c>.
/// It's sent after the list of the items of the shop, and again after each purchase. The tabs answer with the chat command /tab.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowTokenShopViewPlugIn_Name), Description = nameof(PlugInResources.ShowTokenShopViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5D9B2F74-1A6C-4E83-B7D0-8C3E6A1F9B52")]
public class ShowTokenShopViewPlugIn : IShowTokenShopPlugIn
{
    private const byte Code = 0xFF;
    private const byte SubCode = 0x40;
    private const int NameLength = 20;
    private const int HeaderLength = 11 + NameLength;
    private const int ItemLength = 5;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowTokenShopViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowTokenShopViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowTokenShopAsync(byte tab, uint tokens, string tokenName, IReadOnlyList<(byte Slot, byte Width, byte Height, ushort Price)> prices)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var count = Math.Min(prices.Count, byte.MaxValue);
        var length = HeaderLength + (count * ItemLength);

        int Write()
        {
            var span = connection.Output.GetSpan(length)[..length];
            span.Clear();
            span[0] = 0xC2;
            BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)length);
            span[3] = Code;
            span[4] = SubCode;
            span[5] = tab;
            BinaryPrimitives.WriteUInt32BigEndian(span[6..], tokens);

            // The last byte of the name stays 0, so that the text always ends.
            Encoding.Latin1.GetBytes(tokenName.AsSpan(0, Math.Min(tokenName.Length, NameLength - 1)), span[10..]);
            span[10 + NameLength] = (byte)count;
            for (var i = 0; i < count; i++)
            {
                var item = span[(HeaderLength + (i * ItemLength))..];
                item[0] = prices[i].Slot;
                item[1] = prices[i].Width;
                item[2] = prices[i].Height;
                BinaryPrimitives.WriteUInt16BigEndian(item[3..], prices[i].Price);
            }

            return length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
