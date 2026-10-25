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
///   <item>The boss is a copy of the Ice Queen (25) with the designation <see cref="DungeonDefaults.BossDesignation"/>.
///         The dungeon multiplies its health, damage and defense when it appears.</item>
///   <item>The mini game definition has the type <see cref="MiniGameType.Dungeon"/>: 15 minutes, one instance per party,
///         up to 5 players, entered at the spawn gate (the town) of Devias.</item>
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
    /// The id number of the monster definition of the boss.
    /// </summary>
    internal const short BossIdNumber = 3000 + IceQueenNumber;

    /// <summary>
    /// The coordinates of the NPC in the town of Devias, west of the spawn gate.
    /// </summary>
    internal static readonly (byte X, byte Y) NpcPosition = (194, 40);

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

        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(IceQueenNumber)) is { } iceQueen)
        {
            GetOrCreateBoss(context, gameConfiguration, iceQueen);
        }

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

    private static void GetOrCreateBoss(IContext context, GameConfiguration gameConfiguration, MonsterDefinition iceQueen)
    {
        if (gameConfiguration.Monsters.Any(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(BossIdNumber)))
        {
            return;
        }

        var boss = context.CreateNew<MonsterDefinition>();
        boss.SetGuid(BossIdNumber);
        boss.Number = iceQueen.Number;
        boss.Designation = DungeonDefaults.BossDesignation;
        boss.MoveRange = iceQueen.MoveRange;
        boss.AttackRange = iceQueen.AttackRange;
        boss.ViewRange = iceQueen.ViewRange;
        boss.MoveDelay = iceQueen.MoveDelay;
        boss.AttackDelay = iceQueen.AttackDelay;
        boss.RespawnDelay = iceQueen.RespawnDelay;
        boss.Attribute = iceQueen.Attribute;
        boss.NumberOfMaximumItemDrops = 1;
        boss.ObjectKind = iceQueen.ObjectKind;
        boss.IntelligenceTypeName = iceQueen.IntelligenceTypeName;
        boss.AttackSkill = iceQueen.AttackSkill;
        foreach (var originalAttribute in iceQueen.Attributes)
        {
            if (originalAttribute.AttributeDefinition is not { } definition)
            {
                continue;
            }

            var attribute = context.CreateNew<MonsterAttribute>();
            attribute.AttributeDefinition = definition;
            attribute.Value = originalAttribute.Value;
            attribute.SetGuid(BossIdNumber, definition.Id.ExtractFirstTwoBytes());
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
