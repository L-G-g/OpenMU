// <copyright file="SkillQuestFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.SkillQuests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
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
/// requires monster kills (<see cref="QuestDefinition.RequiredMonsterKills"/>, counted by the number of the monster, so
/// that the copies of a monster on other maps count as well) and rewards skills (<see cref="QuestRewardType.Skill"/>).
/// A player gets only the reward skills which its class can use, and a quest is only offered to a player whose class
/// can use at least one of them. The quests of a group are done in the order of their numbers.
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
        return gameConfiguration.Monsters
            .SelectMany(m => m.Quests)
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
        return player.SelectedCharacter?.QuestStates
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

    private static bool IsQualified(Player player, QuestDefinition quest)
    {
        return GetRewardSkills(player, quest).Any()
               && quest.MinimumCharacterLevel <= player.Level
               && (quest.MaximumCharacterLevel == 0 || quest.MaximumCharacterLevel >= player.Level)
               && (quest.QualifiedCharacter is null || Equals(quest.QualifiedCharacter, player.SelectedCharacter?.CharacterClass));
    }

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
            var questState = character.QuestStates.FirstOrDefault(s => s.Group == group.Key);
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

            var lastNumber = questState?.LastFinishedQuest?.Number ?? 0;
            var next = group.Where(q => q.Number > lastNumber).OrderBy(q => q.Number).FirstOrDefault();
            if (next is not null && IsQualified(player, next) && result.Mark == SkillQuestMark.None)
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
            var anyQuestForClass = GetSkillQuests(npc.Definition).Any(q => GetRewardSkills(player, q).Any());
            lines.Add(new(SkillQuestTextColor.White, anyQuestForClass
                ? "Por ahora no tengo nada más para enseñarte."
                : "No tengo nada para enseñarle a tu clase."));
            var nextByLevel = GetSkillQuests(npc.Definition)
                .Where(q => GetRewardSkills(player, q).Any() && q.MinimumCharacterLevel > player.Level)
                .OrderBy(q => q.MinimumCharacterLevel)
                .FirstOrDefault();
            if (nextByLevel is not null)
            {
                lines.Add(new(SkillQuestTextColor.Gray, $"Volvé cuando llegues al nivel {nextByLevel.MinimumCharacterLevel}."));
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
            foreach (var requirement in questState.ActiveQuest!.RequiredMonsterKills.Where(r => r.Monster?.Number == monster.Number))
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

        var state = PlayerStates.GetOrCreateValue(player);
        var key = string.Join('\n', lines.Select(l => $"{(byte)l.Color}{l.Text}"));
        if (!force && key == state.LastTracker)
        {
            return;
        }

        state.LastTracker = key;
        await player.InvokeViewPlugInAsync<ISkillQuestViewPlugIn>(p => p.ShowTrackerAsync(lines)).ConfigureAwait(false);
    }

    private sealed class ViewState
    {
        public string? LastMarks { get; set; }

        public DateTime LastMarksSentUtc { get; set; }

        public string? LastTracker { get; set; }
    }
}
