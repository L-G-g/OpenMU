// <copyright file="Questline.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.SkillQuests;

/// <summary>
/// The kind of a <see cref="QuestlineEntry"/>.
/// </summary>
public enum QuestlineEntryKind
{
    /// <summary>
    /// A step of the story (<see cref="StoryScript.Steps"/>).
    /// </summary>
    Story,

    /// <summary>
    /// A tier of the skill quests: all skill quests with this number which are meant for the class of the player.
    /// </summary>
    SkillTier,
}

/// <summary>
/// A mission of the <see cref="Questline"/>.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Number">The number of the story step, or the number of the skill tier (1 to 4).</param>
/// <param name="MinimumResets">The minimum resets of the entry itself, see <see cref="StoryConfiguration"/> and <see cref="SkillQuestConfiguration"/>.</param>
/// <param name="MinimumLevel">The minimum level of the entry itself (the skill quests of the second tier need level 150).</param>
/// <param name="Prerequisites">The entries which have to be done before, because of the requirements of the entry: the
/// previous entry of its own kind, the step which opens the maps of the monsters to kill, the step which allows the
/// first class change.</param>
public sealed record QuestlineEntry(QuestlineEntryKind Kind, int Number, int MinimumResets, int MinimumLevel, IReadOnlyList<QuestlineEntry> Prerequisites)
{
    /// <summary>
    /// Gets a short name, e.g. for the game masters ("H3" for the story step 3, "S2" for the skill tier 2).
    /// </summary>
    public string Code => (this.Kind == QuestlineEntryKind.Story ? "H" : "S") + this.Number;
}

/// <summary>
/// The questline of PeroniaMU: the steps of the story and the tiers of the skill quests in one line, one after another.
/// </summary>
/// <remarks>
/// The order isn't written down anywhere: it comes from the requirements, so that it follows the configuration.
/// Each entry has prerequisites which it really needs (the previous entry of its kind; for a skill tier, the story step
/// which opens the maps of its monsters, or the step after which Sebina allows the first class change). Among the
/// entries whose prerequisites are placed, the one with the lowest resets comes next (with the resets of its
/// prerequisites), then the one with the lowest level, then a story step before a skill tier.
/// <para>
/// A player can only start an entry when all entries before it are done (<see cref="GetFirstOpenEntry"/>).
/// </para>
/// </remarks>
public static class Questline
{
    private static readonly ConditionalWeakTable<GameConfiguration, CacheEntry> Cache = new();

    /// <summary>
    /// Gets the questline for the configurations.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration, with the skill quests.</param>
    /// <param name="story">The configuration of the story mode.</param>
    /// <param name="skills">The configuration of the skill quests, or <c>null</c> if they aren't active.</param>
    /// <returns>The entries, in their order.</returns>
    public static IReadOnlyList<QuestlineEntry> Get(GameConfiguration gameConfiguration, StoryConfiguration story, SkillQuestConfiguration? skills)
    {
        var key = CreateKey(story, skills);
        var cached = Cache.GetOrCreateValue(gameConfiguration);
        lock (cached)
        {
            if (cached.Key != key || cached.Entries is null)
            {
                cached.Entries = Build(gameConfiguration, story, skills);
                cached.Key = key;
            }

            return cached.Entries;
        }
    }

    /// <summary>
    /// Builds the questline.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration, with the skill quests.</param>
    /// <param name="story">The configuration of the story mode.</param>
    /// <param name="skills">The configuration of the skill quests, or <c>null</c> if they aren't active.</param>
    /// <returns>The entries, in their order.</returns>
    public static IReadOnlyList<QuestlineEntry> Build(GameConfiguration gameConfiguration, StoryConfiguration story, SkillQuestConfiguration? skills)
    {
        var storyEntries = new List<QuestlineEntry>();
        foreach (var step in StoryScript.Steps)
        {
            QuestlineEntry[] prerequisites = storyEntries.Count > 0 ? [storyEntries[^1]] : [];
            storyEntries.Add(new QuestlineEntry(QuestlineEntryKind.Story, step.Number, story.GetMinimumResets(step.Number), 0, prerequisites));
        }

        var entries = new List<QuestlineEntry>(storyEntries);
        if (skills is not null)
        {
            QuestlineEntry? previousTier = null;
            foreach (var tier in SkillQuestFeaturePlugIn.GetAllSkillQuests(gameConfiguration).GroupBy(q => q.Number).OrderBy(g => g.Key))
            {
                var prerequisites = new List<QuestlineEntry>();
                if (previousTier is not null)
                {
                    prerequisites.Add(previousTier);
                }
                else if (story.SkillQuestsRequireStory || story.InterleaveSkillQuests)
                {
                    AddStoryPrerequisite(prerequisites, storyEntries, StoryScript.Awakening);
                }

                if (story.LockMaps)
                {
                    AddStoryPrerequisite(prerequisites, storyEntries, GetUnlockingStep(gameConfiguration, story, tier));
                }

                if (skills.RequiresClassChange(tier.Key) && story.ClassChangeRequiresStory)
                {
                    AddStoryPrerequisite(prerequisites, storyEntries, StoryScript.SchoolOfLight);
                }

                var minimumLevel = tier.Select(q => (int)q.MinimumCharacterLevel).DefaultIfEmpty(0).Min();
                previousTier = new QuestlineEntry(QuestlineEntryKind.SkillTier, tier.Key, skills.GetMinimumResets(tier.Key), minimumLevel, prerequisites);
                entries.Add(previousTier);
            }
        }

        return Sort(entries);
    }

