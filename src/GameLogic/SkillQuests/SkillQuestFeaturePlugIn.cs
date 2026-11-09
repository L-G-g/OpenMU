// <copyright file="SkillQuestFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The skill quests of PeroniaMU: NPCs teach skills when the player completes their quests.
/// </summary>
/// <remarks>
/// The quests are <see cref="QuestDefinition"/>s of the NPCs with a group of <see cref="SkillQuestGroups"/>. A quest
/// requires monster kills (<see cref="QuestDefinition.RequiredMonsterKills"/>, only of that monster definition: the
/// copies of a monster for other maps don't count) and rewards skills (<see cref="QuestRewardType.Skill"/>).
/// A player gets only the reward skills which its class can use, and a quest is only offered to a player whose class
/// can use at least one of them. The number of a quest is its tier: a player does the tiers in order, so a quest is
/// only offered when the quests of the lower tiers with rewards for its class (of every NPC) are done. The
/// tier settings of <see cref="SkillQuestConfiguration"/> add resets and the first class change to a tier.
/// <para>
/// The client (quests.dll) shows marks above the NPCs, a quest window and a tracker of the active quests. It answers
/// with the chat command <c>/quest</c> (see <see cref="SkillQuestChatCommandPlugIn"/>).
/// </para>
/// <para>
/// The kills of a party member, or of a summoned monster or a spectre of the party, on the same map count for every
/// member of the party.
/// </para>
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5B3E9C21-7A4D-4F6B-9E1C-2D8A6F4B3C70")]
public class SkillQuestFeaturePlugIn : IFeaturePlugIn, IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, IObjectAddedToMapPlugIn,
    ISupportCustomConfiguration<SkillQuestConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Skill quests";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "NPCs teach skills when the player completes their quests (quest groups 1000 to 1999). The client (quests.dll) shows golden marks above the NPCs, a quest window and a tracker; it answers with the chat command /quest.";

    private static readonly ConditionalWeakTable<Player, ViewState> PlayerStates = new();

    private static readonly ConditionalWeakTable<GameConfiguration, IReadOnlyList<QuestDefinition>> AllSkillQuests = new();

    private DateTime _nextRunUtc = DateTime.UtcNow;

    private bool _isRunning;

    /// <inheritdoc />
    public SkillQuestConfiguration? Configuration { get; set; }

    private SkillQuestConfiguration CurrentConfiguration => this.Configuration ??= new SkillQuestConfiguration();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new SkillQuestConfiguration();

    /// <summary>
    /// Gets the skill quests of an NPC.
    /// </summary>
    /// <param name="npc">The NPC definition.</param>
    /// <returns>The skill quests, ordered by group and number.</returns>
    public static IEnumerable<QuestDefinition> GetSkillQuests(MonsterDefinition npc)
    {
        return npc.Quests.Where(q => q.IsSkillQuest()).OrderBy(q => q.Group).ThenBy(q => q.Number);
    }

    /// <summary>
    /// Determines whether a skill is the reward of a skill quest. Such a skill can't be learned with an item.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="skill">The skill.</param>
    /// <returns><c>true</c>, if the skill is the reward of a skill quest; otherwise, <c>false</c>.</returns>
    public static bool IsQuestSkill(GameConfiguration gameConfiguration, Skill skill)
    {
        return FindQuestOfSkill(gameConfiguration, skill) is not null;
    }

    /// <summary>
    /// Finds the skill quest which rewards a skill.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="skill">The skill.</param>
    /// <returns>The quest, if there is one.</returns>
    public static QuestDefinition? FindQuestOfSkill(GameConfiguration gameConfiguration, Skill skill)
    {
        return (gameConfiguration.Monsters ?? Enumerable.Empty<MonsterDefinition>())
            .SelectMany(m => m.Quests ?? Enumerable.Empty<QuestDefinition>())
            .Where(q => q.IsSkillQuest())
            .FirstOrDefault(q => q.Rewards.Any(r => r.RewardType == QuestRewardType.Skill && r.SkillReward?.Number == skill.Number));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = this.CurrentConfiguration;
        if (this._nextRunUtc > DateTime.UtcNow || this._isRunning)
        {
            return;
        }

        this._isRunning = true;
        try
        {
            await gameContext.ForEachPlayerAsync(player => this.UpdatePlayerAsync(player, configuration).AsTask()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.Fail(ex.Message, ex.StackTrace);
        }
        finally
        {
            this._isRunning = false;
            this._nextRunUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, configuration.MarkIntervalSeconds));
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public async ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not Player player || !IsRealPlayer(player))
        {
            return;
        }

        // After a warp or the login, the tracker and the marks are sent again.
        var state = PlayerStates.GetOrCreateValue(player);
        state.LastTracker = null;
        state.LastMarks = null;
        await this.UpdatePlayerAsync(player, this.CurrentConfiguration).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster monster || monster.SummonedBy is not null)
        {
            return;
        }

        var killingPlayer = killer switch
        {
            Player player => player,
            Monster { SummonedBy: { } owner } => owner,
            _ => null,
        };

        if (killingPlayer is null)
        {
            return;
        }

        var credited = new HashSet<Player>();
        if (IsRealPlayer(killingPlayer))
        {
            credited.Add(killingPlayer);
        }

        if (this.CurrentConfiguration.CountPartyKills && killingPlayer.Party is { } party)
        {
            foreach (var member in party.PartyList.OfType<Player>())
            {
                if (IsRealPlayer(member) && member.CurrentMap == killingPlayer.CurrentMap)
                {
                    credited.Add(member);
                }
            }
        }

        foreach (var player in credited)
        {
            await this.CountKillAsync(player, monster.Definition).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows the quest window of an NPC to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The task.</returns>
    public async ValueTask ShowDialogAsync(Player player, NonPlayerCharacter npc)
    {
        if (!await this.CheckNpcAsync(player, npc).ConfigureAwait(false))
        {
            return;
        }

        await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowDialogAsync(npc, this.CreateDialog(player, npc))).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the next quest of the NPC.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The task.</returns>
    public async ValueTask AcceptAsync(Player player, NonPlayerCharacter npc)
    {
        if (!await this.CheckNpcAsync(player, npc).ConfigureAwait(false))
        {
            return;
        }

        var (mark, quest, _) = this.Evaluate(player, npc.Definition);
        if (mark == SkillQuestMark.Available && quest is not null)
        {
            var questState = GetOrCreateQuestState(player, quest.Group);
            await questState.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
            questState.ActiveQuest = quest;
            await player.ShowBlueMessageAsync($"Quest aceptada: {quest.Name.GetTranslation(player.Culture)}").ConfigureAwait(false);
        }

        await this.RefreshAsync(player, npc).ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers the completed quest of the NPC and teaches its skills.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The task.</returns>
    public async ValueTask DeliverAsync(Player player, NonPlayerCharacter npc)
    {
        if (!await this.CheckNpcAsync(player, npc).ConfigureAwait(false))
        {
            return;
        }

        var (mark, quest, questState) = this.Evaluate(player, npc.Definition);
        if (mark != SkillQuestMark.Ready || quest is null || questState is null)
        {
            await this.RefreshAsync(player, npc).ConfigureAwait(false);
            return;
        }

        var learned = new List<string>();
        foreach (var skill in GetRewardSkills(player, quest))
        {
            if (player.SkillList is { } skillList && !skillList.ContainsSkill(skill.Number.ToUnsigned()))
            {
                await skillList.AddLearnedSkillAsync(skill).ConfigureAwait(false);
                learned.Add(skill.Name.GetTranslation(player.Culture) ?? skill.Number.ToString());
            }
        }

        await questState.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
        questState.ActiveQuest = null;
        questState.LastFinishedQuest = quest;

        var questName = quest.Name.GetTranslation(player.Culture);
        await player.ShowBlueMessageAsync(learned.Count > 0
            ? $"Quest completada: {questName}. Aprendiste {string.Join(", ", learned)}."
            : $"Quest completada: {questName}. Ya conocias estas skills.").ConfigureAwait(false);

        var lines = new List<SkillQuestLine>
        {
            new(SkillQuestTextColor.Green, $"Quest completada: {questName}"),
            new(SkillQuestTextColor.White, string.Empty),
        };
        if (learned.Count > 0)
        {
            lines.Add(new(SkillQuestTextColor.White, "Aprendiste:"));
            lines.AddRange(learned.Select(name => new SkillQuestLine(SkillQuestTextColor.Gold, $"  {name}")));
        }
        else
        {
            lines.Add(new(SkillQuestTextColor.Gray, "Ya conocías estas skills."));
        }

        await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowDialogAsync(npc, new SkillQuestDialog(GetNpcName(player, npc), lines, SkillQuestAction.None))).ConfigureAwait(false);
        await this.UpdatePlayerAsync(player, this.CurrentConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// Completes the objectives of all active skill quests of the player (for tests by a game master).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask CompleteObjectivesAsync(Player player)
    {
        foreach (var questState in GetActiveSkillQuestStates(player))
        {
            foreach (var requirement in questState.ActiveQuest!.RequiredMonsterKills)
            {
                GetOrCreateRequirementState(player, questState, requirement).KillCount = requirement.MinimumNumber;
            }
        }

        await this.UpdatePlayerAsync(player, this.CurrentConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets all skill quests of the player (for tests by a game master). The learned skills stay.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ResetAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        foreach (var questState in character.QuestStates.Where(s => SkillQuestGroups.IsSkillQuestGroup(s.Group)).ToList())
        {
            await questState.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
            questState.ActiveQuest = null;
            questState.LastFinishedQuest = null;
        }

        await this.UpdatePlayerAsync(player, this.CurrentConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds an NPC with skill quests in the view of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="id">The id of the NPC as the client knows it.</param>
    /// <returns>The NPC, if found.</returns>
    public static NonPlayerCharacter? FindNpc(Player player, ushort id)
    {
        if (player.CurrentMap?.GetObject((ushort)(id & 0x7FFF)) is NonPlayerCharacter npc
            && npc is not Monster
            && GetSkillQuests(npc.Definition).Any())
        {
            return npc;
        }

        return null;
    }

    private static bool IsRealPlayer(Player player)
    {
        return player is not OfflinePlayer
               && player.Account?.IsBot != true
               && player.SelectedCharacter is not null
               && player.PlayerState.CurrentState == MUnique.OpenMU.GameLogic.PlayerState.EnteredWorld;
    }

    private static string GetNpcName(Player player, NonPlayerCharacter npc)
    {
        return npc.Definition.Designation.GetTranslation(player.Culture) ?? "NPC";
    }

    private static CharacterQuestState GetOrCreateQuestState(Player player, short group)
    {
        var character = player.SelectedCharacter!;
        var questState = character.QuestStates.FirstOrDefault(q => q.Group == group);
        if (questState is null)
        {
            questState = player.PersistenceContext.CreateNew<CharacterQuestState>();
            questState.Group = group;
            character.QuestStates.Add(questState);
        }

        return questState;
    }

    private static QuestMonsterKillRequirementState GetOrCreateRequirementState(Player player, CharacterQuestState questState, QuestMonsterKillRequirement requirement)
    {
        if (questState.RequirementStates.FirstOrDefault(s => Equals(s.Requirement, requirement)) is not { } requirementState)
        {
            requirementState = player.PersistenceContext.CreateNew<QuestMonsterKillRequirementState>();
            requirementState.Requirement = requirement;
            questState.RequirementStates.Add(requirementState);
        }

        return requirementState;
    }

    private static int GetKillCount(CharacterQuestState questState, QuestMonsterKillRequirement requirement)
    {
        return questState.RequirementStates.FirstOrDefault(s => Equals(s.Requirement, requirement))?.KillCount ?? 0;
    }

    private static bool IsCompleted(CharacterQuestState questState)
    {
        return questState.ActiveQuest is { } quest
               && quest.RequiredMonsterKills.All(r => GetKillCount(questState, r) >= r.MinimumNumber);
    }

    private static IEnumerable<CharacterQuestState> GetActiveSkillQuestStates(Player player)
    {
        return player.SelectedCharacter?.QuestStates?
                   .Where(s => SkillQuestGroups.IsSkillQuestGroup(s.Group) && s.ActiveQuest is not null)
                   .OrderBy(s => s.Group)
               ?? Enumerable.Empty<CharacterQuestState>();
    }

    private static IEnumerable<Skill> GetRewardSkills(Player player, QuestDefinition quest)
    {
        var characterClass = player.SelectedCharacter?.CharacterClass;
        return quest.Rewards
            .Where(r => r.RewardType == QuestRewardType.Skill)
            .Select(r => r.SkillReward)
            .OfType<Skill>()
            .Where(s => characterClass is not null && s.QualifiedCharacters.Contains(characterClass));
    }

    private static IReadOnlyList<QuestDefinition> GetAllSkillQuests(GameConfiguration configuration)
    {
        return AllSkillQuests.GetValue(configuration, c => c.Monsters
            .SelectMany(m => m.Quests ?? Enumerable.Empty<QuestDefinition>())
            .Where(q => q.IsSkillQuest())
            .ToList());
    }

    /// <summary>
    /// Determines whether a quest is meant for the class of the player: its <see cref="QuestDefinition.QualifiedCharacter"/>
    /// is the class or one of its previous generations, and the class can use at least one of its reward skills.
    /// </summary>
    private static bool IsForPlayer(Player player, QuestDefinition quest)
    {
        var characterClass = player.SelectedCharacter?.CharacterClass;
        if (characterClass is null)
        {
            return false;
        }

        if (quest.QualifiedCharacter is { } qualified)
        {
            var current = qualified;
            var generations = 0;
            while (current is not null && !Equals(current, characterClass) && generations++ < 5)
            {
                current = current.NextGenerationClass;
            }

            if (current is null || !Equals(current, characterClass))
            {
                return false;
            }
        }

        return GetRewardSkills(player, quest).Any();
    }

    private static bool IsFinished(Character character, QuestDefinition quest)
    {
        return character.QuestStates?.FirstOrDefault(s => s.Group == quest.Group)?.LastFinishedQuest?.Number >= quest.Number;
    }

    /// <summary>
    /// Gets the next quest of a group for the player: the first one after the last finished one with rewards for its class.
    /// </summary>
    private static QuestDefinition? GetNextQuest(Player player, IEnumerable<QuestDefinition> group, CharacterQuestState? questState)
    {
        var lastNumber = questState?.LastFinishedQuest?.Number ?? 0;
        return group.Where(q => q.Number > lastNumber && IsForPlayer(player, q)).OrderBy(q => q.Number).FirstOrDefault();
    }

    /// <summary>
    /// Gets the requirement of a quest which the player doesn't meet, as a text for the quest window; or <c>null</c>,
    /// if the player is qualified for the quest.
    /// </summary>
    private string? GetMissingRequirement(Player player, QuestDefinition quest)
    {
        if (player.SelectedCharacter is not { } character || !IsForPlayer(player, quest))
        {
            return "No tengo nada para enseñarle a tu clase.";
        }

        if (quest.MinimumCharacterLevel > player.Level)
        {
            return $"Volvé cuando llegues al nivel {quest.MinimumCharacterLevel}.";
        }

        if (quest.MaximumCharacterLevel > 0 && quest.MaximumCharacterLevel < player.Level)
        {
            return "Ya superaste el nivel de esta lección.";
        }

        var configuration = this.CurrentConfiguration;
        var minimumResets = configuration.GetMinimumResets(quest.Number);
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
        if (resets < minimumResets)
        {
            return $"Volvé cuando tengas {minimumResets} resets.";
        }

        if (configuration.RequiresClassChange(quest.Number)
            && character.CharacterClass is { NextGenerationClass: { IsMasterClass: false } })
        {
            return "Volvé cuando hayas hecho tu primer cambio de clase.";
        }

        var missing = GetAllSkillQuests(player.GameContext.Configuration)
            .Where(q => q.Number < quest.Number && IsForPlayer(player, q) && !IsFinished(character, q))
            .OrderBy(q => q.Number)
            .FirstOrDefault();
        if (missing is not null)
        {
            var giver = missing.QuestGiver?.Designation.GetTranslation(player.Culture) ?? "otro maestro";
            return $"Primero terminá \"{missing.Name.GetTranslation(player.Culture)}\" con {giver}.";
        }

        return null;
    }

    private bool IsQualified(Player player, QuestDefinition quest) => this.GetMissingRequirement(player, quest) is null;

    private static string GetMonsterName(Player player, QuestMonsterKillRequirement requirement)
    {
        return requirement.Monster?.Designation.GetTranslation(player.Culture) ?? "?";
    }

    private async ValueTask<bool> CheckNpcAsync(Player player, NonPlayerCharacter npc)
    {
        if (!IsRealPlayer(player) || npc.CurrentMap != player.CurrentMap)
        {
            return false;
        }

        if (player.GetDistanceTo(npc) > this.CurrentConfiguration.InteractionRange)
        {
            await player.ShowBlueMessageAsync($"Acercate a {GetNpcName(player, npc)} para hablar.").ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluates the skill quests of an NPC for the player: an active quest has precedence, then the next quest of a group.
    /// </summary>
    private (SkillQuestMark Mark, QuestDefinition? Quest, CharacterQuestState? State) Evaluate(Player player, MonsterDefinition npc)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return (SkillQuestMark.None, null, null);
        }

        (SkillQuestMark Mark, QuestDefinition? Quest, CharacterQuestState? State) result = (SkillQuestMark.None, null, null);
        foreach (var group in GetSkillQuests(npc).GroupBy(q => q.Group))
        {
            var questState = character.QuestStates?.FirstOrDefault(s => s.Group == group.Key);
            if (questState?.ActiveQuest is { } active && group.Contains(active))
            {
                var mark = IsCompleted(questState) ? SkillQuestMark.Ready : SkillQuestMark.InProgress;
                if (mark > result.Mark || result.Mark == SkillQuestMark.Available)
                {
                    result = (mark, active, questState);
                }

                continue;
            }

            if (questState?.ActiveQuest is not null)
            {
                continue; // an active quest of the group of another NPC
            }

            var next = GetNextQuest(player, group, questState);
            if (next is not null && result.Mark == SkillQuestMark.None && this.IsQualified(player, next))
            {
                result = (SkillQuestMark.Available, next, questState);
            }
        }

        return result;
    }

    private SkillQuestDialog CreateDialog(Player player, NonPlayerCharacter npc)
    {
        var npcName = GetNpcName(player, npc);
        var (mark, quest, questState) = this.Evaluate(player, npc.Definition);
        var lines = new List<SkillQuestLine>();
        if (quest is null)
        {
            var character = player.SelectedCharacter;
            var nextQuests = GetSkillQuests(npc.Definition)
                .GroupBy(q => q.Group)
                .Select(g => GetNextQuest(player, g, character?.QuestStates?.FirstOrDefault(s => s.Group == g.Key)))
                .OfType<QuestDefinition>()
                .OrderBy(q => q.Number)
                .ToList();
            if (nextQuests.Count == 0)
            {
                var anyQuestForClass = GetSkillQuests(npc.Definition).Any(q => IsForPlayer(player, q));
                lines.Add(new(SkillQuestTextColor.White, anyQuestForClass
                    ? "Ya te enseñé todo lo que sé."
                    : "No tengo nada para enseñarle a tu clase."));
            }
            else
            {
                var next = nextQuests[0];
                lines.Add(new(SkillQuestTextColor.White, "Por ahora no tengo nada más para enseñarte."));
                lines.Add(new(SkillQuestTextColor.Gold, next.Name.GetTranslation(player.Culture) ?? string.Empty));
                lines.Add(new(SkillQuestTextColor.Gray, this.GetMissingRequirement(player, next) ?? string.Empty));
            }

            return new SkillQuestDialog(npcName, lines, SkillQuestAction.None);
        }

        lines.Add(new(SkillQuestTextColor.Gold, quest.Name.GetTranslation(player.Culture) ?? string.Empty));
        lines.Add(new(SkillQuestTextColor.White, mark switch
        {
            SkillQuestMark.Available => "Si querés aprender, primero demostrá lo que valés:",
            SkillQuestMark.Ready => "Cumpliste. Ya podés aprender lo prometido:",
            _ => "Todavía te falta:",
        }));

        foreach (var requirement in quest.RequiredMonsterKills)
        {
            var count = questState?.ActiveQuest == quest ? Math.Min(GetKillCount(questState, requirement), requirement.MinimumNumber) : 0;
            var done = count >= requirement.MinimumNumber;
            var text = mark == SkillQuestMark.Available
                ? $"  Matar {requirement.MinimumNumber} {GetMonsterName(player, requirement)}"
                : $"  {GetMonsterName(player, requirement)}: {count}/{requirement.MinimumNumber}";
            lines.Add(new(done ? SkillQuestTextColor.Green : SkillQuestTextColor.White, text));
        }

        lines.Add(new(SkillQuestTextColor.White, string.Empty));
        lines.Add(new(SkillQuestTextColor.White, "Recompensa:"));
        foreach (var skill in GetRewardSkills(player, quest))
        {
            var known = player.SkillList?.ContainsSkill(skill.Number.ToUnsigned()) == true;
            lines.Add(new(known ? SkillQuestTextColor.Gray : SkillQuestTextColor.Gold, $"  {skill.Name.GetTranslation(player.Culture)}{(known ? " (ya la conocés)" : string.Empty)}"));
        }

        var action = mark switch
        {
            SkillQuestMark.Available => SkillQuestAction.Accept,
            SkillQuestMark.Ready => SkillQuestAction.Deliver,
            _ => SkillQuestAction.None,
        };
        return new SkillQuestDialog(npcName, lines, action);
    }

    private async ValueTask RefreshAsync(Player player, NonPlayerCharacter npc)
    {
        await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowDialogAsync(npc, this.CreateDialog(player, npc))).ConfigureAwait(false);
        await this.UpdatePlayerAsync(player, this.CurrentConfiguration).ConfigureAwait(false);
    }

    private async ValueTask CountKillAsync(Player player, MonsterDefinition monster)
    {
        var changed = false;
        foreach (var questState in GetActiveSkillQuestStates(player).ToList())
        {
            var wasCompleted = IsCompleted(questState);
            foreach (var requirement in questState.ActiveQuest!.RequiredMonsterKills.Where(r => Equals(r.Monster, monster)))
            {
                var requirementState = GetOrCreateRequirementState(player, questState, requirement);
                if (requirementState.KillCount < requirement.MinimumNumber)
                {
                    requirementState.KillCount++;
                    changed = true;
                }
            }

            if (!wasCompleted && IsCompleted(questState))
            {
                var giver = questState.ActiveQuest.QuestGiver?.Designation.GetTranslation(player.Culture) ?? "el NPC";
                await player.ShowBlueMessageAsync($"Objetivos cumplidos: {questState.ActiveQuest.Name.GetTranslation(player.Culture)}. Volve con {giver}.").ConfigureAwait(false);
            }
        }

        if (changed)
        {
            await this.SendTrackerAsync(player, false).ConfigureAwait(false);
        }
    }

    private async ValueTask UpdatePlayerAsync(Player player, SkillQuestConfiguration configuration)
    {
        if (!IsRealPlayer(player) || player.CurrentMap is not { } map)
        {
            return;
        }

        var marks = map.GetNpcsInRange(player.Position, configuration.MarkRange)
            .Where(npc => npc is not Monster && npc.Definition.Quests.Any(q => q.IsSkillQuest()))
            .Select(npc => (Npc: npc, Mark: this.Evaluate(player, npc.Definition).Mark))
            .Where(entry => entry.Mark != SkillQuestMark.None)
            .ToList();

        var state = PlayerStates.GetOrCreateValue(player);
        var markKey = string.Join(';', marks.Select(m => $"{m.Npc.Id}:{(byte)m.Mark}"));
        var now = DateTime.UtcNow;
        if (markKey != state.LastMarks || (marks.Count > 0 && now - state.LastMarksSentUtc >= TimeSpan.FromSeconds(configuration.MarkRepeatSeconds)))
        {
            state.LastMarks = markKey;
            state.LastMarksSentUtc = now;
            await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowMarksAsync(marks)).ConfigureAwait(false);
        }

        await this.SendTrackerAsync(player, false).ConfigureAwait(false);
    }

    private async ValueTask SendTrackerAsync(Player player, bool force)
    {
        var lines = new List<SkillQuestLine>();
        foreach (var questState in GetActiveSkillQuestStates(player))
        {
            var quest = questState.ActiveQuest!;
            var completed = IsCompleted(questState);
            lines.Add(new(completed ? SkillQuestTextColor.Green : SkillQuestTextColor.Gold, quest.Name.GetTranslation(player.Culture) ?? string.Empty));
            if (completed)
            {
                var giver = quest.QuestGiver?.Designation.GetTranslation(player.Culture) ?? "el NPC";
                lines.Add(new(SkillQuestTextColor.Green, $"  Volvé con {giver}"));
                continue;
            }

            foreach (var requirement in quest.RequiredMonsterKills)
            {
                var count = Math.Min(GetKillCount(questState, requirement), requirement.MinimumNumber);
                lines.Add(new(count >= requirement.MinimumNumber ? SkillQuestTextColor.Green : SkillQuestTextColor.White, $"  {GetMonsterName(player, requirement)} {count}/{requirement.MinimumNumber}"));
            }
        }

        this.AddAvailableQuests(player, lines);

        var state = PlayerStates.GetOrCreateValue(player);
        var key = string.Join('\n', lines.Select(l => $"{(byte)l.Color}{l.Text}"));
        if (!force && key == state.LastTracker)
        {
            return;
        }

        state.LastTracker = key;
        await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowTrackerAsync(lines)).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the quests which the player can start to the tracker, with the NPC to talk to.
    /// </summary>
    private void AddAvailableQuests(Player player, List<SkillQuestLine> lines)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var configuration = player.GameContext.Configuration;
        foreach (var group in GetAllSkillQuests(configuration).GroupBy(q => q.Group).OrderBy(g => g.Key))
        {
            var questState = character.QuestStates?.FirstOrDefault(s => s.Group == group.Key);
            if (questState?.ActiveQuest is not null
                || GetNextQuest(player, group, questState) is not { } next
                || !this.IsQualified(player, next))
            {
                continue;
            }

            var giver = next.QuestGiver?.Designation.GetTranslation(player.Culture) ?? "un maestro";
            var map = next.QuestGiver is { } npc ? GetSpawnMapName(player, configuration, npc) : null;
            lines.Add(new(SkillQuestTextColor.Gold, $"Quest disponible: {next.Name.GetTranslation(player.Culture)}"));
            lines.Add(new(SkillQuestTextColor.White, map is null ? $"  Hablá con {giver}" : $"  Hablá con {giver} ({map})"));
        }
    }

    /// <summary>
    /// Gets the name of the map where an NPC stands; <c>null</c> for a wandering NPC.
    /// </summary>
    private static string? GetSpawnMapName(Player player, GameConfiguration configuration, MonsterDefinition npc)
    {
        var spawn = configuration.Maps
            .SelectMany(map => map.MonsterSpawns.Select(spawn => (Map: map, Spawn: spawn)))
            .FirstOrDefault(s => s.Spawn.MonsterDefinition == npc);
        if (spawn.Map is null || spawn.Spawn.SpawnTrigger == SpawnTrigger.Wandering)
        {
            return null;
        }

        return spawn.Map.Name.GetTranslation(player.Culture);
    }

    private sealed class ViewState
    {
        public string? LastMarks { get; set; }

        public DateTime LastMarksSentUtc { get; set; }

        public string? LastTracker { get; set; }
    }
}
