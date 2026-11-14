// <copyright file="DungeonDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The drop generator of a dungeon. It generates the drops with the drop generator of the game and changes them:
/// <list type="bullet">
///   <item>The monsters drop an excellent item with their own chance (<see cref="DungeonConfiguration.MonsterExcellentDropChance"/>),
///         which is higher than on the map outside of the dungeon, and no other items (<see cref="DungeonConfiguration.OnlyExcellentItems"/>).</item>
///   <item>The excellent items are of the zones of the <see cref="DungeonSettings.DropMaps"/> of the dungeon (without them: of the zone
///         of the monster or of a zone before it), with at least <see cref="DungeonConfiguration.MinimumExcellentOptions"/> excellent options.
///         The weapons, shields and armor sets of these zones are split into a low and a high half, and the high items drop
///         more often in the extreme version of the dungeon (<see cref="DungeonConfiguration.ExtremeHighItemWeight"/>).</item>
///   <item>The bosses (the boss of each stage) drop one such excellent item, with the chance <see cref="DungeonStage.ExcellentDropChance"/>.</item>
///   <item>The items which are rolled with need, greed or pass (see <see cref="DungeonLoot"/>) don't fall to the ground.</item>
/// </list>
/// </summary>
public sealed class DungeonDropGenerator : IDropGenerator
{
    private readonly IDropGenerator _gameDropGenerator;
    private readonly DungeonConfiguration _configuration;
    private readonly DungeonLoot _loot;
    private readonly IReadOnlyDictionary<MonsterDefinition, double> _bosses;
    private readonly IReadOnlyList<short> _dropMapNumbers;
    private readonly double _highItemWeight;
    private readonly double _monsterExcellentDropChance;

    /// <summary>
    /// Initializes a new instance of the <see cref="DungeonDropGenerator"/> class.
    /// </summary>
    /// <param name="gameDropGenerator">The drop generator of the game.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="loot">The loot of the dungeon.</param>
    /// <param name="bosses">The definitions of the bosses, with their chance to drop an excellent item.</param>
    /// <param name="dropMapNumbers">The numbers of the maps whose zones give the excellent items.</param>
    /// <param name="highItemWeight">How much more often than a low item a high item drops.</param>
    /// <param name="monsterExcellentDropChance">The chance of a monster which isn't a boss to drop an excellent item.</param>
    internal DungeonDropGenerator(IDropGenerator gameDropGenerator, DungeonConfiguration configuration, DungeonLoot loot, IReadOnlyDictionary<MonsterDefinition, double> bosses, IReadOnlyList<short> dropMapNumbers, double highItemWeight, double monsterExcellentDropChance)
    {
        this._monsterExcellentDropChance = monsterExcellentDropChance;
        this._dropMapNumbers = dropMapNumbers;
        this._highItemWeight = highItemWeight;
        this._gameDropGenerator = gameDropGenerator;
        this._configuration = configuration;
        this._loot = loot;
        this._bosses = bosses;
    }

    /// <inheritdoc />
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var (gameItems, money) = await this._gameDropGenerator.GenerateItemDropsAsync(monster, gainedExperience, player).ConfigureAwait(false);
        var monsterLevel = (int)monster[Stats.Level];
        var items = new List<Item>();
        if (this._bosses.TryGetValue(monster, out var bossChance))
        {
            if (Rand.NextRandomBool(Math.Clamp(bossChance, 0, 1))
                && await this.GenerateExcellentItemAsync(monsterLevel).ConfigureAwait(false) is { } bossItem)
            {
                items.Add(bossItem);
            }
        }
        else
        {
            if (!this._configuration.OnlyExcellentItems)
            {
                // The excellent items of the dungeon replace the ones of the map.
                items.AddRange(gameItems.Where(item => !item.IsExcellent()));
            }

            if (Rand.NextRandomBool(this._monsterExcellentDropChance)
                && await this.GenerateExcellentItemAsync(monsterLevel).ConfigureAwait(false) is { } excellentItem)
            {
                items.Add(excellentItem);
            }
        }

        var groundItems = new List<Item>(items.Count);
        foreach (var item in items)
        {
            if (!await this._loot.TryStartRollAsync(item, player).ConfigureAwait(false))
            {
                groundItems.Add(item);
            }
        }

        return (groundItems, money);
    }

    /// <inheritdoc />
    public Item? GenerateItemDrop(DropItemGroup group) => this._gameDropGenerator.GenerateItemDrop(group);

    /// <inheritdoc />
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups) => this._gameDropGenerator.GenerateItemDrop(groups);

    private async ValueTask<Item?> GenerateExcellentItemAsync(int monsterLevel)
    {
        if (this._gameDropGenerator is not DefaultDropGenerator defaultDropGenerator)
        {
            return null;
        }

        if (this._dropMapNumbers.Count > 0
            && await defaultDropGenerator.GenerateExcellentItemOfZonesAsync(this._dropMapNumbers, monsterLevel, this._configuration.MinimumExcellentOptions, this._highItemWeight).ConfigureAwait(false) is { } item)
        {
            return item;
        }

        return await defaultDropGenerator.GenerateExcellentItemUpToZoneAsync(monsterLevel, this._configuration.MinimumExcellentOptions).ConfigureAwait(false);
    }
}
