// <copyright file="SpectreDuelRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// When a player may duel a rival of its spectre hub: while the story of the rival isn't finished, and within the limits of
/// the configuration: the victories per reset (<see cref="SpectreConfiguration.VictoriesPerReset"/>) and per day
/// (<see cref="SpectreConfiguration.MaximumVictoriesPerDay"/>).
/// </summary>
public static class SpectreDuelRules
{
    /// <summary>
    /// Gets why a player may not duel the rival of a kind now.
    /// </summary>
    /// <param name="owner">The player.</param>
    /// <param name="kind">The kind of the rival.</param>
    /// <param name="configuration">The configuration of the spectres.</param>
    /// <returns>The reason, as the rival says it, or <c>null</c> if the player may duel the rival.</returns>
    public static string? GetBlockReason(Player owner, SpectreKind kind, SpectreConfiguration? configuration)
    {
        if (owner.SelectedCharacter is not { } character)
        {
            return "No hay personaje.";
        }

        if (SpectreProgress.GetVictories(character, kind) >= SpectreProgress.MaximumVictories)
        {
            return "Ya recordé todo lo que fui. No hace falta que volvamos a pelear.";
        }

        var perReset = configuration?.VictoriesPerReset ?? 0;
        if (perReset > 0)
        {
            var resets = (int)(owner.Attributes?[Stats.Resets] ?? 0);
            var total = SpectreProgress.GetTotalVictories(character);
            if (total >= resets * perReset)
            {
                var needed = (total / perReset) + 1;
                return needed == 1
                    ? "Todavía no estás listo. Volvé cuando tengas tu primer reset."
                    : $"Todavía no estás listo. Volvé cuando tengas {needed} resets.";
            }
        }

        var perDay = configuration?.MaximumVictoriesPerDay ?? 0;
        if (perDay > 0 && SpectreProgress.GetVictoriesToday(character) >= perDay)
        {
            return perDay == 1
                ? "Ya ganaste un duelo hoy. Volvé mañana."
                : $"Ya ganaste {perDay} duelos hoy. Volvé mañana.";
        }

        return null;
    }
}
