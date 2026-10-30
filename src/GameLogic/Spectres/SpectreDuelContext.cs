// <copyright file="SpectreDuelContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Threading;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.Views.Duel;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The duel of a player against a rival of its spectre hub (see <see cref="SpectreHub"/>), in a room of the Duel Arena.
/// The duel ends with the first fall: nobody dies, the one whose health drops to zero loses. If nobody falls until the
/// end of the game duration, it's a draw. Afterwards, the player goes back to its hub.
/// </summary>
public sealed class SpectreDuelContext : MiniGameContext
{
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RivalLogoutTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private Player? _owner;
    private SpectreKind _kind;
    private int _tier;
    private RivalPlayer? _rival;
    private int _result;
    private int _rivalSpawnStarted;
    private int _closing;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreDuelContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public SpectreDuelContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    private enum DuelResult
    {
        None,
        Victory,
        Defeat,
    }

    /// <inheritdoc />
    public override bool AllowPlayerKilling => true;

    /// <inheritdoc />
    internal override bool IsInstanceOfNormalMap => true;

    /// <inheritdoc />
    internal override bool SpawnsMonstersOfMap => false;

    /// <inheritdoc />
    protected override bool AllowEnterWhilePlaying => true;

    /// <inheritdoc />
    protected override TimeSpan MinimumEnterDuration => TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    protected override TimeSpan CountdownDuration => TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    /// <remarks>
    /// The player counts only once its client loaded the map, which may take longer than the entering phase.
    /// </remarks>
    protected override int MinimumPlayerCount => 0;

    /// <summary>
    /// Prepares the duel of the player against the rival of a kind at a tier.
    /// </summary>
    /// <param name="owner">The player.</param>
    /// <param name="kind">The kind of the rival.</param>
    /// <param name="tier">The tier of the rival.</param>
    /// <returns><c>true</c>, if the duel is prepared for this rival.</returns>
    internal bool Prepare(Player owner, SpectreKind kind, int tier)
    {
        if (Interlocked.CompareExchange(ref this._owner, owner, null) is { } existingOwner)
        {
            return ReferenceEquals(existingOwner, owner) && this._kind == kind && this._tier == tier;
        }

        this._kind = kind;
        this._tier = tier;
        return true;
    }

    /// <inheritdoc />
    internal override Point? GetEntrySpawnPosition(Player player)
    {
        if (!ReferenceEquals(player.CurrentMap, this.Map))
        {
            return null;
        }

        return player is RivalPlayer ? SpectreHub.DuelRivalStart : SpectreHub.DuelOwnerStart;
    }

