// <copyright file="IceQueenDungeon.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// Adds the first dungeon: an instance of Devias for a party, entered at a guard in the town of Devias,
/// with the Ice Queen as boss. The rules of the dungeon are in the plugin <see cref="DungeonFeaturePlugIn"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>The NPC is a copy of the Crossbow Guard (247), so it looks like a guard in the game client, but it's a
///         passive NPC, which is recognized by its designation <see cref="DungeonDefaults.NpcDesignation"/>.</item>
///   <item>The bosses of the stages are copies of the Hommerd (23), the Elite Yeti (20) and the Ice Queen (25), with the
///         designations of <see cref="DungeonDefaults"/>. The dungeon multiplies their health, damage and defense when they appear.</item>
///   <item>Spawn areas of Devias with the trigger <see cref="SpawnTrigger.AutomaticDuringEvent"/>, so only in the dungeon:
///         Worms on the way from the entrance to the fort of the Hommerd Boss, Ice Monsters on the two ways from there to the
///         Elite Yeti Boss and Assassins on the way to the Ice Queen Boss.</item>
///   <item>The mini game definition has the type <see cref="MiniGameType.Dungeon"/>: 15 minutes, one instance per party,
///         up to 5 players. The entrance is the spawn gate of Devias, but the dungeon places the players outside of the town.</item>
///   <item>The attribute <see cref="Stats.DungeonLastEntry"/> keeps the time of the last entry of a character.</item>
/// </list>
/// </remarks>
internal static class IceQueenDungeon
{
    /// <summary>
    /// The number of the map Devias.
    /// </summary>
    internal const short DeviasNumber = 2;

    /// <summary>
    /// The number of the guard whose look the NPC has.
    /// </summary>
    internal const short GuardNumber = 247;

    /// <summary>
    /// The number of the Ice Queen.
    /// </summary>
    internal const short IceQueenNumber = 25;

    /// <summary>
    /// The id number of the monster definition of the NPC.
    /// </summary>
    internal const short NpcIdNumber = 3000 + GuardNumber;

    /// <summary>
    /// The number of the Hommerd.
    /// </summary>
    internal const short HommerdNumber = 23;

    /// <summary>
    /// The number of the Elite Yeti.
    /// </summary>
    internal const short EliteYetiNumber = 20;

    /// <summary>
    /// The first id number of the spawn areas on the ways of the dungeon.
    /// </summary>
    internal const short WaySpawnIdNumber = 3100;

    /// <summary>
    /// How many Worms each spot of the ways has.
    /// </summary>
    internal const short WormsPerSpot = 5;

    /// <summary>
    /// How many Ice Monsters each spot of the ways has.
    /// </summary>
    internal const short IceMonstersPerSpot = 3;

    /// <summary>
    /// How many Assassins each spot of the ways has.
    /// </summary>
    internal const short AssassinsPerSpot = 6;

    /// <summary>
    /// The coordinates of the NPC in the town of Devias, west of the spawn gate.
    /// </summary>
    internal static readonly (byte X, byte Y) NpcPosition = (194, 40);

    /// <summary>
    /// The spots of the Worms, on the way from the entrance to the fort of the Hommerd Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] WormWay =
    [
        (165, 30), (156, 29), (149, 26), (136, 26), (131, 29), (122, 29),
        (114, 29), (107, 29), (97, 23), (89, 23), (80, 28), (71, 28),
        (63, 28), (55, 28),
    ];

    /// <summary>
    /// The spots of the Ice Monsters, on the two ways from the fort to the Elite Yeti Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] IceMonsterWays =
    [
        (50, 63), (41, 63), (33, 63), (24, 63), (16, 63), (15, 71),
        (15, 80), (15, 88), (14, 97), (14, 105), (14, 114), (14, 122),
        (14, 130), (13, 139), (13, 147), (26, 151), (32, 155), (38, 156),
        (46, 156), (58, 161), (63, 167), (69, 170), (77, 174), (84, 177),
        (92, 181), (98, 185), (106, 189), (113, 194), (119, 198), (126, 203),
        (133, 208), (139, 212), (146, 217), (153, 222), (159, 226), (165, 231),
        (193, 230), (198, 232), (204, 230), (212, 230), (58, 65), (66, 68),
        (74, 70), (83, 74), (91, 74), (102, 75), (108, 80), (115, 84),
        (125, 87), (131, 91), (140, 95), (147, 98), (155, 101), (162, 105),
        (170, 108), (178, 112), (186, 115), (195, 118), (203, 121), (213, 122),
        (210, 145), (215, 149), (216, 157), (217, 165), (218, 173), (219, 181),
        (220, 190), (221, 201),
    ];

    /// <summary>
    /// The spots of the Assassins, on the way to the Ice Queen Boss.
    /// </summary>
    private static readonly (byte X, byte Y)[] AssassinWay =
    [
        (81, 175), (74, 179), (64, 186), (60, 195), (55, 202), (51, 209),
        (47, 217), (39, 220),
    ];

    /// <summary>
    /// Applies the dungeon to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == DeviasNumber) is not { } devias)
        {
            return;
        }

