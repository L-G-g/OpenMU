// <copyright file="StoryModeFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.GameLogic.Spectres;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The story mode of PeroniaMU: Lumen, the stranger and Sebina tell the story of the game in scenes, which open the
/// maps step by step (see <see cref="StoryScript"/>).
/// </summary>
/// <remarks>
/// It builds on two other features:
/// <list type="bullet">
///   <item>The marks above the NPCs and the tracker of the skill quests (<see cref="SkillQuestFeaturePlugIn"/>, quests.dll):
///         the actor of the next step gets a mark, and the tracker tells what to do next. A click on the mark starts the scene.</item>
///   <item>The first person scenes and the duels of the spectre hub (cinematicas.dll, <see cref="SpectreDuelContext"/>):
///         the actor talks in a scene, and the duel against the stranger is a <see cref="StoryDuel"/>.</item>
/// </list>
/// The progress of a character is the number of its last finished step (<see cref="StoryProgress"/>). The client answers
/// the buttons of a scene with the chat command <c>/historia</c> (see <see cref="StoryChatCommandPlugIn"/>).
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7E4B1D93-2C6A-4F58-B3E7-9A1C5D8F2B46")]
public class StoryModeFeaturePlugIn : IFeaturePlugIn, IWarpGateEnteringPlugIn, IDisabledByDefault,
    ISupportCustomConfiguration<StoryConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The number of Lumen the Barmaid.
    /// </summary>
    public const short LumenNumber = 255;

    /// <summary>
    /// The number of Sebina the Priestess.
    /// </summary>
    public const short SebinaNumber = 235;

    internal const string PlugInName = "Story mode";

    internal const string PlugInDescription = "Lumen, the stranger of Lorencia and Sebina tell the story of the game in first person scenes (cinematicas.dll), with a duel against the stranger. The steps open the maps: Devias, Dungeon, Lost Tower and then the rest. Needs the update 'Story Mode', the plugin 'Skill quests' (marks and tracker of quests.dll) and the spectre hub (duel).";

    private const short LorenciaNumber = 0;

    /// <summary>
    /// The Dragon armor and a Lightning Sword: what a knight of the old Empire would wear.
    /// </summary>
    private static readonly (byte Group, short Number)[] StrangerEquipment = [(7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (0, 14)];

    private static readonly TimeSpan TitleInterval = TimeSpan.FromSeconds(4);

    private static readonly ConditionalWeakTable<Player, ViewState> PlayerStates = new();

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public StoryConfiguration? Configuration { get; set; }

    private StoryConfiguration CurrentConfiguration => this.Configuration ??= new StoryConfiguration();

    /// <summary>
    /// Gets the active story mode of a game.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The story mode, or <c>null</c> if it's not active or the game configuration lacks its data.</returns>
    public static StoryModeFeaturePlugIn? Get(IGameContext gameContext)
    {
        return gameContext.FeaturePlugIns.GetPlugIn<StoryModeFeaturePlugIn>() is { } feature
               && StoryProgress.IsAvailable(gameContext.Configuration)
            ? feature
            : null;
    }

    /// <summary>
    /// Gets the actor which an NPC plays.
    /// </summary>
    /// <param name="npc">The definition of the NPC.</param>
    /// <returns>The actor, or <c>null</c> if the NPC has no part in the story.</returns>
    public static StoryActor? GetActor(MonsterDefinition npc)
    {
        if (npc.ObjectKind == NpcObjectKind.Monster)
        {
            return null;
        }

        if (npc.Designation.ValueInNeutralLanguage == StoryScript.StrangerName)
        {
            return StoryActor.Stranger;
        }

        return npc.Number switch
        {
            LumenNumber => StoryActor.Lumen,
            SebinaNumber => StoryActor.Sebina,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the family of a character class.
    /// </summary>
    /// <param name="characterClass">The character class.</param>
    /// <returns>The family.</returns>
    public static StoryClassFamily GetFamily(CharacterClass? characterClass) => ((characterClass?.Number ?? 4) / 4) switch
    {
        0 => StoryClassFamily.Wizard,
        1 => StoryClassFamily.Knight,
        2 => StoryClassFamily.Elf,
        3 => StoryClassFamily.MagicGladiator,
        4 => StoryClassFamily.DarkLord,
        5 => StoryClassFamily.Summoner,
        _ => StoryClassFamily.RageFighter,
    };

    /// <inheritdoc />
    public object CreateDefaultConfig() => new StoryConfiguration();

    /// <inheritdoc />
    public async ValueTask WarpGateEnteringAsync(Player player, ExitGate targetGate, CancelEventArgs eventArgs)
    {
        if (eventArgs.Cancel
            || targetGate.Map is not { } map
            || this.GetMapLock(player, map.Number) is not { } step)
        {
            return;
        }

        eventArgs.Cancel = true;
        var mapName = map.Name.GetTranslation(player.Culture) ?? "ese mapa";
        await player.ShowBlueMessageAsync($"Todavia no conoces el camino a {mapName}. Segui la historia: {step.Chapter}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the step which a player has to finish before it can enter a map.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="mapNumber">The number of the map.</param>
    /// <returns>The step which locks the map for the player; <c>null</c>, if the player can enter.</returns>
    public StoryStep? GetMapLock(Player player, short mapNumber)
    {
        var configuration = this.CurrentConfiguration;
        if (!configuration.LockMaps
            || !IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || (configuration.GameMastersIgnoreLocks && character.CharacterStatus >= CharacterStatus.GameMaster)
            || !StoryProgress.IsAvailable(player.GameContext.Configuration))
        {
            return null;
        }

        var unlockingStep = configuration.GetUnlockingStep(mapNumber);
        return unlockingStep > StoryProgress.GetFinishedStep(character) ? StoryScript.GetStep(unlockingStep) : null;
    }

    /// <summary>
    /// Gets why a quest can't be started yet because of the story.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    /// <returns>The message for the player; <c>null</c>, if the story doesn't block the quest.</returns>
    public string? GetQuestBlock(Player player, QuestDefinition quest)
    {
        if (!this.CurrentConfiguration.ClassChangeRequiresStory
            || quest.IsSkillQuest()
            || quest.QuestGiver?.Number != SebinaNumber
            || !IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || !StoryProgress.IsAvailable(player.GameContext.Configuration)
            || StoryProgress.GetFinishedStep(character) >= StoryScript.SchoolOfLight)
        {
            return null;
        }

        return "Sebina todavia no puede ponerte a prueba. Segui la historia.";
    }

    /// <summary>
    /// Determines whether the skill quests have to wait for the first step of the story.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="hasFinishedSkillQuest">If set to <c>true</c>, the character finished or started a skill quest before.</param>
    /// <returns><c>true</c>, if the player has to talk to Lumen first.</returns>
    public bool BlocksSkillQuests(Player player, bool hasFinishedSkillQuest)
    {
        return this.CurrentConfiguration.SkillQuestsRequireStory
               && !hasFinishedSkillQuest
               && IsStoryPlayer(player)
               && player.SelectedCharacter is { } character
               && StoryProgress.IsAvailable(player.GameContext.Configuration)
               && StoryProgress.GetFinishedStep(character) < StoryScript.Awakening;
    }

    /// <summary>
    /// Gets the mark above an NPC for a player: the actor of the next step of the player has one.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The definition of the NPC.</param>
    /// <returns>The mark.</returns>
    public SkillQuestMark GetMark(Player player, MonsterDefinition npc)
    {
        if (GetActor(npc) is not { } actor
            || this.GetNextStep(player) is not { } step
            || step.Actor != actor)
        {
            return SkillQuestMark.None;
        }

        if (this.GetMissingRequirement(player, step) is not null)
        {
            return SkillQuestMark.Locked;
        }

        // After the duel, the stranger owes the answer.
        return StoryScript.GetStep(step.Number - 1)?.Kind == StoryStepKind.Duel ? SkillQuestMark.Ready : SkillQuestMark.Available;
    }

    /// <summary>
    /// Adds the lines of the story to the tracker of the quests.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="lines">The lines of the tracker.</param>
    public void AddTrackerLines(Player player, List<SkillQuestLine> lines)
    {
        if (this.GetNextStep(player) is not { } step || player.SelectedCharacter is not { } character)
        {
            return;
        }

        var missing = this.GetMissingRequirement(player, step);
        lines.Add(new(missing is null ? SkillQuestTextColor.Gold : SkillQuestTextColor.Gray, $"Historia: {step.Chapter}"));
        lines.Add(missing is null
            ? new(SkillQuestTextColor.White, $"  Hablá con {GetActorName(step.Actor, StoryProgress.GetFinishedStep(character), true)} ({GetActorPlace(step.Actor)})")
            : new(SkillQuestTextColor.Gray, $"  {missing}"));
    }

    /// <summary>
    /// Sends what only the story shows about the NPCs near the player: the name of the stranger.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npcs">The NPCs near the player.</param>
    /// <returns>The task.</returns>
    public async ValueTask UpdatePlayerAsync(Player player, IEnumerable<NonPlayerCharacter> npcs)
    {
        if (!IsStoryPlayer(player) || player.SelectedCharacter is not { } character)
        {
            return;
        }

        var state = PlayerStates.GetOrCreateValue(player);
        var now = DateTime.UtcNow;
        if (now - state.LastTitlesUtc < TitleInterval)
        {
            return;
        }

        state.LastTitlesUtc = now;
        var name = GetActorName(StoryActor.Stranger, StoryProgress.GetFinishedStep(character), false);
        foreach (var npc in npcs.Where(n => GetActor(n.Definition) == StoryActor.Stranger))
        {
            await player.InvokeViewPlugInAsync<IShowSpectresPlugIn>(p => p.ShowTitleAsync(npc, name)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows the scene of an NPC to a player, if the NPC is an actor of the story.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <param name="onlyNextStep">If set to <c>true</c>, the scene is only shown when the NPC is the actor of the next
    /// step of the player; otherwise, the actor also says something when it has nothing to tell.</param>
    /// <returns><c>true</c>, if the story handled it.</returns>
    public async ValueTask<bool> TryShowSceneAsync(Player player, NonPlayerCharacter npc, bool onlyNextStep)
    {
        if (GetActor(npc.Definition) is not { } actor
            || !IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || npc.CurrentMap != player.CurrentMap)
        {
            return false;
        }

        var step = this.GetNextStep(player);
        if (onlyNextStep && step?.Actor != actor)
        {
            return false;
        }

        if (player.GetDistanceTo(npc) > this.CurrentConfiguration.InteractionRange)
        {
            await player.ShowBlueMessageAsync("Acercate para hablar.").ConfigureAwait(false);
            return true;
        }

        var scene = this.CreateScene(player, character, actor, step);
        await player.InvokeViewPlugInAsync<IStorySceneViewPlugIn>(p => p.ShowSceneAsync(npc, scene)).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Handles a button of a scene.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC of the scene.</param>
    /// <param name="buttonId">The id of the button.</param>
    /// <returns>The task.</returns>
    public async ValueTask HandleButtonAsync(Player player, NonPlayerCharacter npc, byte buttonId)
    {
        if (GetActor(npc.Definition) is not { } actor
            || !IsStoryPlayer(player)
            || npc.CurrentMap != player.CurrentMap
            || player.GetDistanceTo(npc) > this.CurrentConfiguration.InteractionRange
            || this.GetNextStep(player) is not { } step
            || step.Actor != actor
            || this.GetMissingRequirement(player, step) is not null)
        {
            return;
        }

        switch (step.Kind)
        {
            case StoryStepKind.Talk when buttonId == StoryButton.FinishId:
                await this.FinishStepAsync(player, step).ConfigureAwait(false);
                break;
            case StoryStepKind.Duel when buttonId == StoryButton.DuelId:
                await this.StartDuelAsync(player, step, npc).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Remembers that the players finished the dungeon of Devias.
    /// </summary>
    /// <param name="player">The player who was in the dungeon when its last boss fell.</param>
    /// <returns>The task.</returns>
    public async ValueTask OnDungeonFinishedAsync(Player player)
    {
        if (!IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || StoryProgress.GetFlags(character).HasFlag(StoryFlags.DungeonFinished)
            || !StoryProgress.AddFlag(player, StoryFlags.DungeonFinished))
        {
            return;
        }

        if (this.GetNextStep(player) is { Number: StoryScript.Dungeon })
        {
            await player.ShowBlueMessageAsync("Historia: el Forastero querra saber que sobreviviste al bosque de hielo.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets the progress of a player, for tests by a game master.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="finishedStep">The number of the last finished step.</param>
    /// <returns>The task.</returns>
    public async ValueTask SetProgressAsync(Player player, int finishedStep)
    {
        finishedStep = Math.Clamp(finishedStep, 0, StoryScript.Steps.Count);
        if (!StoryProgress.SetFinishedStep(player, finishedStep))
        {
            return;
        }

        if (finishedStep == 0)
        {
            StoryProgress.SetFlags(player, StoryFlags.None);
        }

        await RefreshAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the next step of a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The next step; <c>null</c>, if the player finished the story or doesn't take part.</returns>
    public StoryStep? GetNextStep(Player player)
    {
        if (!IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || !StoryProgress.IsAvailable(player.GameContext.Configuration))
        {
            return null;
        }

        return StoryScript.GetStep(StoryProgress.GetFinishedStep(character) + 1);
    }

    /// <summary>
    /// Gets what a player is missing to play a step.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="step">The step.</param>
    /// <returns>The missing requirement, as a text for the tracker; <c>null</c>, if the player can play the step.</returns>
    public string? GetMissingRequirement(Player player, StoryStep step)
    {
        var configuration = this.CurrentConfiguration;
        var minimumResets = configuration.GetMinimumResets(step.Number);
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
        if (resets < minimumResets)
        {
            return minimumResets == 1 ? "Necesitás 1 reset" : $"Necesitás {minimumResets} resets";
        }

        if (step.Number == StoryScript.Dungeon
            && configuration.DungeonRequiresDeviasDungeon
            && player.SelectedCharacter is { } character
            && !StoryProgress.GetFlags(character).HasFlag(StoryFlags.DungeonFinished))
        {
            return "Completá la dungeon de Devias";
        }

        return null;
    }

    private static bool IsStoryPlayer(Player player)
    {
        return player is not OfflinePlayer
               && player.Account?.IsBot != true
               && player.SelectedCharacter is not null;
    }

    private static string GetActorName(StoryActor actor, int finishedStep, bool withArticle) => actor switch
    {
        StoryActor.Lumen => "Lumen",
        StoryActor.Sebina => "Sebina",
        _ when finishedStep >= StoryScript.Dungeon => StoryScript.StrangerRealName,
        _ => withArticle ? "el " + StoryScript.StrangerName : StoryScript.StrangerName,
    };

    private static string GetActorPlace(StoryActor actor) => actor == StoryActor.Sebina ? "Devias" : "Lorencia";

    private static async ValueTask RefreshAsync(Player player)
    {
        PlayerStates.GetOrCreateValue(player).LastTitlesUtc = default;
        if (player.GameContext.FeaturePlugIns.GetPlugIn<SkillQuestFeaturePlugIn>() is { } skillQuests)
        {
            await skillQuests.RefreshPlayerAsync(player).ConfigureAwait(false);
        }
    }

    private static StoryTextContext CreateTextContext(Player player, Character character)
    {
        var (teacher, place) = SkillQuestFeaturePlugIn.GetFirstTeacher(player) ?? ("tu maestro", "el pueblo");
        return new StoryTextContext(
            character.Name,
            GetFamily(character.CharacterClass),
            HasClassChange(character.CharacterClass),
            teacher,
            place);
    }

    private static bool HasClassChange(CharacterClass? characterClass)
        => GetFamily(characterClass) is StoryClassFamily.Wizard or StoryClassFamily.Knight or StoryClassFamily.Elf or StoryClassFamily.Summoner;

    private StoryScene CreateScene(Player player, Character character, StoryActor actor, StoryStep? step)
    {
        var finishedStep = StoryProgress.GetFinishedStep(character);
        var title = GetActorName(actor, finishedStep, false);
        if (step is null || step.Actor != actor)
        {
            return new StoryScene(title, StoryScript.Paginate([StoryScript.GetIdleText(actor, finishedStep)]), [new(StoryButton.CloseId, "Cerrar")], null);
        }

        if (this.GetMissingRequirement(player, step) is { } missing)
        {
            return new StoryScene(title, StoryScript.Paginate([step.LockedText]), [new(StoryButton.CloseId, "Cerrar")], missing);
        }

        var pages = StoryScript.Paginate(step.Text(CreateTextContext(player, character)));
        IReadOnlyList<StoryButton> buttons = step.Kind == StoryStepKind.Duel
            ? [new(StoryButton.DuelId, step.AcceptLabel), new(StoryButton.CloseId, "Volver más tarde")]
            : [new(StoryButton.FinishId, step.AcceptLabel)];
        return new StoryScene(title, pages, buttons, null);
    }

    private async ValueTask FinishStepAsync(Player player, StoryStep step)
    {
        if (player.SelectedCharacter is not { } character
            || StoryProgress.GetFinishedStep(character) != step.Number - 1
            || !StoryProgress.SetFinishedStep(player, step.Number))
        {
            return;
        }

        if (step.Number == StoryScript.Awakening && DungeonEntryAction.GetLastEntry(character) is not null)
        {
            // A character from before the story mode, which already knows the dungeon of Devias.
            StoryProgress.AddFlag(player, StoryFlags.DungeonFinished);
        }

        player.Logger.LogInformation("Story: {Character} finished the step {Step} ({Chapter}).", character.Name, step.Number, step.Chapter);
        if (step.Reward is { } reward)
        {
            await player.ShowGoldenMessageAsync(reward).ConfigureAwait(false);
        }

        switch (step.Number)
        {
            case StoryScript.Awakening:
                await player.ShowBlueMessageAsync("Historia: busca a tu maestro para aprender tus primeras skills.").ConfigureAwait(false);
                break;
            case StoryScript.SchoolOfLight when HasClassChange(character.CharacterClass):
                await player.ShowBlueMessageAsync("Sebina ya puede ponerte a prueba para tu cambio de clase.").ConfigureAwait(false);
                break;
            case StoryScript.OldWorld:
                await player.ShowBlueMessageAsync("Fin de la primera parte de la historia.").ConfigureAwait(false);
                break;
            default:
                break;
        }

        await RefreshAsync(player).ConfigureAwait(false);
    }

    private async ValueTask StartDuelAsync(Player player, StoryStep step, NonPlayerCharacter npc)
    {
        if (player.CurrentMiniGame is not null || player.DuelRoom is not null || !player.IsAlive
            || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            await player.ShowBlueMessageAsync("Ahora no podes empezar el duelo.").ConfigureAwait(false);
            return;
        }

        var configuration = this.CurrentConfiguration;
        var returnPosition = npc.Position;
        var duel = new StoryDuel
        {
            RivalName = StoryScript.StrangerName,
            RivalNameWithArticle = "el " + StoryScript.StrangerName,
            Kind = SpectreKind.Warrior,
            Level = configuration.StrangerLevel,
            Resets = configuration.StrangerResets,
            SkillTier = Math.Clamp(configuration.StrangerSkillTier, 0, SpectreEvolution.EvolutionCount),
            ItemLevel = (byte)Math.Clamp(configuration.StrangerItemLevel, 0, 15),
            Equipment = StrangerEquipment,
            FinishedAsync = (owner, won) => this.OnDuelFinishedAsync(owner, step, won),
            ReturnAsync = owner => ReturnToStrangerAsync(owner, returnPosition),
        };

        if (!await SpectreHub.EnterStoryDuelAsync(player, duel).ConfigureAwait(false))
        {
            player.Logger.LogWarning("Story: the duel of {Player} against the stranger couldn't start.", player);
        }
    }

    private static async ValueTask ReturnToStrangerAsync(Player player, Pathfinding.Point strangerPosition)
    {
        if (player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == LorenciaNumber && m.Discriminator == 0) is not { } lorencia)
        {
            await player.WarpToSafezoneAsync().ConfigureAwait(false);
            return;
        }

        // In front of the stranger.
        var gate = new ExitGate
        {
            Map = lorencia,
            X1 = (byte)Math.Clamp(strangerPosition.X + 1, 0, 255),
            X2 = (byte)Math.Clamp(strangerPosition.X + 2, 0, 255),
            Y1 = (byte)Math.Clamp(strangerPosition.Y + 2, 0, 255),
            Y2 = (byte)Math.Clamp(strangerPosition.Y + 3, 0, 255),
            Direction = Direction.NorthWest,
        };
        await player.WarpToAsync(gate).ConfigureAwait(false);
    }

    private async ValueTask OnDuelFinishedAsync(Player player, StoryStep step, bool won)
    {
        if (!won)
        {
            return;
        }

        await player.ShowGoldenMessageAsync("Venciste al Forastero!").ConfigureAwait(false);
        if (player.SelectedCharacter is { } character
            && StoryProgress.GetFinishedStep(character) == step.Number - 1
            && StoryProgress.SetFinishedStep(player, step.Number))
        {
            player.Logger.LogInformation("Story: {Character} won the duel of the step {Step}.", character.Name, step.Number);
            await player.ShowBlueMessageAsync("Historia: habla con el Forastero para conocer su secreto.").ConfigureAwait(false);
        }
    }

    private sealed class ViewState
    {
        public DateTime LastTitlesUtc { get; set; }
    }
}