    /// <summary>
    /// Gets the first entry which a player didn't do yet: its current mission.
    /// </summary>
    /// <param name="entries">The questline.</param>
    /// <param name="isDone">Determines whether the player did an entry.</param>
    /// <returns>The index of the entry, or -1 if the player did all of them.</returns>
    public static int GetFirstOpenEntry(IReadOnlyList<QuestlineEntry> entries, Func<QuestlineEntry, bool> isDone)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (!isDone(entries[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the entry of a story step or a skill tier.
    /// </summary>
    /// <param name="entries">The questline.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="number">The number.</param>
    /// <returns>The index of the entry, or -1.</returns>
    public static int IndexOf(IReadOnlyList<QuestlineEntry> entries, QuestlineEntryKind kind, int number)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Kind == kind && entries[i].Number == number)
            {
                return i;
            }
        }

        return -1;
    }

    private static void AddStoryPrerequisite(List<QuestlineEntry> prerequisites, List<QuestlineEntry> storyEntries, int step)
    {
        if (step >= 1 && step <= storyEntries.Count && !prerequisites.Contains(storyEntries[step - 1]))
        {
            prerequisites.Add(storyEntries[step - 1]);
        }
    }

    /// <summary>
    /// Gets the story step which opens the maps of the monsters of a skill tier: for each monster the first map where it
    /// lives, and of these the one which opens last.
    /// </summary>
    private static int GetUnlockingStep(GameConfiguration gameConfiguration, StoryConfiguration story, IEnumerable<QuestDefinition> tier)
    {
        var monsters = tier.SelectMany(q => q.RequiredMonsterKills).Select(r => r.Monster).OfType<MonsterDefinition>().Distinct().ToList();
        var result = 0;
        foreach (var monster in monsters)
        {
            var steps = gameConfiguration.Maps
                .Where(map => map.MonsterSpawns.Any(spawn => spawn.MonsterDefinition == monster))
                .Select(map => story.GetUnlockingStep(map.Number))
                .ToList();
            if (steps.Count > 0)
            {
                result = Math.Max(result, steps.Min());
            }
        }

        return result;
    }

    private static List<QuestlineEntry> Sort(List<QuestlineEntry> entries)
    {
        var effectiveResets = new Dictionary<QuestlineEntry, int>();
        var effectiveLevel = new Dictionary<QuestlineEntry, int>();
        int GetResets(QuestlineEntry entry)
        {
            if (!effectiveResets.TryGetValue(entry, out var value))
            {
                value = Math.Max(entry.MinimumResets, entry.Prerequisites.Select(GetResets).DefaultIfEmpty(0).Max());
                effectiveResets[entry] = value;
            }

            return value;
        }

        int GetLevel(QuestlineEntry entry)
        {
            if (!effectiveLevel.TryGetValue(entry, out var value))
            {
                // A level only matters within the same resets.
                value = Math.Max(entry.MinimumLevel, entry.Prerequisites.Where(p => GetResets(p) == GetResets(entry)).Select(GetLevel).DefaultIfEmpty(0).Max());
                effectiveLevel[entry] = value;
            }

            return value;
        }

        var result = new List<QuestlineEntry>(entries.Count);
        var open = new List<QuestlineEntry>(entries);
        while (open.Count > 0)
        {
            var next = open
                .Where(e => e.Prerequisites.All(result.Contains))
                .OrderBy(GetResets)
                .ThenBy(GetLevel)
                .ThenBy(e => e.Kind)
                .ThenBy(e => e.Number)
                .FirstOrDefault();
            if (next is null)
            {
                // Can't happen (the prerequisites always point backwards), but never loop forever.
                result.AddRange(open);
                break;
            }

            result.Add(next);
            open.Remove(next);
        }

        return result;
    }

    private static string CreateKey(StoryConfiguration story, SkillQuestConfiguration? skills)
    {
        var resets = string.Join(',', StoryScript.Steps.Select(s => story.GetMinimumResets(s.Number)));
        var skillKey = skills is null ? "-" : $"{skills.GetMinimumResets(3)},{skills.GetMinimumResets(4)},{skills.Tier4RequiresClassChange}";
        return $"{resets}|{story.LockMaps}|{story.SkillQuestsRequireStory}|{story.InterleaveSkillQuests}|{story.ClassChangeRequiresStory}|{story.DeviasMaps}|{story.DungeonMaps}|{story.LostTowerMaps}|{story.OldWorldMaps}|{skillKey}";
    }

    private sealed class CacheEntry
    {
        public string? Key { get; set; }

        public IReadOnlyList<QuestlineEntry>? Entries { get; set; }
    }
}
