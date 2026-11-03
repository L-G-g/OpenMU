// <copyright file="SpectreHubContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The hub of the spectres of a player (see <see cref="SpectreHub"/>): the rivals of the seven kinds wait at their spots,
/// sad and abandoned. When the player clicks on one (cinematicas.dll sends <c>/espectro hablar</c>), the rival tells a part
/// of its story (<see cref="SpectreDialogues"/>) and offers a duel, if the player may duel it (<see cref="SpectreDuelRules"/>).
/// When the player accepts, both go to the duel.
/// </summary>
public sealed class SpectreHubContext : MiniGameContext
{
    /// <summary>
    /// The distance to a rival up to which <c>/espectro duelo</c> without a rival challenges the nearest one.
    /// </summary>
    private const int ChallengeDistance = 5;

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

    private static readonly TimeSpan IdleAnimationInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the hub waits after the rivals appeared before a rival tells the end of its story, so that the client loaded the map.
    /// </summary>
    private static readonly TimeSpan FinaleDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a rival which talks to the player keeps standing, without its idle animation.
    /// </summary>
    private static readonly TimeSpan TalkDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// When the rival casts its buff in the scene before the last duel: after the fade of the client.
    /// </summary>
    private static readonly TimeSpan LastDuelCastDelay = TimeSpan.FromSeconds(1.2);

    /// <summary>
    /// The time between the casts of Gigantic Storm of the gladiator, which has no buff, in the scene before the last duel.
    /// </summary>
    private static readonly TimeSpan GladiatorCastInterval = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<SpectreKind, RivalPlayer> _rivals = new();
    private readonly ConcurrentDictionary<SpectreKind, DateTime> _talkingUntil = new();
    private readonly CancellationTokenSource _cts = new();
    private Player? _owner;
    private int _duelStarting;
    private int _closing;
    private DateTime _nextIdleAnimation = DateTime.UtcNow;

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

    /// <inheritdoc />
    /// <remarks>
    /// The player counts only once its client loaded the map, which may take longer than the entering phase.
    /// </remarks>
    protected override int MinimumPlayerCount => 0;

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

        if (player.SelectedCharacter is null)
        {
            return;
        }

        if (SpectreDuelRules.GetBlockReason(player, rival.Kind, GetConfiguration(player)) is { } reason)
        {
            await this.SayAsync(rival, reason).ConfigureAwait(false);
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
    /// Lets the player challenge a rival: the one with the object id, as the client of the player knows it, or the nearest one.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="objectId">The object id of the rival, or <c>null</c> for the nearest rival.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ChallengeAsync(Player player, ushort? objectId)
    {
        var rival = objectId is { } id ? this.FindRival(player, id) : this.FindNearestRival(player);
        if (rival is null)
        {
            await player.ShowBlueMessageAsync("Acercate a un espectro para desafiarlo.").ConfigureAwait(false);
            return;
        }

        await this.AnswerChallengeAsync(player, rival, true).ConfigureAwait(false);
    }

    /// <summary>
    /// Lets the player talk to a rival, which the player clicked on: the rival tells the next part of its story and offers a duel.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="objectId">The object id of the rival, as the client of the player knows it.</param>
    /// <returns>The task.</returns>
    internal async ValueTask TalkAsync(Player player, ushort objectId)
    {
        if (!ReferenceEquals(player, this._owner) || this.FindRival(player, objectId) is not { } rival)
        {
            return;
        }

        await this.TalkAsync(player, rival).ConfigureAwait(false);
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

    private static SpectreConfiguration? GetConfiguration(Player player)
        => player.GameContext.FeaturePlugIns.GetPlugIn<SpectreFeaturePlugIn>()?.Configuration;

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
            await owner.ShowBlueMessageAsync("Hace click en un espectro para hablarle. Para salir: /espectro salir o el menu M.").ConfigureAwait(false);
            await this.SpawnRivalsAsync(owner, cancellationToken).ConfigureAwait(false);
            if (SpectreHub.TakePendingFinale(owner) is { } finishedKind)
            {
                _ = Task.Run(() => this.ShowFinaleAsync(owner, finishedKind, cancellationToken));
            }

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
            await rival.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowRivalAsync(rival), false).ConfigureAwait(false);
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
        if (now < this._nextIdleAnimation)
        {
            return;
        }

        this._nextIdleAnimation = now + IdleAnimationInterval;
        foreach (var rival in this._rivals.Values)
        {
            if (rival.CurrentMap != this.Map)
            {
                continue;
            }

            if (!this._talkingUntil.TryGetValue(rival.Kind, out var talkingUntil) || now >= talkingUntil)
            {
                await this.ShowIdleAnimationAsync(rival).ConfigureAwait(false);
            }

            await rival.ShowTitleAsync().ConfigureAwait(false);
            await rival.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowRivalAsync(rival), false).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowIdleAnimationAsync(RivalPlayer rival)
    {
        var animation = rival.Tier == 0 ? IdleAnimations[(int)rival.Kind] : (byte)0x80;
        rival.Pose = animation == 0x80 ? CharacterPose.Sitting : default;
        await rival.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(rival, animation, null, rival.Rotation), false).ConfigureAwait(false);
    }

