// <copyright file="TokenWallet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The tokens of the dungeons which a character has. They are no items: each dungeon has an attribute of the character
/// which counts them (<see cref="Stats.GetDungeonTokens"/>), so they need no place in the inventory and can't be traded.
/// </summary>
public static class TokenWallet
{
    /// <summary>
    /// Gets the number of tokens of a dungeon which the character has.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="dungeonNumber">The number of the dungeon.</param>
    /// <returns>The number of tokens.</returns>
    public static int GetTokens(Character character, byte dungeonNumber)
    {
        var attributeId = Stats.GetDungeonTokens(dungeonNumber).Id;
        return (int)(character.Attributes.FirstOrDefault(a => a.Definition?.Id == attributeId)?.Value ?? 0);
    }

    /// <summary>
    /// Adds tokens of a dungeon to the selected character of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="dungeonNumber">The number of the dungeon.</param>
    /// <param name="amount">The number of tokens to add.</param>
    /// <returns>The new number of tokens, or <c>null</c> if they couldn't be added.</returns>
    public static int? AddTokens(Player player, byte dungeonNumber, int amount)
    {
        if (amount <= 0 || GetOrCreate(player, dungeonNumber) is not { } stat)
        {
            return null;
        }

        stat.Value = Math.Max(0, (int)stat.Value) + amount;
        return (int)stat.Value;
    }

    /// <summary>
    /// Takes tokens of a dungeon from the selected character of the player, if it has enough of them.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="dungeonNumber">The number of the dungeon.</param>
    /// <param name="amount">The number of tokens to take.</param>
    /// <returns><c>true</c>, if the character had enough tokens and they were taken.</returns>
    public static bool TrySpendTokens(Player player, byte dungeonNumber, int amount)
    {
        if (amount < 0 || player.SelectedCharacter is not { } character || GetTokens(character, dungeonNumber) < amount)
        {
            return false;
        }

        if (amount > 0 && GetOrCreate(player, dungeonNumber) is { } stat)
        {
            stat.Value = (int)stat.Value - amount;
        }

        return true;
    }

    private static StatAttribute? GetOrCreate(Player player, byte dungeonNumber)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return null;
        }

        var definition = Stats.GetDungeonTokens(dungeonNumber);
        var stat = character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id);
        if (stat is null)
        {
            // Without the definition in the configuration (the update 'Dungeon Token Merchants' adds it), the attribute couldn't be saved.
            if (player.GameContext.Configuration.Attributes.FirstOrDefault(a => a.Id == definition.Id) is not { } persistentDefinition)
            {
                return null;
            }

            stat = player.PersistenceContext.CreateNew<StatAttribute>(persistentDefinition, 0);
            character.Attributes.Add(stat);
        }

        return stat;
    }
}
