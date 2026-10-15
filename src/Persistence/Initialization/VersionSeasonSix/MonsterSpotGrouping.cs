// <copyright file="MonsterSpotGrouping.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Rearranges the monsters of the hunting maps into spots: groups of <see cref="GroupSize"/> monsters,
/// each in an area of <see cref="SpotSize"/> x <see cref="SpotSize"/> coordinates, about
/// <see cref="SpotDistance"/> coordinates apart. Some of the spots are hotspots with
/// <see cref="HotspotSize"/> monsters.
/// </summary>
/// <remarks>
/// The spots keep the zoning of the original spawns: a map is divided into squares of
/// <see cref="SpotDistance"/> coordinates, and each square which had monsters before gets one spot.
/// Each kind of monster gets about as many spots as its share of the monsters of the map, preferably in
/// the squares where most of its monsters were, and at least one spot. The spot is placed on walkable
/// ground outside of the safezone, as close as possible to where the monsters of its kind were in the square.
/// Monsters with less than <see cref="MinimumMonstersOfDefinition"/> spawns on a map (e.g. bosses),
/// traps and spawns which don't spawn automatically keep their original spawns.
/// Maps which already have spots are left as they are.
/// </remarks>
internal static class MonsterSpotGrouping
{
    /// <summary>
    /// The number of monsters of a normal spot.
    /// </summary>
    internal const short GroupSize = 5;

    /// <summary>
    /// The number of monsters of a hotspot.
    /// </summary>
    internal const short HotspotSize = 7;

    /// <summary>
    /// The width and height of the area of a spot.
    /// </summary>
    internal const int SpotSize = 4;

    /// <summary>
    /// The size of the squares into which a map is divided, which is about the distance between two spots.
    /// </summary>
    internal const int SpotDistance = 20;

    /// <summary>
    /// The number of the first spot of a map, used to derive the id of its spawn area.
    /// It's above the numbers of the original spawn areas.
    /// </summary>
    internal const short FirstSpotNumber = 20000;

    /// <summary>
    /// Every n-th square of a diagonal pattern is a hotspot, so that the hotspots are spread over the map.
    /// </summary>
    private const int HotspotInterval = 5;

    /// <summary>
    /// The radius around a spawn point of a single monster in which it's counted for the squares.
    /// Single monsters stand around their spawn point, so their squares are smoothed by this radius.
    /// </summary>
    private const int PointSpawnRadius = 6;

    /// <summary>
    /// The minimum number of original monsters in a square to get a spot.
    /// </summary>
    private const double MinimumMonstersPerSpot = 1;

    /// <summary>
    /// The minimum number of monsters of a kind on a map to be grouped into spots.
    /// </summary>
    private const int MinimumMonstersOfDefinition = 3;

    private const int MapSize = 256;

    /// <summary>
    /// Gets the numbers of the hunting maps whose monsters are grouped into spots.
    /// Event maps (e.g. Blood Castle, Kalima, Crywolf) and quest maps keep their spawns.
    /// </summary>
    internal static IReadOnlyCollection<byte> HuntingMapNumbers { get; } =
    [
        0, // Lorencia
        1, // Dungeon
        2, // Devias
        3, // Noria
        4, // Lost Tower
        7, // Atlans
        8, // Tarkan
        10, // Icarus
        31, // Land of Trials
        33, // Aida
        37, // Kanturu Ruins
        38, // Kanturu Relics
        51, // Elvenland
        56, // Swamp of Calmness
        57, // Raklion
        63, // Vulcanus
        80, // Karutan 1
        81, // Karutan 2
    ];

