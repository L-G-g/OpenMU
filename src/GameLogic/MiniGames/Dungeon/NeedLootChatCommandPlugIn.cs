// <copyright file="NeedLootChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command /need which chooses need for a rolled item of a dungeon, see <see cref="DungeonLoot"/>.
/// </summary>
[Guid("5A1C7E3B-9D2F-4B6A-8E0C-1F3D5B7A9C11")]
[PlugIn]
[Display(Name = nameof(PlugInResources.NeedLootChatCommandPlugIn_Name), Description = nameof(PlugInResources.NeedLootChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class NeedLootChatCommandPlugIn : LootChoiceChatCommandPlugInBase
{
    private const string Command = "/need";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override LootChoice Choice => LootChoice.Need;
}