    private RivalPlayer? FindRival(Player player, ushort objectId)
        => this._rivals.Values.FirstOrDefault(r => r.CurrentMap == this.Map && r.GetId(player) == objectId);

    private RivalPlayer? FindNearestRival(Player player)
        => this._rivals.Values
            .Where(r => r.CurrentMap == this.Map && r.GetDistanceTo(player) <= ChallengeDistance)
            .OrderBy(r => r.GetDistanceTo(player))
            .FirstOrDefault();

    private async ValueTask TalkAsync(Player owner, RivalPlayer rival)
    {
        if (owner.SelectedCharacter is not { } character)
        {
            return;
        }

        var configuration = GetConfiguration(owner);
        var victories = SpectreProgress.GetVictories(character, rival.Kind);
        var name = SpectreNames.GetName(character, rival.Kind, configuration);
        var text = SpectreDialogues.GetText(rival.Kind, victories, name ?? SpectreClasses.GetName(rival.Kind));
        var reason = SpectreDuelRules.GetBlockReason(owner, rival.Kind, configuration);

        // At the end of its story, the rival has nothing left to say than its text: no reason.
        if (victories >= SpectreProgress.MaximumVictories)
        {
            reason = null;
        }

        var canDuel = victories < SpectreProgress.MaximumVictories && reason is null;
        // The rival stands up (an instant move to its own spot) and faces the player, so the scene frames it the same way always.
        this._talkingUntil[rival.Kind] = DateTime.UtcNow + TalkDuration;
        rival.Pose = default;
        await rival.MoveAsync(rival.Position).ConfigureAwait(false);
        rival.Rotation = rival.GetDirectionTo(owner);
        await rival.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(rival, 0, null, rival.Rotation), false).ConfigureAwait(false);
        await owner.InvokeViewPlugInAsync<IShowSpectresPlugIn>(p => p.ShowRivalDialogueAsync(rival, rival.Title, text, canDuel, reason)).ConfigureAwait(false);
        if (victories == SpectreProgress.MaximumVictories - 1)
        {
            _ = Task.Run(() => this.CastBeforeLastDuelAsync(rival, this._cts.Token));
        }
    }

    /// <summary>
    /// In the scene before the last duel, the rival shows its power: it casts its buff (the skill of its last tier),
    /// or Gigantic Storm three times in a row for the gladiator, which has no buff. Only the animation is shown.
    /// </summary>
    /// <param name="rival">The rival.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    private async Task CastBeforeLastDuelAsync(RivalPlayer rival, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(LastDuelCastDelay, cancellationToken).ConfigureAwait(false);
            var number = SpectreEvolution.GetSkillsOfTier(rival.Kind, SpectreEvolution.EvolutionCount).FirstOrDefault();
            if (rival.CurrentMap != this.Map
                || this.GameContext.Configuration.Skills.FirstOrDefault(s => s.Number == number) is not { } skill)
            {
                return;
            }

            if (rival.Kind == SpectreKind.Gladiator)
            {
                for (var i = 0; i < 3; i++)
                {
                    await rival.ForEachWorldObserverAsync<IShowAreaSkillAnimationPlugIn>(p => p.ShowAreaSkillAnimationAsync(rival, skill, rival.Position, AreaSkillTargetSelector.ConvertAngleToRotation((float)rival.Position.GetAngleDegreeTo(rival.Owner.Position))), true).ConfigureAwait(false);
                    await Task.Delay(GladiatorCastInterval, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            await rival.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(rival, rival, skill, true), true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The owner left the hub.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while the rival casts before the last duel.", this);
        }
    }

    private async Task ShowFinaleAsync(Player owner, SpectreKind kind, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FinaleDelay, cancellationToken).ConfigureAwait(false);
            if (this._rivals.TryGetValue(kind, out var rival) && rival.CurrentMap == this.Map && owner.CurrentMap == this.Map)
            {
                await this.TalkAsync(owner, rival).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The owner left the hub.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while showing the end of the story of a rival.", this);
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
