// <copyright file="SpectrePlayer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.GameLogic.Offline;

/// <summary>
/// A spectre: a connection-less character which a player summoned. It follows its owner, buffs and heals
/// the owner and the owner's other spectres, and hunts the monsters around the owner. It can't be hurt
/// and doesn't duel. Its kills count as kills of the owner (see <see cref="IPlayerSurrogate"/>), and it gets
/// a share of the owner's experience instead (see <see cref="SpectreFeaturePlugIn.ShareExperienceAsync"/>).
/// </summary>
public sealed class SpectrePlayer : OfflinePlayer, IPlayerSurrogate
{
    private SpectreIntelligence? _intelligence;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectrePlayer"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <param name="configuration">The configuration of the spectres.</param>
    public SpectrePlayer(IGameContext gameContext, Player owner, SpectreKind kind, SpectreConfiguration configuration)
        : base(gameContext)
    {
        this.Owner = owner;
        this.Kind = kind;
        this.Configuration = configuration;
        this.MuHelperSettings = new SpectreMuHelperSettings();
    }

    /// <summary>
    /// Gets the player which summoned this spectre.
    /// </summary>
    public Player Owner { get; }

    /// <summary>
    /// Gets the kind of this spectre.
    /// </summary>
    public SpectreKind Kind { get; }

    /// <inheritdoc />
    public override bool RespawnAndContinue => true;

    /// <summary>
    /// Gets or sets the configuration of the spectres.
    /// </summary>
    internal SpectreConfiguration Configuration { get; set; }

    /// <inheritdoc />
    public override async ValueTask StopAsync()
    {
        await this.StopIntelligenceAsync().ConfigureAwait(false);
        await base.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    internal override IEnumerable<IAttackable> GetSupportTargets()
    {
        yield return this.Owner;
        foreach (var spectre in SpectreFeaturePlugIn.GetSpectres(this.Owner))
        {
            if (spectre != this)
            {
                yield return spectre;
            }
        }

        if (this.Owner.Party is { } party)
        {
            foreach (var member in party.PartyList.OfType<IAttackable>())
            {
                if (member != this.Owner)
                {
                    yield return member;
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void StartIntelligence()
    {
        // The base intelligence (OfflinePlayerMuHelper) is not started: it would hunt around a fixed origin,
        // pick up items and pay the offline leveling fee.
        this._intelligence = new SpectreIntelligence(this);
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
}
