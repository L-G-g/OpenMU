// <copyright file="EventDifficulty.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

using System.Collections.Concurrent;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Calculates the stats of the monsters of an event at some resets, from the monsters of the hunting maps
/// which need these resets, see <see cref="EventReworkConfiguration.ScaleMonstersByResets"/>.
/// </summary>
/// <remarks>
/// A stat is compared by the weighted geometric mean of the normal monsters of a map (by their number of spawns),
/// with the multipliers of the <see cref="MapDifficultyScaler"/>. Between two reference maps, the logarithm of a stat rises
/// evenly with the resets. A normal monster of an event gets exactly the stats of the resets; a strong one (a boss, with
/// more than <see cref="StrongMonsterHealthFactor"/> times the median health of its map, or the only one of its kind) keeps its strength in relation
/// to the normal monsters of its map, with at least <see cref="MinimumBossHealthFactor"/> times their health.
/// </remarks>
public sealed class EventDifficulty
{
    /// <summary>
    /// The number of the scaled stats: health, damage, defense, attack rate and defense rate.
    /// </summary>
    public const int StatCount = 5;

    /// <summary>
    /// A monster with more than this factor of the median health of the monsters of its map is a strong monster.
    /// </summary>
    internal const double StrongMonsterHealthFactor = 2;

    /// <summary>
    /// A strong monster has at least this factor of the health of the normal monsters at the same resets.
    /// </summary>
    internal const double MinimumBossHealthFactor = 3;

    /// <summary>
    /// Monsters of a reference map with more than this factor of the median health, or less than its inverse,
    /// e.g. bosses, don't count for the reference stats.
    /// </summary>
    private const double ReferenceOutlierFactor = 3;

    /// <summary>
    /// Above the last reference map, the stats keep rising as from the reference map at least this number of resets before it.
    /// </summary>
    private const int ExtrapolationResets = 90;

    private readonly List<(int Resets, double[] LogStats)> _references;

    private readonly ConcurrentDictionary<GameMapDefinition, EventMapStats> _eventMaps = new();

    private EventDifficulty(List<(int Resets, double[] LogStats)> references)
    {
        this._references = references;
    }

    /// <summary>
    /// Gets the scaled stats of the monsters, in the order of their index.
    /// </summary>
    internal static AttributeDefinition[][] StatAttributes { get; } =
    [
        [Stats.MaximumHealth],
        [Stats.MinimumPhysBaseDmg, Stats.MaximumPhysBaseDmg],
        [Stats.DefenseBase],
        [Stats.AttackRatePvm],
        [Stats.DefenseRatePvm],
    ];

    /// <summary>
    /// Creates the difficulty from the reference maps.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="configuration">The configuration of the event rework.</param>
    /// <param name="scalerConfiguration">The configuration of the map difficulty scaler, or <c>null</c> if it's not active.</param>
    /// <returns>The difficulty, or <c>null</c> if no reference map has monsters.</returns>
    public static EventDifficulty? Create(GameConfiguration gameConfiguration, EventReworkConfiguration configuration, MapDifficultyScalerConfiguration? scalerConfiguration)
    {
        var references = new List<(int Resets, double[] LogStats)>();
        foreach (var reference in configuration.DifficultyReferenceMaps ?? Enumerable.Empty<EventDifficultyReferenceMap>())
        {
            var map = gameConfiguration.Maps.FirstOrDefault(m => m.Number == reference.MapNumber && m.Discriminator == 0);
            if (map is null)
            {
                continue;
            }

            var monsters = map.MonsterSpawns
                .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.Automatic
                                && spawn.MonsterDefinition is { ObjectKind: NpcObjectKind.Monster } definition
                                && !IsElite(definition))
                .GroupBy(spawn => spawn.MonsterDefinition!)
                .Select(group => (Definition: group.Key, Weight: (double)group.Sum(spawn => Math.Max((int)spawn.Quantity, 1))))
                .ToList();
            var medianHealth = GetWeightedMedian(monsters.Select(m => (GetBaseStat(m.Definition, 0), m.Weight)));
            if (medianHealth <= 0)
            {
                continue;
            }

            var normal = monsters
                .Where(m => GetBaseStat(m.Definition, 0) is var health
                            && health <= medianHealth * ReferenceOutlierFactor
                            && health >= medianHealth / ReferenceOutlierFactor)
                .ToList();
            var logStats = new double[StatCount];
            for (var stat = 0; stat < StatCount; stat++)
            {
                logStats[stat] = GetLogMean(normal.Select(m =>
                {
                    var multipliers = MapDifficultyScaler.GetMultipliers(scalerConfiguration, reference.MapNumber, m.Definition.Number);
                    return (GetBaseStat(m.Definition, stat) * GetMultiplier(multipliers, stat), m.Weight);
                }));
            }

            references.Add((reference.Resets, logStats));
        }

