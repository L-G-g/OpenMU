// <copyright file="MoreDungeons.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the dungeons of Tarkan and of Kanturu Ruins, and the extreme version of each dungeon, including the one of
/// Devias (<see cref="IceQueenDungeon"/>). The rules of the dungeons are in the plugin <see cref="DungeonFeaturePlugIn"/>,
/// and their stages in the <see cref="DungeonDefaults"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Each dungeon has two NPCs in the town of its map: a copy of the Crossbow Guard (247) for the normal version and
///         a copy of the Berdysh Guard (249) for the extreme version. They are passive NPCs, which are recognized by their designations.</item>
///   <item>The bosses and their escorts are copies of the monsters of the map, with the designation of the monster plus
///         <see cref="DungeonDefaults.BossSuffix"/>. The dungeon multiplies their health, damage and defense when they appear.</item>
///   <item>Spawn areas with the trigger <see cref="SpawnTrigger.AutomaticDuringEvent"/>, so only in the dungeons, along the ways
///         between the bosses.</item>
///   <item>A mini game definition of the type <see cref="MiniGameType.Dungeon"/> for each version of each dungeon, like the one of Devias.</item>
///   <item>An attribute for each dungeon which keeps the time of the last entry of a character, see <see cref="Stats.GetDungeonLastEntry"/>.</item>
/// </list>
/// </remarks>
internal static class MoreDungeons
{
    /// <summary>
    /// The number of the map Tarkan.
    /// </summary>
    internal const short TarkanNumber = 8;

    /// <summary>
    /// The number of the map Kanturu Ruins.
    /// </summary>
    internal const short KanturuRuinsNumber = 37;

    /// <summary>
    /// The number of the guard whose look the NPCs of the extreme versions have.
    /// </summary>
    internal const short ExtremeGuardNumber = 249;

    /// <summary>
    /// What is added to the number of a monster for the id number of its boss copy.
    /// </summary>
    internal const short BossIdNumberOffset = 5000;

    /// <summary>
    /// The first id number of the spawn areas on the ways of a dungeon.
    /// </summary>
    internal const short WaySpawnIdNumber = 5100;

    /// <summary>
    /// The first id number of the monster definitions of the NPCs; each dungeon has two.
    /// </summary>
    internal const short NpcIdNumber = 6000;

    /// <summary>
    /// The spots of the Bloody Wolves of Tarkan, on the two ways from the entrance to the Mutant Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] TarkanBloodyWolfWays =
    [
        (144, 48), (135, 45), (126, 42), (117, 42), (108, 45), (101, 52),
        (94, 58), (87, 69), (81, 76), (75, 83), (145, 69), (136, 70),
        (127, 72), (118, 75), (109, 79), (100, 82), (91, 83),
    ];

    /// <summary>
    /// The spots of the Iron Wheels of Tarkan, on the two ways from the Mutant Boss to the Beam Knight Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] TarkanIronWheelWays =
    [
        (67, 96), (68, 112), (68, 121), (65, 130), (63, 139), (61, 148),
        (62, 157), (64, 166), (67, 175), (70, 184), (76, 126), (80, 135),
        (84, 144), (89, 153), (92, 162), (94, 171), (96, 180), (87, 183),
    ];

    /// <summary>
    /// The spots of the Tantalos of Tarkan, on the way from the Beam Knight Boss to the Death Beam Knight Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] TarkanTantalosWay =
    [
        (54, 200), (50, 215), (43, 221), (36, 227), (30, 234), (22, 239),
        (17, 247),
    ];

    /// <summary>
    /// The spots of the Splinter Wolves and Iron Riders of Kanturu Ruins, on the ways from the entrance to the Satyros Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] KanturuWolfAndRiderWays =
    [
        (62, 234), (71, 236), (81, 240), (91, 240), (100, 240), (109, 239),
        (120, 239), (149, 241), (157, 236), (177, 228), (184, 222), (190, 214),
        (197, 201), (200, 192), (194, 185), (126, 227), (140, 225), (146, 214),
        (157, 204), (168, 202), (177, 204),
    ];

    /// <summary>
    /// The spots of the Blade Hunters and Berserker Warriors of Kanturu Ruins, on the way from the Satyros Boss to the Kentauros Warrior Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] KanturuHunterAndBerserkerWay =
    [
        (201, 167), (197, 158), (207, 140), (213, 127), (227, 122), (230, 107),
        (236, 99), (240, 89), (232, 83), (227, 74), (232, 66), (230, 57),
        (227, 48), (221, 41), (211, 37), (196, 38), (187, 37), (176, 38),
        (167, 39), (168, 48), (158, 56), (151, 50), (214, 166), (221, 160),
        (225, 151), (226, 140),
    ];

