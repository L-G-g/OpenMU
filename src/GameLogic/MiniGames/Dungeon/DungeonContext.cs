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
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The context of a dungeon: an instance of a hunting map (the entrance map of the definition, e.g. Devias) for one party,
/// isolated from the other players. The game level of the definition selects the dungeon of the
/// <see cref="DungeonConfiguration.Dungeons"/> and its normal or extreme version, in which the monsters are stronger
/// (<see cref="DungeonConfiguration.ExtremeHealthMultiplier"/> etc.) and drop the high items more often.
/// </summary>
/// <remarks>
/// The map has the usual monsters of the map, which are stronger (<see cref="DungeonConfiguration.MonsterHealthMultiplier"/> etc.)
/// and drop through the <see cref="DungeonDropGenerator"/>, plus the monsters of the spawn areas of the map which spawn only in events,
/// e.g. along the ways between the bosses. The dungeon has <see cref="DungeonConfiguration.Stages"/>, each with a boss: it appears once
/// at a fixed place when the boss of the stage before is dead, the players killed enough monsters of the stage and enough time passed.
/// Golden messages tell the players what to do. A boss has a magic effect which the client shows (<see cref="DungeonConfiguration.BossEffectNumber"/>).
/// The players appear at <see cref="DungeonSettings.EntryX"/>, <see cref="DungeonSettings.EntryY"/>, and
/// the safezone of the map is blocked (<see cref="DungeonConfiguration.BlockSafezone"/>). The game ends after the game duration of the definition; dying or leaving the map
/// leaves the dungeon for good, because the entry is used up.
/// </remarks>
public sealed class DungeonContext : MiniGameContext
{
    /// <summary>
    /// The terrain attribute which blocks the safezone, like the gates of the Imperial Guardian.
    /// </summary>
    private const TerrainAttributeType SafezoneBlockAttribute = TerrainAttributeType.Water;

    /// <summary>
    /// The maximum number of areas in one message to the client, which has one byte for its size.
    /// </summary>
    private const int MaximumAreasPerMessage = 50;

    private const int MapSize = 256;

    /// <summary>
    /// Every how many kills of the monsters of the stage the players see the progress.
    /// </summary>
    private const int KillProgressStep = 25;

    /// <summary>
    /// The range around a boss in which its escorts appear.
    /// </summary>
    private const int EscortRange = 3;

