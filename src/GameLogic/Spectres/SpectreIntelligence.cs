// <copyright file="SpectreIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The intelligence of a <see cref="SpectrePlayer"/>. Every tick, it keeps the spectre close to its owner,
/// buffs and heals the owner and the owner's other spectres, and then attacks the monsters around the owner.
/// </summary>
internal sealed class SpectreIntelligence : AsyncDisposable
{
    /// <summary>
    /// The range around the owner in which a spectre hunts.
    /// </summary>
    internal const int HuntingRange = 6;

    /// <summary>
    /// The distance to the owner up to which an idle spectre stays where it is.
    /// </summary>
    private const int IdleDistance = 2;

    /// <summary>
    /// The number of ticks after which a spectre, which is out of the hunting range of its owner, is warped to the
    /// owner instead of walking, e.g. when no path leads to the owner.
    /// </summary>
    private const int MaximumTicksOutOfRange = 6;

    /// <summary>
    /// The number of ticks after which the spectre is shown as spectre to the players around it again, so that
    /// players which just came close see it as spectre, too.
    /// </summary>
    private const int ShowSpectreInterval = 4;

    private readonly SpectrePlayer _spectre;
    private readonly BuffHandler _buffHandler;
    private readonly HealingHandler _healingHandler;
    private readonly MovementHandler _movementHandler;
    private readonly CombatHandler _combatHandler;
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(500));

    private Task? _loopTask;

    private int _ticksOutOfRange;

    private int _ticks;

    private (int Level, int Resets) _shownProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreIntelligence"/> class.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    public SpectreIntelligence(SpectrePlayer spectre)
    {
        this._spectre = spectre;
        var settings = spectre.MuHelperSettings;
        this._buffHandler = new BuffHandler(spectre, settings);
        this._healingHandler = new HealingHandler(spectre, settings);
        this._movementHandler = new MovementHandler(spectre, settings);
        this._combatHandler = new CombatHandler(spectre, settings, this._movementHandler);
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
                this._spectre.Logger.LogError(ex, "Error in the loop of spectre {Name}.", this._spectre.Name);
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
        while (await this._timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await this._spectre.RunPersistenceExclusiveAsync(this.TickAsync).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
            catch (Exception ex)
            {
                this._spectre.Logger.LogError(ex, "Error in the tick of spectre {Name}.", this._spectre.Name);
            }
        }
    }

    private async ValueTask TickAsync()
    {
        await this._spectre.DrainPendingBotActionsAsync().ConfigureAwait(false);

        var spectre = this._spectre;
        var owner = spectre.Owner;
        if (this._ticks++ % ShowSpectreInterval == 0)
        {
            await this.ShowSpectreAsync().ConfigureAwait(false);
        }

        if (spectre.PlayerState.CurrentState != PlayerState.EnteredWorld
            || owner.PlayerState.CurrentState != PlayerState.EnteredWorld
            || owner.CurrentMap is not { } ownerMap
            || owner.IsTeleporting
            || !spectre.IsAlive)
        {
            return;
        }

        if (spectre.Configuration.UnlimitedMana && spectre.Attributes is { } attributes)
        {
            attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
            attributes[Stats.CurrentAbility] = attributes[Stats.MaximumAbility];
        }

        var isOutOfRange = spectre.CurrentMap == ownerMap && spectre.GetDistanceTo(owner) > HuntingRange;
        this._ticksOutOfRange = isOutOfRange ? this._ticksOutOfRange + 1 : 0;
        if (spectre.CurrentMap != ownerMap
            || spectre.GetDistanceTo(owner) > spectre.Configuration.WarpDistance
            || this._ticksOutOfRange > MaximumTicksOutOfRange)
        {
            this._ticksOutOfRange = 0;
            await this.WarpToOwnerAsync(ownerMap).ConfigureAwait(false);
            return;
        }

        spectre.HuntingOrigin = owner.Position;

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

        if (spectre.IsWalking)
        {
            return;
        }

        if (spectre.GetDistanceTo(owner) > HuntingRange)
        {
            await this.WalkToOwnerAsync(ownerMap).ConfigureAwait(false);
            return;
        }

        if (!owner.IsAtSafezone())
        {
            await this._combatHandler.PerformAttackAsync().ConfigureAwait(false);
        }

        if (!this._combatHandler.HasTarget && spectre.GetDistanceTo(owner) > IdleDistance)
        {
            await this.WalkToOwnerAsync(ownerMap).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowSpectreAsync()
    {
        var spectre = this._spectre;
        if (spectre.CurrentMap is null)
        {
            return;
        }

        await spectre.ForEachWorldObserverAsync<IShowSpectresPlugIn>(p => p.ShowSpectreAsync(spectre), false).ConfigureAwait(false);

        // The window of the owner shows the level and resets of the spectre.
        var progress = (spectre.Level, (int)(spectre.Attributes?[Stats.Resets] ?? 0));
        if (progress != this._shownProgress)
        {
            this._shownProgress = progress;
            await SpectreFeaturePlugIn.SendPanelAsync(spectre.Owner).ConfigureAwait(false);
        }
    }

    private async ValueTask WalkToOwnerAsync(GameMap ownerMap)
    {
        var target = ownerMap.Terrain.GetRandomCoordinate(this._spectre.Owner.Position, 1);
        await this._movementHandler.WalkToAsync(target).ConfigureAwait(false);
    }

    private async ValueTask WarpToOwnerAsync(GameMap ownerMap)
    {
        var position = this._spectre.Owner.Position;
        var gate = new ExitGate
        {
            Map = ownerMap.Definition,
            X1 = (byte)Math.Max(position.X - 1, 0),
            X2 = (byte)Math.Min(position.X + 1, byte.MaxValue),
            Y1 = (byte)Math.Max(position.Y - 1, 0),
            Y2 = (byte)Math.Min(position.Y + 1, byte.MaxValue),
        };

        await this._spectre.WarpToAsync(gate).ConfigureAwait(false);
    }
}
