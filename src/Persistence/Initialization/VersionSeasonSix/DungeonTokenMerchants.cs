// <copyright file="DungeonTokenMerchants.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.TokenMerchants;

/// <summary>
/// Adds the data of the tokens of the dungeons and of their merchants, see <see cref="TokenMerchantFeaturePlugIn"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>An attribute for each dungeon, which counts the tokens of a character (<see cref="Stats.GetDungeonTokens"/>).</item>
///   <item>The merchants of Tarkan and of Kanturu Ruins, next to the guards of their dungeons. The client only knows the NPCs
///         of the game, so they are second definitions with the numbers (and so the looks) of the Wandering Merchant Harold
///         and of the Weapons Merchant Bolo, and are recognized by their designations. The merchant of Devias is Zienna, who exists already.</item>
/// </list>
/// The merchants have no merchant store in the data: the plugin creates their items from the drops of the dungeons.
/// </remarks>
internal static class DungeonTokenMerchants
{
    /// <summary>
    /// The number of the Wandering Merchant Harold, whose look the merchant of Tarkan has.
    /// </summary>
    internal const short HaroldNumber = 250;

    /// <summary>
    /// The number of the Weapons Merchant Bolo, whose look the merchant of Kanturu has.
    /// </summary>
    internal const short BoloNumber = 578;

    /// <summary>
    /// The first id number of the monster definitions of the merchants.
    /// </summary>
    internal const short NpcIdNumber = 6100;

    /// <summary>
    /// Gets the new merchants: the id number of the definition, the number of the look, the designation, the map and the coordinates.
    /// </summary>
    internal static IReadOnlyList<(short IdNumber, short LookNumber, string Designation, short MapNumber, byte X, byte Y)> Merchants { get; } =
    [
        (NpcIdNumber, HaroldNumber, TokenMerchantConfiguration.TarkanNpcDesignation, MoreDungeons.TarkanNumber, 199, 60),
        (NpcIdNumber + 1, BoloNumber, TokenMerchantConfiguration.KanturuNpcDesignation, MoreDungeons.KanturuRuinsNumber, 22, 216),
    ];

    /// <summary>
    /// Applies the data to the game configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var merchant in TokenMerchantConfiguration.CreateDefaultMerchants())
        {
            var tokens = Stats.GetDungeonTokens(merchant.Dungeon);
            if (gameConfiguration.Attributes.All(a => a.Id != tokens.Id))
            {
                gameConfiguration.Attributes.Add(context.CreateNew<AttributeDefinition>(tokens.Id, tokens.Designation, tokens.Description));
            }
        }

        foreach (var (idNumber, lookNumber, designation, mapNumber, x, y) in Merchants)
        {
            if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == mapNumber && m.Discriminator == 0) is { } map)
            {
                AddMerchant(context, gameConfiguration, map, idNumber, lookNumber, designation, x, y);
            }
        }
    }

    private static void AddMerchant(IContext context, GameConfiguration gameConfiguration, GameMapDefinition map, short idNumber, short lookNumber, string designation, byte x, byte y)
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
            npc.NpcWindow = NpcWindow.Merchant;
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
        spawn.X1 = x;
        spawn.X2 = x;
        spawn.Y1 = y;
        spawn.Y2 = y;
        spawn.Quantity = 1;
        map.MonsterSpawns.Add(spawn);
    }
}