    /// <summary>
    /// The spots of the Gigantis Warriors of Kanturu Ruins, on the way from the Kentauros Warrior Boss to the Genocider Warrior Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] KanturuGigantisWay =
    [
        (157, 36), (151, 27), (143, 22), (134, 27), (124, 33), (120, 42),
        (121, 51), (118, 60), (111, 66), (102, 69), (93, 68), (84, 63),
        (81, 54), (88, 38), (86, 29), (77, 26), (68, 29), (72, 53),
        (63, 47),
    ];

    /// <summary>
    /// Gets the dungeons: the map, the NPCs, the bosses and the monsters on the ways.
    /// </summary>
    internal static IReadOnlyList<DungeonData> Dungeons { get; } =
    [
        new(
            DungeonDefaults.DeviasDungeonNumber,
            IceQueenDungeon.DeviasNumber,
            "Devias",
            null,
            (196, 40),
            [],
            []),
        new(
            DungeonDefaults.TarkanDungeonNumber,
            TarkanNumber,
            "Tarkan",
            (195, 60),
            (197, 60),
            [
                (DungeonDefaults.MutantNumber, DungeonDefaults.MutantBossDesignation),
                (DungeonDefaults.BeamKnightNumber, DungeonDefaults.BeamKnightBossDesignation),
                (DungeonDefaults.DeathBeamKnightNumber, DungeonDefaults.DeathBeamKnightBossDesignation),
                (DungeonDefaults.ZaikanNumber, DungeonDefaults.ZaikanBossDesignation),
            ],
            [
                new([DungeonDefaults.BloodyWolfNumber], 5, TarkanBloodyWolfWays),
                new([DungeonDefaults.IronWheelNumber], 5, TarkanIronWheelWays),
                new([DungeonDefaults.TantalosNumber], 7, TarkanTantalosWay),
            ]),
        new(
            DungeonDefaults.KanturuDungeonNumber,
            KanturuRuinsNumber,
            "Kanturu",
            (22, 212),
            (22, 214),
            [
                (DungeonDefaults.SatyrosNumber, DungeonDefaults.SatyrosBossDesignation),
                (DungeonDefaults.KentaurosWarriorNumber, DungeonDefaults.KentaurosWarriorBossDesignation),
                (DungeonDefaults.GenociderWarriorNumber, DungeonDefaults.GenociderWarriorBossDesignation),
            ],
            [
                new([DungeonDefaults.SplinterWolfNumber, DungeonDefaults.IronRiderNumber], 4, KanturuWolfAndRiderWays),
                new([DungeonDefaults.BladeHunterNumber, DungeonDefaults.BerserkerWarriorNumber], 4, KanturuHunterAndBerserkerWay),
                new([DungeonDefaults.GigantisWarriorNumber], 5, KanturuGigantisWay),
            ]),
    ];

