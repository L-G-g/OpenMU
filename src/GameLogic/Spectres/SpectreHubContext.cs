// <copyright file="SpectreHubContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Duel;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The hub of the spectres of a player (see <see cref="SpectreHub"/>): the rivals of the seven kinds wait at their spots,
/// sad and abandoned, and challenge the player when it comes close. When the player accepts, both go to the duel.
/// </summary>
public sealed class SpectreHubContext : MiniGameContext
{
    /// <summary>
    /// The distance to a rival at which it challenges the player.
    /// </summary>
    private const int ChallengeDistance = 3;

    /// <summary>
    /// The spots of the rivals in the room of the Duel Arena, by kind, around the entry.
    /// </summary>
    private static readonly Point[] RivalSpots =
    [
        new(147, 70), new(150, 74), new(153, 75), new(156, 74), new(160, 70), new(145, 66), new(162, 66),
    ];

    /// <summary>
    /// The animations of the waiting rivals, by kind: sad poses of the emotes of the client.
    /// </summary>
    private static readonly byte[] IdleAnimations = [0x80, 0x89, 0x8F, 0x80, 0x8B, 0x89, 0x80];

    private static readonly TimeSpan ChallengeCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AnyChallengeCooldown = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan IdleAnimationInterval = TimeSpan.FromSeconds(12);

    private readonly ConcurrentDictionary<SpectreKind, RivalPlayer> _rivals = new();
    private readonly ConcurrentDictionary<SpectreKind, DateTime> _nextChallenges = new();
    private readonly CancellationTokenSource _cts = new();
    private Player? _owner;
    private int _duelStarting;
    private int _closing;
    private DateTime _nextIdleAnimation = DateTime.UtcNow;
    private DateTime _nextAnyChallenge = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreHubContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public SpectreHubContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <inheritdoc />
    internal override bool IsInstanceOfNormalMap => true;

    /// <inheritdoc />
    internal override bool SpawnsMonstersOfMap => false;

    /// <inheritdoc />
    protected override bool AllowEnterWhilePlaying => true;

    /// <inheritdoc />
    protected override TimeSpan MinimumEnterDuration => TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    protected override TimeSpan CountdownDuration => TimeSpan.FromSeconds(1);

    /// <summary>
    /// Handles the answer of the player to the challenge of a rival, or the challenge of a rival by the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="rival">The rival.</param>
    /// <param name="accepted">If set to <c>true</c>, the player accepted the duel.</param>
    /// <returns>The task.</returns>
    internal async ValueTask AnswerChallengeAsync(Player player, RivalPlayer rival, bool accepted)
    {
        if (!ReferenceEquals(player, this._owner) || !ReferenceEquals(rival.CurrentMiniGame, this))
        {
            return;
        }

        if (!accepted)
        {
            await this.SayAsync(rival, "Otra vez sera...").ConfigureAwait(false);
            return;
        }

        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        if (SpectreProgress.GetVictories(character, rival.Kind) >= SpectreProgress.MaximumVictories)
        {
            await this.SayAsync(rival, "Ya recorde todo lo que fui. No tengo nada mas para ensenarte.").ConfigureAwait(false);
            return;
        }

        if (Interlocked.CompareExchange(ref this._duelStarting, 1, 0) != 0)
        {
            return;
        }

        if (!await SpectreHub.EnterDuelAsync(player, rival.Kind, rival.Tier).ConfigureAwait(false))
        {
            Interlocked.Exchange(ref this._duelStarting, 0);
        }
    }

    /// <summary>
    /// Lets the player challenge the nearest rival, e.g. when it closed the request of the rival.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ChallengeNearestRivalAsync(Player player)
    {
        var rival = this._rivals.Values
            .Where(r => r.CurrentMap == this.Map)
            .OrderBy(r => r.GetDistanceTo(player))
            .FirstOrDefault();
        if (rival is null || rival.GetDistanceTo(player) > ChallengeDistance + 2)
        {
            await player.ShowBlueMessageAsync("Acercate a un espectro para desafiarlo.").ConfigureAwait(false);
            return;
        }

        await this.AnswerChallengeAsync(player, rival, true).ConfigureAwait(false);
    }

