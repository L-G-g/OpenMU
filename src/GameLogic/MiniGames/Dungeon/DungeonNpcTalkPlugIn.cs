// <copyright file="DungeonNpcTalkPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the party enter the dungeon when its leader talks to the NPC of the dungeon (<see cref="DungeonConfiguration.NpcDesignation"/>).
/// </summary>
/// <remarks>
/// The NPC is recognized by the designation of its definition instead of its number, because it uses the number
/// (and with it the look in the game client) of another NPC.
/// </remarks>
[Guid("8D2A4C61-5E3B-4F7A-9C1D-2B6E8F0A3C72")]
[PlugIn]
[Display(Name = nameof(PlugInResources.DungeonNpcTalkPlugIn_Name), Description = nameof(PlugInResources.DungeonNpcTalkPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class DungeonNpcTalkPlugIn : IPlayerTalkToNpcPlugIn
{
    private readonly DungeonEntryAction _entryAction = new();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (DungeonFeaturePlugIn.GetConfiguration(player.GameContext) is not { } configuration
            || npc.Definition.Designation.ValueInNeutralLanguage != configuration.NpcDesignation)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        if (player.CurrentMiniGame is not null)
        {
            // The NPC stands in the town of the dungeon map, too.
            return;
        }

        await this._entryAction.TryEnterAsync(player).ConfigureAwait(false);
    }
}