    /// <summary>
    /// Applies the dungeons to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var settings = DungeonDefaults.CreateDungeons();
        foreach (var dungeon in Dungeons)
        {
            if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == dungeon.MapNumber && m.Discriminator == 0) is not { } map
                || settings.FirstOrDefault(s => s.Number == dungeon.Number) is not { } dungeonSettings)
            {
                continue;
            }

            var lastEntry = Stats.GetDungeonLastEntry(dungeon.Number);
            if (gameConfiguration.Attributes.All(a => a.Id != lastEntry.Id))
            {
                gameConfiguration.Attributes.Add(context.CreateNew<AttributeDefinition>(lastEntry.Id, lastEntry.Designation, lastEntry.Description));
            }

            if (dungeon.NpcPosition is { } npcPosition)
            {
                AddNpc(context, gameConfiguration, map, (short)(NpcIdNumber + (dungeon.Number * 2)), IceQueenDungeon.GuardNumber, dungeonSettings.NpcDesignation, npcPosition);
            }

            AddNpc(context, gameConfiguration, map, (short)(NpcIdNumber + (dungeon.Number * 2) + 1), ExtremeGuardNumber, dungeonSettings.ExtremeNpcDesignation, dungeon.ExtremeNpcPosition);

            foreach (var (number, designation) in dungeon.Bosses)
            {
                if (FindMonster(gameConfiguration, map, number) is { } original)
                {
                    GetOrCreateBoss(context, gameConfiguration, original, designation);
                }
            }

            AddWaySpawns(context, gameConfiguration, map, dungeon.Ways);
            AddMiniGameDefinition(context, gameConfiguration, map, dungeonSettings.GameLevel, $"Dungeon {dungeon.Name}", $"The dungeon of {dungeon.Name}: an instance of the map for a party, with stronger monsters and bosses.");
            AddMiniGameDefinition(context, gameConfiguration, map, dungeonSettings.ExtremeGameLevel, $"Dungeon {dungeon.Name} (Extreme)", $"The extreme version of the dungeon of {dungeon.Name}: even stronger monsters, which drop the high items more often.");
        }

        AddMissingStages(gameConfiguration);
    }

    /// <summary>
    /// Adds the default stages of the dungeons which have no stages to a saved configuration of the plugin.
    /// A configuration which was saved when there was only the dungeon of Devias has just its stages, which are kept.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    private static void AddMissingStages(GameConfiguration gameConfiguration)
    {
        var referenceHandler = new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler();
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(DungeonFeaturePlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration)
            || plugInConfiguration.GetConfiguration<DungeonConfiguration>(referenceHandler) is not { } configuration)
        {
            // Without a saved configuration, the plugin uses its defaults.
            return;
        }

        var missingStages = DungeonDefaults.CreateStages()
            .Where(stage => configuration.Dungeons.Any(dungeon => dungeon.Number == stage.Dungeon)
                            && configuration.Stages.All(existing => existing.Dungeon != stage.Dungeon))
            .ToList();
        if (missingStages.Count == 0)
        {
            return;
        }

        configuration.Stages = configuration.Stages.Concat(missingStages).ToList();
        plugInConfiguration.SetConfiguration(configuration, new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler());
    }

    /// <summary>
    /// Finds the monster with the number which lives on the map, or else the original monster of the number.
    /// </summary>
    private static MonsterDefinition? FindMonster(GameConfiguration gameConfiguration, GameMapDefinition map, short number)
    {
        return map.MonsterSpawns
                   .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.Automatic)
                   .Select(spawn => spawn.MonsterDefinition)
                   .FirstOrDefault(monster => monster is { ObjectKind: NpcObjectKind.Monster } && monster.Number == number)
               ?? MapLevelLadder.GetOriginalMonster(gameConfiguration, number);
    }

    private static void AddNpc(IContext context, GameConfiguration gameConfiguration, GameMapDefinition map, short idNumber, short lookNumber, string designation, (byte X, byte Y) position)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(idNumber);
        var npc = gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id);
        if (npc is null)
        {
            var look = gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(lookNumber));
            npc = context.CreateNew<MonsterDefinition>();
            npc.SetGuid(idNumber);
            npc.Number = lookNumber;
            npc.Designation = designation;
            npc.ObjectKind = NpcObjectKind.PassiveNpc;
            npc.NpcWindow = NpcWindow.Undefined;
            npc.MoveRange = 0;
            npc.ViewRange = look?.ViewRange ?? 7;
            npc.MoveDelay = look?.MoveDelay ?? TimeSpan.FromMilliseconds(400);
            npc.RespawnDelay = look?.RespawnDelay ?? TimeSpan.Zero;
            gameConfiguration.Monsters.Add(npc);
        }

        var spawnId = GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, idNumber);
        if (map.MonsterSpawns.Any(s => s.GetId() == spawnId))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(spawnId);
        spawn.GameMap = map;
        spawn.MonsterDefinition = npc;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthEast;
        spawn.X1 = position.X;
        spawn.X2 = position.X;
        spawn.Y1 = position.Y;
        spawn.Y2 = position.Y;
        spawn.Quantity = 1;
        map.MonsterSpawns.Add(spawn);
    }

    private static void GetOrCreateBoss(IContext context, GameConfiguration gameConfiguration, MonsterDefinition original, string designation)
    {
        var bossIdNumber = (short)(BossIdNumberOffset + original.Number);
        if (gameConfiguration.Monsters.Any(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(bossIdNumber)))
        {
            return;
        }

        var boss = context.CreateNew<MonsterDefinition>();
        boss.SetGuid(bossIdNumber);
        boss.Number = original.Number;
        boss.Designation = designation;
        boss.MoveRange = original.MoveRange;
        boss.AttackRange = original.AttackRange;
        boss.ViewRange = original.ViewRange;
        boss.MoveDelay = original.MoveDelay;
        boss.AttackDelay = original.AttackDelay;
        boss.RespawnDelay = original.RespawnDelay;
        boss.Attribute = original.Attribute;
        boss.NumberOfMaximumItemDrops = 1;
        boss.ObjectKind = original.ObjectKind;
        boss.IntelligenceTypeName = original.IntelligenceTypeName;
        boss.AttackSkill = original.AttackSkill;
        foreach (var originalAttribute in original.Attributes)
        {
            if (originalAttribute.AttributeDefinition is not { } definition)
            {
                continue;
            }

            var attribute = context.CreateNew<MonsterAttribute>();
            attribute.AttributeDefinition = definition;
            attribute.Value = originalAttribute.Value;
            attribute.SetGuid(bossIdNumber, definition.Id.ExtractFirstTwoBytes());
            boss.Attributes.Add(attribute);
        }

        gameConfiguration.Monsters.Add(boss);
    }

    private static void AddWaySpawns(IContext context, GameConfiguration gameConfiguration, GameMapDefinition map, IReadOnlyList<DungeonWay> ways)
    {
        var id = WaySpawnIdNumber;
        foreach (var way in ways)
        {
            var monsters = way.MonsterNumbers.Select(number => FindMonster(gameConfiguration, map, number)).ToList();
            for (var i = 0; i < way.Spots.Length; i++)
            {
                var (x, y) = way.Spots[i];
                var spawnId = GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, id++);

                // The kinds of monsters of a way take turns from spot to spot.
                if (monsters[i % monsters.Count] is not { } monster || map.MonsterSpawns.Any(s => s.GetId() == spawnId))
                {
                    continue;
                }

                // A spot of 4 x 4 coordinates, which are all walkable.
                var spawn = context.CreateNew<MonsterSpawnArea>();
                spawn.SetGuid(spawnId);
                spawn.GameMap = map;
                spawn.MonsterDefinition = monster;
                spawn.SpawnTrigger = SpawnTrigger.AutomaticDuringEvent;
                spawn.Direction = Direction.Undefined;
                spawn.X1 = (byte)(x - 1);
                spawn.X2 = (byte)(x + 2);
                spawn.Y1 = (byte)(y - 1);
                spawn.Y2 = (byte)(y + 2);
                spawn.Quantity = way.MonstersPerSpot;
                map.MonsterSpawns.Add(spawn);
            }
        }
    }

    private static void AddMiniGameDefinition(IContext context, GameConfiguration gameConfiguration, GameMapDefinition map, byte gameLevel, string name, string description)
    {
        if (gameConfiguration.MiniGameDefinitions.Any(d => d.Type == MiniGameType.Dungeon && d.GameLevel == gameLevel)
            || map.ExitGates.FirstOrDefault(g => g.IsSpawnGate) is not { } entrance)
        {
            return;
        }

        var dungeon = context.CreateNew<MiniGameDefinition>();
        dungeon.SetGuid((short)MiniGameType.Dungeon, gameLevel);
        gameConfiguration.MiniGameDefinitions.Add(dungeon);
        dungeon.Name = name;
        dungeon.Description = description;
        dungeon.EnterDuration = TimeSpan.FromSeconds(10);
        dungeon.GameDuration = TimeSpan.FromMinutes(15);
        dungeon.ExitDuration = TimeSpan.FromMinutes(1);
        dungeon.MaximumPlayerCount = 5;
        dungeon.MinimumCharacterLevel = 1;
        dungeon.MaximumCharacterLevel = 400;
        dungeon.MinimumSpecialCharacterLevel = 1;
        dungeon.MaximumSpecialCharacterLevel = 400;
        dungeon.Entrance = entrance;
        dungeon.Type = MiniGameType.Dungeon;
        dungeon.TicketItem = null;
        dungeon.GameLevel = gameLevel;
        dungeon.MapCreationPolicy = MiniGameMapCreationPolicy.OnePerParty;
        dungeon.SaveRankingStatistics = false;
        dungeon.AllowParty = true;
    }

    /// <summary>
    /// The data of a dungeon.
    /// </summary>
    /// <param name="Number">The number of the dungeon, see <see cref="DungeonSettings.Number"/>.</param>
    /// <param name="MapNumber">The number of the map of the dungeon.</param>
    /// <param name="Name">The name of the dungeon in the mini game definitions.</param>
    /// <param name="NpcPosition">The coordinates of the NPC of the normal version; <c>null</c> if it exists already.</param>
    /// <param name="ExtremeNpcPosition">The coordinates of the NPC of the extreme version.</param>
    /// <param name="Bosses">The numbers of the monsters which get a boss copy, with the designations of the copies.</param>
    /// <param name="Ways">The ways of the dungeon.</param>
    internal record DungeonData(
        byte Number,
        short MapNumber,
        string Name,
        (byte X, byte Y)? NpcPosition,
        (byte X, byte Y) ExtremeNpcPosition,
        (short Number, string Designation)[] Bosses,
        DungeonWay[] Ways);

    /// <summary>
    /// A way of a dungeon, with spots of monsters.
    /// </summary>
    /// <param name="MonsterNumbers">The numbers of the monsters, which take turns from spot to spot.</param>
    /// <param name="MonstersPerSpot">How many monsters each spot has.</param>
    /// <param name="Spots">The coordinates of the spots.</param>
    internal record DungeonWay(short[] MonsterNumbers, short MonstersPerSpot, (byte X, byte Y)[] Spots);
}
