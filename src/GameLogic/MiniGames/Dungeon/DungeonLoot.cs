// <copyright file="DungeonLoot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Shares the drops of a dungeon between its players with need, greed or pass.
/// </summary>
/// <remarks>
/// A dropped item doesn't fall to the ground when more than one player is in the dungeon. Instead, each of them chooses
/// need (only if the class of the character or one of its next generation classes can use the item), greed or pass, with
/// the chat commands /need, /greed and /pass or the window of loot.dll. A player who doesn't choose in time passes.
/// The players who chose need or greed roll a dice, need with a higher maximum (<see cref="DungeonConfiguration.NeedRollMaximum"/>),
/// so it has a better chance without always winning. The highest number wins; the item goes into the inventory of the winner,
/// or to its feet if the inventory is full. If everybody passed, the item falls to the ground for anybody.
/// </remarks>
public sealed class DungeonLoot
{
    private readonly DungeonContext _context;
    private readonly DungeonConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private readonly List<LootRoll> _openRolls = new();
    private int _lastRollId;

    /// <summary>
    /// Initializes a new instance of the <see cref="DungeonLoot"/> class.
    /// </summary>
    /// <param name="context">The dungeon.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    internal DungeonLoot(DungeonContext context, DungeonConfiguration configuration, ILogger logger)
    {
        this._context = context;
        this._configuration = configuration;
        this._logger = logger;
    }

