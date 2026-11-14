// <copyright file="TabChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command /tab which shows another tab of the opened token merchant, see <see cref="TokenMerchantShop"/>.
/// The tabs of tokenshop.dll in the PeroniaMU client send it; without them, the players can type it.
/// </summary>
[Guid("2E8C6A40-9F13-4D75-B6A2-7D1E4C9B3F85")]
[PlugIn]
[Display(Name = nameof(PlugInResources.TabChatCommandPlugIn_Name), Description = nameof(PlugInResources.TabChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class TabChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/tab";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (TokenMerchantFeaturePlugIn.Get(player.GameContext) is not { Configuration: { } configuration } plugIn)
        {
            return;
        }

        var argument = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
        var tab = int.TryParse(argument, out var number) ? number - 1 : -1;
        await plugIn.Shop.SwitchTabAsync(player, configuration, tab).ConfigureAwait(false);
    }
}
