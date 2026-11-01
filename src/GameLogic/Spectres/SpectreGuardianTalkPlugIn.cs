// <copyright file="SpectreGuardianTalkPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tells the story of the spectres when a player talks to their guardian (<see cref="SpectreConfiguration.GuardianDesignation"/>),
/// and lets the player enter the spectre hub (<see cref="SpectreHub"/>) with <c>/espectro refugio</c>, which the window of the story sends.
/// </summary>
/// <remarks>
/// The guardian is recognized by the designation of its definition instead of its number, because it uses the number
/// (and with it the look in the game client) of another NPC.
/// </remarks>
[Guid("B6C2E8F4-3A5D-4E7B-9C1F-2D4A6B8E0C35")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SpectreGuardianTalkPlugIn_Name), Description = nameof(PlugInResources.SpectreGuardianTalkPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class SpectreGuardianTalkPlugIn : IPlayerTalkToNpcPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<SpectreFeaturePlugIn>() is not { } feature
            || npc.Definition.Designation.ValueInNeutralLanguage != (feature.Configuration?.GuardianDesignation ?? SpectreHub.GuardianDesignation))
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        SpectreHub.RememberGuardianVisit(player);
        await player.InvokeViewPlugInAsync<IShowSpectresPlugIn>(p => p.ShowGuardianStoryAsync(SpectreHub.Story)).ConfigureAwait(false);
        await player.ShowBlueMessageAsync("Para entrar al refugio de los espectros: /espectro refugio").ConfigureAwait(false);
    }
}
