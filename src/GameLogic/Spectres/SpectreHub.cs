// <copyright file="SpectreHub.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The spectre hub: a private place for each player, a room of the Duel Arena, where the characters which
/// players abandoned wait, one of each class. Beating one of them in a duel unlocks it as spectre, and beating it again
/// makes the spectre evolve (see <see cref="SpectreProgress"/> and <see cref="SpectreEvolution"/>).
/// </summary>
/// <remarks>
/// The hub and the duel are two levels of the mini game <see cref="MiniGameType.SpectreHub"/> with one instance per player:
/// the hub (<see cref="SpectreHubContext"/>) and the duel (<see cref="SpectreDuelContext"/>), in two rooms of the Duel Arena.
/// The players enter the hub at the guardian of the spectres in Lorencia (<see cref="SpectreGuardianTalkPlugIn"/>).
/// </remarks>
public static class SpectreHub
{
    /// <summary>
    /// The default designation of the NPC which lets the players enter the hub.
    /// </summary>
    public const string GuardianDesignation = "Guardian de los espectros";

    /// <summary>
    /// The game level of the hub.
    /// </summary>
    public const byte HubGameLevel = 1;

    /// <summary>
    /// The game level of the duel.
    /// </summary>
    public const byte DuelGameLevel = 2;

    /// <summary>
    /// The number of the map of the hub, the Duel Arena. The client crashes in the world of the character selection (74).
    /// </summary>
    public const short HubMapNumber = 64;

    /// <summary>
    /// The number of the map of the duel, the Duel Arena.
    /// </summary>
    public const short DuelMapNumber = 64;

    /// <summary>
    /// How long a player may enter the hub after it talked to the guardian.
    /// </summary>
    private static readonly TimeSpan GuardianInvitationDuration = TimeSpan.FromMinutes(5);

    private static readonly ConditionalWeakTable<Player, StrongBox<DateTime>> GuardianVisits = new();

    /// <summary>
    /// Gets where the player enters the hub.
    /// </summary>
    public static Point HubEntry { get; } = new(153, 64);

    /// <summary>
    /// Gets where the player starts the duel.
    /// </summary>
    public static Point DuelOwnerStart { get; } = new(101, 64);

    /// <summary>
    /// Gets where the rival starts the duel.
    /// </summary>
    public static Point DuelRivalStart { get; } = new(101, 75);

    /// <summary>
    /// Gets the story which the guardian tells, as lines of text. The client of the PeroniaMU shows it in a window.
    /// </summary>
    public static IReadOnlyList<string> Story { get; } =
    [
        "Cada vez que alguien deja de jugar, su personaje se queda solo.",
        "No desaparece: se queda esperando, del otro lado de la seleccion de personajes.",
        "Con el tiempo pierde su equipo, su nombre y las ganas de pelear. Asi nace un espectro.",
        "Yo cuido el refugio donde esperan: un guerrero, un mago, una elfa, un gladiador, un lord, una invocadora y un peleador.",
        "Si vences a uno en duelo, recupera las ganas de pelear y te sigue como espectro.",
        "Si lo volves a vencer, despierta un poco mas de lo que fue: se pone su equipo y recuerda sus habilidades, y tu espectro aprende lo mismo.",
        "Acercate a ellos en el refugio y te van a desafiar. Queres entrar?",
    ];

    /// <summary>
    /// Remembers that a player talked to the guardian, so it may enter the hub for a while.
    /// </summary>
    /// <param name="player">The player.</param>
    internal static void RememberGuardianVisit(Player player)
    {
        GuardianVisits.AddOrUpdate(player, new StrongBox<DateTime>(DateTime.UtcNow));
    }

    /// <summary>
    /// Determines whether a player talked to the guardian recently.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player may enter the hub.</returns>
    internal static bool HasVisitedGuardian(Player player)
        => GuardianVisits.TryGetValue(player, out var visit) && DateTime.UtcNow - visit.Value < GuardianInvitationDuration;

    /// <summary>
    /// Lets a player enter its hub, from the town or from the duel.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player is entering the hub.</returns>
    internal static ValueTask<bool> EnterHubAsync(Player player) => EnterAsync(player, HubGameLevel, null);

    /// <summary>
    /// Lets a player enter the duel against the rival of a kind, from its hub.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="kind">The kind of the rival.</param>
    /// <param name="tier">The tier of the rival.</param>
    /// <returns><c>true</c>, if the player is entering the duel.</returns>
    internal static ValueTask<bool> EnterDuelAsync(Player player, SpectreKind kind, int tier)
        => EnterAsync(player, DuelGameLevel, game => (game as SpectreDuelContext)?.Prepare(player, kind, tier) is true);

    /// <summary>
    /// Gets the name of a kind as the rivals are called, e.g. "Guerrero".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    internal static string GetRivalName(SpectreKind kind)
    {
        var name = SpectreClasses.GetName(kind);
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static async ValueTask<bool> EnterAsync(Player player, byte gameLevel, Func<MiniGameContext, bool>? prepare)
    {
        var definition = player.GameContext.Configuration.MiniGameDefinitions.FirstOrDefault(d => d.Type == MiniGameType.SpectreHub && d.GameLevel == gameLevel);
        if (definition?.Entrance is not { } entrance)
        {
            await player.ShowBlueMessageAsync("El refugio de los espectros no esta disponible.").ConfigureAwait(false);
            return false;
        }

        var game = await player.GameContext.MiniGames.GetOrCreateAsync(definition, player).ConfigureAwait(false);
        if (prepare?.Invoke(game) is false)
        {
            await player.ShowBlueMessageAsync("No se pudo preparar el duelo.").ConfigureAwait(false);
            return false;
        }

        // The player enters the new game before it leaves the map of its current game, which keeps the new game
        // (see SpectreHubContext and SpectreDuelContext), so that the player gets to the map of the new game.
        var result = await game.TryEnterAsync(player).ConfigureAwait(false);
        if (result != EnterResult.Success)
        {
            player.Logger.LogWarning("Player {Player} couldn't enter the spectre hub level {Level}: {Result}.", player, gameLevel, result);
            await player.ShowBlueMessageAsync("No se pudo entrar al refugio de los espectros.").ConfigureAwait(false);
            return false;
        }

        await player.WarpToAsync(entrance).ConfigureAwait(false);
        return true;
    }
}
