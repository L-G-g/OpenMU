// <copyright file="FlightTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Flight;
using MUnique.OpenMU.GameLogic.NPC;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests for the rules of the <see cref="FlightPlugIn"/>.
/// </summary>
[TestFixture]
public class FlightTests
{
    /// <summary>
    /// Tests that the default configuration allows the regular hunting maps, including Kanturu.
    /// </summary>
    /// <param name="mapNumber">The map number.</param>
    [TestCase(0)] // Lorencia
    [TestCase(2)] // Devias
    [TestCase(8)] // Tarkan
    [TestCase(37)] // Kanturu ruins
    [TestCase(38)] // Kanturu relics
    [TestCase(57)] // Raklion
    public void DefaultAllowsRegularMaps(int mapNumber)
    {
        Assert.That(new FlightConfiguration().IsMapAllowed(mapNumber), Is.True);
    }

    /// <summary>
    /// Tests that the default configuration doesn't allow events, Castle Siege, Kalima, the spectre hall and Icarus.
    /// </summary>
    /// <param name="mapNumber">The map number.</param>
    [TestCase(9)] // Devil Square
    [TestCase(10)] // Icarus keeps its own rules
    [TestCase(11)] // Blood Castle
    [TestCase(18)] // Chaos Castle
    [TestCase(24)] // Kalima
    [TestCase(30)] // Valley of Loren (Castle Siege)
    [TestCase(39)] // Kanturu event
    [TestCase(45)] // Illusion Temple
    [TestCase(64)] // Duel Arena, the spectre hall
    [TestCase(65)] // Doppelganger
    [TestCase(69)] // Imperial Guardian
    public void DefaultDeniesEventMaps(int mapNumber)
    {
        Assert.That(new FlightConfiguration().IsMapAllowed(mapNumber), Is.False);
    }

    /// <summary>
    /// Tests that a flying player passes over blocked tiles and water, but not over tiles without ground.
    /// </summary>
    [Test]
    public void FlyableTiles()
    {
        var data = new byte[ushort.MaxValue + 3];
        data[3 + (10 * 256) + 10] = (byte)TerrainAttributeType.Blocked;
        data[3 + (10 * 256) + 11] = (byte)TerrainAttributeType.NoGround;
        data[3 + (10 * 256) + 12] = (byte)TerrainAttributeType.Water;
        data[3 + (10 * 256) + 13] = (byte)(TerrainAttributeType.Blocked | TerrainAttributeType.NoGround);
        var terrain = new GameMapTerrain(data);

        Assert.That(FlightPlugIn.IsFlyableTile(terrain, new Pathfinding.Point(10, 10)), Is.True);
        Assert.That(FlightPlugIn.IsFlyableTile(terrain, new Pathfinding.Point(11, 10)), Is.False);
        Assert.That(FlightPlugIn.IsFlyableTile(terrain, new Pathfinding.Point(12, 10)), Is.True);
        Assert.That(FlightPlugIn.IsFlyableTile(terrain, new Pathfinding.Point(13, 10)), Is.False);
        Assert.That(FlightPlugIn.IsFlyableTile(terrain, new Pathfinding.Point(14, 10)), Is.True);
    }

    /// <summary>
    /// Tests that a monster can hit a walking player, but not a flying one.
    /// </summary>
    [Test]
    public async Task MonstersCantHitFlyingPlayersAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        Assert.That(await player.AttackByAsync(monster, null, false).ConfigureAwait(false), Is.Not.Null);

        player.IsFlying = true;
        Assert.That(await player.AttackByAsync(monster, null, false).ConfigureAwait(false), Is.Null);
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext)
    {
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var monsterDefinition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster, AttackDelay = TimeSpan.FromHours(1) };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = 140,
            Y1 = 140,
            X2 = 140,
            Y2 = 140,
            Quantity = 1,
        };

        var monster = new Monster(spawnArea, monsterDefinition, map, NullDropGenerator.Instance, new NullMonsterIntelligence(), gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        return monster;
    }
}