        if (gameConfiguration.Attributes.All(a => a.Id != Stats.DungeonLastEntry.Id))
        {
            gameConfiguration.Attributes.Add(context.CreateNew<AttributeDefinition>(Stats.DungeonLastEntry.Id, Stats.DungeonLastEntry.Designation, Stats.DungeonLastEntry.Description));
        }

        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(GuardNumber)) is { } guard)
        {
            var npc = GetOrCreateNpc(context, gameConfiguration, guard);
            AddNpcSpawn(context, devias, npc);
        }

        foreach (var (number, designation) in new[]
                 {
                     (HommerdNumber, DungeonDefaults.FirstSemiBossDesignation),
                     (EliteYetiNumber, DungeonDefaults.SecondSemiBossDesignation),
                     (IceQueenNumber, DungeonDefaults.BossDesignation),
                 })
        {
            if (FindMonster(gameConfiguration, number) is { } original)
            {
                GetOrCreateBoss(context, gameConfiguration, original, designation);
            }
        }

        AddWaySpawns(context, gameConfiguration, devias);

        AddMiniGameDefinition(context, gameConfiguration, devias);
    }

    private static MonsterDefinition GetOrCreateNpc(IContext context, GameConfiguration gameConfiguration, MonsterDefinition guard)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(NpcIdNumber);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id) is { } existing)
        {
            return existing;
        }

        var npc = context.CreateNew<MonsterDefinition>();
        npc.SetGuid(NpcIdNumber);
        npc.Number = guard.Number;
        npc.Designation = DungeonDefaults.NpcDesignation;
        npc.ObjectKind = NpcObjectKind.PassiveNpc;
        npc.NpcWindow = NpcWindow.Undefined;
        npc.MoveRange = 0;
        npc.ViewRange = guard.ViewRange;
        npc.MoveDelay = guard.MoveDelay;
        npc.RespawnDelay = guard.RespawnDelay;
        gameConfiguration.Monsters.Add(npc);
        return npc;
    }

    private static void AddNpcSpawn(IContext context, GameMapDefinition devias, MonsterDefinition npc)
    {
        var id = GuidHelper.CreateGuid<MonsterSpawnArea>(devias.Number, NpcIdNumber);
        if (devias.MonsterSpawns.Any(spawn => spawn.GetId() == id))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(id);
        spawn.GameMap = devias;
        spawn.MonsterDefinition = npc;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthEast;
        spawn.X1 = NpcPosition.X;
        spawn.X2 = NpcPosition.X;
        spawn.Y1 = NpcPosition.Y;
        spawn.Y2 = NpcPosition.Y;
        spawn.Quantity = 1;
        devias.MonsterSpawns.Add(spawn);
    }

    /// <summary>
    /// Finds a monster of the season by its number. Some monsters have ids which aren't made of the number.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The number of the monster.</param>
    /// <returns>The monster, or <c>null</c> if it isn't defined.</returns>
    private static MonsterDefinition? FindMonster(GameConfiguration gameConfiguration, short number)
    {
        return gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(number))
               ?? gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number && m.ObjectKind == NpcObjectKind.Monster && m.GetId() != GuidHelper.CreateGuid<MonsterDefinition>((short)(3000 + number)));
    }

    private static void AddWaySpawns(IContext context, GameConfiguration gameConfiguration, GameMapDefinition devias)
    {
        var id = WaySpawnIdNumber;
        foreach (var (number, quantity, spots) in new[]
                 {
                     (DungeonDefaults.WormNumber, WormsPerSpot, WormWay),
                     (DungeonDefaults.IceMonsterNumber, IceMonstersPerSpot, IceMonsterWays),
                     (DungeonDefaults.AssassinNumber, AssassinsPerSpot, AssassinWay),
                 })
        {
            var monster = FindMonster(gameConfiguration, number);
            foreach (var (x, y) in spots)
            {
                var spawnId = GuidHelper.CreateGuid<MonsterSpawnArea>(devias.Number, id++);
                if (monster is null || devias.MonsterSpawns.Any(spawn => spawn.GetId() == spawnId))
                {
                    continue;
                }

                // A spot of 4 x 4 coordinates, which are all walkable.
                var spawn = context.CreateNew<MonsterSpawnArea>();
                spawn.SetGuid(spawnId);
                spawn.GameMap = devias;
                spawn.MonsterDefinition = monster;
                spawn.SpawnTrigger = SpawnTrigger.AutomaticDuringEvent;
                spawn.Direction = Direction.Undefined;
                spawn.X1 = (byte)(x - 1);
                spawn.X2 = (byte)(x + 2);
                spawn.Y1 = (byte)(y - 1);
                spawn.Y2 = (byte)(y + 2);
                spawn.Quantity = quantity;
                devias.MonsterSpawns.Add(spawn);
            }
        }
    }

    private static void GetOrCreateBoss(IContext context, GameConfiguration gameConfiguration, MonsterDefinition original, string designation)
    {
        var bossIdNumber = (short)(3000 + original.Number);
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

    private static void AddMiniGameDefinition(IContext context, GameConfiguration gameConfiguration, GameMapDefinition devias)
    {
        if (gameConfiguration.MiniGameDefinitions.Any(d => d.Type == MiniGameType.Dungeon && d.GameLevel == DungeonDefaults.GameLevel)
            || devias.ExitGates.FirstOrDefault(g => g.IsSpawnGate) is not { } entrance)
        {
            return;
        }

        var dungeon = context.CreateNew<MiniGameDefinition>();
        dungeon.SetGuid((short)MiniGameType.Dungeon, DungeonDefaults.GameLevel);
        gameConfiguration.MiniGameDefinitions.Add(dungeon);
        dungeon.Name = "Dungeon Ice Queen";
        dungeon.Description = "The first dungeon: an instance of Devias for a party, with stronger monsters and the Ice Queen as boss.";
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
        dungeon.GameLevel = DungeonDefaults.GameLevel;
        dungeon.MapCreationPolicy = MiniGameMapCreationPolicy.OnePerParty;
        dungeon.SaveRankingStatistics = false;
        dungeon.AllowParty = true;
    }
}
