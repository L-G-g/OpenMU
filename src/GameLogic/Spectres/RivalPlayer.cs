// <copyright file="RivalPlayer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// A rival of the spectre hub: a connection-less character, which waits in the hub of its owner (<see cref="SpectreHubContext"/>)
/// and fights its owner in the duel (<see cref="SpectreDuelContext"/>). The clients show it with the name of its class.
/// Only its owner can hurt it, and only during the duel.
/// </summary>
public sealed class RivalPlayer : OfflinePlayer, ISpectreCharacter
{
    private readonly string? _storyName;

    private RivalIntelligence? _intelligence;

    /// <summary>
    /// Initializes a new instance of the <see cref="RivalPlayer"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="owner">The owner of the hub.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier.</param>
    /// <param name="spot">The position of the rival on its map.</param>
    /// <param name="storyName">The name of the rival, if it's a character of the story mode instead of a rival of the hub.</param>
    public RivalPlayer(IGameContext gameContext, Player owner, SpectreKind kind, int tier, Point spot, string? storyName = null)
        : base(gameContext)
    {
        this._storyName = storyName;
        this.Owner = owner;
        this.Kind = kind;
        this.Tier = tier;
        this.Spot = spot;
        this.MuHelperSettings = new SpectreMuHelperSettings(this, RivalIntelligence.HuntingRange, RivalIntelligence.DarkRavenMode);
    }

    /// <summary>
    /// Gets the owner of the hub, which the rival challenges.
    /// </summary>
    public Player Owner { get; }

    /// <inheritdoc />
    public SpectreKind Kind { get; }

    /// <summary>
    /// Gets the tier of the rival: 0 for the rival which unlocks the spectre, 1 to <see cref="SpectreEvolution.EvolutionCount"/> for the evolutions.
    /// </summary>
    public int Tier { get; }

    /// <summary>
    /// Gets the position of the rival on its map.
    /// </summary>
    public Point Spot { get; }

    /// <inheritdoc />
    public override string DisplayName => this._storyName ?? SpectreHub.GetRivalName(this.Kind);

    /// <summary>
    /// Gets the name of the rival as the client of the PeroniaMU shows it, e.g. "Guerrero Olvidado", or the name of the spectre
    /// when its story is finished, e.g. "Godofredo el Legendario".
    /// </summary>
    public string Title => this._storyName ?? SpectreHub.GetTitle(this.Owner, this.Kind, this.Tier);

    /// <inheritdoc />
    public override bool RespawnAndContinue => true;

    /// <summary>
    /// Shows the <see cref="Title"/> to the players which see the rival.
    /// </summary>
    /// <returns>The task.</returns>
    public ValueTask ShowTitleAsync() => this.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowTitleAsync(this, this.Title), false);

    /// <summary>
    /// Shows the <see cref="DisplayName"/> instead of the <see cref="Title"/> to the players which see the rival: the client
    /// lets a player hit its duel opponent only when the opponent has the name of the duel, and the duel has the short name.
    /// </summary>
    /// <returns>The task.</returns>
    public ValueTask ShowDuelNameAsync() => this.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowTitleAsync(this, this.DisplayName), false);

    /// <summary>
    /// Determines whether an attacker can hurt this rival: only its owner (or the pets of the owner), during the duel.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <returns><c>true</c>, if the attacker can hurt this rival.</returns>
    public bool CanBeAttackedBy(IAttacker attacker)
    {
        var attackingPlayer = attacker is IPlayerSurrogate surrogate ? surrogate.Owner : attacker as Player;
        return ReferenceEquals(attackingPlayer, this.Owner)
               && this.CurrentMiniGame is SpectreDuelContext { IsEventRunning: true } duel
               && ReferenceEquals(this.Owner.CurrentMiniGame, duel);
    }

    /// <inheritdoc />
    public override async ValueTask StopAsync()
    {
        await this.StopIntelligenceAsync().ConfigureAwait(false);
        await base.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    internal override IEnumerable<IAttackable> GetSupportTargets() => [];

    /// <inheritdoc />
    protected override void StartIntelligence()
    {
        this.MultiplyHealth();
        this._intelligence = new RivalIntelligence(this);
        this._intelligence.Start();
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await this.StopIntelligenceAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    private async ValueTask StopIntelligenceAsync()
    {
        if (this._intelligence is { } intelligence)
        {
            this._intelligence = null;
            await intelligence.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void MultiplyHealth()
    {
        var multiplier = this.GameContext.FeaturePlugIns.GetPlugIn<SpectreFeaturePlugIn>()?.Configuration?.RivalHealthMultiplier ?? 1;
        if (this.Attributes is not { } attributes || multiplier <= 0 || Math.Abs(multiplier - 1) < 0.001f)
        {
            return;
        }

        attributes.AddElement(new SimpleElement(multiplier, AggregateType.Multiplicate), Stats.MaximumHealth);
        attributes.AddElement(new SimpleElement(multiplier, AggregateType.Multiplicate), Stats.MaximumShield);
        attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
        attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
    }
}
