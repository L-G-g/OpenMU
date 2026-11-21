// <copyright file="EventReworkPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The rework of the events of PeroniaMU:
/// <list type="bullet">
///   <item>Devil Square, Blood Castle, Chaos Castle and Kalima have one entrance: the level is chosen by the resets of the character.</item>
///   <item>Devil Square is an experience event: its experience rises from 1.5x to 3x during the game, and its monsters drop nothing.</item>
///   <item>Blood Castle is an item event: the player who delivers the weapon gets an item of the level (excellent items of the dungeons, ancient items).</item>
///   <item>Chaos Castle is a jewel event: its monsters give no experience, and each of them drops a jewel.</item>
/// </list>
/// The times of the events and their announcements are configured in their start plugins (cycle length and offset, opening announcement).
/// </summary>
[PlugIn]
[Display(Name = "Event rework", Description = "Devil Square, Blood Castle, Chaos Castle and Kalima by resets (one entrance per event); Devil Square with rising experience and without drops; Blood Castle with an item reward per level for the player who delivers the weapon; Chaos Castle without experience and with a jewel per kill.")]
[Guid("2B7E4D91-6A3C-4F85-9E12-7C5A0B3D8F64")]
public class EventReworkPlugIn : IFeaturePlugIn, IExperienceCalculationPlugIn, IMonsterItemDropPlugIn, IMoneyDropCalculationPlugIn,
    IObjectAddedToMapPlugIn, IObjectRemovedFromMapPlugIn, IAttackableGotKilledPlugIn,
    ISupportCustomConfiguration<EventReworkConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    private const int ClassRetries = 30;

    /// <summary>
    /// The numbers of the maps of Kalima 1 to 7.
    /// </summary>
    private static readonly short[] KalimaMapNumbers = [24, 25, 26, 27, 28, 29, 36];

    /// <summary>
    /// The way through Kalima, from the entrance to Kundun. All levels of Kalima have the same layout.
    /// </summary>
    private static readonly (double X, double Y)[] KalimaWay =
    [
        (7, 19), (60, 15), (110, 12), (117, 45), (115, 95), (95, 95), (74, 76), (57, 75), (40, 75), (26, 76),
    ];

    private readonly ConcurrentDictionary<AttackableNpcBase, (SimpleElement Element, AttributeDefinition Attribute)[]> _appliedStats = new();

    private readonly PlugInManager? _plugInManager;

    private (EventReworkConfiguration? Configuration, MapDifficultyScalerConfiguration? Scaler, EventDifficulty? Difficulty) _difficulty;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventReworkPlugIn"/> class.
    /// The data initialization and the admin panel create it with this constructor for its default configuration.
    /// </summary>
    public EventReworkPlugIn()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventReworkPlugIn"/> class.
    /// </summary>
    /// <param name="plugInManager">The plugin manager, to find its game context.</param>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public EventReworkPlugIn(PlugInManager? plugInManager)
    {
        this._plugInManager = plugInManager;
    }

    /// <inheritdoc />
    public EventReworkConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EventReworkConfiguration();

    /// <summary>
    /// Gets the configuration of the event rework of the game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The configuration, or <c>null</c> if the event rework is not active.</returns>
    public static EventReworkConfiguration? GetConfiguration(IGameContext gameContext)
    {
        return gameContext.FeaturePlugIns.GetPlugIn<EventReworkPlugIn>() is { } plugIn
            ? plugIn.Configuration ??= new EventReworkConfiguration()
            : null;
    }

    /// <summary>
    /// Gets the level of a mini game which a player enters by its resets.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="miniGameType">The type of the mini game.</param>
    /// <returns>The game level, or <c>null</c> if the level of this mini game isn't chosen by resets.</returns>
    public static int? GetGameLevelByResets(Player player, MiniGameType miniGameType)
    {
        if (GetConfiguration(player.GameContext) is not { } configuration)
        {
            return null;
        }

        if (!IsLevelByResets(configuration, miniGameType))
        {
            return null;
        }

        var levels = player.GameContext.Configuration.MiniGameDefinitions
            .Where(d => d.Type == miniGameType && d.IsAvailable())
            .Select(d => d.GameLevel)
            .ToList();
        if (levels.Count == 0)
        {
            return null;
        }

        var level = configuration.GetLevelOfResets(GetResets(player));
        return levels.Where(l => l <= level).DefaultIfEmpty(levels.Min()).Max();
    }

    /// <summary>
    /// Gets the name of a mini game which is shown to the players. If the level of the mini game is chosen by the resets,
    /// it shows the resets of its level instead of the level, e.g. "Devil Square (30-74 rr)" instead of "Devil Square 2".
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <returns>The name.</returns>
    public static string GetDisplayName(IGameContext gameContext, MiniGameDefinition definition)
    {
        var name = definition.Name.ToString() ?? string.Empty;
        if (GetConfiguration(gameContext) is not { } configuration
            || !IsLevelByResets(configuration, definition.Type)
            || configuration.GetResetRangeText(definition.GameLevel) is not { } resets)
        {
            return name;
        }

        return $"{TrimLevel(name)} ({resets})";
    }

    /// <summary>
    /// Gets the name of a mini game for the golden messages. If the level of the mini game is chosen by the resets,
    /// it's only the name of the event, e.g. "Devil Square" instead of "Devil Square 2"; the board shows the resets.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <returns>The name.</returns>
    public static string GetMessageName(IGameContext gameContext, MiniGameDefinition definition)
    {
        var name = definition.Name.ToString() ?? string.Empty;
        return GetConfiguration(gameContext) is { } configuration && IsLevelByResets(configuration, definition.Type)
            ? TrimLevel(name)
            : name;
    }

    /// <summary>
    /// Determines whether the stats of a monster are set by the resets of its event, see <see cref="EventReworkConfiguration.ScaleMonstersByResets"/>.
    /// Then, the <see cref="MapDifficultyScaler"/> doesn't change it.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="map">The map.</param>
    /// <param name="npc">The monster.</param>
    /// <returns><c>true</c>, if the stats of the monster are set by the resets.</returns>
    public static bool IsScaledByResets(IGameContext? gameContext, GameMap map, AttackableNpcBase npc)
    {
        return gameContext is not null
               && GetConfiguration(gameContext) is { ScaleMonstersByResets: true } configuration
               && GetScaledLevel(configuration, map, npc) is not null;
    }

    /// <summary>
    /// Gets the mini game which an NPC enters directly, see <see cref="EventReworkConfiguration.DirectEntryAtNpc"/>.
    /// </summary>
    /// <param name="player">The player who talks to the NPC.</param>
    /// <param name="npcWindow">The window of the NPC.</param>
    /// <returns>The type of the mini game, or <c>null</c> if the NPC opens its window as usual.</returns>
    public static MiniGameType? GetDirectEntryMiniGame(Player player, NpcWindow npcWindow)
    {
        var type = npcWindow switch
        {
            NpcWindow.DevilSquare => MiniGameType.DevilSquare,
            NpcWindow.BloodCastle => MiniGameType.BloodCastle,
            _ => MiniGameType.Undefined,
        };
        return type != MiniGameType.Undefined
               && GetConfiguration(player.GameContext) is { DirectEntryAtNpc: true }
               && GetGameLevelByResets(player, type) is not null
            ? type
            : null;
    }

    /// <summary>
    /// Gets the level of the Kalima which a Lost Kalima Map of a player opens by its resets.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="maximumLevel">The maximum level of Kalima.</param>
    /// <returns>The level of Kalima, or <c>null</c> if the level of Kalima isn't chosen by resets.</returns>
    public static int? GetKalimaLevelByResets(Player player, int maximumLevel)
    {
        if (GetConfiguration(player.GameContext) is not { KalimaByResets: true } configuration)
        {
            return null;
        }

        return Math.Clamp(configuration.GetLevelOfResets(GetResets(player)), 1, maximumLevel);
    }

    /// <summary>
    /// Creates the drop generator of Chaos Castle, if each kill drops a jewel.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The drop generator, or <c>null</c> if Chaos Castle drops its original jewels.</returns>
    public static IDropGenerator? CreateChaosCastleDropGenerator(IGameContext gameContext)
    {
        if (GetConfiguration(gameContext) is not { ChaosCastleJewelPerKill: true } configuration)
        {
            return null;
        }

        var jewels = configuration.ChaosCastleJewels
            .Where(j => j.Weight > 0)
            .Select(j => (Definition: gameContext.Configuration.Items.FirstOrDefault(i => i.Group == j.Group && i.Number == j.Number && i.IsActive), j.Weight))
            .Where(j => j.Definition is not null)
            .Select(j => (j.Definition!, j.Weight))
            .ToList();
        return jewels.Count > 0 ? new EventJewelDropGenerator(gameContext.DropGenerator, jewels) : null;
    }

    /// <summary>
    /// Gets the time before the end of Blood Castle from which the weapon can be delivered to the Archangel.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The time, or <c>null</c> if the weapon can be delivered at any time.</returns>
    public static TimeSpan? GetBloodCastleDeliveryWindow(IGameContext gameContext)
    {
        return GetConfiguration(gameContext) is { BloodCastleDeliveryBeforeEnd: { } window } && window > TimeSpan.Zero
            ? window
            : null;
    }

    /// <summary>
    /// Determines whether a reward of the definition of Blood Castle is given, or whether it's replaced by the item reward of the event rework.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="reward">The reward.</param>
    /// <returns><c>true</c>, if the reward is given.</returns>
    public static bool IsBloodCastleRewardGiven(IGameContext gameContext, MiniGameReward reward)
    {
        return reward.RewardType is not (MiniGameRewardType.Item or MiniGameRewardType.ItemDrop)
               || GetConfiguration(gameContext) is not { BloodCastleItemRewards: true };
    }

    /// <summary>
    /// Gives the item reward of Blood Castle to the player who delivered the weapon.
    /// </summary>
    /// <param name="winner">The player who delivered the weapon.</param>
    /// <param name="definition">The definition of the Blood Castle.</param>
    /// <param name="map">The map of the Blood Castle, on which the item drops when the inventory is full.</param>
    /// <returns>The value task.</returns>
    public static async ValueTask GiveBloodCastleRewardAsync(Player winner, MiniGameDefinition definition, GameMap map)
    {
        var gameContext = winner.GameContext;
        if (GetConfiguration(gameContext) is not { BloodCastleItemRewards: true } configuration
            || configuration.BloodCastleRewards.FirstOrDefault(r => r.GameLevel == definition.GameLevel) is not { } reward
            || gameContext.DropGenerator is not DefaultDropGenerator dropGenerator)
        {
            return;
        }

        var characterClass = configuration.BloodCastleRewardOfOwnClass ? winner.SelectedCharacter?.CharacterClass : null;
        Item? item;
        try
        {
            item = reward.Kind switch
            {
                BloodCastleRewardKind.DungeonExcellentItem => await GenerateDungeonItemAsync(dropGenerator, configuration, reward, gameContext, characterClass).ConfigureAwait(false),
                BloodCastleRewardKind.AncientArmorOrWeapon => await GenerateAncientItemAsync(dropGenerator, GetAncientTier(dropGenerator, configuration, reward.AncientTier), characterClass).ConfigureAwait(false),
                BloodCastleRewardKind.AncientJewelry => await GenerateAncientItemAsync(dropGenerator, dropGenerator.AncientItemDefinitions.Where(IsJewelry).ToList(), characterClass).ConfigureAwait(false),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            winner.Logger.LogError(ex, "Error when generating the Blood Castle reward {reward}", reward);
            return;
        }

        if (item is null)
        {
            winner.Logger.LogWarning("No Blood Castle reward item could be generated for {reward}", reward);
            return;
        }

        var name = item.Definition?.Name.ValueInNeutralLanguage ?? "?";
        if (winner.Inventory?.CheckInvSpace(item) is { } slot && slot >= InventoryConstants.LastEquippableItemSlotIndex)
        {
            // The generated item is temporary; the inventory only accepts an item of the persistence context of the player.
            var persistentItem = item is TemporaryItem temporaryItem ? temporaryItem.MakePersistent(winner.PersistenceContext) : item;
            if (await winner.Inventory.AddItemAsync(slot, persistentItem).ConfigureAwait(false))
            {
                await winner.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(persistentItem)).ConfigureAwait(false);
                await winner.ShowGoldenMessageAsync($"Premio de Blood Castle: {name}").ConfigureAwait(false);
                return;
            }

            if (!ReferenceEquals(persistentItem, item))
            {
                winner.PersistenceContext.Detach(persistentItem);
            }
        }

        // The game ends shortly after the delivery and warps the players out, so an item on the floor would be lost easily.
        if (await TryAddToVaultAsync(winner, item).ConfigureAwait(false))
        {
            await winner.ShowGoldenMessageAsync($"Premio de Blood Castle: {name} (inventario lleno, quedo en el baul)").ConfigureAwait(false);
            return;
        }

        var droppedItem = new DroppedItem(item, winner.Position, winner.CurrentMap ?? map, winner, winner.GetAsEnumerable());
        await (winner.CurrentMap ?? map).AddAsync(droppedItem).ConfigureAwait(false);
        await winner.ShowGoldenMessageAsync($"Premio de Blood Castle: {name} (en el piso, inventario y baul llenos)").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        // A monster respawns without leaving the map (GameMap.RespawnAsync), so it gets the stats of the resets of the time
        // or the position of its respawn here, before its health is set again from its maximum health when it respawns.
        if (killed is not AttackableNpcBase npc
            || !this._appliedStats.TryGetValue(npc, out var elements)
            || this.Configuration is not { ScaleMonstersByResets: true } configuration
            || GameContextLookup.Get(this._plugInManager) is not { } gameContext
            || GetScaledLevel(configuration, npc.CurrentMap, npc) is not { } scaled
            || this.GetDifficulty(gameContext, configuration) is not { } difficulty)
        {
            return ValueTask.CompletedTask;
        }

        var (start, end) = configuration.GetResetRangeOfLevel(scaled.Level);
        var resets = start + ((end - start) * Math.Clamp(scaled.Progress, 0, 1));
        var multipliers = difficulty.GetMultipliers(npc.CurrentMap.Definition, npc.Definition, resets);
        var index = 0;
        for (var stat = 0; stat < EventDifficulty.StatCount && index < elements.Length; stat++)
        {
            elements[index].Element.Value = multipliers[stat];
            index += EventDifficulty.StatAttributes[stat].Length;
        }

        // TEMP TEST LOG (do not commit)
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not AttackableNpcBase npc
            || npc is Monster { SummonedBy: not null }
            || this.Configuration is not { ScaleMonstersByResets: true } configuration
            || GameContextLookup.Get(this._plugInManager) is not { } gameContext
            || GetScaledLevel(configuration, map, npc) is not { } scaled
            || this.GetDifficulty(gameContext, configuration) is not { } difficulty)
        {
            return ValueTask.CompletedTask;
        }

        var (start, end) = configuration.GetResetRangeOfLevel(scaled.Level);
        var resets = start + ((end - start) * Math.Clamp(scaled.Progress, 0, 1));
        var multipliers = difficulty.GetMultipliers(map.Definition, npc.Definition, resets);
        var elements = new List<(SimpleElement Element, AttributeDefinition Attribute)>();
        for (var stat = 0; stat < EventDifficulty.StatCount; stat++)
        {
            var element = new SimpleElement(multipliers[stat], AggregateType.Multiplicate);
            foreach (var attribute in EventDifficulty.StatAttributes[stat])
            {
                elements.Add((element, attribute));
            }
        }

        if (!this._appliedStats.TryAdd(npc, elements.ToArray()))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var (element, attribute) in elements)
        {
            npc.Attributes.AddElement(element, attribute);
        }

        if (npc.SpawnArea.MaximumHealthOverride is null && npc.IsAlive)
        {
            // The health was set before the monster was added to the map.
            npc.Health = (int)npc.Attributes[Stats.MaximumHealth];
        }

        // TEMP TEST LOG (do not commit)
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is AttackableNpcBase npc && this._appliedStats.TryRemove(npc, out var elements))
        {
            foreach (var (element, attribute) in elements)
            {
                npc.Attributes.RemoveElement(element, attribute);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        if (this.Configuration is not { } configuration
            || args.KilledObject is Player
            || player.CurrentMiniGame is not { IsEventRunning: true } miniGame)
        {
            return ValueTask.CompletedTask;
        }

        switch (miniGame.Definition.Type)
        {
            case MiniGameType.DevilSquare:
                args.Experience *= GetDevilSquareMultiplier(configuration, miniGame);
                break;
            case MiniGameType.ChaosCastle when configuration.ChaosCastleWithoutExperience:
                args.Experience = 0;
                break;
            default:
                break;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void ItemDropping(AttackableNpcBase monster, Player killer, Item item, CancelEventArgs eventArgs)
    {
        if (this.IsDevilSquareWithoutDrops(monster, killer))
        {
            eventArgs.Cancel = true;
        }
    }

    /// <inheritdoc />
    public ValueTask CalculateMoneyDropAsync(Player killer, MoneyDropCalculationArgs args)
    {
        if (this.Configuration is { DevilSquareWithoutDrops: true }
            && killer.CurrentMiniGame is { Definition.Type: MiniGameType.DevilSquare } miniGame
            && killer.CurrentMap == miniGame.Map)
        {
            args.Money = 0;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the experience multiplier of a running Devil Square: from the start multiplier to the end multiplier during the game duration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="miniGame">The Devil Square.</param>
    /// <returns>The multiplier.</returns>
    internal static double GetDevilSquareMultiplier(EventReworkConfiguration configuration, MiniGameContext miniGame)
    {
        var duration = miniGame.Definition.GameDuration;
        var progress = miniGame.GameStartedAtUtc is { } startedAt && duration > TimeSpan.Zero
            ? Math.Clamp((DateTime.UtcNow - startedAt) / duration, 0, 1)
            : 0;
        return configuration.DevilSquareStartExperienceMultiplier
               + ((configuration.DevilSquareEndExperienceMultiplier - configuration.DevilSquareStartExperienceMultiplier) * progress);
    }

    /// <summary>
    /// Gets the level of the event of a monster whose stats are set by the resets, and how far the event is: the time of the game
    /// in Devil Square and Blood Castle, the way from the entrance to Kundun in Kalima (0 to 1).
    /// </summary>
    private static (int Level, double Progress)? GetScaledLevel(EventReworkConfiguration configuration, GameMap map, AttackableNpcBase npc)
    {
        if (npc.Definition.ObjectKind is not (NpcObjectKind.Monster or NpcObjectKind.Destructible))
        {
            return null;
        }

        if (npc.EventStateProvider is MiniGameContext { Definition: { Type: MiniGameType.DevilSquare or MiniGameType.BloodCastle } definition } miniGame)
        {
            var duration = definition.GameDuration.TotalSeconds;
            var elapsed = miniGame.GameStartedAtUtc is { } startedAt ? (DateTime.UtcNow - startedAt).TotalSeconds : 0;
            return (definition.GameLevel, duration > 0 ? elapsed / duration : 0);
        }

        if (!configuration.KalimaByResets)
        {
            return null;
        }

        var kalimaIndex = Array.IndexOf(KalimaMapNumbers, (short)map.MapId);
        if (kalimaIndex < 0 || npc.EventStateProvider is not null)
        {
            return null;
        }

        return (kalimaIndex + 1, GetProgressOnKalimaWay(npc.Position.X, npc.Position.Y));
    }

    /// <summary>
    /// Gets how far a position is on the way through Kalima: the length of the way up to the nearest point of it, in relation to its whole length.
    /// </summary>
    private static double GetProgressOnKalimaWay(double x, double y)
    {
        var total = 0.0;
        var bestDistance = double.MaxValue;
        var bestLength = 0.0;
        for (var i = 1; i < KalimaWay.Length; i++)
        {
            var (ax, ay) = KalimaWay[i - 1];
            var (bx, by) = KalimaWay[i];
            var dx = bx - ax;
            var dy = by - ay;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var fraction = length > 0 ? Math.Clamp((((x - ax) * dx) + ((y - ay) * dy)) / (length * length), 0, 1) : 0;
            var px = ax + (fraction * dx);
            var py = ay + (fraction * dy);
            var distance = ((x - px) * (x - px)) + ((y - py) * (y - py));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLength = total + (fraction * length);
            }

            total += length;
        }

        return total > 0 ? bestLength / total : 0;
    }

    private static bool IsLevelByResets(EventReworkConfiguration configuration, MiniGameType miniGameType) => miniGameType switch
    {
        MiniGameType.DevilSquare => configuration.DevilSquareByResets,
        MiniGameType.BloodCastle => configuration.BloodCastleByResets,
        MiniGameType.ChaosCastle => configuration.ChaosCastleByResets,
        _ => false,
    };

    private static int GetResets(Player player) => (int)(player.Attributes?[Stats.Resets] ?? 0);

    private static string TrimLevel(string name) => name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd();

    /// <summary>
    /// Tries to add an item to the vault of the account of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item, which may be temporary.</param>
    /// <returns><c>true</c>, if the item was added to the vault.</returns>
    private static async ValueTask<bool> TryAddToVaultAsync(Player player, Item item)
    {
        if (player.Account is not { } account)
        {
            return false;
        }

        IStorage vault;
        try
        {
            account.Vault ??= player.PersistenceContext.CreateNew<ItemStorage>();

            // An opened vault window already holds the storage of the vault; otherwise, it's created like the vault NPC does.
            vault = player.Vault is { } opened && ReferenceEquals(opened.ItemStorage, account.Vault)
                ? opened
                : new Storage(account.IsVaultExtended ? InventoryConstants.WarehouseSize * 2 : InventoryConstants.WarehouseSize, account.Vault);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error when opening the vault for the Blood Castle reward");
            return false;
        }

        if (vault.CheckInvSpace(item) is not { } slot)
        {
            return false;
        }

        var persistentItem = item is TemporaryItem temporaryItem ? temporaryItem.MakePersistent(player.PersistenceContext) : item;
        if (await vault.AddItemAsync(slot, persistentItem).ConfigureAwait(false))
        {
            return true;
        }

        if (!ReferenceEquals(persistentItem, item))
        {
            player.PersistenceContext.Detach(persistentItem);
        }

        return false;
    }

    private static bool IsJewelry(ItemDefinition definition) => definition.Group == 13;

    private static bool IsArmorOrWeapon(ItemDefinition definition) => definition.Group <= 11;

    private static IReadOnlyList<ItemDefinition> GetAncientTier(DefaultDropGenerator dropGenerator, EventReworkConfiguration configuration, int tier)
    {
        var all = dropGenerator.AncientItemDefinitions
            .Where(IsArmorOrWeapon)
            .Distinct()
            .OrderBy(d => d.DropLevel)
            .ThenBy(d => d.Group)
            .ThenBy(d => d.Number)
            .ToList();
        var tierCount = Math.Max(configuration.BloodCastleAncientTierCount, 1);
        if (tier < 1 || tier > tierCount || all.Count == 0)
        {
            return all;
        }

        var start = all.Count * (tier - 1) / tierCount;
        var end = all.Count * tier / tierCount;
        return all.GetRange(start, Math.Max(end - start, 1));
    }

    private static async ValueTask<Item?> GenerateAncientItemAsync(DefaultDropGenerator dropGenerator, IReadOnlyList<ItemDefinition> definitions, CharacterClass? characterClass)
    {
        var usable = characterClass is null ? definitions : definitions.Where(d => DungeonLoot.CanNeed(characterClass, d)).ToList();
        return await dropGenerator.GenerateAncientItemAsync(usable.Count > 0 ? usable : definitions).ConfigureAwait(false);
    }

    private static async ValueTask<Item?> GenerateDungeonItemAsync(DefaultDropGenerator dropGenerator, EventReworkConfiguration configuration, BloodCastleReward reward, IGameContext gameContext, CharacterClass? characterClass)
    {
        var dungeons = DungeonFeaturePlugIn.GetConfiguration(gameContext)?.Dungeons ?? DungeonDefaults.CreateDungeons();
        if (dungeons.FirstOrDefault(d => d.Number == reward.DungeonNumber) is not { } dungeon
            || dungeon.GetDropMapNumbers() is not { Count: > 0 } mapNumbers)
        {
            return null;
        }

        Item? item = null;
        for (var i = 0; i < ClassRetries; i++)
        {
            item = await dropGenerator.GenerateExcellentItemOfZonesAsync(mapNumbers, 0, configuration.BloodCastleMinimumExcellentOptions, 1).ConfigureAwait(false);
            if (item?.Definition is null || characterClass is null || DungeonLoot.CanNeed(characterClass, item.Definition))
            {
                break;
            }
        }

        return item;
    }

    private EventDifficulty? GetDifficulty(IGameContext gameContext, EventReworkConfiguration configuration)
    {
        var scaler = gameContext.FeaturePlugIns.GetPlugIn<MapDifficultyScaler>()?.Configuration;
        var current = this._difficulty;
        if (current.Configuration == configuration && current.Scaler == scaler && current.Difficulty is not null)
        {
            return current.Difficulty;
        }

        var difficulty = EventDifficulty.Create(gameContext.Configuration, configuration, scaler);
        this._difficulty = (configuration, scaler, difficulty);
        return difficulty;
    }

    private bool IsDevilSquareWithoutDrops(AttackableNpcBase monster, Player killer)
    {
        return this.Configuration is { DevilSquareWithoutDrops: true }
               && killer.CurrentMiniGame is { Definition.Type: MiniGameType.DevilSquare } miniGame
               && monster.CurrentMap == miniGame.Map;
    }
}