    /// <summary>
    /// Groups the monsters of all hunting maps into spots.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var maps = gameConfiguration.Maps
            .Where(map => map.Discriminator == 0 && HuntingMapNumbers.Contains((byte)map.Number))
            .ToList();
        foreach (var map in maps)
        {
            GroupIntoSpots(context, map);
        }
    }

    private static void GroupIntoSpots(IContext context, GameMapDefinition map)
    {
        var firstSpotId = GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, FirstSpotNumber);
        if (map.MonsterSpawns.Any(spawn => spawn.GetId() == firstSpotId))
        {
            // The monsters of this map are already grouped.
            return;
        }

        var terrain = new GameMapTerrain(map);
        var spawns = map.MonsterSpawns
            .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.Automatic && spawn.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
            .GroupBy(spawn => spawn.MonsterDefinition!)
            .Where(group => group.Sum(spawn => spawn.Quantity) >= MinimumMonstersOfDefinition)
            .SelectMany(group => group)
            .ToList();
        if (spawns.Count == 0)
        {
            return;
        }

        var density = CalculateDensity(spawns, terrain);
        var squares = FindSquares(density, terrain);
        var monsters = AssignMonsters(squares, density.Keys);

        foreach (var spawn in spawns)
        {
            map.MonsterSpawns.Remove(spawn);
        }

        for (var i = 0; i < squares.Count; i++)
        {
            var square = squares[i];
            var monster = monsters[i];
            var (x, y) = square.GetSpotPosition(monster);
            var spot = context.CreateNew<MonsterSpawnArea>();
            spot.SetGuid(map.Number, (short)(FirstSpotNumber + i));
            spot.GameMap = map;
            spot.MonsterDefinition = monster;
            spot.SpawnTrigger = SpawnTrigger.Automatic;
            spot.Direction = Direction.Undefined;
            spot.X1 = x;
            spot.X2 = (byte)(x + SpotSize - 1);
            spot.Y1 = y;
            spot.Y2 = (byte)(y + SpotSize - 1);
            spot.Quantity = square.IsHotspot ? HotspotSize : GroupSize;
            map.MonsterSpawns.Add(spot);
        }
    }

    private static bool IsSpawnable(GameMapTerrain terrain, int x, int y)
    {
        return x is >= 0 and < MapSize
               && y is >= 0 and < MapSize
               && terrain.WalkMap[x, y]
               && !terrain.SafezoneMap[x, y];
    }

    /// <summary>
    /// Calculates how many monsters of each kind the original spawns had on each coordinate.
    /// </summary>
    private static Dictionary<MonsterDefinition, double[,]> CalculateDensity(IEnumerable<MonsterSpawnArea> spawns, GameMapTerrain terrain)
    {
        var density = new Dictionary<MonsterDefinition, double[,]>();
        foreach (var spawn in spawns)
        {
            var coordinates = GetCoordinates(spawn, terrain);
            if (coordinates.Count == 0)
            {
                continue;
            }

            if (!density.TryGetValue(spawn.MonsterDefinition!, out var grid))
            {
                grid = new double[MapSize, MapSize];
                density.Add(spawn.MonsterDefinition!, grid);
            }

            var monstersPerCoordinate = (double)spawn.Quantity / coordinates.Count;
            foreach (var (x, y) in coordinates)
            {
                grid[x, y] += monstersPerCoordinate;
            }
        }

        return density;
    }

    private static List<(int X, int Y)> GetCoordinates(MonsterSpawnArea spawn, GameMapTerrain terrain)
    {
        var coordinates = new List<(int X, int Y)>();
        if (spawn.IsPoint())
        {
            for (var x = spawn.X1 - PointSpawnRadius; x <= spawn.X1 + PointSpawnRadius; x++)
            {
                for (var y = spawn.Y1 - PointSpawnRadius; y <= spawn.Y1 + PointSpawnRadius; y++)
                {
                    var dx = x - spawn.X1;
                    var dy = y - spawn.Y1;
                    if ((dx * dx) + (dy * dy) <= PointSpawnRadius * PointSpawnRadius && IsSpawnable(terrain, x, y))
                    {
                        coordinates.Add((x, y));
                    }
                }
            }

            return coordinates;
        }

        for (var x = Math.Min(spawn.X1, spawn.X2); x <= Math.Max(spawn.X1, spawn.X2); x++)
        {
            for (var y = Math.Min(spawn.Y1, spawn.Y2); y <= Math.Max(spawn.Y1, spawn.Y2); y++)
            {
                if (IsSpawnable(terrain, x, y))
                {
                    coordinates.Add((x, y));
                }
            }
        }

        return coordinates;
    }

    /// <summary>
    /// Finds the squares which had monsters before and have room for a spot.
    /// A square needs at least <see cref="MinimumMonstersPerSpot"/> monsters, except the square with the most
    /// monsters of a kind which has no other square, so that monsters which were spread thinly over a big area
    /// don't disappear.
    /// </summary>
    private static List<Square> FindSquares(Dictionary<MonsterDefinition, double[,]> density, GameMapTerrain terrain)
    {
        var candidates = new List<Square>();
        for (var left = 0; left < MapSize; left += SpotDistance)
        {
            for (var top = 0; top < MapSize; top += SpotDistance)
            {
                var square = new Square(left, top);
                foreach (var (monster, grid) in density)
                {
                    square.AddMonsters(monster, grid);
                }

                if (square.TotalMonsters <= 0)
                {
                    continue;
                }

                square.FindSpotPositions(terrain);
                if (square.SpotPositions.Count > 0)
                {
                    candidates.Add(square);
                }
            }
        }

        var squares = candidates.Where(square => square.TotalMonsters >= MinimumMonstersPerSpot).ToList();
        foreach (var monster in density.Keys.OrderBy(m => m.Number))
        {
            if (squares.Any(square => square.GetMonsters(monster) > 0))
            {
                continue;
            }

            var best = candidates
                .Where(square => square.GetMonsters(monster) > 0)
                .OrderByDescending(square => square.GetMonsters(monster))
                .FirstOrDefault();
            if (best is not null)
            {
                squares.Add(best);
            }
        }

        return squares;
    }

    /// <summary>
    /// Decides which kind of monster gets the spot of each square.
    /// Each kind gets about as many spots as its share of all monsters, at least one, in the squares
    /// where it has the biggest share. The remaining squares get the kind with most monsters in it.
    /// </summary>
    private static List<MonsterDefinition> AssignMonsters(List<Square> squares, IEnumerable<MonsterDefinition> monsters)
    {
        var totalMonsters = squares.Sum(square => square.TotalMonsters);
        var remainingSpots = monsters.ToDictionary(
            monster => monster,
            monster => Math.Max(1, (int)Math.Round(squares.Sum(square => square.GetMonsters(monster)) / totalMonsters * squares.Count)));

        var candidates = squares
            .SelectMany((square, index) => square.Monsters.Select(m => (Index: index, Monster: m.Key, Share: m.Value.Count / square.TotalMonsters)))
            .OrderByDescending(candidate => candidate.Share)
            .ThenBy(candidate => candidate.Index)
            .ThenBy(candidate => candidate.Monster.Number);

        var assigned = new MonsterDefinition?[squares.Count];
        foreach (var (index, monster, _) in candidates)
        {
            if (assigned[index] is null && remainingSpots[monster] > 0)
            {
                assigned[index] = monster;
                remainingSpots[monster]--;
            }
        }

        return squares.Select((square, index) => assigned[index] ?? square.MostCommonMonster).ToList();
    }

    /// <summary>
    /// A square of a map which may get one spot.
    /// </summary>
    private sealed class Square
    {
        private readonly int _left;
        private readonly int _top;

        public Square(int left, int top)
        {
            this._left = left;
            this._top = top;
        }

        /// <summary>
        /// Gets the number of original monsters of each kind in this square and the sum of their coordinates.
        /// </summary>
        public Dictionary<MonsterDefinition, (double Count, double SumX, double SumY)> Monsters { get; } = new();

        public double TotalMonsters { get; private set; }

        public List<(byte X, byte Y)> SpotPositions { get; } = new();

        public bool IsHotspot => ((this._left / SpotDistance) + (2 * (this._top / SpotDistance))) % HotspotInterval == 0;

        public MonsterDefinition MostCommonMonster => this.Monsters
            .OrderByDescending(m => m.Value.Count)
            .ThenBy(m => m.Key.Number)
            .First().Key;

        public double GetMonsters(MonsterDefinition monster) => this.Monsters.TryGetValue(monster, out var value) ? value.Count : 0;

        public void AddMonsters(MonsterDefinition monster, double[,] grid)
        {
            double count = 0;
            double sumX = 0;
            double sumY = 0;
            for (var x = this._left; x < Math.Min(this._left + SpotDistance, MapSize); x++)
            {
                for (var y = this._top; y < Math.Min(this._top + SpotDistance, MapSize); y++)
                {
                    var value = grid[x, y];
                    count += value;
                    sumX += value * x;
                    sumY += value * y;
                }
            }

            if (count > 0)
            {
                this.Monsters.Add(monster, (count, sumX, sumY));
                this.TotalMonsters += count;
            }
        }

        /// <summary>
        /// Finds the positions of the upper left corner of a spot which lies completely on spawnable ground in this square.
        /// </summary>
        public void FindSpotPositions(GameMapTerrain terrain)
        {
            var right = Math.Min(this._left + SpotDistance, MapSize) - SpotSize;
            var bottom = Math.Min(this._top + SpotDistance, MapSize) - SpotSize;
            for (var x = this._left; x <= right; x++)
            {
                for (var y = this._top; y <= bottom; y++)
                {
                    if (IsFreeArea(terrain, x, y))
                    {
                        this.SpotPositions.Add(((byte)x, (byte)y));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the position of the spot which is nearest to where the monsters of the specified kind were.
        /// </summary>
        public (byte X, byte Y) GetSpotPosition(MonsterDefinition monster)
        {
            var (count, sumX, sumY) = this.Monsters.TryGetValue(monster, out var value)
                ? value
                : (this.TotalMonsters, this.Monsters.Values.Sum(m => m.SumX), this.Monsters.Values.Sum(m => m.SumY));
            var centerX = sumX / count;
            var centerY = sumY / count;
            const double halfSpot = (SpotSize - 1) / 2.0;
            return this.SpotPositions
                .OrderBy(p => Math.Pow(p.X + halfSpot - centerX, 2) + Math.Pow(p.Y + halfSpot - centerY, 2))
                .ThenBy(p => p.X)
                .ThenBy(p => p.Y)
                .First();
        }

        private static bool IsFreeArea(GameMapTerrain terrain, int left, int top)
        {
            for (var x = left; x < left + SpotSize; x++)
            {
                for (var y = top; y < top + SpotSize; y++)
                {
                    if (!IsSpawnable(terrain, x, y))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
