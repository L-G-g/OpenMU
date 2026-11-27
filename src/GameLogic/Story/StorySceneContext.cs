// <copyright file="StorySceneContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.Spectres;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The stage of a <see cref="StoryMemory"/>: an instance of a map for one player, in which the actors of the memory
/// (characters without a connection, like the rivals of the spectre hub) play a scene of the past. The player doesn't
/// take part: its character waits hidden at the <see cref="StoryMemory.ViewerSpot"/>, while the client shows the stage
/// with the camera of the memory and the lines of the actors. The client tells when a line is shown at which an actor
/// enters the stage (<see cref="OnLineShownAsync"/>). When the player leaves the map, the stage is closed.
/// </summary>
public sealed class StorySceneContext : MiniGameContext
{
    /// <summary>
    /// The animation of a character which sits down.
    /// </summary>
    private const byte SitAnimation = 0x80;

    /// <summary>
    /// The animation of a character which stands, which ends a pose and turns the character.
    /// </summary>
    private const byte StandAnimation = 0x7A;

    /// <summary>
    /// How long an actor which leaves the stage may walk before it disappears.
    /// </summary>
    private static readonly TimeSpan LeaveTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long the stage waits after the actors appeared before the client shows it, so that the client loaded the map
    /// and the actors.
    /// </summary>
    private static readonly TimeSpan CurtainDelay = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// The interval in which the names and the poses of the actors are sent again.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<int, RivalPlayer> _actors = new();
    private readonly ConcurrentDictionary<int, NonPlayerCharacter> _npcs = new();
    private readonly ConcurrentDictionary<int, byte> _heldAnimations = new();
    private readonly CancellationTokenSource _cts = new();
    private StoryMemory _memory = StoryMemory.Sacrifice;
    private Func<Player, ValueTask>? _returnAsync;
    private Func<Player, ValueTask>? _completedAsync;
    private Player? _owner;
    private int _shownLine = -1;
    private int _closing;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorySceneContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public StorySceneContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <inheritdoc />
    internal override bool IsInstanceOfNormalMap => true;

    /// <inheritdoc />
    internal override bool SpawnsMonstersOfMap => false;

    /// <inheritdoc />
    /// <remarks>The event spawns of Devias are the monsters of its dungeon.</remarks>
    internal override bool SpawnsMonstersOfEvent => false;

    /// <inheritdoc />
    protected override bool AllowEnterWhilePlaying => true;

    /// <inheritdoc />
    protected override TimeSpan MinimumEnterDuration => TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    protected override TimeSpan CountdownDuration => TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    /// <remarks>
    /// The player counts only once its client loaded the map, which may take longer than the entering phase.
    /// </remarks>
    protected override int MinimumPlayerCount => 0;

    /// <summary>
    /// Prepares the stage for a memory, before the player enters.
    /// </summary>
    /// <param name="memory">The memory.</param>
    /// <param name="returnAsync">How the player goes back when the memory ended.</param>
    /// <param name="completedAsync">What happens when the client of the player showed the last line, or <c>null</c>.</param>
    internal void Prepare(StoryMemory memory, Func<Player, ValueTask> returnAsync, Func<Player, ValueTask>? completedAsync = null)
    {
        this._memory = memory;
        this._returnAsync = returnAsync;
        this._completedAsync = completedAsync;
    }

    /// <summary>
    /// Handles that the client of the player shows a line of the memory: the actors which enter at this line appear.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="line">The index of the line.</param>
    /// <returns>The task.</returns>
    internal async ValueTask OnLineShownAsync(Player player, int line)
    {
        if (!ReferenceEquals(player, this._owner) || line < 1 || line >= this._memory.Lines.Count)
        {
            return;
        }

        // Each line only once, and never backwards.
        int shown;
        do
        {
            shown = this._shownLine;
            if (line <= shown)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref this._shownLine, line, shown) != shown);

        for (var index = 0; index < this._memory.Actors.Count; index++)
        {
            var entersAt = this._memory.Actors[index].EntersAtLine;
            if (entersAt > Math.Max(shown, 0) && entersAt <= line)
            {
                await this.SpawnActorAsync(player, index, this._cts.Token).ConfigureAwait(false);
            }
        }

