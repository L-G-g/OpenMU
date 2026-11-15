// <copyright file="TokensChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command /tokens which shows the tokens of the dungeons which the character has.
/// A game master can add tokens to its own character for tests: /tokens [dungeon number] [amount].
/// </summary>
[Guid("9A4D1B67-3C58-4E2F-8B90-5F6E2D7C1A43")]
[PlugIn]
[Display(Name = nameof(PlugInResources.TokensChatCommandPlugIn_Name), Description = nameof(PlugInResources.TokensChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class TokensChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/tokens";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (TokenMerchantFeaturePlugIn.Get(player.GameContext) is not { Configuration: { } configuration }
            || player.SelectedCharacter is not { } character)
        {
            return;
        }

        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (arguments.Length == 3
            && character.CharacterStatus == CharacterStatus.GameMaster
            && byte.TryParse(arguments[1], out var dungeonNumber)
            && int.TryParse(arguments[2], out var amount)
            && configuration.FindByDungeon(dungeonNumber) is not null)
        {
            TokenWallet.AddTokens(player, dungeonNumber, amount);
        }

        foreach (var merchant in configuration.Merchants)
        {
            await player.ShowBlueMessageAsync(string.Format(TokenMerchantMessages.TokenLineFormat, merchant.TokenName, TokenWallet.GetTokens(character, merchant.Dungeon))).ConfigureAwait(false);
        }
    }
}
