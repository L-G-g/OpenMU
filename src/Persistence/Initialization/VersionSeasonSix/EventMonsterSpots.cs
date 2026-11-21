// <copyright file="EventMonsterSpots.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network;

/// <summary>
/// Groups the monsters of Devil Square and Kalima into spots of <see cref="MonsterSpotGrouping.GroupSize"/> monsters,
/// and hotspots of <see cref="MonsterSpotGrouping.HotspotSize"/>, like the hunting maps.
/// </summary>
/// <remarks>
/// Kalima is grouped like a hunting map (<see cref="MonsterSpotGrouping"/>).
/// The arena of Devil Square is small, so its spots are <see cref="DevilSquareSpotDistance"/> coordinates apart.
/// Each wave of Devil Square gets spots all over the arena, alternating its kinds of monsters; the bosses keep their spawn.
/// </remarks>
internal static class EventMonsterSpots
{
    /// <summary>
    /// The distance between two spots in the arena of Devil Square.
    /// </summary>
    internal const int DevilSquareSpotDistance = 8;

    /// <summary>
    /// The number of the first spot of a level of Devil Square, used to derive the id of its spawn area.
    /// </summary>
    internal const short FirstDevilSquareSpotNumber = 21000;

    /// <summary>
    /// The numbers of the maps of Kalima 1 to 7.
    /// </summary>
    internal static IReadOnlyCollection<byte> KalimaMapNumbers { get; } = [24, 25, 26, 27, 28, 29, 36];

    /// <summary>
    /// The numbers of the maps of Devil Square: 1 to 4, and 5 to 7.
    /// </summary>
    private static IReadOnlyCollection<short> DevilSquareMapNumbers { get; } = [9, 32];

    /// <summary>
    /// Groups the monsters of Devil Square and Kalima into spots.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        MonsterSpotGrouping.Apply(context, gameConfiguration, KalimaMapNumbers);
        foreach (var map in gameConfiguration.Maps.Where(m => DevilSquareMapNumbers.Contains(m.Number)).ToList())
        {
            GroupDevilSquare(context, map);
        }
    }

    private static void GroupDevilSquare(IContext context, GameMapDefinition map)
    {
        var parentNumber = (short)NumberConversionExtensions.MakeWord((byte)map.Number, (byte)map.Discriminator);
        var firstId = GuidHelper.CreateGuid<MonsterSpawnArea>(parentNumber, FirstDevilSquareSpotNumber);
        if (map.MonsterSpawns.Any(spawn => spawn.GetId() == firstId))
        {
            // This level is already grouped.
            return;
        }

        var waves = map.MonsterSpawns
            .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.AutomaticDuringWave && spawn.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
            .GroupBy(spawn => spawn.WaveNumber)
            .OrderBy(wave => wave.Key)
            .ToList();
        if (waves.Count == 0)
        {
            return;
        }

        var terrain = new GameMapTerrain(map);
        var number = FirstDevilSquareSpotNumber;
        foreach (var wave in waves)
        {
            var spawns = wave.ToList();
            var monsters = spawns.Select(spawn => spawn.MonsterDefinition!).Distinct().ToList();
            var left = spawns.Min(spawn => Math.Min(spawn.X1, spawn.X2));
            var right = spawns.Max(spawn => Math.Max(spawn.X1, spawn.X2));
            var top = spawns.Min(spawn => Math.Min(spawn.Y1, spawn.Y2));
            var bottom = spawns.Max(spawn => Math.Max(spawn.Y1, spawn.Y2));
            var positions = FindSpotPositions(terrain, left, right, top, bottom);
            if (positions.Count == 0)
            {
                continue;
            }

            foreach (var spawn in spawns)
            {
                map.MonsterSpawns.Remove(spawn);
            }

            foreach (var (x, y, column, row) in positions)
            {
                var spot = context.CreateNew<MonsterSpawnArea>();
                spot.SetGuid(parentNumber, number++);
                spot.GameMap = map;
                spot.MonsterDefinition = monsters[(column + row) % monsters.Count];
                spot.SpawnTrigger = SpawnTrigger.AutomaticDuringWave;
                spot.WaveNumber = wave.Key;
                spot.Direction = Direction.Undefined;
                spot.X1 = x;
                spot.X2 = (byte)(x + MonsterSpotGrouping.SpotSize - 1);
                spot.Y1 = y;
                spot.Y2 = (byte)(y + MonsterSpotGrouping.SpotSize - 1);
                spot.Quantity = (column + (2 * row)) % 5 == 0 ? MonsterSpotGrouping.HotspotSize : MonsterSpotGrouping.GroupSize;
                map.MonsterSpawns.Add(spot);
            }
        }
    }

    /// <summary>
    /// Finds the positions of the spots in the arena: on a grid of <see cref="DevilSquareSpotDistance"/>, where at least
    /// three quarters of the area of the spot are walkable and outside of the safezone.
    /// </summary>
    private static List<(byte X, byte Y, int Column, int Row)> FindSpotPositions(GameMapTerrain terrain, int left, int right, int top, int bottom)
    {
        var positions = new List<(byte X, byte Y, int Column, int Row)>();
        var size = MonsterSpotGrouping.SpotSize;
        var row = 0;
        for (var y = top + 1; y + size - 1 <= bottom; y += DevilSquareSpotDistance, row++)
        {
            var column = 0;
            for (var x = left + 1; x + size - 1 <= right; x += DevilSquareSpotDistance, column++)
            {
                var walkable = 0;
                for (var dx = 0; dx < size; dx++)
                {
                    for (var dy = 0; dy < size; dy++)
                    {
                        if (terrain.WalkMap[x + dx, y + dy] && !terrain.SafezoneMap[x + dx, y + dy])
                        {
                            walkable++;
                        }
                    }
                }

                if (walkable * 4 >= size * size * 3)
                {
                    positions.Add(((byte)x, (byte)y, column, row));
                }
            }
        }

        return positions;
    }
}
