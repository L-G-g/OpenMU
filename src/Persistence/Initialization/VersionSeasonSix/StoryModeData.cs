// <copyright file="StoryModeData.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Story;

/// <summary>
/// The data of the story mode of PeroniaMU (see <see cref="StoryModeFeaturePlugIn"/>):
/// <list type="bullet">
///   <item>The attributes <see cref="Stats.StoryProgress"/> and <see cref="Stats.StoryFlags"/>, which keep the progress
///         of a character.</item>
///   <item>The stranger: an NPC in Lorencia, which the plugin recognizes by its designation
///         <see cref="StoryScript.StrangerName"/>. It looks like a knight (the model of the Doorkeeper Titus).</item>
/// </list>
/// Lumen the Barmaid and Sebina the Priestess already exist.
/// </summary>
internal static class StoryModeData
{
    /// <summary>
    /// The number of the NPC whose look the stranger takes: the Doorkeeper Titus, a knight.
    /// </summary>
    internal const short StrangerLookNumber = 479;

    /// <summary>
    /// The number from which the id of the stranger is made.
    /// </summary>
    internal const short StrangerIdNumber = 3000 + StrangerLookNumber;

    private const short LorenciaNumber = 0;

    /// <summary>
    /// The place of the stranger: the gate of the walled yard in the north of Lorencia, outside of the safezone.
    /// </summary>
    private static readonly (byte X, byte Y) StrangerPosition = (141, 208);

    /// <summary>
    /// Applies the data of the story mode to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var attribute in new[] { Stats.StoryProgress, Stats.StoryFlags })
        {
            if (gameConfiguration.Attributes.All(a => a.Id != attribute.Id))
            {
                gameConfiguration.Attributes.Add(context.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
            }
        }

        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == LorenciaNumber && m.Discriminator == 0) is not { } lorencia)
        {
            return;
        }

        var stranger = GetOrCreateStranger(context, gameConfiguration);
        AddStrangerSpawn(context, lorencia, stranger);
    }

    private static MonsterDefinition GetOrCreateStranger(IContext context, GameConfiguration gameConfiguration)
    {
        var id = GuidHelper.CreateGuid<MonsterDefinition>(StrangerIdNumber);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == id) is { } existing)
        {
            return existing;
        }

        var look = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == StrangerLookNumber);
        var stranger = context.CreateNew<MonsterDefinition>();
        stranger.SetGuid(StrangerIdNumber);
        stranger.Number = StrangerLookNumber;
        stranger.Designation = StoryScript.StrangerName;
        stranger.ObjectKind = NpcObjectKind.PassiveNpc;
        stranger.NpcWindow = NpcWindow.Undefined;
        stranger.MoveRange = 0;
        stranger.ViewRange = look?.ViewRange ?? 5;
        stranger.MoveDelay = look?.MoveDelay ?? TimeSpan.FromMilliseconds(400);
        stranger.RespawnDelay = look?.RespawnDelay ?? TimeSpan.FromSeconds(1);
        gameConfiguration.Monsters.Add(stranger);
        return stranger;
    }

    private static void AddStrangerSpawn(IContext context, GameMapDefinition lorencia, MonsterDefinition stranger)
    {
        var id = GuidHelper.CreateGuid<MonsterSpawnArea>(lorencia.Number, StrangerIdNumber);
        if (lorencia.MonsterSpawns.Any(spawn => spawn.GetId() == id))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(id);
        spawn.GameMap = lorencia;
        spawn.MonsterDefinition = stranger;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthWest;
        spawn.X1 = StrangerPosition.X;
        spawn.X2 = StrangerPosition.X;
        spawn.Y1 = StrangerPosition.Y;
        spawn.Y2 = StrangerPosition.Y;
        spawn.Quantity = 1;
        lorencia.MonsterSpawns.Add(spawn);
    }
}