    /// <inheritdoc />
    internal override bool PreventsDeath(Player player, IAttacker attacker)
    {
        var isOwner = ReferenceEquals(player, this._owner);
        if (!isOwner && !ReferenceEquals(player, this._rival))
        {
            return false;
        }

        var result = isOwner ? DuelResult.Defeat : DuelResult.Victory;
        if (Interlocked.CompareExchange(ref this._result, (int)result, (int)DuelResult.None) == (int)DuelResult.None)
        {
            // Not inline: the end of the game would run within the hit.
            _ = Task.Run(this.FinishEvent);
        }

        return true;
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectAddedToMapAsync((GameMap Map, ILocateable Object) args)
    {
        await base.OnObjectAddedToMapAsync(args).ConfigureAwait(false);
        if (ReferenceEquals(args.Object, this._owner) && Interlocked.Exchange(ref this._rivalSpawnStarted, 1) == 0)
        {
            _ = Task.Run(() => this.SpawnRivalAsync(this._owner!, this._cts.Token));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnObjectRemovedFromMapAsync((GameMap Map, ILocateable Object) args)
    {
        // A player which goes back to the hub entered it before it left this map, so it keeps its game.
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
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        await base.OnGameStartAsync(players).ConfigureAwait(false);
        if (this._owner is not { } owner)
        {
            return;
        }

        if (this._rival is not { } rival)
        {
            await owner.ShowBlueMessageAsync("El espectro no llego al duelo.").ConfigureAwait(false);
            this.FinishEvent();
            return;
        }

        // The client of the player treats the rival as its opponent of a duel, so it can attack it like any target.
        await owner.InvokeViewPlugInAsync<IShowDuelRequestResultPlugIn>(p => p.ShowDuelRequestResultAsync(DuelStartResult.Success, rival)).ConfigureAwait(false);
        await rival.ShowDuelNameAsync().ConfigureAwait(false);
        await owner.ShowGoldenMessageAsync("A pelear!").ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
        if (this._owner is not { } owner)
        {
            return;
        }

        // Without the result first, the client shows that the duel was canceled.
        if (this._rival is { } rival && (DuelResult)this._result is DuelResult.Victory or DuelResult.Defeat)
        {
            var (winner, loser) = (DuelResult)this._result == DuelResult.Victory ? ((Player)owner, (Player)rival) : (rival, owner);
            await owner.InvokeViewPlugInAsync<IDuelFinishedPlugIn>(p => p.DuelFinishedAsync(winner, loser)).ConfigureAwait(false);
        }

        await owner.InvokeViewPlugInAsync<IDuelEndedPlugIn>(p => p.DuelEndedAsync()).ConfigureAwait(false);
        var rivalName = SpectreEvolution.GetTitleWithArticle(this._kind, this._tier);
        switch ((DuelResult)this._result)
        {
            case DuelResult.Victory:
                await this.WinAsync(owner, rivalName).ConfigureAwait(false);
                break;
            case DuelResult.Defeat:
                await owner.ShowGoldenMessageAsync($"{char.ToUpperInvariant(rivalName[0])}{rivalName[1..]} te vencio").ConfigureAwait(false);
                await owner.ShowBlueMessageAsync("Podes volver a desafiarlo cuando quieras.").ConfigureAwait(false);
                break;
            default:
                await owner.ShowGoldenMessageAsync("Empate: se termino el tiempo").ConfigureAwait(false);
                break;
        }

        foreach (var player in new[] { owner, this._rival })
        {
            if (player?.Attributes is { } attributes)
            {
                attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
                attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
            }
        }

        _ = Task.Run(() => this.ReturnToHubAsync(owner));
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        Interlocked.Exchange(ref this._closing, 1);
        await this._cts.CancelAsync().ConfigureAwait(false);
        await this.StopRivalAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
        this._cts.Dispose();
    }

    private async ValueTask WinAsync(Player owner, string rivalName)
    {
        var defeated = rivalName.StartsWith("el ", StringComparison.Ordinal) ? "al " + rivalName[3..] : "a " + rivalName;
        if (owner.SelectedCharacter is not { } character)
        {
            return;
        }

        var victories = SpectreProgress.GetVictories(character, this._kind);
        if (victories != this._tier || !SpectreProgress.SetVictories(owner, this._kind, victories + 1))
        {
            await owner.ShowGoldenMessageAsync($"Venciste {defeated}").ConfigureAwait(false);
            return;
        }

        var kindName = SpectreClasses.GetName(this._kind);
        if (this._tier == 0)
        {
            await owner.ShowGoldenMessageAsync($"Venciste {defeated}! Ahora te sigue").ConfigureAwait(false);
            await owner.ShowBlueMessageAsync($"Desbloqueaste al espectro {kindName}. Invocalo desde la ventana de espectros.").ConfigureAwait(false);
        }
        else
        {
            await owner.ShowGoldenMessageAsync($"Venciste {defeated}! Tu espectro evoluciona").ConfigureAwait(false);
            await owner.ShowBlueMessageAsync($"Tu espectro {kindName} ahora es {SpectreEvolution.GetTitleWithArticle(this._kind, this._tier)} (evolucion {this._tier} de {SpectreEvolution.EvolutionCount}).").ConfigureAwait(false);
        }

        var skillNames = SpectreEvolution.GetSkillsOfTier(this._kind, this._tier)
            .Select(number => owner.GameContext.Configuration.Skills.FirstOrDefault(s => s.Number == number)?.Name.ValueInNeutralLanguage)
            .OfType<string>()
            .ToList();
        if (skillNames.Count > 0)
        {
            await owner.ShowBlueMessageAsync($"Aprendio: {string.Join(", ", skillNames)}.").ConfigureAwait(false);
        }

        var newPets = SpectreEvolution.GetPets(this._kind, this._tier) & ~SpectreEvolution.GetPets(this._kind, this._tier - 1);
        if (newPets.HasFlag(SpectreGrantedPet.DarkRaven))
        {
            await owner.ShowBlueMessageAsync("Recibio un Dark Raven.").ConfigureAwait(false);
        }

        if (newPets.HasFlag(SpectreGrantedPet.DarkHorse))
        {
            await owner.ShowBlueMessageAsync("Recibio un Dark Horse.").ConfigureAwait(false);
        }

        if (newPets.HasFlag(SpectreGrantedPet.Dinorant))
        {
            await owner.ShowBlueMessageAsync("Recibio un Dinorant.").ConfigureAwait(false);
        }

        await SpectreFeaturePlugIn.SendPanelAsync(owner).ConfigureAwait(false);
    }

    private async Task SpawnRivalAsync(Player owner, CancellationToken cancellationToken)
    {
        try
        {
            if (owner.Account is not { } account
                || await SpectreRivals.GetOrCreateCharacterAsync(owner, this._kind, this._tier).ConfigureAwait(false) is not { } characterName)
            {
                return;
            }

            // The rival of the hub may still be leaving the game.
            var waitUntil = DateTime.UtcNow + RivalLogoutTimeout;
            while (this.GameContext.GetPlayerByCharacterName(characterName) is not null && DateTime.UtcNow < waitUntil)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            var rival = new RivalPlayer(this.GameContext, owner, this._kind, this._tier, SpectreHub.DuelRivalStart);
            rival.CurrentMiniGame = this;
            this._rival = rival;
            if (!await rival.InitializeAsync(SpectreRivals.GetLoginName(account), characterName).ConfigureAwait(false))
            {
                this._rival = null;
                await rival.DisposeAsync().ConfigureAwait(false);
                return;
            }

            rival.Rotation = rival.GetDirectionTo(owner);
            await rival.ShowDuelNameAsync().ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                await this.StopRivalAsync().ConfigureAwait(false);
                return;
            }

            await owner.ShowGoldenMessageAsync($"Duelo contra {SpectreEvolution.GetTitleWithArticle(this._kind, this._tier)}").ConfigureAwait(false);
            await owner.ShowBlueMessageAsync("Pierde el primero que se queda sin vida. Nadie muere.").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The player left the duel.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while spawning the rival.", this);
        }
    }

    private async Task ReturnToHubAsync(Player owner)
    {
        try
        {
            await Task.Delay(ReturnDelay).ConfigureAwait(false);
            await this.StopRivalAsync().ConfigureAwait(false);
            if (ReferenceEquals(owner.CurrentMiniGame, this) && owner.PlayerState.CurrentState == PlayerState.EnteredWorld)
            {
                if (!await SpectreHub.EnterHubAsync(owner).ConfigureAwait(false))
                {
                    await owner.WarpToSafezoneAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{context}: Error while returning to the hub.", this);
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
            this.Logger.LogError(ex, "{context}: Error while closing the duel.", this);
        }
    }

    private async ValueTask StopRivalAsync()
    {
        if (Interlocked.Exchange(ref this._rival, null) is not { } rival)
        {
            return;
        }

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
}
