// <copyright file="SkillQuestChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command of the skill quests (see <see cref="SkillQuestFeaturePlugIn"/>), which quests.dll sends:
/// <c>/quest &lt;npc id&gt;</c> opens the quest window of the NPC, <c>/quest aceptar &lt;npc id&gt;</c> accepts its next quest
/// and <c>/quest entregar &lt;npc id&gt;</c> delivers its completed quest. Game masters can also use
/// <c>/quest completar</c> (completes the objectives of the active skill quests) and <c>/quest reiniciar</c>
/// (forgets the skill quests; the learned skills stay).
/// </summary>
[Guid("A7C2E4F1-3B5D-4E8A-9C1F-6D2B8E4A7C93")]
[PlugIn]
[Display(Name = "Chat command: /quest", Description = "The chat command of the skill quests, which quests.dll sends: /quest <npc id>, /quest aceptar <npc id>, /quest entregar <npc id>. Game masters: /quest completar, /quest reiniciar.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class SkillQuestChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/quest";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<SkillQuestFeaturePlugIn>() is not { } feature)
        {
            await player.ShowBlueMessageAsync("Las quests de skills no estan activadas.").ConfigureAwait(false);
            return;
        }

        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).ToList();
        var action = arguments.FirstOrDefault()?.ToLowerInvariant();
        var isGameMaster = player.SelectedCharacter?.CharacterStatus >= CharacterStatus.GameMaster;
        switch (action)
        {
            case "completar" when isGameMaster:
                await feature.CompleteObjectivesAsync(player).ConfigureAwait(false);
                await player.ShowBlueMessageAsync("Objetivos de las quests activas completados.").ConfigureAwait(false);
                return;
            case "reiniciar" when isGameMaster:
                await feature.ResetAsync(player).ConfigureAwait(false);
                await player.ShowBlueMessageAsync("Quests de skills reiniciadas.").ConfigureAwait(false);
                return;
            case "aceptar" when arguments.Count == 2 && TryFindNpc(player, arguments[1]) is { } npc:
                await feature.AcceptAsync(player, npc).ConfigureAwait(false);
                return;
            case "entregar" when arguments.Count == 2 && TryFindNpc(player, arguments[1]) is { } npc:
                await feature.DeliverAsync(player, npc).ConfigureAwait(false);
                return;
            case not null when arguments.Count == 1 && TryFindStoryNpc(player, action) is { } storyNpc
                                && Story.StoryModeFeaturePlugIn.Get(player.GameContext) is { } story
                                && await story.TryShowSceneAsync(player, storyNpc, true).ConfigureAwait(false):
                // The mark belongs to the story mode.
                return;
            case not null when arguments.Count == 1 && TryFindNpc(player, action) is { } npc:
                await feature.ShowDialogAsync(player, npc).ConfigureAwait(false);
                return;
            default:
                // quests.dll sends the commands; a player who types something else gets no answer.
                return;
        }
    }

    private static NPC.NonPlayerCharacter? TryFindStoryNpc(Player player, string id)
    {
        return ushort.TryParse(id, out var npcId) ? Story.StoryChatCommandPlugIn.FindActor(player, npcId) : null;
    }

    private static NPC.NonPlayerCharacter? TryFindNpc(Player player, string id)
    {
        return ushort.TryParse(id, out var npcId) ? SkillQuestFeaturePlugIn.FindNpc(player, npcId) : null;
    }
}