    /// <summary>
    /// Determines whether a character class can choose need for an item: the item has no class requirement,
    /// or the class or one of its next generation classes is qualified for it.
    /// </summary>
    /// <param name="characterClass">The character class.</param>
    /// <param name="item">The item definition.</param>
    /// <returns><c>true</c> if the character class can choose need.</returns>
    public static bool CanNeed(CharacterClass? characterClass, ItemDefinition item)
    {
        if (item.QualifiedCharacters.Count == 0)
        {
            return true;
        }

        for (var current = characterClass; current is not null; current = current.NextGenerationClass)
        {
            if (item.QualifiedCharacters.Contains(current))
            {
                return true;
            }

            if (ReferenceEquals(current.NextGenerationClass, current))
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Takes the choice of a player for a roll.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="choice">The choice.</param>
    /// <param name="rollId">The id of the roll, or <c>null</c> for the oldest roll which waits for the choice of the player.</param>
    public async ValueTask ChooseAsync(Player player, LootChoice choice, int? rollId)
    {
        LootRoll? roll;
        bool needNotAllowed = false;
        bool isComplete = false;
        lock (this._sync)
        {
            roll = this._openRolls.FirstOrDefault(r => (rollId is null || r.Id == rollId) && r.CanNeed.ContainsKey(player) && !r.Choices.ContainsKey(player));
            if (roll is not null)
            {
                if (choice == LootChoice.Need && !roll.CanNeed[player])
                {
                    needNotAllowed = true;
                }
                else
                {
                    roll.Choices[player] = choice;
                    isComplete = roll.Choices.Count == roll.CanNeed.Count;
                }
            }
        }

        if (roll is null)
        {
            await player.ShowBlueMessageAsync(DungeonMessages.NoOpenRoll).ConfigureAwait(false);
            return;
        }

        if (needNotAllowed)
        {
            await player.ShowBlueMessageAsync(string.Format(DungeonMessages.NeedNotAllowedFormat, roll.Id)).ConfigureAwait(false);
            return;
        }

        await player.ShowBlueMessageAsync(string.Format(DungeonMessages.ChoiceTakenFormat, GetName(choice), roll.Id)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowLootRollPlugIn>(p => p.CloseLootRollAsync(roll.Id, false)).ConfigureAwait(false);
        if (isComplete)
        {
            await this.ResolveAsync(roll).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts a roll for a dropped item, if it's an item which is rolled and more than one player is in the dungeon.
    /// </summary>
    /// <param name="item">The dropped item.</param>
    /// <param name="killer">The player who killed the monster.</param>
    /// <returns><c>true</c> if the item is rolled, so it must not fall to the ground.</returns>
    internal async ValueTask<bool> TryStartRollAsync(Item item, Player killer)
    {
        if (item.Definition is not { } definition || !this.IsRolled(item))
        {
            return false;
        }

        var players = await this._context.GetPlayersAsync().ConfigureAwait(false);
        if (players.Count < 2 || killer.CurrentMap is not { } map)
        {
            return false;
        }

        LootRoll roll;
        lock (this._sync)
        {
            roll = new LootRoll(++this._lastRollId, item, map, killer.Position, players.ToDictionary(p => p, p => CanNeed(p.SelectedCharacter?.CharacterClass, definition)));
            this._openRolls.Add(roll);
        }

        var seconds = (int)Math.Ceiling(this._configuration.RollDuration.TotalSeconds);
        var description = Describe(item);
        foreach (var (player, canNeed) in roll.CanNeed)
        {
            var format = canNeed ? DungeonMessages.RollStartedFormat : DungeonMessages.RollStartedWithoutNeedFormat;
            await player.ShowBlueMessageAsync(string.Format(format, roll.Id, description, seconds)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IShowLootRollPlugIn>(p => p.ShowLootRollAsync(roll.Id, item, description, this._configuration.RollDuration, canNeed)).ConfigureAwait(false);
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(this._configuration.RollDuration).ConfigureAwait(false);
            await this.ResolveAsync(roll).ConfigureAwait(false);
        });

        return true;
    }

    /// <summary>
    /// Resolves all open rolls with the choices so far, e.g. when the dungeon ends.
    /// </summary>
    internal async ValueTask ResolveAllAsync()
    {
        List<LootRoll> rolls;
        lock (this._sync)
        {
            rolls = this._openRolls.ToList();
        }

        foreach (var roll in rolls)
        {
            await this.ResolveAsync(roll).ConfigureAwait(false);
        }
    }

    private static string GetName(LootChoice choice) => choice switch
    {
        LootChoice.Need => "need",
        LootChoice.Greed => "greed",
        _ => "pass",
    };

    private static string Describe(Item item)
    {
        var name = item.Definition?.Name.ValueInNeutralLanguage ?? "?";
        var text = item.Level > 0 ? $"{name} +{item.Level}" : name;
        var excellentOptions = item.ItemOptions.Count(o => object.Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));
        if (excellentOptions > 0)
        {
            text += $" (exc {excellentOptions} opc)";
        }
        else if (item.IsAncient())
        {
            text += " (ancient)";
        }

        return text;
    }

    private bool IsRolled(Item item)
    {
        return this._configuration.RollAllItems || item.IsExcellent();
    }

    private async ValueTask ResolveAsync(LootRoll roll)
    {
        List<(Player Player, LootChoice Choice, int Number)> results;
        lock (this._sync)
        {
            if (!this._openRolls.Remove(roll))
            {
                // already resolved
                return;
            }

            results = roll.CanNeed.Keys
                .Select(player =>
                {
                    var choice = roll.Choices.GetValueOrDefault(player, LootChoice.Pass);
                    var number = choice switch
                    {
                        LootChoice.Need => Rand.NextInt(1, this._configuration.NeedRollMaximum + 1),
                        LootChoice.Greed => Rand.NextInt(1, this._configuration.GreedRollMaximum + 1),
                        _ => 0,
                    };
                    return (player, choice, number);
                })
                .ToList();
        }

        var description = Describe(roll.Item);
        var rollers = results.Where(r => r.Choice != LootChoice.Pass).ToList();
        Player? winner = null;
        string message;
        if (rollers.Count == 0)
        {
            message = string.Format(DungeonMessages.RollAllPassedFormat, roll.Id, description);
        }
        else
        {
            var highest = rollers.Max(r => r.Number);
            winner = rollers.Where(r => r.Number == highest).ToList().SelectRandom()!.Player;
            var details = string.Join(", ", results.Select(r => r.Choice == LootChoice.Pass ? $"{r.Player.Name} pass" : $"{r.Player.Name} {GetName(r.Choice)} {r.Number}"));
            message = string.Format(DungeonMessages.RollWonFormat, roll.Id, description, winner.Name, details);
        }

        // The window is closed before the item is added: loot.dll then knows that the item which appears in the
        // inventory of the winner wasn't picked up, and the client doesn't show a wrong "obtained" message.
        foreach (var player in roll.CanNeed.Keys)
        {
            var won = ReferenceEquals(player, winner);
            await player.InvokeViewPlugInAsync<IShowLootRollPlugIn>(p => p.CloseLootRollAsync(roll.Id, won)).ConfigureAwait(false);
        }

        if (winner is null)
        {
            await this.DropToGroundAsync(roll).ConfigureAwait(false);
        }
        else
        {
            await this.GiveToWinnerAsync(roll.Item, winner).ConfigureAwait(false);
        }

        foreach (var player in roll.CanNeed.Keys)
        {
            await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
        }
    }

    private async ValueTask DropToGroundAsync(LootRoll roll)
    {
        if (this._context.IsDisposed || this._context.IsDisposing)
        {
            this._logger.LogInformation("{context}: The map ended before the rolled item {item} could fall to the ground.", this._context, roll.Item);
            return;
        }

        var droppedItem = new DroppedItem(roll.Item, roll.Position, roll.Map, null, null);
        await roll.Map.AddAsync(droppedItem).ConfigureAwait(false);
    }

    private async ValueTask GiveToWinnerAsync(Item item, Player winner)
    {
        if (winner.Inventory is { } inventory
            && inventory.CheckInvSpace(item) is { } slot
            && slot > InventoryConstants.LastEquippableItemSlotIndex
            && await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
        {
            if (inventory.GetItem(slot) is { } addedItem)
            {
                await winner.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(addedItem)).ConfigureAwait(false);
            }

            return;
        }

        if (winner.CurrentMap is { } map)
        {
            var droppedItem = new DroppedItem(item, winner.Position, map, null, [winner]);
            await map.AddAsync(droppedItem).ConfigureAwait(false);
            await winner.ShowBlueMessageAsync(string.Format(DungeonMessages.InventoryFullFormat, Describe(item))).ConfigureAwait(false);
        }
        else
        {
            this._logger.LogWarning("{context}: {winner} won {item}, but neither has space in the inventory nor is on a map.", this._context, winner, item);
        }
    }

    /// <summary>
    /// A roll for a dropped item.
    /// </summary>
    /// <param name="Id">The id, which the players use in the chat commands.</param>
    /// <param name="Item">The item.</param>
    /// <param name="Map">The map where it dropped.</param>
    /// <param name="Position">The position where it dropped.</param>
    /// <param name="CanNeed">The players of the roll, with the information if they can choose need.</param>
    private sealed record LootRoll(int Id, Item Item, GameMap Map, Point Position, Dictionary<Player, bool> CanNeed)
    {
        /// <summary>
        /// Gets the choices of the players who chose already.
        /// </summary>
        public Dictionary<Player, LootChoice> Choices { get; } = new();
    }
}