    private static readonly TimeSpan[] RemainingTimeMessages = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)];

    private readonly DungeonConfiguration _configuration;
    private readonly DungeonSettings _dungeon;
    private readonly bool _isExtreme;
    private readonly List<(DungeonStage Stage, MonsterDefinition? Boss, MonsterDefinition? Escort)> _stages;
    private readonly ConcurrentDictionary<Monster, byte> _preparedMonsters = new();
    private readonly ConcurrentDictionary<Monster, int> _bosses = new();
    private readonly ConcurrentDictionary<Monster, byte> _escorts = new();
    private readonly ConcurrentDictionary<short, int> _kills = new();
    private readonly object _stageLock = new();
    private int _stageIndex;
    private bool _isStageBossSpawned;
    private bool _isStageReadyAnnounced;
    private DateTime _startTime = DateTime.UtcNow;
    private List<(byte StartX, byte StartY, byte EndX, byte EndY)>? _blockedAreas;
    private int _safezoneBlocked;

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
        (this._dungeon, this._isExtreme) = this._configuration.FindByGameLevel(definition.GameLevel)
                                           ?? (this._configuration.Dungeons.FirstOrDefault() ?? new DungeonSettings { Number = DungeonDefaults.DeviasDungeonNumber }, false);
        this._stages = this._configuration.GetStages(this._dungeon)
            .Select(stage => (stage, FindMonster(stage.BossDesignation), stage.EscortCount > 0 ? FindMonster(stage.EscortDesignation) : null))
            .ToList();
        this.Loot = new DungeonLoot(this, this._configuration, this.Logger);
        var bossDefinitions = new Dictionary<MonsterDefinition, double>();
        foreach (var (stage, boss, escort) in this._stages)
        {
            if (escort is not null)
            {
                bossDefinitions[escort] = stage.EscortExcellentDropChance;
            }

            if (boss is not null)
            {
                bossDefinitions[boss] = stage.ExcellentDropChance;
            }
        }

        this.DropGenerator = new DungeonDropGenerator(
            gameContext.DropGenerator,
            this._configuration,
            this.Loot,
            bossDefinitions,
            this._dungeon.GetDropMapNumbers(),
            this._configuration.GetHighItemWeight(this._dungeon, this._isExtreme),
            this._configuration.GetMonsterExcellentDropChance(this._dungeon));

        MonsterDefinition? FindMonster(string designation)
        {
            return string.IsNullOrEmpty(designation)
                ? null
                : gameContext.Configuration.Monsters.FirstOrDefault(m => m.Designation.ValueInNeutralLanguage == designation);
        }
    }

    /// <summary>
    /// Gets the loot of the dungeon, which shares the drops with need, greed or pass.
    /// </summary>
    public DungeonLoot Loot { get; }

    /// <summary>
    /// Gets the settings of this dungeon.
    /// </summary>
    public DungeonSettings Dungeon => this._dungeon;

    /// <summary>
    /// Gets a value indicating whether this is the extreme version of the dungeon.
    /// </summary>
    public bool IsExtreme => this._isExtreme;

    /// <inheritdoc />
    internal override bool IsInstanceOfNormalMap => true;

    /// <inheritdoc />
    internal override bool SpawnsMonstersOfMap => this._configuration.SpawnMonstersOfMap;

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
    internal override Point? GetEntrySpawnPosition(Player player)
    {
        return ReferenceEquals(player.CurrentMap, this.Map)
            ? new Point(this._dungeon.EntryX, this._dungeon.EntryY)
            : null;
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        this._startTime = DateTime.UtcNow;
        await base.OnGameStartAsync(players).ConfigureAwait(false);
        var name = string.IsNullOrEmpty(this._dungeon.Name) ? DungeonMessages.DefaultName : this._dungeon.Name;
        await this.ShowGoldenTextAsync(string.Format(
            this._isExtreme ? DungeonMessages.StartedExtremeFormat : DungeonMessages.StartedFormat,
            (int)Math.Round(this.Definition.GameDuration.TotalMinutes),
            name)).ConfigureAwait(false);
        if (this._stages.Count > 0)
        {
            await this.ShowGoldenTextAsync(this._stages[0].Stage.Hint).ConfigureAwait(false);
        }

        _ = Task.Run(() => this.RunTimersAsync(this.GameEndedToken), this.GameEndedToken);
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectAddedToMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectAddedToMapAsync(args).ConfigureAwait(false);
        if (this._configuration.BlockSafezone && Interlocked.Exchange(ref this._safezoneBlocked, 1) == 0)
        {
            this._blockedAreas = BlockSafezone(args.Map.Terrain);
        }

        if (args.Object is Player player && this._blockedAreas is { Count: > 0 } areas)
        {
            await SendBlockedAreasAsync(player, areas, true).ConfigureAwait(false);
        }

        if (args.Object is Monster { Definition.ObjectKind: NpcObjectKind.Monster } monster
            && !this._bosses.ContainsKey(monster)
            && !this._escorts.ContainsKey(monster)
            && this._preparedMonsters.TryAdd(monster, 0))
        {
            // The spawns of the map are created with the drop generator of the game, and a respawned monster is added again.
            this.ApplyMultipliers(monster, false);
            monster.Health = (int)monster.Attributes[Stats.MaximumHealth];
            monster.DropGenerator = this.DropGenerator;
            this.Logger.LogDebug("{context}: Prepared {monster} with {health} health.", this, monster, monster.Health);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectRemovedFromMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectRemovedFromMapAsync(args).ConfigureAwait(false);
        if (args.Object is Player player && this._blockedAreas is { Count: > 0 } areas)
        {
            // The player goes back to the same map number (e.g. Devias), whose terrain the client might not load again.
            await SendBlockedAreasAsync(player, areas, false).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void OnMonsterDied(object? sender, DeathInformation e)
    {
        base.OnMonsterDied(sender, e);
        if (sender is not Monster monster)
        {
            return;
        }

        if (this._bosses.TryGetValue(monster, out var bossStage))
        {
            _ = this.OnBossDiedAsync(monster, bossStage);
            return;
        }

        if (this._escorts.ContainsKey(monster))
        {
            // The escorts of a boss don't count for a stage.
            return;
        }

        var kills = this._kills.AddOrUpdate(monster.Definition.Number, 1, (_, count) => count + 1);
        string? message = null;
        lock (this._stageLock)
        {
            if (this._stageIndex < this._stages.Count
                && !this._isStageBossSpawned
                && this._stages[this._stageIndex].Stage is { } stage
                && GetKillCount(stage, monster.Definition.Number) is { } killCount
                && kills < killCount
                && kills % KillProgressStep == 0)
            {
                message = string.Format(DungeonMessages.KillProgressFormat, kills, killCount, monster.Definition.Designation.ValueInNeutralLanguage);
            }
        }

        if (message is not null)
        {
            _ = this.ShowGoldenTextAsync(message);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        await this.ShowGoldenTextAsync(DungeonMessages.TimeOver).ConfigureAwait(false);
        await this.Loot.ResolveAllAsync().ConfigureAwait(false);
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
    }

    /// <summary>
    /// Blocks the safezone of the terrain of the dungeon, so that the players can't go into the town.
    /// </summary>
    /// <param name="terrain">The terrain of the dungeon map, which belongs only to this dungeon.</param>
    /// <returns>The blocked areas, as rectangles, to tell the clients.</returns>
    private static List<(byte StartX, byte StartY, byte EndX, byte EndY)> BlockSafezone(GameMapTerrain terrain)
    {
        // Runs of safezone coordinates per row; a run continues the rectangle above if it has the same columns.
        var areas = new List<(byte StartX, byte StartY, byte EndX, byte EndY)>();
        var open = new Dictionary<(int StartX, int EndX), int>();
        for (var y = 0; y <= MapSize; y++)
        {
            // The row after the last one has no runs, so that all open rectangles are closed.
            var runs = new HashSet<(int StartX, int EndX)>();
            for (var x = 0; y < MapSize && x < MapSize; x++)
            {
                if (!terrain.SafezoneMap[x, y])
                {
                    continue;
                }

                var start = x;
                while (x + 1 < MapSize && terrain.SafezoneMap[x + 1, y])
                {
                    x++;
                }

                runs.Add((start, x));
            }

            foreach (var (run, startY) in open.Where(entry => !runs.Contains(entry.Key)).ToList())
            {
                areas.Add(((byte)run.StartX, (byte)startY, (byte)run.EndX, (byte)(y - 1)));
                open.Remove(run);
            }

            foreach (var run in runs)
            {
                open.TryAdd(run, y);
            }
        }

        foreach (var (startX, startY, endX, endY) in areas)
        {
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    terrain.ApplyTerrainAttribute((byte)x, (byte)y, SafezoneBlockAttribute, true);
                }
            }
        }

        return areas;
    }

    private static async ValueTask SendBlockedAreasAsync(Player player, List<(byte StartX, byte StartY, byte EndX, byte EndY)> areas, bool block)
    {
        foreach (var chunk in areas.Chunk(MaximumAreasPerMessage))
        {
            await player.InvokeViewPlugInAsync<IChangeTerrainAttributesViewPlugin>(p => p.ChangeAttributesAsync(SafezoneBlockAttribute, block, chunk)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets how many monsters with the number the players have to kill in the stage.
    /// </summary>
    /// <returns>The number of kills, or <c>null</c> if the monster doesn't count for the stage.</returns>
    private static int? GetKillCount(DungeonStage stage, short monsterNumber)
    {
        if (stage.MonsterNumber == monsterNumber)
        {
            return stage.KillCount;
        }

        return stage.SecondMonsterNumber > 0 && stage.SecondMonsterNumber == monsterNumber ? stage.SecondKillCount : null;
    }

    private bool AreKillsReached(DungeonStage stage)
    {
        return this._kills.GetValueOrDefault(stage.MonsterNumber) >= stage.KillCount
               && (stage.SecondMonsterNumber <= 0 || this._kills.GetValueOrDefault(stage.SecondMonsterNumber) >= stage.SecondKillCount);
    }

    private void ApplyMultipliers(Monster monster, bool isBoss)
    {
        var (health, damage, defense) = this._configuration.GetMultipliers(this._dungeon, this._isExtreme, isBoss);

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
            await Task.WhenAll(this.ShowRemainingTimeAsync(cancellationToken), this.RunStagesAsync(cancellationToken)).ConfigureAwait(false);
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

    /// <summary>
    /// Checks every second whether the boss of the current stage can appear. A skipped wait (/skipwait of a game master)
    /// lets it appear right away, without the kills and the time.
    /// </summary>
    private async Task RunStagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var skipped = await this.DelayWithSkipAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            int stageIndex;
            string? message = null;
            var spawn = false;
            lock (this._stageLock)
            {
                stageIndex = this._stageIndex;
                if (stageIndex >= this._stages.Count)
                {
                    return;
                }

                if (this._isStageBossSpawned)
                {
                    continue;
                }

                var stage = this._stages[stageIndex].Stage;
                var killsReached = this.AreKillsReached(stage);
                var remainingTime = stage.MinimumTime - (DateTime.UtcNow - this._startTime);
                if (skipped || (killsReached && remainingTime <= TimeSpan.Zero))
                {
                    this._isStageBossSpawned = true;
                    spawn = true;
                }
                else if (killsReached && !this._isStageReadyAnnounced)
                {
                    this._isStageReadyAnnounced = true;
                    message = string.Format(DungeonMessages.StageReadyFormat, stage.KillCount + (stage.SecondMonsterNumber > 0 ? stage.SecondKillCount : 0), stage.BossDesignation, (int)Math.Ceiling(remainingTime.TotalSeconds));
                }
            }

            if (message is not null)
            {
                await this.ShowGoldenTextAsync(message).ConfigureAwait(false);
            }

            if (spawn)
            {
                await this.SpawnBossAsync(stageIndex).ConfigureAwait(false);
            }
        }
    }

    private async Task OnBossDiedAsync(Monster boss, int stageIndex)
    {
        try
        {
            string? nextHint = null;
            var isFinished = false;
            lock (this._stageLock)
            {
                if (stageIndex != this._stageIndex)
                {
                    return;
                }

                this._stageIndex++;
                this._isStageBossSpawned = false;
                this._isStageReadyAnnounced = false;
                if (this._stageIndex < this._stages.Count)
                {
                    nextHint = this._stages[this._stageIndex].Stage.Hint;
                }
                else
                {
                    isFinished = true;
                }
            }

            await this.ShowGoldenTextAsync(string.Format(DungeonMessages.BossKilledFormat, boss.Definition.Designation.ValueInNeutralLanguage)).ConfigureAwait(false);
            await this.ShowGoldenTextAsync(nextHint ?? DungeonMessages.Completed).ConfigureAwait(false);
            if (isFinished
                && this._dungeon.Number == DungeonDefaults.DeviasDungeonNumber
                && Story.StoryModeFeaturePlugIn.Get(this.GameContext) is { } story)
            {
                await this.ForEachPlayerAsync(player => story.OnDungeonFinishedAsync(player).AsTask()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Unexpected error after the boss {boss} died.", this, boss);
        }
    }

    private async Task ShowRemainingTimeAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        foreach (var remaining in RemainingTimeMessages.Where(remaining => remaining < this.Definition.GameDuration).OrderByDescending(remaining => remaining))
        {
            var wait = this.Definition.GameDuration - remaining - (DateTime.UtcNow - start);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            var minutes = (int)remaining.TotalMinutes;
            await this.ShowGoldenTextAsync(minutes == 1 ? DungeonMessages.RemainingOneMinute : string.Format(DungeonMessages.RemainingMinutesFormat, minutes)).ConfigureAwait(false);
        }
    }

    private async ValueTask SpawnBossAsync(int stageIndex)
    {
        var (stage, definition, escortDefinition) = this._stages[stageIndex];
        if (definition is null)
        {
            this.Logger.LogWarning("{context}: The boss '{boss}' isn't defined.", this, stage.BossDesignation);
            return;
        }

        if (await this.SpawnBossMonsterAsync(definition, stage.BossX, stage.BossY, 0, boss => this._bosses[boss] = stageIndex).ConfigureAwait(false) is not { } boss)
        {
            return;
        }

        for (var i = 0; escortDefinition is not null && i < stage.EscortCount; i++)
        {
            await this.SpawnBossMonsterAsync(escortDefinition, stage.BossX, stage.BossY, EscortRange, escort => this._escorts[escort] = 0).ConfigureAwait(false);
        }

        await this.ShowGoldenTextAsync(string.Format(DungeonMessages.BossAppearedFormat, definition.Designation.ValueInNeutralLanguage, boss.Position.X, boss.Position.Y)).ConfigureAwait(false);
    }

    /// <summary>
    /// Spawns a boss or an escort of a boss, with the multipliers and the magic effect of the bosses.
    /// </summary>
    /// <param name="definition">The definition of the monster.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="range">The range around the coordinates in which the monster appears.</param>
    /// <param name="register">Registers the monster, before it's added to the map.</param>
    /// <returns>The monster, or <c>null</c> if it couldn't be spawned.</returns>
    private async ValueTask<Monster?> SpawnBossMonsterAsync(MonsterDefinition definition, byte x, byte y, int range, Action<Monster> register)
    {
        var spawnArea = new MonsterSpawnArea
        {
            GameMap = this.Map.Definition,
            MonsterDefinition = definition,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = (byte)Math.Max(x - range, 0),
            X2 = (byte)Math.Min(x + range, byte.MaxValue),
            Y1 = (byte)Math.Max(y - range, 0),
            Y2 = (byte)Math.Min(y + range, byte.MaxValue),
        };
        var boss = new Monster(spawnArea, definition, this.Map, this.DropGenerator, new BasicMonsterIntelligence(), this.GameContext.PlugInManager, this.GameContext.PathFinderPool, this);
        this.ApplyMultipliers(boss, true);
        try
        {
            boss.Initialize();
        }
        catch (InvalidOperationException ex)
        {
            this.Logger.LogWarning(ex, "{context}: Couldn't spawn {boss} at {x}, {y}.", this, definition.Designation, x, y);
            boss.Dispose();
            return null;
        }

        register(boss);
        await this.Map.AddAsync(boss).ConfigureAwait(false);
        boss.OnSpawn();
        if (this._configuration.BossEffectNumber > 0)
        {
            var effect = new MagicEffectDefinition
            {
                Number = this._configuration.BossEffectNumber,
                Name = "Dungeon Boss",
                InformObservers = true,
                StopByDeath = false,
            };
            await boss.MagicEffectList.AddEffectAsync(new MagicEffect(Timeout.InfiniteTimeSpan, effect)).ConfigureAwait(false);
        }

        return boss;
    }

    private async ValueTask ShowGoldenTextAsync(string text)
    {
        await this.ForEachPlayerAsync(player => player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).AsTask()).ConfigureAwait(false);
    }
}
