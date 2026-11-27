// <copyright file="StoryModeFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
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

        if (npc.Designation.ValueInNeutralLanguage == StoryMemory.SeerName)
        {
            return StoryActor.Seer;
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
    /// Gets why a player can't start a skill quest yet because of the story: with the questline
    /// (<see cref="StoryConfiguration.InterleaveSkillQuests"/>), a story step before the tier of the quest isn't done yet;
    /// otherwise, the player didn't talk to Lumen yet (see <see cref="BlocksSkillQuests"/>).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="tier">The number of the skill quest, which is its tier.</param>
    /// <param name="hasStartedSkillQuests">If set to <c>true</c>, the character finished or started a skill quest before.</param>
    /// <returns>The message for the quest window; <c>null</c>, if the story doesn't block the quest.</returns>
    public string? GetSkillQuestBlock(Player player, int tier, bool hasStartedSkillQuests)
    {
        if (!this.CurrentConfiguration.InterleaveSkillQuests)
        {
            return this.BlocksSkillQuests(player, hasStartedSkillQuests) ? "Primero hablá con Lumen en Lorencia." : null;
        }

        if (!IsStoryPlayer(player)
            || player.SelectedCharacter is not { } character
            || !StoryProgress.IsAvailable(player.GameContext.Configuration))
        {
            return null;
        }

        var entries = this.GetQuestline(player.GameContext);
        var index = Questline.IndexOf(entries, QuestlineEntryKind.SkillTier, tier);
        var first = Questline.GetFirstOpenEntry(entries, e => IsDone(player, e));
        if (index < 0 || first < 0 || first >= index || entries[first].Kind != QuestlineEntryKind.Story
            || StoryScript.GetStep(entries[first].Number) is not { } step)
        {
            // A skill tier before it is open: the skill quests tell that themselves.
            return null;
        }

        return $"Primero seguí la historia: hablá con {GetActorName(step.Actor, StoryProgress.GetFinishedStep(character), true)} ({GetActorPlace(step)}).";
    }

    /// <summary>
    /// Gets the questline: the steps of the story and the tiers of the skill quests in their order.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The entries of the questline.</returns>
    public IReadOnlyList<QuestlineEntry> GetQuestline(IGameContext gameContext)
    {
        var skills = gameContext.FeaturePlugIns.GetPlugIn<SkillQuestFeaturePlugIn>();
        return Questline.Get(gameContext.Configuration, this.CurrentConfiguration, skills is null ? null : skills.Configuration ?? new SkillQuestConfiguration());
    }

    /// <summary>
    /// Gets the current mission of a player in the questline.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The index of the first entry which the player didn't do, and the questline; the index is -1 if it did all.</returns>
    public (int Index, IReadOnlyList<QuestlineEntry> Entries) GetCurrentMission(Player player)
    {
        var entries = this.GetQuestline(player.GameContext);
        return (Questline.GetFirstOpenEntry(entries, e => IsDone(player, e)), entries);
    }

    /// <summary>
    /// Gets the mark above an NPC for a player: the actor of the next step of the player has one.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The mark.</returns>
    public SkillQuestMark GetMark(Player player, NonPlayerCharacter npc)
    {
        if (GetActor(npc.Definition) == StoryActor.Seer
            && this.GetNextStep(player) is { } step
            && !ShowsMemoryOf(npc, step))
        {
            // A seer of a level of Kalima above the one of the memory.
            return SkillQuestMark.None;
        }

        return this.GetMark(player, npc.Definition);
    }

    /// <summary>
    /// Gets the mark above an NPC for a player, without regard to the map where it stands.
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
            this.AddSkillTierTrackerLines(player, lines);
            return;
        }

        if (this.CurrentConfiguration.InterleaveSkillQuests
            && this.GetCurrentMission(player) is var (index, entries)
            && index >= 0
            && entries[index] is { Kind: QuestlineEntryKind.SkillTier })
        {
            // The current mission is a skill tier: the skill quests show it (active or available), or the line below.
            this.AddSkillTierTrackerLines(player, lines);
            return;
        }

        var missing = this.GetMissingRequirement(player, step);
        lines.Add(new(missing is null ? SkillQuestTextColor.Gold : SkillQuestTextColor.Gray, $"Historia: {step.Chapter}"));
        lines.Add(missing is null
            ? new(SkillQuestTextColor.White, $"  Hablá con {GetActorName(step.Actor, StoryProgress.GetFinishedStep(character), true)} ({GetActorPlace(step)})")
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
        var finishedStep = StoryProgress.GetFinishedStep(character);
        foreach (var npc in npcs)
        {
            if (GetActor(npc.Definition) is { } actor and (StoryActor.Stranger or StoryActor.Seer))
            {
                var name = GetActorName(actor, finishedStep, false);
                await player.InvokeViewPlugInAsync<IShowSpectresPlugIn>(p => p.ShowTitleAsync(npc, name)).ConfigureAwait(false);
            }
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

        var scene = this.CreateScene(player, character, npc, actor, step);
        player.InvulnerableUntilUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(this.CurrentConfiguration.SceneProtectionSeconds, 0, 3600));
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
        // Every button closes the scene (the client also sends the ones which only close it).
        player.InvulnerableUntilUtc = default;
        if (GetActor(npc.Definition) is not { } actor
            || !IsStoryPlayer(player)
            || npc.CurrentMap != player.CurrentMap
            || player.GetDistanceTo(npc) > this.CurrentConfiguration.InteractionRange)
        {
            return;
        }

        if (actor == StoryActor.Seer)
        {
            if (buttonId != StoryButton.MemoryId || player.SelectedCharacter is not { } viewer)
            {
                return;
            }

            if (this.GetNextStep(player) is { Kind: StoryStepKind.Memory, Memory: { } memory } memoryStep
                && ShowsMemoryOf(npc, memoryStep)
                && this.GetMissingRequirement(player, memoryStep) is null)
            {
                await this.StartMemoryAsync(player, npc, memory, memoryStep).ConfigureAwait(false);
            }
            else if (GetWatchedMemory(StoryProgress.GetFinishedStep(viewer), GetKalimaLevel(npc)) is { Memory: { } watched })
            {
                await this.StartMemoryAsync(player, npc, watched, null).ConfigureAwait(false);
            }

            return;
        }

        if (this.GetNextStep(player) is not { } step
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
        if (this.GetQuestlineBlock(player, step) is { } block)
        {
            return block;
        }

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

    /// <summary>
    /// Adds the next skill quest to the tracker when it's the current mission of the questline but the player can't start
    /// it yet (the skill quests only show the quests which are active or can be started).
    /// </summary>
    private void AddSkillTierTrackerLines(Player player, List<SkillQuestLine> lines)
    {
        if (!this.CurrentConfiguration.InterleaveSkillQuests
            || player.SelectedCharacter is not { } character
            || player.GameContext.FeaturePlugIns.GetPlugIn<SkillQuestFeaturePlugIn>() is not { } skills)
        {
            return;
        }

        var (index, entries) = this.GetCurrentMission(player);
        if (index < 0
            || entries[index] is not { Kind: QuestlineEntryKind.SkillTier } entry
            || skills.GetOpenQuestOfTier(player, entry.Number) is not var (quest, missing)
            || missing is null
            || character.QuestStates.Any(s => s.ActiveQuest == quest))
        {
            return;
        }

        lines.Add(new(SkillQuestTextColor.Gray, $"Próxima misión: {quest.Name.GetTranslation(player.Culture)}"));
        lines.Add(new(SkillQuestTextColor.Gray, $"  {missing}"));
    }

    /// <summary>
    /// Determines whether a player did an entry of the questline.
    /// </summary>
    private static bool IsDone(Player player, QuestlineEntry entry) => entry.Kind switch
    {
        QuestlineEntryKind.Story => player.SelectedCharacter is { } character && StoryProgress.GetFinishedStep(character) >= entry.Number,
        _ => SkillQuestFeaturePlugIn.IsTierFinished(player, entry.Number),
    };

    /// <summary>
    /// Gets the level of Kalima of the map of an NPC.
    /// </summary>
    private static int GetKalimaLevel(NonPlayerCharacter npc) => npc.CurrentMap is { } map ? StoryMemory.GetKalimaLevel((short)map.MapId) : 0;

    /// <summary>
    /// Determines whether a seer shows the memory of a step: it stands on the level of Kalima of the step, or deeper.
    /// </summary>
    private static bool ShowsMemoryOf(NonPlayerCharacter npc, StoryStep step)
    {
        var level = GetKalimaLevel(npc);
        return step.Kind == StoryStepKind.Memory && level > 0 && level >= step.KalimaLevel;
    }

    /// <summary>
    /// Gets the deepest memory which a player already saw and which a seer of a level of Kalima can show again.
    /// </summary>
    private static StoryStep? GetWatchedMemory(int finishedStep, int kalimaLevel)
    {
        return StoryScript.Steps
            .Where(s => s.Kind == StoryStepKind.Memory && s.Memory is not null && s.Number <= finishedStep && s.KalimaLevel <= kalimaLevel && kalimaLevel > 0)
            .OrderByDescending(s => s.KalimaLevel)
            .ThenByDescending(s => s.Number)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets which skill quest of the questline a player has to finish before a step of the story.
    /// </summary>
    private string? GetQuestlineBlock(Player player, StoryStep step)
    {
        if (!this.CurrentConfiguration.InterleaveSkillQuests
            || player.GameContext.FeaturePlugIns.GetPlugIn<SkillQuestFeaturePlugIn>() is not { } skills)
        {
            return null;
        }

        var (first, entries) = this.GetCurrentMission(player);
        var index = Questline.IndexOf(entries, QuestlineEntryKind.Story, step.Number);
        if (first < 0 || index < 0 || first >= index || entries[first].Kind != QuestlineEntryKind.SkillTier
            || skills.GetOpenQuestOfTier(player, entries[first].Number) is not var (quest, _))
        {
            return null;
        }

        var giver = quest.QuestGiver?.Designation.GetTranslation(player.Culture) ?? "tu maestro";
        return $"Primero terminá \"{quest.Name.GetTranslation(player.Culture)}\" con {giver}";
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
        StoryActor.Seer => withArticle ? "la " + StoryMemory.SeerName : StoryMemory.SeerName,
        _ when finishedStep >= StoryScript.Dungeon => StoryScript.StrangerRealName,
        _ => withArticle ? "el " + StoryScript.StrangerName : StoryScript.StrangerName,
    };

    private static string GetActorPlace(StoryStep step) => step.Actor switch
    {
        StoryActor.Sebina => "Devias",
        StoryActor.Seer => $"entrada de Kalima {Math.Max(step.KalimaLevel, 1)}",
        _ => "Lorencia",
    };

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

    private StoryScene CreateScene(Player player, Character character, NonPlayerCharacter npc, StoryActor actor, StoryStep? step)
    {
        var finishedStep = StoryProgress.GetFinishedStep(character);
        var title = GetActorName(actor, finishedStep, false);
        if (actor == StoryActor.Seer)
        {
            return this.CreateSeerScene(player, character, npc, step, title);
        }

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

    private StoryScene CreateSeerScene(Player player, Character character, NonPlayerCharacter npc, StoryStep? step, string title)
    {
        if (step is { Actor: StoryActor.Seer } && ShowsMemoryOf(npc, step))
        {
            if (this.GetMissingRequirement(player, step) is { } missing)
            {
                return new StoryScene(title, StoryScript.Paginate([step.LockedText]), [new(StoryButton.CloseId, "Cerrar")], missing);
            }

            var pages = StoryScript.Paginate(step.Text(CreateTextContext(player, character)));
            return new StoryScene(title, pages, [new(StoryButton.MemoryId, step.AcceptLabel), new(StoryButton.CloseId, "Más tarde")], null);
        }

        if (GetWatchedMemory(StoryProgress.GetFinishedStep(character), GetKalimaLevel(npc)) is not null)
        {
            return new StoryScene(title, StoryScript.Paginate(StoryMemory.SeerAgainText), [new(StoryButton.MemoryId, "Verlo otra vez"), new(StoryButton.CloseId, "Cerrar")], null);
        }

        return new StoryScene(title, StoryScript.Paginate([StoryScript.GetIdleText(StoryActor.Seer, StoryProgress.GetFinishedStep(character))]), [new(StoryButton.CloseId, "Cerrar")], null);
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
                await player.ShowBlueMessageAsync("Fin de la primera parte de la historia. Una vidente te espera en la entrada de Kalima 1.").ConfigureAwait(false);
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

    /// <summary>
    /// Starts a memory: the player goes to the stage of the memory (<see cref="StorySceneContext"/>), and comes back to
    /// the NPC afterwards.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The seer.</param>
    /// <param name="memory">The memory.</param>
    /// <param name="step">The step which is finished when the player saw the last line, or <c>null</c> if the player watches it again.</param>
    private async ValueTask StartMemoryAsync(Player player, NonPlayerCharacter npc, StoryMemory memory, StoryStep? step)
    {
        var definition = player.GameContext.Configuration.MiniGameDefinitions.FirstOrDefault(d => d.Type == MiniGameType.StoryScene);
        if (definition?.Entrance is not { } entrance
            || player.CurrentMiniGame is not null || player.DuelRoom is not null || !player.IsAlive
            || player.PlayerState.CurrentState != PlayerState.EnteredWorld
            || player.CurrentMap?.Definition is not { } returnMap)
        {
            await player.ShowBlueMessageAsync("Ahora no podes ver el recuerdo.").ConfigureAwait(false);
            return;
        }

        var returnPosition = player.Position;
        var returnGate = new ExitGate
        {
            Map = returnMap,
            X1 = returnPosition.X,
            X2 = returnPosition.X,
            Y1 = returnPosition.Y,
            Y2 = returnPosition.Y,
            Direction = player.Rotation,
        };

        // Nobody else on the stage: the spectres of the player stay behind.
        await SpectreFeaturePlugIn.DismissAllAsync(player).ConfigureAwait(false);
        var game = await player.GameContext.MiniGames.GetOrCreateAsync(definition, player).ConfigureAwait(false);
        if (game is not StorySceneContext stage)
        {
            return;
        }

        Func<Player, ValueTask>? completedAsync = step is null ? null : owner => this.FinishStepAsync(owner, step);
        stage.Prepare(memory, owner => owner.WarpToAsync(returnGate), completedAsync);
        var result = await game.TryEnterAsync(player).ConfigureAwait(false);
        if (result != EnterResult.Success)
        {
            player.Logger.LogWarning("Story: {Player} couldn't enter the stage of a memory: {Result}.", player, result);
            await player.ShowBlueMessageAsync("Ahora no podes ver el recuerdo.").ConfigureAwait(false);
            return;
        }

        // The stage is a map with monsters around; the player only watches.
        player.InvulnerableUntilUtc = DateTime.UtcNow.Add(definition.GameDuration + TimeSpan.FromMinutes(1));
        await player.InvokeViewPlugInAsync<IStorySceneViewPlugIn>(p => p.ShowCurtainAsync()).ConfigureAwait(false);
        await player.WarpToAsync(entrance).ConfigureAwait(false);
    }

    private static async ValueTask ReturnToStrangerAsync(Player player, Pathfinding.Point strangerPosition)
    {
        if (player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == LorenciaNumber && m.Discriminator == 0) is not { } lorencia)
        {
            await player.WarpToSafezoneAsync().ConfigureAwait(false);
            return;
        }

        // In front of the stranger, who looks to the south west (towards smaller Y coordinates).
        var gate = new ExitGate
        {
            Map = lorencia,
            X1 = strangerPosition.X,
            X2 = (byte)Math.Clamp(strangerPosition.X + 1, 0, 255),
            Y1 = (byte)Math.Clamp(strangerPosition.Y - 3, 0, 255),
            Y2 = (byte)Math.Clamp(strangerPosition.Y - 2, 0, 255),
            Direction = Direction.NorthEast,
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
