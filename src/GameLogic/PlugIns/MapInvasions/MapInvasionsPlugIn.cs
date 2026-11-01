// <copyright file="MapInvasionsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.MapInvasions;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;

/// <summary>
/// Invades one map after another with monsters which stay until the next invasion of the same map.
/// </summary>
/// <remarks>
/// Every <see cref="MapInvasionsConfiguration.Interval"/>, the next invasion of a cycle is started. A cycle contains
/// each of the <see cref="MapInvasionsConfiguration.Invasions"/> once, in a random order, so no map is invaded twice
/// before all the others were invaded. An invasion removes the monsters which are left from the previous invasion
/// of its map, and spawns new ones near random monster spots of the map. They don't respawn after they died.
/// Each of them drops the configured items in addition to its normal drop, for the player who killed it (or its party).
/// The invasions aren't saved, so a cycle starts again from the beginning when the game server starts.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MapInvasionsPlugIn_Name), Description = nameof(PlugInResources.MapInvasionsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6F0E2B7A-3C41-4D9B-8E25-A7C1D4F3B962")]
public class MapInvasionsPlugIn : IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, ISupportCustomConfiguration<MapInvasionsConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// The maximum distance between a dropped item and the monster.
    /// </summary>
    private const byte MaximumDropDistance = 2;

    /// <summary>
    /// The number of tries to find a free coordinate near a spot before a random coordinate of the map is taken.
    /// </summary>
    private const int SpawnTries = 20;

    private readonly ConditionalWeakTable<IGameContext, InvasionState> _states = new();

    private int _forceStartCount;

    /// <inheritdoc />
    public MapInvasionsConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MapInvasionsConfiguration();

    /// <inheritdoc />
    public void ForceStart()
    {
        Interlocked.Increment(ref this._forceStartCount);
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is not { Invasions.Count: > 0 } configuration)
        {
            return;
        }

        var state = this._states.GetValue(gameContext, _ => new InvasionState(DateTime.UtcNow + configuration.FirstInvasionDelay));
        var forceStartCount = Volatile.Read(ref this._forceStartCount);
        if (DateTime.UtcNow < state.NextInvasionUtc && forceStartCount == state.HandledForceStartCount)
        {
            return;
        }

        using var l = await state.Lock.LockAsync().ConfigureAwait(false);
        if (DateTime.UtcNow < state.NextInvasionUtc && forceStartCount == state.HandledForceStartCount)
        {
            return;
        }

        state.HandledForceStartCount = forceStartCount;
        state.NextInvasionUtc = DateTime.UtcNow + (configuration.Interval > TimeSpan.FromMinutes(1) ? configuration.Interval : TimeSpan.FromMinutes(1));
        if (state.TakeNextInvasion(configuration) is not { } invasion)
        {
            return;
        }

        try
        {
            await this.InvadeAsync(gameContext, state, invasion, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<MapInvasionsPlugIn>().LogError(ex, "The invasion of map {map} failed.", invasion.MapNumber);
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster monster
            || GetPlayer(killer) is not { } player
            || !this._states.TryGetValue(player.GameContext, out var state)
            || !state.Monsters.TryRemove(monster, out var invasion))
        {
            return;
        }

        try
        {
            await DropItemsAsync(monster, invasion, player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Dropping the items of the invasion monster {monster} failed.", monster);
        }
    }

    private static Player? GetPlayer(IAttacker? killer)
    {
        return killer switch
        {
            Player player => player,
            IPlayerSurrogate surrogate => surrogate.Owner,
            Monster { SummonedBy: { } owner } => owner,
            _ => null,
        };
    }

    private static async ValueTask DropItemsAsync(Monster monster, MapInvasionsConfiguration.MapInvasion invasion, Player killer)
    {
        var map = monster.CurrentMap;
        var owners = killer.Party?.PartyList.AsEnumerable() ?? killer.GetAsEnumerable();
        var firstItem = true;
        foreach (var drop in invasion.Drops)
        {
            var items = drop.Items.ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var count = Rand.NextInt(drop.MinimumCount, Math.Max(drop.MinimumCount, drop.MaximumCount) + 1);
            for (var i = 0; i < count; i++)
            {
                var dropItem = items[Rand.NextInt(0, items.Count)];
                if (killer.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == dropItem.Group && d.Number == dropItem.Number) is not { } definition)
                {
                    continue;
                }

                var item = new TemporaryItem { Definition = definition, Level = Math.Min(dropItem.Level, definition.MaximumItemLevel) };
                item.Durability = item.GetMaximumDurabilityOfOnePiece();
                var position = firstItem ? monster.Position : map.Terrain.GetRandomCoordinate(monster.Position, MaximumDropDistance);
                firstItem = false;
                var droppedItem = new DroppedItem(item, position, map, null, owners);
                await map.AddAsync(droppedItem).ConfigureAwait(false);
            }
        }
    }

    private static Point? GetSpawnPoint(GameMap map, IReadOnlyList<MonsterSpawnArea> spots, byte distance)
    {
        var terrain = map.Terrain;
        for (var i = 0; i < SpawnTries && spots.Count > 0; i++)
        {
            var spot = spots[Rand.NextInt(0, spots.Count)];
            var x = Rand.NextInt(Math.Max(0, spot.X1 - distance), Math.Min(byte.MaxValue, spot.X2 + distance) + 1);
            var y = Rand.NextInt(Math.Max(0, spot.Y1 - distance), Math.Min(byte.MaxValue, spot.Y2 + distance) + 1);
            if (terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y])
            {
                return new Point((byte)x, (byte)y);
            }
        }

        return terrain.RandomWalkableCoordinate;
    }

    private async ValueTask InvadeAsync(GameContext gameContext, InvasionState state, MapInvasionsConfiguration.MapInvasion invasion, MapInvasionsConfiguration configuration)
    {
        await state.RemoveMonstersAsync(invasion).ConfigureAwait(false);
        if (invasion.Monster is not { } monsterDefinition
            || await gameContext.GetMapAsync((ushort)invasion.MapNumber).ConfigureAwait(false) is not { } map)
        {
            return;
        }

        var spots = map.Definition.MonsterSpawns
            .Where(s => s.SpawnTrigger == SpawnTrigger.Automatic && s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
            .ToList();
        for (var i = 0; i < invasion.Count; i++)
        {
            if (GetSpawnPoint(map, spots, configuration.SpawnDistance) is not { } point)
            {
                break;
            }

            var area = new MonsterSpawnArea
            {
                GameMap = map.Definition,
                MonsterDefinition = monsterDefinition,
                SpawnTrigger = SpawnTrigger.OnceAtEventStart,
                Quantity = 1,
                X1 = point.X,
                X2 = point.X,
                Y1 = point.Y,
                Y2 = point.Y,
            };

            var monster = new Monster(area, monsterDefinition, map, gameContext.DropGenerator, new BasicMonsterIntelligence(), gameContext.PlugInManager, gameContext.PathFinderPool);
            monster.Initialize();
            state.Monsters[monster] = invasion;
            await map.AddAsync(monster).ConfigureAwait(false);
            monster.OnSpawn();
        }

        await gameContext.ForEachPlayerAsync(player =>
        {
            var message = (configuration.StartMessage.GetTranslation(player.Culture) ?? string.Empty)
                .Replace("{monster}", monsterDefinition.Designation.GetTranslation(player.Culture), StringComparison.InvariantCulture)
                .Replace("{map}", map.Definition.Name.GetTranslation(player.Culture), StringComparison.InvariantCulture);
            return string.IsNullOrWhiteSpace(message)
                ? Task.CompletedTask
                : player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, MessageType.GoldenCenter)).AsTask();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// The state of the invasions of a game server.
    /// </summary>
    private sealed class InvasionState
    {
        private readonly List<MapInvasionsConfiguration.MapInvasion> _remainingOfCycle = [];

        public InvasionState(DateTime firstInvasionUtc)
        {
            this.NextInvasionUtc = firstInvasionUtc;
        }

        public AsyncLock Lock { get; } = new();

        public DateTime NextInvasionUtc { get; set; }

        public int HandledForceStartCount { get; set; }

        /// <summary>
        /// Gets the alive monsters of the invasions, with the invasion which spawned them.
        /// </summary>
        public ConcurrentDictionary<Monster, MapInvasionsConfiguration.MapInvasion> Monsters { get; } = new();

        /// <summary>
        /// Takes the next invasion of the cycle, and starts a new cycle in a random order when the cycle is over.
        /// </summary>
        public MapInvasionsConfiguration.MapInvasion? TakeNextInvasion(MapInvasionsConfiguration configuration)
        {
            // The configuration may have changed since the cycle started.
            this._remainingOfCycle.RemoveAll(invasion => !configuration.Invasions.Contains(invasion));
            if (this._remainingOfCycle.Count == 0)
            {
                this._remainingOfCycle.AddRange(configuration.Invasions.OrderBy(_ => Rand.NextInt(0, int.MaxValue)));
            }

            if (this._remainingOfCycle.Count == 0)
            {
                return null;
            }

            var next = this._remainingOfCycle[^1];
            this._remainingOfCycle.RemoveAt(this._remainingOfCycle.Count - 1);
            return next;
        }

        /// <summary>
        /// Removes the alive monsters of the previous invasion of a map.
        /// </summary>
        public async ValueTask RemoveMonstersAsync(MapInvasionsConfiguration.MapInvasion invasion)
        {
            var monsters = this.Monsters
                .Where(pair => pair.Value == invasion || pair.Value.MapNumber == invasion.MapNumber)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var monster in monsters)
            {
                this.Monsters.TryRemove(monster, out _);
                if (!monster.IsDisposed && !monster.IsDisposing)
                {
                    await monster.CurrentMap.RemoveAsync(monster).ConfigureAwait(false);
                    monster.Dispose();
                }
            }
        }
    }
}
