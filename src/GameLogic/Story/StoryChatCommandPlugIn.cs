// <copyright file="StoryChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command of the story mode (see <see cref="StoryModeFeaturePlugIn"/>), which cinematicas.dll sends:
/// <c>/historia &lt;button id&gt; &lt;npc id&gt;</c> presses a button of the scene of the NPC, and
/// <c>/historia hablar &lt;npc id&gt;</c> starts the scene of an NPC. Game masters can also use
/// <c>/historia paso &lt;number&gt;</c> (sets the last finished step, 0 starts again), <c>/historia dungeon</c>
/// (counts the dungeon of Devias as finished), <c>/historia resets &lt;number&gt;</c> (sets the resets of the character,
/// for tests) and <c>/historia info</c>.
/// </summary>
[Guid("2F8C5A17-6D3E-4B91-A7C4-5E9B1D3F8A62")]
[PlugIn]
[Display(Name = "Chat command: /historia", Description = "The chat command of the story mode, which cinematicas.dll sends: /historia <button id> <npc id>, /historia hablar <npc id>. Game masters: /historia paso <number>, /historia dungeon, /historia resets <number>, /historia info.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class StoryChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/historia";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Finds an NPC in the map of the player which is an actor of the story.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="id">The id of the NPC.</param>
    /// <returns>The NPC, or <c>null</c>.</returns>
    public static NonPlayerCharacter? FindActor(Player player, ushort id)
    {
        return player.CurrentMap?.GetObject((ushort)(id & 0x7FFF)) is NonPlayerCharacter npc
               && npc is not Monster
               && StoryModeFeaturePlugIn.GetActor(npc.Definition) is not null
            ? npc
            : null;
    }

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (StoryModeFeaturePlugIn.Get(player.GameContext) is not { } story)
        {
            await player.ShowBlueMessageAsync("El modo historia no esta activado.").ConfigureAwait(false);
            return;
        }

        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).ToList();
        var action = arguments.FirstOrDefault()?.ToLowerInvariant();
        var isGameMaster = player.SelectedCharacter?.CharacterStatus >= CharacterStatus.GameMaster;
        switch (action)
        {
            case "paso" when isGameMaster && arguments.Count == 2 && int.TryParse(arguments[1], out var step):
                await story.SetProgressAsync(player, step).ConfigureAwait(false);
                await ShowInfoAsync(player, story).ConfigureAwait(false);
                return;
            case "dungeon" when isGameMaster:
                StoryProgress.AddFlag(player, StoryFlags.DungeonFinished);
                await story.SetProgressAsync(player, StoryProgress.GetFinishedStep(player.SelectedCharacter!)).ConfigureAwait(false);
                await ShowInfoAsync(player, story).ConfigureAwait(false);
                return;
            case "resets" when isGameMaster && arguments.Count == 2 && int.TryParse(arguments[1], out var resets) && player.Attributes is { } attributes:
                attributes[Attributes.Stats.Resets] = Math.Max(resets, 0);
                await story.SetProgressAsync(player, StoryProgress.GetFinishedStep(player.SelectedCharacter!)).ConfigureAwait(false);
                await player.ShowBlueMessageAsync($"Resets: {Math.Max(resets, 0)}.").ConfigureAwait(false);
                await ShowInfoAsync(player, story).ConfigureAwait(false);
                return;
            case "info":
                await ShowInfoAsync(player, story).ConfigureAwait(false);
                return;
            case "hablar" when arguments.Count == 2 && ushort.TryParse(arguments[1], out var talkId) && FindActor(player, talkId) is { } talkNpc:
                await story.TryShowSceneAsync(player, talkNpc, false).ConfigureAwait(false);
                return;
            case not null when arguments.Count == 2
                               && byte.TryParse(action, out var buttonId)
                               && ushort.TryParse(arguments[1], out var npcId)
                               && FindActor(player, npcId) is { } npc:
                await story.HandleButtonAsync(player, npc, buttonId).ConfigureAwait(false);
                return;
            default:
                // cinematicas.dll sends the commands; a player who types something else gets no answer.
                return;
        }
    }

    private static async ValueTask ShowInfoAsync(Player player, StoryModeFeaturePlugIn story)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var finished = StoryProgress.GetFinishedStep(character);
        if (story.GetNextStep(player) is not { } next)
        {
            await player.ShowBlueMessageAsync($"Historia: terminaste la primera parte ({finished} de {StoryScript.Steps.Count} pasos).").ConfigureAwait(false);
            return;
        }

        var missing = story.GetMissingRequirement(player, next);
        await player.ShowBlueMessageAsync($"Historia: paso {next.Number} de {StoryScript.Steps.Count}, {RemoveAccents(next.Chapter)}{(missing is null ? string.Empty : $" ({RemoveAccents(missing)})")}.").ConfigureAwait(false);
    }

    /// <summary>
    /// The client shows the messages of the chat without accents.
    /// </summary>
    private static string RemoveAccents(string text)
    {
        return text.Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u').Replace('ñ', 'n');
    }
}
