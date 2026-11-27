// <copyright file="EventBoardChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command /eventos: sends the board of the next events to eventboard.dll of the client (key H).
/// With "/eventos chat", the events are written in the chat, for a client without eventboard.dll.
/// </summary>
[Guid("8D3F6A25-4B1E-4C97-A5D2-1E9C7B4F3A86")]
[PlugIn]
[Display(Name = "Chat command: /eventos", Description = "Sends the board of the next events to eventboard.dll of the client (key H). '/eventos chat' writes them in the chat.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class EventBoardChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/eventos";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var entries = EventBoard.GetEntries(player);
        if (command.Trim().EndsWith(" chat", StringComparison.OrdinalIgnoreCase))
        {
            if (entries.Count == 0)
            {
                await player.ShowBlueMessageAsync("No hay eventos programados.").ConfigureAwait(false);
            }

            foreach (var entry in entries)
            {
                var time = TimeSpan.FromSeconds(entry.Seconds);
                var text = entry.IsOpen
                    ? $"{entry.Name}: ABIERTO, cierra en {time:mm\\:ss}"
                    : $"{entry.Name}: abre en {(int)time.TotalHours}:{time:mm\\:ss}";
                await player.ShowBlueMessageAsync(text).ConfigureAwait(false);
            }

            return;
        }

        await player.InvokeViewPlugInAsync<IEventBoardViewPlugIn>(p => p.ShowEventBoardAsync(entries)).ConfigureAwait(false);
    }
}
