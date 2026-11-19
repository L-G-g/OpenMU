// <copyright file="EventBoard.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameLogic.PlugIns.WanderingMerchants;

/// <summary>
/// Determines the next events of the board (eventboard.dll, key H): the events which are open now and the next starts
/// of the active scheduled events (mini games, invasions, happy hour), with the level of the mini games which the player enters.
/// </summary>
public static class EventBoard
{
    /// <summary>
    /// The default number of events of the board.
    /// </summary>
    public const int DefaultCount = 10;

    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(2);

    /// <summary>
    /// Gets the next events for a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="count">The maximum number of events.</param>
    /// <returns>The events, the open ones first, then ordered by their start.</returns>
    public static IReadOnlyList<EventBoardEntry> GetEntries(Player player, int count = DefaultCount)
    {
        var gameContext = player.GameContext;
        var utcNow = DateTime.UtcNow;
        var open = new List<EventBoardEntry>();
        var upcoming = new List<(DateTime OpensAtUtc, string Name)>();
        foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<IScheduledPlugIn>())
        {
            if (plugIn is WanderingMerchantsPlugIn)
            {
                continue;
            }

            var name = GetName(plugIn, player);
            var delay = TimeSpan.Zero;
            if (plugIn is IPeriodicMiniGameStartPlugIn miniGameStart)
            {
                if (!miniGameStart.IsEnabledOn(gameContext))
                {
                    continue;
                }

                var openGame = gameContext.MiniGames.GetRunningMiniGames(miniGameStart.Key)
                    .Where(game => game.State == MiniGameState.Open && game.EnterEndsAtUtc > utcNow)
                    .OrderByDescending(game => game.EnterEndsAtUtc)
                    .FirstOrDefault();
                if (openGame is not null)
                {
                    open.Add(new EventBoardEntry(name, (int)Math.Ceiling((openGame.EnterEndsAtUtc - utcNow).TotalSeconds), true));
                }

                // The entrance opens after the delay of the preparation.
                delay = GetConfiguration(plugIn)?.PreStartMessageDelay ?? TimeSpan.Zero;
            }

            var time = utcNow;
            for (var i = 0; i < count; i++)
            {
                if (plugIn.GetNextStartUtc(time, gameContext.ServerTimeZone) is not { } next
                    || next <= time
                    || next - utcNow > LookAhead)
                {
                    break;
                }

                upcoming.Add((next + delay, name));
                time = next;
            }
        }

        return open
            .Concat(upcoming
                .Where(e => e.OpensAtUtc > utcNow)
                .OrderBy(e => e.OpensAtUtc)
                .Select(e => new EventBoardEntry(e.Name, (int)Math.Ceiling((e.OpensAtUtc - utcNow).TotalSeconds), false)))
            .Take(count)
            .ToList();
    }

    private static PeriodicTaskConfiguration? GetConfiguration(object plugIn)
    {
        return plugIn.GetType().GetProperty("Configuration")?.GetValue(plugIn) as PeriodicTaskConfiguration;
    }

    private static string GetName(IScheduledPlugIn plugIn, Player player)
    {
        if (plugIn is IPeriodicMiniGameStartPlugIn miniGameStart)
        {
            var name = miniGameStart.Key switch
            {
                MiniGameType.DevilSquare => "Devil Square",
                MiniGameType.BloodCastle => "Blood Castle",
                MiniGameType.ChaosCastle => "Chaos Castle",
                MiniGameType.Kanturu => "Kanturu",
                MiniGameType.IllusionTemple => "Illusion Temple",
                _ => miniGameStart.Key.ToString(),
            };

            return EventReworkPlugIn.GetGameLevelByResets(player, miniGameStart.Key) is { } level ? $"{name} {level}" : name;
        }

        return plugIn switch
        {
            GoldenInvasionPlugIn => "Invasion Dorada",
            RedDragonInvasionPlugIn => "Red Dragon",
            WhiteWizardInvasionPlugIn => "White Wizard",
            HappyHourPlugIn => "Happy Hour",
            _ => plugIn.GetType().Name.Replace("PlugIn", string.Empty, StringComparison.Ordinal),
        };
    }
}
