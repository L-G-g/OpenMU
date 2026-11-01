// <copyright file="MapDropsAndInvasions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.MapInvasions;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the jewels, the feathers, the gemstones and the spheres drop each in its own maps, and invades
/// fifteen maps one after another with monsters which always drop them, see <see cref="MapInvasionsPlugIn"/>.
/// </summary>
/// <remarks>
/// This is a custom feature on top of the <see cref="MapLevelLadder"/>:
/// <list type="bullet">
///   <item>The Jewels of Chaos, Bless, Soul and Life, the Jewel of Guardian, the Loch's Feather, the Crest of Monarch,
///         the Gemstone and the spheres don't drop anymore from the common drops of the maps, but only from the monsters
///         of the maps of <see cref="MapDrops"/>. The chance of a drop is chosen so that a player who kills
///         <see cref="ReferenceKillsPerHour"/> monsters per hour finds one in the expected time.
///         The other drops of the jewels stay as they are, e.g. of the boxes, the bosses and the events.</item>
///   <item>Crywolf keeps the materials of the Fenrir, but each of them drops only from some of its monsters,
///         see <see cref="FenrirMaterialMonsters"/>. Soram drops none, it's the monster of the invasion of Crywolf.</item>
///   <item>The <see cref="MapInvasionsPlugIn"/> invades the maps of <see cref="Invasions"/>. The invasion monsters are
///         exclusive to the invasions, so their normal spawns on the invaded maps are removed. The invasions of the
///         Red Dragon and the White Wizard are deactivated, so that they don't mix with the new ones.</item>
/// </list>
/// </remarks>
internal static class MapDropsAndInvasions
{
    /// <summary>
    /// The number of monsters which a player kills per hour, which the chances of <see cref="MapDrops"/> are based on.
    /// It's a bit above the 1,500 which were measured with a strong character in Raklion.
    /// </summary>
    internal const int ReferenceKillsPerHour = 2500;

    /// <summary>
    /// The number from which the ids of the drop item groups of <see cref="MapDrops"/> are derived.
    /// </summary>
    internal const short MapDropItemGroupIdNumber = 4100;

    /// <summary>
    /// The number of monsters of an invasion.
    /// </summary>
    internal const int InvasionMonsterCount = 10;

    private const short CommonJewelDropItemGroupNumber = 4;

    private const byte JewelZoneIndex = 11;

    private const byte JewelZoneGroupKind = 3;

    private static readonly (byte Group, short Number) JewelOfChaos = (12, 15);
    private static readonly (byte Group, short Number) JewelOfBless = (14, 13);
    private static readonly (byte Group, short Number) JewelOfSoul = (14, 14);
    private static readonly (byte Group, short Number) JewelOfLife = (14, 16);
    private static readonly (byte Group, short Number) JewelOfCreation = (14, 22);
    private static readonly (byte Group, short Number) JewelOfGuardian = (14, 31);
    private static readonly (byte Group, short Number) Gemstone = (14, 41);
    private static readonly (byte Group, short Number) LochsFeather = (13, 14);
    private static readonly (byte Group, short Number) SphereMono = (12, 70);
    private static readonly (byte Group, short Number) SplinterOfArmor = (13, 32);
    private static readonly (byte Group, short Number) BlessOfGuardian = (13, 33);
    private static readonly (byte Group, short Number) ClawOfBeast = (13, 34);

    /// <summary>
    /// Gets the drops of the maps: the numbers of the maps, the items with their level (each one equally likely),
    /// and the expected minutes of hunting for one drop. All monsters of the maps drop them.
    /// </summary>
    /// <remarks>
    /// The Crest of Monarch is a Loch's Feather +1. In Icarus, one of four feathers is a crest, because it's only for the Dark Lord.
    /// </remarks>
    internal static IReadOnlyList<(byte[] MapNumbers, (byte Group, short Number)[] Items, byte ItemLevel, double ExpectedMinutes, string Description)> MapDrops { get; } =
    [
        ([0, 3, 51], [JewelOfChaos], 0, 30, "Jewel of Chaos of Lorencia, Noria and Elbeland"),
        ([2], [JewelOfBless], 0, 45, "Jewel of Bless of Devias"),
        ([1], [JewelOfSoul], 0, 45, "Jewel of Soul of Dungeon"),
        ([4], [JewelOfLife], 0, 45, "Jewel of Life of Lost Tower"),
        ([7], [JewelOfBless], 0, 40, "Jewel of Bless of Atlans"),
        ([8], [JewelOfSoul], 0, 40, "Jewel of Soul of Tarkan"),
        ([33], [JewelOfLife], 0, 40, "Jewel of Life of Aida"),
        ([10], [LochsFeather], 0, 90 / 0.75, "Loch's Feather of Icarus"),
        ([10], [LochsFeather], 1, 90 / 0.25, "Crest of Monarch of Icarus"),
        ([37], [Gemstone], 0, 60, "Gemstone of Kanturu"),
        ([38], [JewelOfGuardian], 0, 60, "Jewel of Guardian of Kanturu Relics"),
        ([56], [JewelOfChaos, JewelOfBless, JewelOfSoul, JewelOfLife], 0, 30, "Jewels of Swamp of Calmness"),
        ([57], [SphereMono], 0, 45, "Sphere (Mono) of Raklion"),
        ([4], [JewelOfCreation], 0, 45, "Jewel of Creation of Lost Tower"),
        ([33], [JewelOfCreation], 0, 40, "Jewel of Creation of Aida"),
    ];

