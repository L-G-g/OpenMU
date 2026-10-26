// <copyright file="PassLootChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command /pass which chooses pass for a rolled item of a dungeon, see <see cref="DungeonLoot"/>.
/// </summary>
[Guid("5A1C7E3B-9D2F-4B6A-8E0C-1F3D5B7A9C13")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PassLootChatCommandPlugIn_Name), Description = nameof(PlugInResources.PassLootChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class PassLootChatCommandPlugIn : LootChoiceChatCommandPlugInBase
{
    private const string Command = "/pass";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override LootChoice Choice => LootChoice.Pass;
}
