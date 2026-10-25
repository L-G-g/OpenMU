// <copyright file="DungeonDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The drop generator of a dungeon. It generates the drops with the drop generator of the game and changes them:
/// <list type="bullet">
///   <item>The monsters drop excellent items more often (<see cref="DungeonConfiguration.ExcellentDropChanceMultiplier"/>).</item>
///   <item>The excellent items are of the zone of the monster or of a zone before it, with at least
///         <see cref="DungeonConfiguration.MinimumExcellentOptions"/> excellent options.</item>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DungeonDropGenerator"/> class.
    /// </summary>
    /// <param name="gameDropGenerator">The drop generator of the game.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="loot">The loot of the dungeon.</param>
    /// <param name="bosses">The definitions of the bosses, with their chance to drop an excellent item.</param>
    internal DungeonDropGenerator(IDropGenerator gameDropGenerator, DungeonConfiguration configuration, DungeonLoot loot, IReadOnlyDictionary<MonsterDefinition, double> bosses)
    {
        this._gameDropGenerator = gameDropGenerator;
        this._configuration = configuration;
        this._loot = loot;
        this._bosses = bosses;
    }

    /// <summary>
    /// Gets the chance of a monster to drop an excellent item on a map: the sum of the chances of its excellent drop item groups.
    /// </summary>
    /// <param name="monster">The monster.</param>
    /// <param name="map">The map.</param>
    /// <returns>The chance, from 0 to 1.</returns>
    public static double GetExcellentDropChance(MonsterDefinition monster, GameMapDefinition map)
    {
        var level = monster[Stats.Level];
        return monster.DropItemGroups.Concat(map.DropItemGroups)
            .Where(group => group.ItemType is SpecialItemType.Excellent or SpecialItemType.ZoneExcellentItem
                            && group.Chance is > 0 and < 1
                            && (group.Monster is null || group.Monster.Equals(monster))
                            && (group.MinimumMonsterLevel is null || level >= group.MinimumMonsterLevel)
                            && (group.MaximumMonsterLevel is null || level <= group.MaximumMonsterLevel))
            .Distinct()
            .Sum(group => group.Chance);
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
            foreach (var item in gameItems)
            {
                items.Add(item.IsExcellent()
                    ? await this.GenerateExcellentItemAsync(monsterLevel).ConfigureAwait(false) ?? item
                    : item);
            }

            var extraChance = player.CurrentMap?.Definition is { } map
                ? GetExcellentDropChance(monster, map) * Math.Max(this._configuration.ExcellentDropChanceMultiplier - 1, 0)
                : 0;
            if (extraChance > 0
                && Rand.NextRandomBool(extraChance)
                && await this.GenerateExcellentItemAsync(monsterLevel).ConfigureAwait(false) is { } extraItem)
            {
                items.Add(extraItem);
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

        return await defaultDropGenerator.GenerateExcellentItemUpToZoneAsync(monsterLevel, this._configuration.MinimumExcellentOptions).ConfigureAwait(false);
    }
}