    /// <summary>
    /// Gets the monsters of Crywolf which drop each material of the Fenrir: the index of its drop item group
    /// (see <see cref="CrywolfFortress.AddFenrirMaterialDropGroups"/>) and the numbers of the monsters.
    /// </summary>
    internal static IReadOnlyList<(short GroupIndex, short[] MonsterNumbers)> FenrirMaterialMonsters { get; } =
    [
        (1, [310, 311, 312, 313]), // Splinter of Armor: Hammer, Lance and Bow Scout, Werewolf
        (2, [314, 315]), // Bless of Guardian: Scout Hero, Werewolf Hero
        (3, [316]), // Claw of Beast: Balram
    ];

    /// <summary>
    /// Gets the invasions: the number of the map, the number of the monster, and what each monster always drops
    /// (each drop gives between its minimum and maximum of items, each one chosen at random of its items).
    /// </summary>
    internal static IReadOnlyList<(short MapNumber, short MonsterNumber, (int Minimum, int Maximum, (byte Group, short Number, byte Level)[] Items)[] Drops)> Invasions { get; } =
    [
        (0, 44, [(1, 1, [Item(JewelOfChaos)])]), // Lorencia: Red Dragon
        (3, 135, [(1, 1, [Item(JewelOfChaos)])]), // Noria: White Wizard
        (51, 44, [(1, 1, [Item(JewelOfChaos)])]), // Elbeland: Red Dragon
        (2, 25, [(1, 1, [Item(JewelOfBless)])]), // Devias: Ice Queen
        (1, 18, [(1, 1, [Item(JewelOfSoul)])]), // Dungeon: Gorgon
        (4, 38, [(1, 1, [Item(JewelOfLife)])]), // Lost Tower: Balrog
        (7, 49, [(1, 2, [Item(JewelOfBless)])]), // Atlans: Hydra
        (8, 58, [(1, 2, [Item(JewelOfSoul)])]), // Tarkan: Tantallos
        (33, 552, [(1, 2, [Item(JewelOfLife)])]), // Aida: Bloody Witch Queen
        (10, 75, [(1, 1, [Item(LochsFeather), Item(LochsFeather), Item(LochsFeather), Item(LochsFeather, 1)])]), // Icarus: Great Drakan
        (37, 357, [(1, 1, [Item(Gemstone)])]), // Kanturu: Genocider
        (38, 360, [(1, 1, [Item(JewelOfGuardian)])]), // Kanturu Relics: Dreadfear
        (56, 557, [(2, 4, [Item(JewelOfChaos), Item(JewelOfBless), Item(JewelOfSoul), Item(JewelOfLife)])]), // Swamp of Calmness: Sapi Queen
        (CrywolfFortress.Number, 317, [(1, 1, [Item(SplinterOfArmor)]), (1, 1, [Item(BlessOfGuardian)]), (1, 1, [Item(ClawOfBeast)])]), // Crywolf: Soram
        (57, 565, [(1, 1, [Item(SphereMono)])]), // Raklion: Dark Iron Knight
    ];

    /// <summary>
    /// Gets the items which don't drop from the common drops of the maps anymore.
    /// </summary>
    private static IReadOnlyList<(byte Group, short Number)> MovedItems { get; } =
        [JewelOfChaos, JewelOfBless, JewelOfSoul, JewelOfLife, JewelOfGuardian, Gemstone, LochsFeather];

    /// <summary>
    /// Gets the spheres, which drop only in Raklion, and only the first tier.
    /// </summary>
    private static IReadOnlyList<(byte Group, short Number)> Spheres { get; } = [(12, 70), (12, 71), (12, 72), (12, 73), (12, 74)];

