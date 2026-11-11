// <copyright file="RivalIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Threading;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Offline;

/// <summary>
/// The intelligence of a <see cref="RivalPlayer"/>. In the hub it does nothing, because the hub shows its poses
/// and challenges (see <see cref="SpectreHubContext"/>). In the duel, it buffs and heals itself and attacks its owner,
/// which is the only other participant of the duel (see <see cref="Bots.BotPvpRules.IsLegalPvpTarget"/>).
/// </summary>
internal sealed class RivalIntelligence : AsyncDisposable
{
    /// <summary>
    /// The range around its owner in which the rival looks for targets: the whole room of the duel.
    /// </summary>
    internal const int HuntingRange = 20;

    /// <summary>
    /// The mode of the dark raven of the lord: it attacks with the lord.
    /// </summary>
    internal const int DarkRavenMode = 2;

    private readonly RivalPlayer _rival;
    private readonly BuffHandler _buffHandler;
    private readonly HealingHandler _healingHandler;
    private readonly CombatHandler _combatHandler;
    private readonly PetHandler _petHandler;
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(500));

    private Task? _loopTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="RivalIntelligence"/> class.
    /// </summary>
    /// <param name="rival">The rival.</param>
    public RivalIntelligence(RivalPlayer rival)
    {
        this._rival = rival;
        var settings = rival.MuHelperSettings;
        this._buffHandler = new BuffHandler(rival, settings);
        this._healingHandler = new HealingHandler(rival, settings);
        var movementHandler = new MovementHandler(rival, settings);
        this._combatHandler = new CombatHandler(rival, settings, movementHandler);
        this._petHandler = new PetHandler(rival, settings);
    }

    /// <summary>
    /// Starts the intelligence.
    /// </summary>
    public void Start()
    {
        this._loopTask = this.RunLoopAsync(this._cts.Token);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        this._timer.Dispose();
        await this._cts.CancelAsync().ConfigureAwait(false);
        if (this._loopTask is { } loopTask)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                this._rival.Logger.LogError(ex, "Error in the loop of rival {Name}.", this._rival.Name);
            }
        }

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._cts.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var petInitialized = false;
        while (await this._timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!petInitialized && this._rival.CurrentMiniGame is SpectreDuelContext)
                {
                    // The raven only fights in the duel.
                    petInitialized = true;
                    await this._petHandler.InitializeAsync().ConfigureAwait(false);
                }

                await this._rival.RunPersistenceExclusiveAsync(this.TickAsync).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
            catch (Exception ex)
            {
                this._rival.Logger.LogError(ex, "Error in the tick of rival {Name}.", this._rival.Name);
            }
        }
    }

    private async ValueTask TickAsync()
    {
        await this._rival.DrainPendingBotActionsAsync().ConfigureAwait(false);

        var rival = this._rival;
        var owner = rival.Owner;
        if (rival.CurrentMiniGame is not SpectreDuelContext { IsEventRunning: true } duel
            || !ReferenceEquals(owner.CurrentMiniGame, duel)
            || rival.PlayerState.CurrentState != PlayerState.EnteredWorld
            || owner.PlayerState.CurrentState != PlayerState.EnteredWorld
            || owner.CurrentMap != rival.CurrentMap
            || !rival.IsAlive)
        {
            return;
        }

        if (rival.Attributes is { } attributes)
        {
            // Like the spectres, the rivals can't use potions.
            attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
            attributes[Stats.CurrentAbility] = attributes[Stats.MaximumAbility];
        }

        rival.HuntingOrigin = owner.Position;

        if (this._combatHandler.SkillCooldownTicks > 0)
        {
            this._combatHandler.DecrementCooldown();
            return;
        }

        if (!await this._buffHandler.PerformBuffsAsync().ConfigureAwait(false))
        {
            return;
        }

        await this._healingHandler.PerformHealthRecoveryAsync().ConfigureAwait(false);

        if (rival.IsWalking)
        {
            return;
        }

        await this._combatHandler.PerformAttackAsync().ConfigureAwait(false);
    }
}
