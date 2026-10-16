// <copyright file="EliteQuestMonsters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameServer.MessageHandler.Quests;

/// <summary>
/// Adds stronger "Elite" versions of some monsters, which are the only ones which drop the items
/// of the first class change quest of Sebina the Priestess.
/// </summary>
/// <remarks>
/// This is a custom feature, the original game drops the quest items from any monster of a level range:
/// the Scroll of Emperor from the levels 45 to 60, and the second item (Broken Sword, Tear of Elf,
/// Soul Shard of Wizard, Eye of Abyssal) from the levels 62 to 76.
/// Here, the Shadow Elite in Lost Tower drops the Scroll of Emperor, and the Death Knight Elite in Lost Tower,
/// the Lizard King Elite in Atlans and the Mutant Elite in Tarkan drop the second item.
/// Each of them spawns alone at <see cref="SpotsPerMap"/> places of its map, respawns after
/// <see cref="RespawnDelay"/> and drops the item to a player (or party member) who needs it for the quest.
/// The elite monsters have the number of their original, so that the client shows them with the same model and name.
/// </remarks>
internal static class EliteQuestMonsters
{
    /// <summary>
    /// The number of places where an elite monster spawns on its map.
    /// </summary>
    internal const int SpotsPerMap = 3;

    /// <summary>
    /// The first number of the spawn areas of the elite monsters, used to derive their ids.
    /// It's above the numbers of the monster spots of <see cref="MonsterSpotGrouping"/>.
    /// </summary>
    internal const short FirstSpawnNumber = 21000;

    /// <summary>
    /// The offset which is added to the number of the original monster to get the number from which the ids
    /// of the elite monster and its attributes are derived. The elite has the same <see cref="MonsterDefinition.Number"/>,
    /// so it needs another number for its ids.
    /// </summary>
    internal const short IdNumberOffset = 1000;

    /// <summary>
    /// The multiplier for the maximum health of the original monster.
    /// </summary>
    internal const float HealthMultiplier = 8;

    /// <summary>
    /// The multiplier for damage, defense, attack rate and defense rate of the original monster.
    /// </summary>
    internal const float CombatMultiplier = 1.5f;

    /// <summary>
    /// The maximum number of items an elite monster drops, so that it can drop quest items for some party members
    /// and still a normal item.
    /// </summary>
    internal const int MaximumItemDrops = 3;

    private const short SebinaThePriestessNumber = 235;