    /// <summary>
    /// Applies the drops and the invasions to the configuration. It can be applied more than once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        RemoveCommonDrops(gameConfiguration);
        MoveFenrirMaterialsToMonsters(gameConfiguration);
        RemoveInvasionMonsterSpawns(gameConfiguration);
        AddMapDrops(context, gameConfiguration);
        ConfigureInvasions(context, gameConfiguration);
    }

    private static (byte Group, short Number, byte Level) Item((byte Group, short Number) item, byte level = 0) => (item.Group, item.Number, level);

    private static ItemDefinition? GetItem(GameConfiguration gameConfiguration, (byte Group, short Number) item)
    {
        return gameConfiguration.Items.FirstOrDefault(i => i.Group == item.Group && i.Number == item.Number);
    }

    private static GameMapDefinition? GetMap(GameConfiguration gameConfiguration, short number)
    {
        return gameConfiguration.Maps.FirstOrDefault(map => map.Number == number && map.Discriminator == 0);
    }

    private static DropItemGroup? GetDropItemGroup(GameConfiguration gameConfiguration, Guid id)
    {
        return gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id);
    }

    /// <summary>
    /// Removes the moved items from the common drops: the common group of the jewels, the jewels of Swamp of Calmness and Crywolf
    /// (only the Jewel of Creation stays, with its chance before), the Jewel of Guardian of the Land of Trials and the feathers of Icarus.
    /// The spheres don't drop as random items anymore.
    /// </summary>
    private static void RemoveCommonDrops(GameConfiguration gameConfiguration)
    {
        var movedItems = MovedItems.Select(item => GetItem(gameConfiguration, item)).OfType<ItemDefinition>().ToList();
        if (GetDropItemGroup(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(CommonJewelDropItemGroupNumber)) is { } commonJewels)
        {
            movedItems.ForEach(item => commonJewels.PossibleItems.Remove(item));
        }

        if (GetDropItemGroup(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(MapLevelLadder.ZoneDropItemGroupIdNumber, JewelZoneIndex, JewelZoneGroupKind)) is { } zoneJewels
            && GetItem(gameConfiguration, JewelOfCreation) is { } creation
            && zoneJewels.PossibleItems.Any(item => item != creation))
        {
            // The Jewel of Creation keeps the chance which it had among the jewels of the zone.
            zoneJewels.Chance = MapLevelLadder.ZoneJewelDropChance / MapLevelLadder.ZoneJewels.Count;
            zoneJewels.PossibleItems.Clear();
            zoneJewels.PossibleItems.Add(creation);
            zoneJewels.Description = "Jewel of Creation of the zone Swamp of Calmness, Crywolf Fortress";
        }

        var removedMapGroups = new (short MapNumber, Guid GroupId)[]
        {
            (LandOfTrials.Number, GuidHelper.CreateGuid<DropItemGroup>(LandOfTrials.Number, 1)),
            (Icarus.Number, GuidHelper.CreateGuid<DropItemGroup>(Icarus.Number, 1)),
            (Icarus.Number, GuidHelper.CreateGuid<DropItemGroup>(Icarus.Number, 2)),
        };
        foreach (var (mapNumber, groupId) in removedMapGroups)
        {
            if (GetMap(gameConfiguration, mapNumber) is { } map
                && map.DropItemGroups.FirstOrDefault(g => g.GetId() == groupId) is { } group)
            {
                map.DropItemGroups.Remove(group);
            }
        }

        foreach (var sphere in Spheres)
        {
            if (GetItem(gameConfiguration, sphere) is { } definition)
            {
                definition.DropsFromMonsters = false;
            }
        }
    }

    /// <summary>
    /// Moves the materials of the Fenrir from all monsters of Crywolf to the monsters of <see cref="FenrirMaterialMonsters"/>.
    /// </summary>
    private static void MoveFenrirMaterialsToMonsters(GameConfiguration gameConfiguration)
    {
        if (GetMap(gameConfiguration, CrywolfFortress.Number) is not { } crywolf)
        {
            return;
        }

        foreach (var (groupIndex, monsterNumbers) in FenrirMaterialMonsters)
        {
            if (GetDropItemGroup(gameConfiguration, GuidHelper.CreateGuid<DropItemGroup>(CrywolfFortress.Number, groupIndex)) is not { } group)
            {
                continue;
            }

            crywolf.DropItemGroups.Remove(group);
            foreach (var monsterNumber in monsterNumbers)
            {
                if (MapLevelLadder.GetOriginalMonster(gameConfiguration, monsterNumber) is { } monster && !monster.DropItemGroups.Contains(group))
                {
                    monster.DropItemGroups.Add(group);
                }
            }
        }
    }

    /// <summary>
    /// Removes the automatic spawns of the invasion monsters on their invaded maps, because they are exclusive to the invasions.
    /// </summary>
    private static void RemoveInvasionMonsterSpawns(GameConfiguration gameConfiguration)
    {
        foreach (var (mapNumber, monsterNumber, _) in Invasions)
        {
            if (GetMap(gameConfiguration, mapNumber) is not { } map)
            {
                continue;
            }

            foreach (var spawn in map.MonsterSpawns.Where(s => s.MonsterDefinition?.Number == monsterNumber && s.SpawnTrigger == SpawnTrigger.Automatic).ToList())
            {
                map.MonsterSpawns.Remove(spawn);
            }
        }
    }

    /// <summary>
    /// Adds the drop item groups of <see cref="MapDrops"/> to the monsters which spawn on their maps.
    /// </summary>
    /// <remarks>
    /// They are added to the monsters instead of the maps, because a drop item group of a map only drops items up to the
    /// level of the monster (e.g. the Gemstone has a drop level of 150), and these drops should come from every monster of the map.
    /// </remarks>
    private static void AddMapDrops(IContext context, GameConfiguration gameConfiguration)
    {
        var minutesPerKill = 60.0 / ReferenceKillsPerHour;
        for (short i = 0; i < MapDrops.Count; i++)
        {
            var (mapNumbers, items, itemLevel, expectedMinutes, description) = MapDrops[i];
            var id = GuidHelper.CreateGuid<DropItemGroup>(MapDropItemGroupIdNumber, i);
            if (GetDropItemGroup(gameConfiguration, id) is not { } group)
            {
                group = context.CreateNew<DropItemGroup>();
                group.SetGuid(id);
                gameConfiguration.DropItemGroups.Add(group);
            }

            group.Description = description;
            group.Chance = minutesPerKill / expectedMinutes;
            group.ItemLevel = itemLevel;
            group.ItemType = SpecialItemType.None;
            group.PossibleItems.Clear();
            items.Select(item => GetItem(gameConfiguration, item)).OfType<ItemDefinition>().ToList().ForEach(group.PossibleItems.Add);

            var monsters = gameConfiguration.Maps
                .Where(map => mapNumbers.Contains((byte)map.Number) && map.Discriminator == 0)
                .SelectMany(map => map.MonsterSpawns)
                .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.Automatic && spawn.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
                .Select(spawn => spawn.MonsterDefinition!)
                .Distinct();
            foreach (var monster in monsters.Where(m => !m.DropItemGroups.Contains(group)))
            {
                monster.DropItemGroups.Add(group);
            }
        }
    }

    /// <summary>
    /// Configures and activates the <see cref="MapInvasionsPlugIn"/>, and deactivates the invasions of the Red Dragon and the White Wizard.
    /// </summary>
    private static void ConfigureInvasions(IContext context, GameConfiguration gameConfiguration)
    {
        var configuration = new MapInvasionsConfiguration();
        foreach (var (mapNumber, monsterNumber, drops) in Invasions)
        {
            if (MapLevelLadder.GetOriginalMonster(gameConfiguration, monsterNumber) is not { } monster)
            {
                continue;
            }

            var invasion = new MapInvasionsConfiguration.MapInvasion { MapNumber = mapNumber, Monster = monster, Count = InvasionMonsterCount };
            foreach (var (minimum, maximum, items) in drops)
            {
                var drop = new MapInvasionsConfiguration.InvasionDrop { MinimumCount = minimum, MaximumCount = maximum };
                foreach (var (group, number, level) in items)
                {
                    drop.Items.Add(new MapInvasionsConfiguration.InvasionDropItem { Group = group, Number = number, Level = level });
                }

                invasion.Drops.Add(drop);
            }

            configuration.Invasions.Add(invasion);
        }

        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(MapInvasionsPlugIn).GUID);
        plugInConfiguration.IsActive = true;
        plugInConfiguration.SetConfiguration(configuration, new ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler());

        // They stay configured, so that they can be activated again in the plugin list.
        GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(RedDragonInvasionPlugIn).GUID, new RedDragonInvasionPlugIn().CreateDefaultConfig()).IsActive = false;
        GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(WhiteWizardInvasionPlugIn).GUID, new WhiteWizardInvasionPlugIn().CreateDefaultConfig()).IsActive = false;
    }

    private static PlugInConfiguration GetOrCreatePlugInConfiguration(IContext context, GameConfiguration gameConfiguration, Guid typeId, object? defaultConfiguration = null)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId) is { } existing)
        {
            return existing;
        }

        var plugInConfiguration = context.CreateNew<PlugInConfiguration>();
        plugInConfiguration.SetGuid(typeId);
        plugInConfiguration.TypeId = typeId;
        if (defaultConfiguration is not null)
        {
            plugInConfiguration.SetConfiguration(defaultConfiguration, null);
        }

        gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        return plugInConfiguration;
    }
}