    /// <inheritdoc />
    internal override Point? GetEntrySpawnPosition(Player player)
    {
        if (!ReferenceEquals(player.CurrentMap, this.Map))
        {
            return null;
        }

        return player is RivalPlayer rival ? rival.Spot : SpectreHub.HubEntry;
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectAddedToMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectAddedToMapAsync(args).ConfigureAwait(false);
        if (args.Object is Player player and not RivalPlayer && Interlocked.CompareExchange(ref this._owner, player, null) is null)
        {
            _ = Task.Run(() => this.RunAsync(player, this._cts.Token));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectRemovedFromMapAsync((GameMap Map, ILocateable Object) args)
    {
        // A player which goes to the duel entered it before it left this map, so it keeps its game.
        var nextGame = (args.Object as Player)?.CurrentMiniGame;
        await base.OnObjectRemovedFromMapAsync(args).ConfigureAwait(false);
        if (args.Object is not Player player)
        {
            return;
        }

        if (nextGame is not null && !ReferenceEquals(nextGame, this))
        {
            player.CurrentMiniGame = nextGame;
        }

        if (ReferenceEquals(player, this._owner))
        {
            _ = Task.Run(this.CloseAsync);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        Interlocked.Exchange(ref this._closing, 1);
        await this._cts.CancelAsync().ConfigureAwait(false);
        await this.StopRivalsAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
        this._cts.Dispose();
    }

    private static async ValueTask StopRivalAsync(RivalPlayer rival)
    {
        try
        {
            await rival.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            rival.Logger.LogError(ex, "Error while stopping the rival {Name}.", rival.Name);
        }
        finally
        {
            await rival.DisposeAsync().ConfigureAwait(false);
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
            this.Logger.LogError(ex, "{context}: Error while closing the hub.", this);
        }
    }

    private async ValueTask StopRivalsAsync()
    {
        foreach (var kind in this._rivals.Keys.ToList())
        {
            if (this._rivals.TryRemove(kind, out var rival))
            {
                await StopRivalAsync(rival).ConfigureAwait(false);
            }
        }
    }

    private async Task RunAsync(Player owner, CancellationToken cancellationToken)
    {
        try
        {
            await owner.ShowGoldenMessageAsync("Refugio de los espectros").ConfigureAwait(false);
            await owner.ShowBlueMessageAsync("Acercate a un espectro para que te desafie. Para salir: /espectro salir o el menu M.").ConfigureAwait(false);
            await this.SpawnRivalsAsync(owner, cancellationToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await this.TickAsync(owner).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The owner left the hub.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error in the hub.", this);
        }
    }

    private async ValueTask SpawnRivalsAsync(Player owner, CancellationToken cancellationToken)
    {
        if (owner.SelectedCharacter is not { } ownerCharacter)
        {
            return;
        }

        foreach (var kind in SpectreClasses.AllKinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tier = Math.Min(SpectreProgress.GetVictories(ownerCharacter, kind), SpectreEvolution.EvolutionCount);
            var characterName = await SpectreRivals.GetOrCreateCharacterAsync(owner, kind, tier).ConfigureAwait(false);
            if (characterName is null)
            {
                continue;
            }

            var rival = new RivalPlayer(this.GameContext, owner, kind, tier, RivalSpots[(int)kind]);
            rival.CurrentMiniGame = this;
            if (!this._rivals.TryAdd(kind, rival)
                || !await rival.InitializeAsync(SpectreRivals.GetLoginName(owner.Account!), characterName).ConfigureAwait(false))
            {
                this._rivals.TryRemove(new KeyValuePair<SpectreKind, RivalPlayer>(kind, rival));
                await rival.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            rival.Rotation = Direction.South;
            if (cancellationToken.IsCancellationRequested && this._rivals.TryRemove(kind, out var lateRival))
            {
                await StopRivalAsync(lateRival).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask TickAsync(Player owner)
    {
        if (owner.PlayerState.CurrentState != PlayerState.EnteredWorld || owner.CurrentMap != this.Map || !owner.IsAlive)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var showIdleAnimations = now >= this._nextIdleAnimation;
        if (showIdleAnimations)
        {
            this._nextIdleAnimation = now + IdleAnimationInterval;
        }

        RivalPlayer? challenger = null;
        foreach (var rival in this._rivals.Values)
        {
            if (rival.CurrentMap != this.Map)
            {
                continue;
            }

            if (showIdleAnimations)
            {
                await this.ShowIdleAnimationAsync(rival).ConfigureAwait(false);
            }

            if (rival.GetDistanceTo(owner) <= ChallengeDistance
                && (challenger is null || rival.GetDistanceTo(owner) < challenger.GetDistanceTo(owner))
                && (!this._nextChallenges.TryGetValue(rival.Kind, out var nextChallenge) || now >= nextChallenge))
            {
                challenger = rival;
            }
        }

        // One challenge at a time: the client shows one duel request.
        if (challenger is not null && this._duelStarting == 0 && now >= this._nextAnyChallenge)
        {
            this._nextChallenges[challenger.Kind] = now + ChallengeCooldown;
            this._nextAnyChallenge = now + AnyChallengeCooldown;
            await this.ChallengeAsync(owner, challenger).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowIdleAnimationAsync(RivalPlayer rival)
    {
        var animation = rival.Tier == 0 ? IdleAnimations[(int)rival.Kind] : (byte)0x80;
        rival.Pose = animation == 0x80 ? CharacterPose.Sitting : default;
        await rival.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(rival, animation, null, rival.Rotation), false).ConfigureAwait(false);
    }

    private async ValueTask ChallengeAsync(Player owner, RivalPlayer rival)
    {
        var victories = owner.SelectedCharacter is { } character ? SpectreProgress.GetVictories(character, rival.Kind) : 0;
        rival.Rotation = rival.GetDirectionTo(owner);
        string line;
        if (victories >= SpectreProgress.MaximumVictories)
        {
            line = "Ya recorde todo lo que fui. Gracias por no olvidarme.";
        }
        else if (victories == 0)
        {
            line = "...hace mucho que nadie me elige. Si me vences en duelo, te sigo.";
        }
        else
        {
            line = $"Empiezo a recordar quien fui ({rival.Tier}/{SpectreEvolution.EvolutionCount}). Peleame otra vez.";
        }

        await this.SayAsync(rival, line).ConfigureAwait(false);
        if (victories < SpectreProgress.MaximumVictories)
        {
            await owner.InvokeViewPlugInAsync<IShowDuelRequestPlugIn>(p => p.ShowDuelRequestAsync(rival)).ConfigureAwait(false);
        }
    }

    private async ValueTask SayAsync(RivalPlayer rival, string line)
    {
        if (this._owner is { } owner)
        {
            await owner.InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(line, rival.DisplayName, ChatMessageType.Normal)).ConfigureAwait(false);
        }
    }
}
