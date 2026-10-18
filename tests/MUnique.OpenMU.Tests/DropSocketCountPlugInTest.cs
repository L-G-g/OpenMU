// <copyright file="DropSocketCountPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Runtime.InteropServices;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests for the <see cref="DropSocketCountPlugIn"/>.
/// </summary>
[TestFixture]
public class DropSocketCountPlugInTest
{
    private const short MonsterNumber = 459;
    private const short OtherMonsterNumber = 457;
    private const short MapNumber = 0;

    /// <summary>
    /// Tests that the rule of the monster wins over the rule of the map.
    /// </summary>
    [Test]
    public async Task MonsterRuleWinsOverMapRuleAsync()
    {
        var (plugIn, monster, killer) = await CreateAsync(MonsterNumber).ConfigureAwait(false);
        for (var i = 0; i < 20; i++)
        {
            var item = CreateSocketItem(5);
            await plugIn.MonsterItemDroppingAsync(monster, killer, item).ConfigureAwait(false);
            Assert.That(item.SocketCount, Is.EqualTo(3));
        }
    }

    /// <summary>
    /// Tests that the rule of the map applies to the other monsters, and that the number of sockets is between its minimum and maximum.
    /// </summary>
    [Test]
    public async Task MapRuleAppliesToOtherMonstersAsync()
    {
        var (plugIn, monster, killer) = await CreateAsync(OtherMonsterNumber).ConfigureAwait(false);
        var counts = new HashSet<int>();
        for (var i = 0; i < 100; i++)
        {
            var item = CreateSocketItem(5);
            await plugIn.MonsterItemDroppingAsync(monster, killer, item).ConfigureAwait(false);
            counts.Add(item.SocketCount);
        }

        Assert.That(counts, Is.EquivalentTo(new[] { 1, 2 }));
    }

    /// <summary>
    /// Tests that the number of sockets doesn't exceed the maximum of the item.
    /// </summary>
    [Test]
    public async Task SocketCountDoesNotExceedMaximumOfItemAsync()
    {
        var (plugIn, monster, killer) = await CreateAsync(MonsterNumber).ConfigureAwait(false);
        var item = CreateSocketItem(2);
        await plugIn.MonsterItemDroppingAsync(monster, killer, item).ConfigureAwait(false);
        Assert.That(item.SocketCount, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that items without sockets keep having none.
    /// </summary>
    [Test]
    public async Task ItemWithoutSocketsStaysAsync()
    {
        var (plugIn, monster, killer) = await CreateAsync(MonsterNumber).ConfigureAwait(false);
        var item = CreateSocketItem(0);
        await plugIn.MonsterItemDroppingAsync(monster, killer, item).ConfigureAwait(false);
        Assert.That(item.SocketCount, Is.Zero);
    }

    /// <summary>
    /// Tests that the plugin is called before the item is dropped, so that the dropped item has the changed number of sockets.
    /// </summary>
    [Test]
    public async Task DroppedItemHasSocketCountOfRuleAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IMonsterItemDroppingPlugIn>(CreatePlugIn());
        var recorder = new RecordingPlugIn();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IMonsterItemDroppedPlugIn>(recorder);
        var item = CreateSocketItem(5);
        var dropGenerator = new Mock<IDropGenerator>();
        dropGenerator.Setup(g => g.GenerateItemDropsAsync(It.IsAny<DataModel.Configuration.MonsterDefinition>(), It.IsAny<int>(), It.IsAny<Player>()))
            .Returns(ValueTask.FromResult<(IEnumerable<Item> Items, uint? Money)>((new[] { item }, null)));
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext, dropGenerator.Object, MonsterNumber).ConfigureAwait(false);

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);

        // The drop happens delayed, after the death animation.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (recorder.Items.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.That(recorder.Items.Select(d => d.Item.SocketCount), Is.EqualTo(new[] { 3 }));
    }

    private static DropSocketCountPlugIn CreatePlugIn()
    {
        var configuration = new DropSocketCountConfiguration();
        configuration.Rules.Add(new() { MapNumber = MapNumber, MinimumSockets = 1, MaximumSockets = 2 });
        configuration.Rules.Add(new() { MonsterNumber = MonsterNumber, MinimumSockets = 3, MaximumSockets = 3 });
        return new DropSocketCountPlugIn { Configuration = configuration };
    }

    private static BasicModel.Item CreateSocketItem(int maximumSockets)
    {
        return new BasicModel.Item { Definition = new BasicModel.ItemDefinition { MaximumSockets = maximumSockets } };
    }

    private static async ValueTask<(DropSocketCountPlugIn PlugIn, Monster Monster, Player Killer)> CreateAsync(short monsterNumber)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext, new Mock<IDropGenerator>().Object, monsterNumber).ConfigureAwait(false);
        return (CreatePlugIn(), monster, killer);
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext, IDropGenerator dropGenerator, short number)
    {
        // A unique id, because the attributes of monster definitions are cached by their id.
        var monsterDefinition = new MonsterDefinition { Id = Guid.NewGuid(), Number = number, ObjectKind = NpcObjectKind.Monster };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        var map = await gameContext.GetMapAsync(MapNumber).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = 100,
            Y1 = 100,
            X2 = 100,
            Y2 = 100,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            dropGenerator,
            new Mock<INpcIntelligence>().Object,
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }

    [Guid("1F0E5C3B-7A2D-4C8E-9B6F-3D4A5E6F7081")]
    private sealed class RecordingPlugIn : IMonsterItemDroppedPlugIn
    {
        public System.Collections.Concurrent.ConcurrentBag<DroppedItem> Items { get; } = new();

        public ValueTask MonsterItemDroppedAsync(AttackableNpcBase monster, Player killer, DroppedItem droppedItem)
        {
            this.Items.Add(droppedItem);
            return ValueTask.CompletedTask;
        }
    }
}