    /// <summary>
    /// Gets the time after which a killed elite monster respawns.
    /// </summary>
    internal static TimeSpan RespawnDelay { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the elite monsters: the number of the original monster, the name, the number of the map, and the number
    /// of the quest of Sebina whose required items it drops.
    /// </summary>
    internal static IReadOnlyList<(short OriginalNumber, string Name, short MapNumber, short QuestNumber)> Elites { get; } =
    [
        (36, "Shadow Elite", 4, 0), // Lost Tower, Scroll of Emperor
        (40, "Death Knight Elite", 4, 1), // Lost Tower
        (48, "Lizard King Elite", 7, 1), // Atlans
        (62, "Mutant Elite", 8, 1), // Tarkan
    ];

    private static IReadOnlyCollection<AttributeDefinition> CombatAttributes { get; } =
    [
        Stats.MinimumPhysBaseDmg,
        Stats.MaximumPhysBaseDmg,
        Stats.DefenseBase,
        Stats.AttackRatePvm,
        Stats.DefenseRatePvm,
    ];

    /// <summary>
    /// Adds the elite monsters with their spawns and drops, and stops the normal monsters from dropping the quest items.
    /// Elite monsters, spawns and drops which already exist are kept as they are.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var quests = gameConfiguration.Monsters
            .Where(m => m.Number == SebinaThePriestessNumber)
            .SelectMany(m => m.Quests)
            .Where(q => q.Group == QuestConstants.LegacyQuestGroup && Elites.Any(e => e.QuestNumber == q.Number))
            .ToList();

        for (var i = 0; i < Elites.Count; i++)
        {
            var (originalNumber, name, mapNumber, questNumber) = Elites[i];
            var original = gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(originalNumber));
            var map = gameConfiguration.Maps.FirstOrDefault(m => m.Number == mapNumber && m.Discriminator == 0);
            if (original is null || map is null)
            {
                continue;
            }

            var elite = GetOrCreateElite(context, gameConfiguration, original, name);
            AddSpawns(context, map, original, elite, (short)(FirstSpawnNumber + (i * SpotsPerMap)));

            var requirements = quests.Where(q => q.Number == questNumber).SelectMany(q => q.RequiredItems).ToList();
            foreach (var item in requirements.Select(r => r.Item).OfType<ItemDefinition>().Distinct())
            {
                AddDrop(context, gameConfiguration, elite, item);
            }

            foreach (var dropItemGroup in requirements.Select(r => r.DropItemGroup).OfType<DropItemGroup>())
            {
                // Only the elite monsters drop it now.
                dropItemGroup.Chance = 0;
                dropItemGroup.MinimumMonsterLevel = null;
                dropItemGroup.MaximumMonsterLevel = null;
                dropItemGroup.Monster = null;
            }
        }
    }

    private static MonsterDefinition GetOrCreateElite(IContext context, GameConfiguration gameConfiguration, MonsterDefinition original, string name)
    {
        var idNumber = (short)(original.Number + IdNumberOffset);
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(idNumber)) is { } existing)
        {
            return existing;
        }

        var elite = context.CreateNew<MonsterDefinition>();
        elite.SetGuid(idNumber);
        elite.Number = original.Number;
        elite.Designation = name;
        elite.MoveRange = original.MoveRange;
        elite.AttackRange = original.AttackRange;
        elite.ViewRange = original.ViewRange;
        elite.MoveDelay = original.MoveDelay;
        elite.AttackDelay = original.AttackDelay;
        elite.RespawnDelay = RespawnDelay;
        elite.Attribute = original.Attribute;
        elite.NumberOfMaximumItemDrops = MaximumItemDrops;
        elite.ObjectKind = original.ObjectKind;
        elite.IntelligenceTypeName = original.IntelligenceTypeName;
        elite.AttackSkill = original.AttackSkill;

        foreach (var originalAttribute in original.Attributes)
        {
            if (originalAttribute.AttributeDefinition is not { } definition)
            {
                continue;
            }

            var attribute = context.CreateNew<MonsterAttribute>();
            attribute.AttributeDefinition = definition;
            attribute.Value = originalAttribute.Value * GetMultiplier(definition);
            attribute.SetGuid(idNumber, definition.Id.ExtractFirstTwoBytes());
            elite.Attributes.Add(attribute);
        }

        gameConfiguration.Monsters.Add(elite);
        return elite;
    }

    private static float GetMultiplier(AttributeDefinition definition)
    {
        if (definition.Id == Stats.MaximumHealth.Id)
        {
            return HealthMultiplier;
        }

        return CombatAttributes.Any(a => a.Id == definition.Id) ? CombatMultiplier : 1;
    }

    /// <summary>
    /// Adds the spawns of the elite at the places of the original monster which are farthest apart from each other.
    /// </summary>
    private static void AddSpawns(IContext context, GameMapDefinition map, MonsterDefinition original, MonsterDefinition elite, short firstSpawnNumber)
    {
        var spawnIds = Enumerable.Range(0, SpotsPerMap)
            .Select(i => GuidHelper.CreateGuid<MonsterSpawnArea>((short)map.Number, (short)(firstSpawnNumber + i)))
            .ToList();
        if (map.MonsterSpawns.Any(spawn => spawnIds.Contains(spawn.GetId())))
        {
            return;
        }

        var candidates = map.MonsterSpawns
            .Where(spawn => spawn.MonsterDefinition == original && spawn.SpawnTrigger == SpawnTrigger.Automatic)
            .OrderByDescending(spawn => spawn.Quantity)
            .ThenBy(spawn => spawn.X1)
            .ThenBy(spawn => spawn.Y1)
            .ToList();
        var chosen = new List<MonsterSpawnArea>();
        while (chosen.Count < SpotsPerMap && candidates.Count > 0)
        {
            var next = chosen.Count == 0
                ? candidates[0]
                : candidates.MaxBy(candidate => chosen.Min(c => GetDistanceSquared(c, candidate)))!;
            chosen.Add(next);
            candidates.Remove(next);
        }

        for (var i = 0; i < chosen.Count; i++)
        {
            var place = chosen[i];
            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid(spawnIds[i]);
            spawn.GameMap = map;
            spawn.MonsterDefinition = elite;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            spawn.Direction = Direction.Undefined;
            spawn.X1 = place.X1;
            spawn.X2 = place.X2;
            spawn.Y1 = place.Y1;
            spawn.Y2 = place.Y2;
            spawn.Quantity = 1;
            map.MonsterSpawns.Add(spawn);
        }
    }

    private static int GetDistanceSquared(MonsterSpawnArea a, MonsterSpawnArea b)
    {
        var dx = ((a.X1 + a.X2) / 2) - ((b.X1 + b.X2) / 2);
        var dy = ((a.Y1 + a.Y2) / 2) - ((b.Y1 + b.Y2) / 2);
        return (dx * dx) + (dy * dy);
    }

    private static void AddDrop(IContext context, GameConfiguration gameConfiguration, MonsterDefinition elite, ItemDefinition item)
    {
        var id = GuidHelper.CreateGuid<DropItemGroup>((short)(elite.Number + IdNumberOffset), item.Number);
        if (elite.DropItemGroups.Any(g => g.GetId() == id))
        {
            return;
        }

        var dropItemGroup = context.CreateNew<DropItemGroup>();
        dropItemGroup.SetGuid(id);
        dropItemGroup.Description = $"Quest Item '{item.Name.ValueInNeutralLanguage}' ({elite.Designation.ValueInNeutralLanguage})";
        dropItemGroup.Chance = 1;
        dropItemGroup.ItemLevel = 0;
        dropItemGroup.Monster = elite;
        dropItemGroup.PossibleItems.Add(item);
        gameConfiguration.DropItemGroups.Add(dropItemGroup);
        elite.DropItemGroups.Add(dropItemGroup);
    }
}
