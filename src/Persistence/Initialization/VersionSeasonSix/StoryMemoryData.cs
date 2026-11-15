// <copyright file="StoryMemoryData.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Story;

/// <summary>
/// The data of the memories of the story mode (see <see cref="StoryMemory"/>): the seer, an NPC at the entrance of Atlans
/// who shows them, and the mini game <see cref="MiniGameType.StoryScene"/> with one instance of Devias per player, which
/// is the stage of the first memory.
/// </summary>
internal static class StoryMemoryData
{
    /// <summary>
    /// The number of the NPC whose look the seer has: Oracle Layla.
    /// </summary>
    internal const short SeerLookNumber = 259;

    /// <summary>
    /// The id number of the monster definition of the seer.
    /// </summary>
    internal const short SeerIdNumber = 3000 + SeerLookNumber;

    private const short AtlansNumber = 7;

    private const byte GameLevel = 1;

    /// <summary>
    /// The coordinates of the seer, in the safezone at the entrance of Atlans.
    /// </summary>
    private static readonly (byte X, byte Y) SeerPosition = (24, 20);

    /// <summary>
    /// Applies the data to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == AtlansNumber && m.Discriminator == 0) is { } atlans)
        {
            var seer = GetOrCreateSeer(context, gameConfiguration);
            AddSeerSpawn(context, atlans, seer);
        }

        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == StoryMemory.SacrificeMapNumber && m.Discriminator == 0) is { } devias)
        {
            var viewerSpot = StoryMemory.Sacrifice.ViewerSpot;
            AddMiniGameDefinition(context, gameConfiguration, GetOrCreateGate(context, devias, viewerSpot.X, viewerSpot.Y));
        }
    }

    private static MonsterDefinition GetOrCreateSeer(IContext context, GameConfiguration gameConfiguration)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(SeerIdNumber);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id) is { } existing)
        {
            return existing;
        }

        var look = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == SeerLookNumber);
        var seer = context.CreateNew<MonsterDefinition>();
        seer.SetGuid(SeerIdNumber);
        seer.Number = SeerLookNumber;
        seer.Designation = StoryMemory.SeerName;
        seer.ObjectKind = NpcObjectKind.PassiveNpc;
        seer.NpcWindow = NpcWindow.Undefined;
        seer.MoveRange = 0;
        seer.ViewRange = look?.ViewRange ?? 5;
        seer.MoveDelay = look?.MoveDelay ?? TimeSpan.FromMilliseconds(400);
        seer.RespawnDelay = look?.RespawnDelay ?? TimeSpan.FromSeconds(1);
        gameConfiguration.Monsters.Add(seer);
        return seer;
    }

    private static void AddSeerSpawn(IContext context, GameMapDefinition atlans, MonsterDefinition seer)
    {
        var id = GuidHelper.CreateGuid<MonsterSpawnArea>(atlans.Number, SeerIdNumber);
        if (atlans.MonsterSpawns.Any(spawn => spawn.GetId() == id))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(id);
        spawn.GameMap = atlans;
        spawn.MonsterDefinition = seer;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthWest;
        spawn.X1 = SeerPosition.X;
        spawn.X2 = SeerPosition.X;
        spawn.Y1 = SeerPosition.Y;
        spawn.Y2 = SeerPosition.Y;
        spawn.Quantity = 1;
        atlans.MonsterSpawns.Add(spawn);
    }

    private static ExitGate GetOrCreateGate(IContext context, GameMapDefinition map, byte x, byte y)
    {
        if (map.ExitGates.FirstOrDefault(g => g.X1 == x && g.Y1 == y && g.X2 == x && g.Y2 == y && !g.IsSpawnGate) is { } existing)
        {
            return existing;
        }

        var gate = context.CreateNew<ExitGate>();
        gate.Map = map;
        gate.X1 = x;
        gate.X2 = x;
        gate.Y1 = y;
        gate.Y2 = y;
        gate.Direction = Direction.South;
        gate.IsSpawnGate = false;
        map.ExitGates.Add(gate);
        return gate;
    }

    private static void AddMiniGameDefinition(IContext context, GameConfiguration gameConfiguration, ExitGate entrance)
    {
        if (gameConfiguration.MiniGameDefinitions.Any(d => d.Type == MiniGameType.StoryScene && d.GameLevel == GameLevel))
        {
            return;
        }

        var definition = context.CreateNew<MiniGameDefinition>();
        definition.SetGuid((short)MiniGameType.StoryScene, GameLevel);
        gameConfiguration.MiniGameDefinitions.Add(definition);
        definition.Name = "Recuerdo: el sacrificio";
        definition.Description = "The stage of a memory of the story mode: the throne room of the castle of Devias, for one player.";
        definition.EnterDuration = TimeSpan.Zero;
        definition.GameDuration = TimeSpan.FromMinutes(10);
        definition.ExitDuration = TimeSpan.FromMinutes(1);
        definition.MaximumPlayerCount = 1;
        definition.MinimumCharacterLevel = 1;
        definition.MaximumCharacterLevel = 400;
        definition.MinimumSpecialCharacterLevel = 1;
        definition.MaximumSpecialCharacterLevel = 400;
        definition.Entrance = entrance;
        definition.Type = MiniGameType.StoryScene;
        definition.TicketItem = null;
        definition.GameLevel = GameLevel;
        definition.MapCreationPolicy = MiniGameMapCreationPolicy.OnePerPlayer;
        definition.SaveRankingStatistics = false;
        definition.AllowParty = true;
        definition.ArePlayerKillersAllowedToEnter = true;
    }
}
