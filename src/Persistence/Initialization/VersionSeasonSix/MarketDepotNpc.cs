// <copyright file="MarketDepotNpc.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Adds the "Mercader", which opens the market depot of the account (see <see cref="NpcWindow.MarketDepot"/>),
/// next to the vault keeper of Lorencia.
/// </summary>
/// <remarks>
/// This is a custom feature. The client only knows the NPCs of the game, so the Mercader is a second definition
/// with the number of Market Union Member Julia: the client shows it as Julia. The original Julia keeps warping to the Loren Market.
/// </remarks>
internal static class MarketDepotNpc
{
    /// <summary>
    /// The number of the NPC whose model the client shows: Market Union Member Julia.
    /// </summary>
    internal const short NpcNumber = 547;

    /// <summary>
    /// The number from which the id of the Mercader definition is derived. It's above the offsets of the copies of the <see cref="MapLevelLadder"/>.
    /// </summary>
    internal const short DefinitionIdNumber = 4000 + NpcNumber;

    /// <summary>
    /// The number from which the id of the spawn in Lorencia is derived.
    /// </summary>
    internal const short SpawnIdNumber = 3100;

    /// <summary>
    /// The position in Lorencia, next to the vault keeper at 147/145.
    /// </summary>
    internal static readonly (byte X, byte Y) LorenciaPosition = (149, 143);

    /// <summary>
    /// Applies the change to the configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var definitionId = GuidHelper.CreateGuid<MonsterDefinition>(DefinitionIdNumber);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == definitionId) is not { } npc)
        {
            npc = context.CreateNew<MonsterDefinition>();
            npc.SetGuid(definitionId);
            npc.Number = NpcNumber;
            npc.Designation = "Mercader";
            npc.NpcWindow = NpcWindow.MarketDepot;
            npc.ObjectKind = NpcObjectKind.PassiveNpc;
            gameConfiguration.Monsters.Add(npc);
        }

        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == 0) is not { } lorencia)
        {
            return;
        }

        var id = GuidHelper.CreateGuid<MonsterSpawnArea>(lorencia.Number, SpawnIdNumber);
        if (lorencia.MonsterSpawns.Any(spawn => spawn.GetId() == id))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(id);
        spawn.GameMap = lorencia;
        spawn.MonsterDefinition = npc;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthWest;
        spawn.X1 = LorenciaPosition.X;
        spawn.X2 = LorenciaPosition.X;
        spawn.Y1 = LorenciaPosition.Y;
        spawn.Y2 = LorenciaPosition.Y;
        spawn.Quantity = 1;
        lorencia.MonsterSpawns.Add(spawn);
    }
}
