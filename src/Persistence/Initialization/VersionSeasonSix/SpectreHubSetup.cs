// <copyright file="SpectreHubSetup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// Adds the spectre hub, where the players unlock and evolve their spectres in duels against the rivals
/// (see <see cref="SpectreHub"/>). The rules are in the plugin of the spectres.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>The attributes of the victories against the rivals and of the pets of the spectres (<see cref="SpectreProgress"/>).</item>
///   <item>The mini game <see cref="MiniGameType.SpectreHub"/> with one instance per player: the hub (30 minutes) and the duel
///         (3 minutes), in two rooms of the Duel Arena (<see cref="SpectreHub.HubMapNumber"/>).</item>
///   <item>The guardian of the spectres in Lorencia, a copy of the Mirage (385) of the Illusion Temple, which is recognized
///         by its designation <see cref="SpectreHub.GuardianDesignation"/>.</item>
/// </list>
/// </remarks>
internal static class SpectreHubSetup
{
    /// <summary>
    /// The number of the NPC whose look the guardian has.
    /// </summary>
    internal const short GuardianLookNumber = 385;

    /// <summary>
    /// The id number of the monster definition of the guardian.
    /// </summary>
    internal const short GuardianIdNumber = 3000 + GuardianLookNumber;

    private const short LorenciaNumber = 0;

    /// <summary>
    /// The coordinates of the guardian in the town of Lorencia.
    /// </summary>
    private static readonly (byte X, byte Y) GuardianPosition = (138, 116);

    /// <summary>
    /// Applies the spectre hub to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var attribute in SpectreProgress.VictoryAttributes.Append(SpectreProgress.GrantedPetsAttribute))
        {
            if (gameConfiguration.Attributes.All(a => a.Id != attribute.Id))
            {
                gameConfiguration.Attributes.Add(context.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
            }
        }

        var lorencia = gameConfiguration.Maps.FirstOrDefault(m => m.Number == LorenciaNumber);
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == SpectreHub.HubMapNumber) is { } hubMap
            && gameConfiguration.Maps.FirstOrDefault(m => m.Number == SpectreHub.DuelMapNumber) is { } duelMap)
        {
            var hubEntrance = GetOrCreateGate(context, hubMap, (byte)SpectreHub.HubEntry.X, (byte)SpectreHub.HubEntry.Y);
            var duelEntrance = GetOrCreateGate(context, duelMap, (byte)SpectreHub.DuelOwnerStart.X, (byte)SpectreHub.DuelOwnerStart.Y);
            AddMiniGameDefinition(context, gameConfiguration, SpectreHub.HubGameLevel, "Refugio de los espectros", "The spectre hub of a player, where the rivals wait.", hubEntrance, TimeSpan.FromMinutes(30));
            AddMiniGameDefinition(context, gameConfiguration, SpectreHub.DuelGameLevel, "Duelo de espectros", "The duel of a player against a rival of its spectre hub.", duelEntrance, TimeSpan.FromMinutes(3));
        }

        if (lorencia is not null
            && gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(GuardianLookNumber)) is { } look)
        {
            var guardian = GetOrCreateGuardian(context, gameConfiguration, look);
            AddGuardianSpawn(context, lorencia, guardian);
        }
    }

    private static ExitGate GetOrCreateGate(IContext context, GameMapDefinition map, byte x, byte y)
    {
        if (map.ExitGates.FirstOrDefault(g => g.X1 == x && g.Y1 == y && g.X2 == x && g.Y2 == y) is { } existing)
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

    private static void AddMiniGameDefinition(IContext context, GameConfiguration gameConfiguration, byte level, string name, string description, ExitGate entrance, TimeSpan duration)
    {
        if (gameConfiguration.MiniGameDefinitions.Any(d => d.Type == MiniGameType.SpectreHub && d.GameLevel == level))
        {
            return;
        }

        var definition = context.CreateNew<MiniGameDefinition>();
        definition.SetGuid((short)MiniGameType.SpectreHub, level);
        gameConfiguration.MiniGameDefinitions.Add(definition);
        definition.Name = name;
        definition.Description = description;
        definition.EnterDuration = TimeSpan.Zero;
        definition.GameDuration = duration;
        definition.ExitDuration = TimeSpan.FromMinutes(1);
        definition.MaximumPlayerCount = 1;
        definition.MinimumCharacterLevel = 1;
        definition.MaximumCharacterLevel = 400;
        definition.MinimumSpecialCharacterLevel = 1;
        definition.MaximumSpecialCharacterLevel = 400;
        definition.Entrance = entrance;
        definition.Type = MiniGameType.SpectreHub;
        definition.TicketItem = null;
        definition.GameLevel = level;
        definition.MapCreationPolicy = MiniGameMapCreationPolicy.OnePerPlayer;
        definition.SaveRankingStatistics = false;
        definition.AllowParty = true;
        definition.ArePlayerKillersAllowedToEnter = true;
    }

    private static MonsterDefinition GetOrCreateGuardian(IContext context, GameConfiguration gameConfiguration, MonsterDefinition look)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(GuardianIdNumber);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id) is { } existing)
        {
            return existing;
        }

        var guardian = context.CreateNew<MonsterDefinition>();
        guardian.SetGuid(GuardianIdNumber);
        guardian.Number = look.Number;
        guardian.Designation = SpectreHub.GuardianDesignation;
        guardian.ObjectKind = NpcObjectKind.PassiveNpc;
        guardian.NpcWindow = NpcWindow.Undefined;
        guardian.MoveRange = 0;
        guardian.ViewRange = look.ViewRange;
        guardian.MoveDelay = look.MoveDelay;
        guardian.RespawnDelay = look.RespawnDelay;
        gameConfiguration.Monsters.Add(guardian);
        return guardian;
    }

    private static void AddGuardianSpawn(IContext context, GameMapDefinition lorencia, MonsterDefinition guardian)
    {
        var id = GuidHelper.CreateGuid<MonsterSpawnArea>(lorencia.Number, GuardianIdNumber);
        if (lorencia.MonsterSpawns.Any(spawn => spawn.GetId() == id))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(id);
        spawn.GameMap = lorencia;
        spawn.MonsterDefinition = guardian;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.South;
        spawn.X1 = GuardianPosition.X;
        spawn.X2 = GuardianPosition.X;
        spawn.Y1 = GuardianPosition.Y;
        spawn.Y2 = GuardianPosition.Y;
        spawn.Quantity = 1;
        lorencia.MonsterSpawns.Add(spawn);
    }
}
