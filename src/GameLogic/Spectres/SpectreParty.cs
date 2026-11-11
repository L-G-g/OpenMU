// <copyright file="SpectreParty.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The party of a player with the player's spectres: a real party, so that the party buffs reach them, which never
/// has other players. The experience isn't shared by the party, but as before by <see cref="SpectreFeaturePlugIn.ShareExperienceAsync"/>,
/// and everything else which a party shares (money, quest items, event entries) treats the player as being alone.
/// When the player gets into a party with other players, the spectres leave.
/// </summary>
public static class SpectreParty
{
    /// <summary>
    /// Determines whether the party is the party of a player with spectres.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <returns><c>true</c>, if it's a party with spectres.</returns>
    public static bool IsSpectreParty(this Party? party) => party?.PartyList.Any(member => member is SpectrePlayer) is true;

    /// <summary>
    /// Gets the party of a player with other players: <c>null</c> if the player is alone or only with its spectres.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The party with other players.</returns>
    public static Party? GetPlayerParty(this IPartyMember player) => player.Party is { } party && !party.IsSpectreParty() ? party : null;

    /// <summary>
    /// Lets the spectres of the players leave, before the players get into a party with each other.
    /// </summary>
    /// <param name="players">The players.</param>
    /// <returns>The task.</returns>
    public static async ValueTask PrepareForPlayerPartyAsync(params IPartyMember?[] players)
    {
        foreach (var player in players.OfType<Player>())
        {
            if (player is SpectrePlayer || !player.Party.IsSpectreParty())
            {
                continue;
            }

            await SpectreFeaturePlugIn.DismissAllAsync(player).ConfigureAwait(false);
            await SpectreFeaturePlugIn.SendPanelAsync(player).ConfigureAwait(false);
            await player.ShowBlueMessageAsync("Tus espectros se fueron porque armaste party con otros jugadores.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a spectre to the party of its owner. If the owner has no party, a new one is created.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <returns><c>true</c>, if the spectre is in the party of its owner.</returns>
    internal static async ValueTask<bool> JoinOwnerAsync(SpectrePlayer spectre)
    {
        var owner = spectre.Owner;
        if (owner.Party is { } party)
        {
            if (!party.IsSpectreParty() && party.PartyList.Count > 1)
            {
                return false;
            }

            return party.PartyList.Contains(spectre) || await party.AddAsync(spectre).ConfigureAwait(false);
        }

        var newParty = owner.GameContext.PartyManager.CreateParty();
        return await newParty.AddAsync(owner).ConfigureAwait(false)
               && await newParty.AddAsync(spectre).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a spectre from its party. When only the owner is left, the party ends.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask LeaveAsync(SpectrePlayer spectre)
    {
        if (spectre.Party is { } party)
        {
            await party.KickMySelfAsync(spectre).ConfigureAwait(false);
        }
    }
}
