// <copyright file="LootChoiceChatCommandPlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// Base class of the chat commands which choose need, greed or pass for a rolled item of a dungeon.
/// The command can be followed by the id of the roll; without it, it's for the oldest roll which waits for the choice.
/// </summary>
public abstract class LootChoiceChatCommandPlugInBase : IChatCommandPlugIn
{
    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Gets the choice of the command.
    /// </summary>
    protected abstract LootChoice Choice { get; }

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (player.CurrentMiniGame is not DungeonContext dungeon)
        {
            await player.ShowBlueMessageAsync(DungeonMessages.NotInDungeon).ConfigureAwait(false);
            return;
        }

        var argument = command.Length > this.Key.Length ? command[this.Key.Length..].Trim() : string.Empty;
        int? rollId = int.TryParse(argument, out var id) ? id : null;
        await dungeon.Loot.ChooseAsync(player, this.Choice, rollId).ConfigureAwait(false);
    }
}