        // The cues of the lines in between too, if the client skipped a notification.
        foreach (var cue in this._memory.Cues ?? [])
        {
            if (cue.Line > Math.Max(shown, 0) && cue.Line <= line)
            {
                await this.PlayCueAsync(cue).ConfigureAwait(false);
            }
        }

        if (line >= this._memory.Lines.Count - 1 && Interlocked.Exchange(ref this._completedAsync, null) is { } completedAsync)
        {
            // The player saw the whole memory.
            await completedAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lets the player leave the stage: it goes back to where it came from.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    internal async ValueTask LeaveAsync(Player player)
    {
        if (!ReferenceEquals(player, this._owner) || player.CurrentMap != this.Map)
        {
            return;
        }

        player.InvulnerableUntilUtc = default;
        if (this._returnAsync is { } returnAsync)
        {
            await returnAsync(player).ConfigureAwait(false);
        }
        else
        {
            await player.WarpToSafezoneAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    internal override Point? GetEntrySpawnPosition(Player player)
    {
        if (!ReferenceEquals(player.CurrentMap, this.Map))
        {
            return null;
        }

        return player is RivalPlayer actor ? actor.Spot : this._memory.ViewerSpot;
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectAddedToMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectAddedToMapAsync(args).ConfigureAwait(false);
        if (args.Object is Player player and not OfflinePlayer && Interlocked.CompareExchange(ref this._owner, player, null) is null)
        {
            _ = Task.Run(() => this.RunAsync(player, this._cts.Token));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectRemovedFromMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectRemovedFromMapAsync(args).ConfigureAwait(false);
        if (args.Object is Player player && ReferenceEquals(player, this._owner))
        {
            player.InvulnerableUntilUtc = default;
            _ = Task.Run(this.CloseAsync);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
        if (this._owner is { } owner && owner.CurrentMap == this.Map)
        {
            // The time of the stage is over.
            _ = Task.Run(async () => await this.LeaveAsync(owner).ConfigureAwait(false));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        Interlocked.Exchange(ref this._closing, 1);
        await this._cts.CancelAsync().ConfigureAwait(false);
        await this.StopActorsAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
        this._cts.Dispose();
    }

    private static async ValueTask StopActorAsync(RivalPlayer actor)
    {
        try
        {
            await actor.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            actor.Logger.LogError(ex, "Error while stopping the actor {Name}.", actor.Name);
        }
        finally
        {
            await actor.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task CloseAsync()
    {
        if (Interlocked.Exchange(ref this._closing, 1) != 0)
        {
            return;
        }

        try
        {
            await this.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while closing the stage.", this);
        }
    }

    private async ValueTask StopActorsAsync()
    {
        foreach (var index in this._actors.Keys.ToList())
        {
            if (this._actors.TryRemove(index, out var actor))
            {
                await StopActorAsync(actor).ConfigureAwait(false);
            }
        }

        foreach (var index in this._npcs.Keys.ToList())
        {
            if (this._npcs.TryRemove(index, out var npc))
            {
                try
                {
                    await this.Map.RemoveAsync(npc).ConfigureAwait(false);
                }
                finally
                {
                    await npc.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Lets an actor play a cue.
    /// </summary>
    /// <param name="cue">The cue.</param>
    /// <returns>The task.</returns>
    internal async ValueTask PlayCueAsync(StoryMemoryCue cue)
    {
        if (!this._actors.TryGetValue(cue.Actor, out var actor) || actor.CurrentMap != this.Map)
        {
            return;
        }

        switch (cue.Kind)
        {
            case StoryMemoryCueKind.Animate:
                if (cue.Holds)
                {
                    this._heldAnimations[cue.Actor] = cue.Animation;
                    actor.Pose = cue.Animation == SitAnimation ? CharacterPose.Sitting : default;
                }
                else
                {
                    this._heldAnimations.TryRemove(cue.Actor, out _);
                }

                await ShowAnimationAsync(actor, cue.Animation).ConfigureAwait(false);
                break;
            case StoryMemoryCueKind.StandUp:
                this._heldAnimations.TryRemove(cue.Actor, out _);
                actor.Pose = default;
                await ShowAnimationAsync(actor, StandAnimation).ConfigureAwait(false);
                break;
            case StoryMemoryCueKind.Turn when cue.Target is { } target:
                actor.Rotation = actor.Position.GetDirectionTo(target);
                await ShowAnimationAsync(actor, this._heldAnimations.TryGetValue(cue.Actor, out var held) ? held : StandAnimation).ConfigureAwait(false);
                break;
            case StoryMemoryCueKind.Walk when cue.Target is { } target:
                this._heldAnimations.TryRemove(cue.Actor, out _);
                actor.Pose = default;
                await this.WalkAsync(actor, target).ConfigureAwait(false);
                break;
            case StoryMemoryCueKind.Leave when cue.Target is { } target:
                this._heldAnimations.TryRemove(cue.Actor, out _);
                actor.Pose = default;
                var start = actor.Position;
                await this.WalkAsync(actor, target).ConfigureAwait(false);

                // The position of the actor is its target from the start of the walk on: the clients need the time of the steps.
                var steps = Math.Max(Math.Abs(target.X - start.X), Math.Abs(target.Y - start.Y));
                var walkTime = (actor.StepDelay * steps) + TimeSpan.FromMilliseconds(800);
                _ = Task.Run(() => this.RemoveWhenArrivedAsync(cue.Actor, actor, walkTime, this._cts.Token));
                break;
            default:
                break;
        }
    }

    private async Task RemoveWhenArrivedAsync(int index, RivalPlayer actor, TimeSpan walkTime, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(walkTime < LeaveTimeout ? walkTime : LeaveTimeout, cancellationToken).ConfigureAwait(false);
            if (this._actors.TryRemove(new KeyValuePair<int, RivalPlayer>(index, actor)))
            {
                await StopActorAsync(actor).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The stage is closed; the actor is stopped there.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while an actor left the stage.", this);
        }
    }

    /// <summary>
    /// Lets an actor walk to a target. The path finder of the monsters avoids the safezones, like the church of Devias,
    /// so the actor walks the straight line (in parts, as long as the client accepts) if it finds no path.
    /// </summary>
    private async ValueTask WalkAsync(RivalPlayer actor, Point target)
    {
        if (await new MovementHandler(actor, null).WalkToPositionAsync(target).ConfigureAwait(false))
        {
            return;
        }

        _ = Task.Run(() => this.WalkStraightAsync(actor, target, this._cts.Token));
    }

    private async Task WalkStraightAsync(RivalPlayer actor, Point target, CancellationToken cancellationToken)
    {
        const int maximumSteps = 15;
        try
        {
            var until = DateTime.UtcNow + LeaveTimeout;
            while (actor.Position != target && actor.CurrentMap == this.Map && DateTime.UtcNow < until)
            {
                if (actor.IsWalking)
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var steps = new List<WalkingStep>();
                var position = actor.Position;
                while (position != target && steps.Count < maximumSteps)
                {
                    var next = new Point(
                        (byte)(position.X + Math.Sign(target.X - position.X)),
                        (byte)(position.Y + Math.Sign(target.Y - position.Y)));
                    steps.Add(new WalkingStep(position, next, position.GetDirectionTo(next)));
                    position = next;
                }

                await actor.WalkToAsync(position, steps.ToArray()).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The stage is closed.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while an actor walked.", this);
        }
    }

    private static ValueTask ShowAnimationAsync(RivalPlayer actor, byte animation)
        => actor.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(actor, animation, null, actor.Rotation), false);

    private async ValueTask SpawnNpcAsync(int index)
    {
        var definition = this._memory.Actors[index];
        if (this._npcs.ContainsKey(index)
            || this.GameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == definition.NpcNumber) is not { } look)
        {
            return;
        }

        var area = new MonsterSpawnArea
        {
            MonsterDefinition = look,
            GameMap = this.Map.Definition,
            X1 = definition.Spot.X,
            X2 = definition.Spot.X,
            Y1 = definition.Spot.Y,
            Y2 = definition.Spot.Y,
            Direction = definition.Spot.GetDirectionTo(definition.LooksAt),
            Quantity = 1,
        };
        var npc = new NonPlayerCharacter(area, look, this.Map);
        if (!this._npcs.TryAdd(index, npc))
        {
            await npc.DisposeAsync().ConfigureAwait(false);
            return;
        }

        npc.Initialize();
        await this.Map.AddAsync(npc).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the index of the character of an actor among the characters of the memory: the NPCs need none.
    /// </summary>
    private int GetCharacterIndex(int index) => this._memory.Actors.Take(index).Count(a => !a.IsNpc);

    private async Task RunAsync(Player owner, CancellationToken cancellationToken)
    {
        try
        {
            var memory = this._memory;
            for (var index = 0; index < memory.Actors.Count; index++)
            {
                if (memory.Actors[index].EntersAtLine <= 0)
                {
                    await this.SpawnActorAsync(owner, index, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Created now, so that the actor appears without a delay at its line.
                    await this.GetOrCreateCharacterAsync(owner, index).ConfigureAwait(false);
                }
            }

            await Task.Delay(CurtainDelay, cancellationToken).ConfigureAwait(false);
            await this.RefreshActorsAsync().ConfigureAwait(false);
            await owner.ShowGoldenMessageAsync(memory.Title).ConfigureAwait(false);
            await owner.InvokeViewPlugInAsync<IStorySceneViewPlugIn>(p => p.ShowMemoryAsync(memory)).ConfigureAwait(false);

            using var timer = new PeriodicTimer(RefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (owner.PlayerState.CurrentState == PlayerState.EnteredWorld && owner.CurrentMap == this.Map)
                {
                    await this.RefreshActorsAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The owner left the stage.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error on the stage.", this);
        }
    }

    private ValueTask<string?> GetOrCreateCharacterAsync(Player owner, int index)
    {
        var definition = this._memory.Actors[index];
        if (definition.IsNpc)
        {
            return ValueTask.FromResult<string?>(null);
        }

        return SpectreRivals.GetOrCreateSceneActorAsync(owner, this.GetCharacterIndex(index), definition.Name, definition.Kind, definition.ItemLevel, definition.Equipment);
    }

    private async ValueTask SpawnActorAsync(Player owner, int index, CancellationToken cancellationToken)
    {
        var definition = this._memory.Actors[index];
        if (definition.IsNpc)
        {
            await this.SpawnNpcAsync(index).ConfigureAwait(false);
            return;
        }

        if (owner.Account is not { } account
            || this._actors.ContainsKey(index)
            || await this.GetOrCreateCharacterAsync(owner, index).ConfigureAwait(false) is not { } characterName)
        {
            return;
        }

        // The actor of an earlier visit of the stage may still be leaving the game.
        var waitUntil = DateTime.UtcNow.AddSeconds(5);
        while (this.GameContext.GetPlayerByCharacterName(characterName) is not null && DateTime.UtcNow < waitUntil)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        var actor = new RivalPlayer(this.GameContext, owner, definition.Kind, 0, definition.Spot, definition.Name);
        actor.CurrentMiniGame = this;
        if (!this._actors.TryAdd(index, actor)
            || !await actor.InitializeAsync(SpectreRivals.GetLoginName(account), characterName).ConfigureAwait(false))
        {
            this._actors.TryRemove(new KeyValuePair<int, RivalPlayer>(index, actor));
            await actor.DisposeAsync().ConfigureAwait(false);
            return;
        }

        actor.Rotation = definition.Spot.GetDirectionTo(definition.LooksAt);
        await this.RefreshActorAsync(index, actor).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            if (this._actors.TryRemove(index, out var lateActor))
            {
                await StopActorAsync(lateActor).ConfigureAwait(false);
            }

            return;
        }

        if (definition.WalksTo is { } target)
        {
            // The clients first show the actor where it appears.
            await Task.Delay(700, cancellationToken).ConfigureAwait(false);
            await this.WalkAsync(actor, target).ConfigureAwait(false);
        }
    }

    private async ValueTask RefreshActorsAsync()
    {
        foreach (var (index, actor) in this._actors)
        {
            if (actor.CurrentMap == this.Map)
            {
                await this.RefreshActorAsync(index, actor).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask RefreshActorAsync(int index, RivalPlayer actor)
    {
        var definition = this._memory.Actors[index];
        await actor.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowTitleAsync(actor, definition.Title), false).ConfigureAwait(false);
        if (this._heldAnimations.TryGetValue(index, out var held))
        {
            await ShowAnimationAsync(actor, held).ConfigureAwait(false);
        }
        else if (definition.IsSitting)
        {
            actor.Pose = CharacterPose.Sitting;
            await ShowAnimationAsync(actor, SitAnimation).ConfigureAwait(false);
        }
    }
}