        return references.Count == 0 ? null : new EventDifficulty(references.OrderBy(r => r.Resets).ToList());
    }

    /// <summary>
    /// Gets the multipliers of the stats of a monster of an event map at some resets.
    /// </summary>
    /// <param name="eventMap">The definition of the event map.</param>
    /// <param name="monster">The monster.</param>
    /// <param name="resets">The resets.</param>
    /// <returns>The multipliers, by the index of the stat.</returns>
    public float[] GetMultipliers(GameMapDefinition eventMap, MonsterDefinition monster, double resets)
    {
        var mapStats = this._eventMaps.GetOrAdd(eventMap, EventMapStats.Create);
        var target = this.GetLogStats(resets);
        var health = GetBaseStat(monster, 0);
        var isStrong = health > mapStats.MedianHealth * StrongMonsterHealthFactor
                       || (mapStats.SingleMonsters.Contains(monster) && health > mapStats.MedianHealth);
        var result = new float[StatCount];
        for (var stat = 0; stat < StatCount; stat++)
        {
            var baseStat = GetBaseStat(monster, stat);
            var compared = isStrong ? mapStats.LogStats[stat] : (baseStat > 0 ? Math.Log(baseStat) : double.NaN);
            if (baseStat <= 0 || double.IsNaN(compared) || double.IsNaN(target[stat]))
            {
                result[stat] = 1;
                continue;
            }

            var logResult = target[stat] - compared;
            if (isStrong && stat == 0)
            {
                // A boss has at least this factor of the health of the normal monsters.
                logResult = Math.Max(logResult, target[stat] + Math.Log(MinimumBossHealthFactor) - Math.Log(baseStat));
            }

            result[stat] = (float)Math.Exp(logResult);
        }

        return result;
    }

    /// <summary>
    /// Gets the logarithms of the reference stats at some resets.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <returns>The logarithms of the stats.</returns>
    internal double[] GetLogStats(double resets)
    {
        var references = this._references;
        if (references.Count == 1 || resets <= references[0].Resets)
        {
            return references[0].LogStats;
        }

        var upper = references.FindIndex(r => r.Resets >= resets);
        if (upper > 0)
        {
            return Interpolate(references[upper - 1], references[upper], resets, false);
        }

        var last = references[^1];
        var previous = references.LastOrDefault(r => r.Resets <= last.Resets - ExtrapolationResets);
        if (previous.LogStats is null || previous.Resets == last.Resets)
        {
            previous = references[^2];
        }

        return Interpolate(previous, last, resets, true);
    }

    private static double[] Interpolate((int Resets, double[] LogStats) lower, (int Resets, double[] LogStats) upper, double resets, bool onlyRising)
    {
        var fraction = upper.Resets == lower.Resets ? 0 : (resets - lower.Resets) / (upper.Resets - lower.Resets);
        var result = new double[StatCount];
        for (var stat = 0; stat < StatCount; stat++)
        {
            var difference = upper.LogStats[stat] - lower.LogStats[stat];
            if (onlyRising)
            {
                // Above the last map, a stat which falls from the previous map to the last one stays as in the last one.
                result[stat] = upper.LogStats[stat] + (Math.Max(difference, 0) * (fraction - 1));
            }
            else
            {
                result[stat] = lower.LogStats[stat] + (difference * fraction);
            }
        }

        return result;
    }

    private static bool IsElite(MonsterDefinition definition)
    {
        return definition.Designation.ValueInNeutralLanguage.EndsWith("Elite", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetMultiplier(MapDifficultyScalerConfiguration.DifficultyMultipliers? multipliers, int stat)
    {
        var value = stat switch
        {
            0 => multipliers?.Health,
            1 => multipliers?.Damage,
            2 => multipliers?.Defense,
            3 => multipliers?.AttackRate,
            _ => multipliers?.DefenseRate,
        };

        return value is > 0 ? value.Value : 1;
    }

    private static double GetBaseStat(MonsterDefinition definition, int stat)
    {
        var attributes = StatAttributes[stat];
        var sum = 0.0;
        foreach (var attribute in attributes)
        {
            sum += definition.Attributes.FirstOrDefault(a => a.AttributeDefinition == attribute)?.Value ?? 0;
        }

        return sum / attributes.Length;
    }

    private static double GetLogMean(IEnumerable<(double Value, double Weight)> values)
    {
        var sum = 0.0;
        var weights = 0.0;
        foreach (var (value, weight) in values)
        {
            if (value > 0)
            {
                sum += Math.Log(value) * weight;
                weights += weight;
            }
        }

        return weights > 0 ? sum / weights : double.NaN;
    }

    private static double GetWeightedMedian(IEnumerable<(double Value, double Weight)> values)
    {
        var sorted = values.Where(v => v.Value > 0).OrderBy(v => v.Value).ToList();
        var half = sorted.Sum(v => v.Weight) / 2;
        var cumulated = 0.0;
        foreach (var (value, weight) in sorted)
        {
            cumulated += weight;
            if (cumulated >= half)
            {
                return value;
            }
        }

        return 0;
    }

    /// <summary>
    /// The stats of the normal monsters of an event map.
    /// </summary>
    private sealed record EventMapStats(double MedianHealth, double[] LogStats, IReadOnlySet<MonsterDefinition> SingleMonsters)
    {
        public static EventMapStats Create(GameMapDefinition map)
        {
            var monsters = map.MonsterSpawns
                .Where(spawn => spawn.MonsterDefinition is { ObjectKind: NpcObjectKind.Monster })
                .GroupBy(spawn => spawn.MonsterDefinition!)
                .Select(group => (Definition: group.Key, Weight: (double)group.Sum(spawn => Math.Max((int)spawn.Quantity, 1))))
                .ToList();
            var medianHealth = GetWeightedMedian(monsters.Select(m => (GetBaseStat(m.Definition, 0), m.Weight)));
            var normal = monsters.Where(m => GetBaseStat(m.Definition, 0) <= medianHealth * StrongMonsterHealthFactor).ToList();
            var logStats = new double[StatCount];
            for (var stat = 0; stat < StatCount; stat++)
            {
                logStats[stat] = GetLogMean(normal.Select(m => (GetBaseStat(m.Definition, stat), m.Weight)));
            }

            // A monster with a single spawn of one monster on the map is a boss, e.g. Kundun.
            var singleMonsters = map.MonsterSpawns
                .Where(spawn => spawn.MonsterDefinition is { ObjectKind: NpcObjectKind.Monster })
                .GroupBy(spawn => spawn.MonsterDefinition!)
                .Where(group => group.Sum(spawn => Math.Max((int)spawn.Quantity, 1)) == 1)
                .Select(group => group.Key)
                .ToHashSet();
            return new EventMapStats(medianHealth, logStats, singleMonsters);
        }
    }
}
