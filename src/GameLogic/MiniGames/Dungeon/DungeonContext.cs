// <copyright file="DungeonContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The context of a dungeon: an instance of a hunting map (the entrance map of the definition, e.g. Devias) for one party,
/// isolated from the other players.
/// </summary>
/// <remarks>
/// The map has the usual monsters of the map, which are stronger (<see cref="DungeonConfiguration.MonsterHealthMultiplier"/> etc.)
/// and drop through the <see cref="DungeonDropGenerator"/>. After <see cref="DungeonConfiguration.BossSpawnDelay"/>, the boss appears
/// once at a fixed place. The game ends after the game duration of the definition; dying or leaving the map
/// leaves the dungeon for good, because the entry is used up.
/// </remarks>
public sealed class DungeonContext : MiniGameContext
{
    private static readonly TimeSpan[] RemainingTimeMessages = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)];

    private readonly DungeonConfiguration _configuration;
    private readonly MonsterDefinition? _bossDefinition;
    private readonly ConcurrentDictionary<Monster, byte> _preparedMonsters = new();
    private Monster? _boss;

    /// <summary>
    /// Initializes a new instance of the <see cref="DungeonContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public DungeonContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
        this._configuration = DungeonFeaturePlugIn.GetConfiguration(gameContext) ?? new DungeonConfiguration();
        this._bossDefinition = gameContext.Configuration.Monsters.FirstOrDefault(m => m.Designation.ValueInNeutralLanguage == this._configuration.BossDesignation);
        this.Loot = new DungeonLoot(this, this._configuration, this.Logger);
        this.DropGenerator = new DungeonDropGenerator(gameContext.DropGenerator, this._configuration, this.Loot, this._bossDefinition);
    }

    /// <summary>
    /// Gets the loot of the dungeon, which shares the drops with need, greed or pass.
    /// </summary>
    public DungeonLoot Loot { get; }

    /// <inheritdoc />
    protected override TimeSpan MinimumEnterDuration => TimeSpan.Zero;

    /// <inheritdoc />
    protected override TimeSpan CountdownDuration => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the players which are in the dungeon.
    /// </summary>
    /// <returns>The players.</returns>
    internal async ValueTask<List<Player>> GetPlayersAsync()
    {
        var players = new List<Player>();
        await this.ForEachPlayerAsync(player =>
        {
            lock (players)
            {
                players.Add(player);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);
        return players;
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        await base.OnGameStartAsync(players).ConfigureAwait(false);
        await this.ShowGoldenTextAsync(string.Format(
            DungeonMessages.StartedFormat,
            (int)Math.Round(this.Definition.GameDuration.TotalMinutes),
            (int)Math.Round(this._configuration.BossSpawnDelay.TotalMinutes))).ConfigureAwait(false);
        _ = Task.Run(() => this.RunTimersAsync(this.GameEndedToken), this.GameEndedToken);
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectAddedToMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectAddedToMapAsync(args).ConfigureAwait(false);
        if (args.Object is Monster { Definition.ObjectKind: NpcObjectKind.Monster } monster
            && !ReferenceEquals(monster, this._boss)
            && this._preparedMonsters.TryAdd(monster, 0))
        {
            // The spawns of the map are created with the drop generator of the game, and a respawned monster is added again.
            ApplyMultipliers(monster, this._configuration.MonsterHealthMultiplier, this._configuration.MonsterDamageMultiplier, this._configuration.MonsterDefenseMultiplier);
            monster.Health = (int)monster.Attributes[Stats.MaximumHealth];
            monster.DropGenerator = this.DropGenerator;
        }
    }

    /// <inheritdoc />
    protected override void OnMonsterDied(object? sender, DeathInformation e)
    {
        base.OnMonsterDied(sender, e);
        if (sender is Monster monster && ReferenceEquals(monster, this._boss))
        {
            _ = this.ShowGoldenTextAsync(string.Format(DungeonMessages.BossKilledFormat, monster.Definition.Designation.ValueInNeutralLanguage));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        await this.ShowGoldenTextAsync(DungeonMessages.TimeOver).ConfigureAwait(false);
        await this.Loot.ResolveAllAsync().ConfigureAwait(false);
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
    }

    private static void ApplyMultipliers(Monster monster, float health, float damage, float defense)
    {
        Multiply(monster, Stats.MaximumHealth, health);
        Multiply(monster, Stats.MinimumPhysBaseDmg, damage);
        Multiply(monster, Stats.MaximumPhysBaseDmg, damage);
        Multiply(monster, Stats.DefenseBase, defense);

        static void Multiply(Monster monster, AttributeDefinition attribute, float multiplier)
        {
            if (Math.Abs(multiplier - 1) > 0.001f)
            {
                monster.Attributes.AddElement(new SimpleElement(multiplier, AggregateType.Multiplicate), attribute);
            }
        }
    }

    private async Task RunTimersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var start = DateTime.UtcNow;
            var events = RemainingTimeMessages
                .Where(remaining => remaining < this.Definition.GameDuration)
                .Select(remaining => (At: this.Definition.GameDuration - remaining, Boss: false, Remaining: remaining))
                .Append((At: this._configuration.BossSpawnDelay, Boss: true, Remaining: TimeSpan.Zero))
                .Where(e => e.At < this.Definition.GameDuration)
                .OrderBy(e => e.At)
                .ToList();
            foreach (var timedEvent in events)
            {
                var wait = timedEvent.At - (DateTime.UtcNow - start);
                if (wait > TimeSpan.Zero)
                {
                    await this.DelayWithSkipAsync(wait, cancellationToken).ConfigureAwait(false);
                }

                if (timedEvent.Boss)
                {
                    await this.SpawnBossAsync().ConfigureAwait(false);
                }
                else
                {
                    await this.ShowGoldenTextAsync(string.Format(DungeonMessages.RemainingMinutesFormat, (int)timedEvent.Remaining.TotalMinutes)).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The game ended.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Unexpected error in the timers of the dungeon.", this);
        }
    }

    private async ValueTask SpawnBossAsync()
    {
        if (this._bossDefinition is not { } definition)
        {
            this.Logger.LogWarning("{context}: The boss '{boss}' isn't defined.", this, this._configuration.BossDesignation);
            return;
        }

        var spawnArea = new MonsterSpawnArea
        {
            GameMap = this.Map.Definition,
            MonsterDefinition = definition,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = this._configuration.BossX,
            X2 = this._configuration.BossX,
            Y1 = this._configuration.BossY,
            Y2 = this._configuration.BossY,
        };
        var boss = new Monster(spawnArea, definition, this.Map, this.DropGenerator, new BasicMonsterIntelligence(), this.GameContext.PlugInManager, this.GameContext.PathFinderPool, this);
        ApplyMultipliers(boss, this._configuration.BossHealthMultiplier, this._configuration.BossDamageMultiplier, this._configuration.BossDefenseMultiplier);
        try
        {
            boss.Initialize();
        }
        catch (InvalidOperationException ex)
        {
            this.Logger.LogWarning(ex, "{context}: Couldn't spawn the boss at {x}, {y}.", this, spawnArea.X1, spawnArea.Y1);
            boss.Dispose();
            return;
        }

        this._boss = boss;
        await this.Map.AddAsync(boss).ConfigureAwait(false);
        boss.OnSpawn();
        await this.ShowGoldenTextAsync(string.Format(DungeonMessages.BossAppearedFormat, definition.Designation.ValueInNeutralLanguage, boss.Position.X, boss.Position.Y)).ConfigureAwait(false);
    }

    private async ValueTask ShowGoldenTextAsync(string text)
    {
        await this.ForEachPlayerAsync(player => player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).AsTask()).ConfigureAwait(false);
    }
}
