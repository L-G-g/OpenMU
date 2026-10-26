// <copyright file="DungeonConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// The configuration of the dungeon, see <see cref="DungeonFeaturePlugIn"/>.
/// </summary>
public class DungeonConfiguration
{
    /// <summary>
    /// Gets or sets the designation of the NPC at which the party leader enters the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_NpcDesignation_Name), Description = nameof(PlugInResources.DungeonConfiguration_NpcDesignation_Description))]
    public string NpcDesignation { get; set; } = DungeonDefaults.NpcDesignation;

    /// <summary>
    /// Gets or sets the game level of the <see cref="MiniGameDefinition"/> of the dungeon, which has the type <see cref="MiniGameType.Dungeon"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_GameLevel_Name), Description = nameof(PlugInResources.DungeonConfiguration_GameLevel_Description))]
    public byte GameLevel { get; set; } = DungeonDefaults.GameLevel;

    /// <summary>
    /// Gets or sets the time after an entry until a character can enter again.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_EntryCooldown_Name), Description = nameof(PlugInResources.DungeonConfiguration_EntryCooldown_Description))]
    public TimeSpan EntryCooldown { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets a value indicating whether game masters can enter again before the <see cref="EntryCooldown"/> passed, e.g. for tests.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_GameMastersIgnoreCooldown_Name), Description = nameof(PlugInResources.DungeonConfiguration_GameMastersIgnoreCooldown_Description))]
    public bool GameMastersIgnoreCooldown { get; set; } = true;

    /// <summary>
    /// Gets or sets the x coordinate where the players appear when they enter the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_EntryX_Name), Description = nameof(PlugInResources.DungeonConfiguration_EntryX_Description))]
    public byte EntryX { get; set; } = 165;

    /// <summary>
    /// Gets or sets the y coordinate where the players appear when they enter the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_EntryY_Name))]
    public byte EntryY { get; set; } = 41;

    /// <summary>
    /// Gets or sets a value indicating whether the safezone of the map can't be entered in the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BlockSafezone_Name), Description = nameof(PlugInResources.DungeonConfiguration_BlockSafezone_Description))]
    public bool BlockSafezone { get; set; } = true;

    /// <summary>
    /// Gets or sets the multiplier for the health of the monsters of the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterHealthMultiplier_Name))]
    public float MonsterHealthMultiplier { get; set; } = 3;

    /// <summary>
    /// Gets or sets the multiplier for the damage of the monsters of the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterDamageMultiplier_Name))]
    public float MonsterDamageMultiplier { get; set; } = 3;

    /// <summary>
    /// Gets or sets the multiplier for the defense of the monsters of the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterDefenseMultiplier_Name))]
    public float MonsterDefenseMultiplier { get; set; } = 3;

    /// <summary>
    /// Gets or sets the multiplier for the chance of the monsters of the dungeon to drop an excellent item.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExcellentDropChanceMultiplier_Name), Description = nameof(PlugInResources.DungeonConfiguration_ExcellentDropChanceMultiplier_Description))]
    public float ExcellentDropChanceMultiplier { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum number of excellent options of the excellent items which drop in the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MinimumExcellentOptions_Name))]
    public int MinimumExcellentOptions { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether the usual monsters of the map appear in the dungeon. Without them,
    /// only the spawn areas of the map with the trigger 'Automatic during event' appear, e.g. along the ways between the bosses.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_SpawnMonstersOfMap_Name), Description = nameof(PlugInResources.DungeonConfiguration_SpawnMonstersOfMap_Description))]
    public bool SpawnMonstersOfMap { get; set; }

    /// <summary>
    /// Gets or sets the stages of the dungeon. Each stage ends with a boss, which appears when the boss of the stage before
    /// is dead, the players killed enough monsters of the stage and enough time passed.
    /// </summary>
    /// <remarks>
    /// It's an <see cref="ICollection{T}"/>, so that the admin panel shows it as a table of objects, and its stages
    /// are edited in place in that table (<see cref="MemberOfAggregateAttribute"/> and <see cref="ScaffoldColumnAttribute"/>),
    /// because they aren't entities of the database which could be edited on their own page.
    /// </remarks>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_Stages_Name), Description = nameof(PlugInResources.DungeonConfiguration_Stages_Description))]
    public ICollection<DungeonStage> Stages { get; set; } = DungeonDefaults.CreateStages();

    /// <summary>
    /// Gets or sets the multiplier for the health of the bosses of the stages.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossHealthMultiplier_Name))]
    public float BossHealthMultiplier { get; set; } = 10;

    /// <summary>
    /// Gets or sets the multiplier for the damage of the bosses of the stages.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDamageMultiplier_Name))]
    public float BossDamageMultiplier { get; set; } = 5;

    /// <summary>
    /// Gets or sets the multiplier for the defense of the bosses of the stages.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDefenseMultiplier_Name))]
    public float BossDefenseMultiplier { get; set; } = 5;

    /// <summary>
    /// Gets or sets the number of the magic effect which the client shows on the boss, so that it looks different
    /// from the normal monsters of its kind. The elites.dll of the client draws it with a glow. 0 shows no effect.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossEffectNumber_Name), Description = nameof(PlugInResources.DungeonConfiguration_BossEffectNumber_Description))]
    public short BossEffectNumber { get; set; } = 2;

    /// <summary>
    /// Gets or sets the time the players have to choose need, greed or pass for a dropped item.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_RollDuration_Name))]
    public TimeSpan RollDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the highest number of the dice of a player who chose need. It's higher than <see cref="GreedRollMaximum"/>,
    /// so that need has a better chance than greed.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_NeedRollMaximum_Name), Description = nameof(PlugInResources.DungeonConfiguration_NeedRollMaximum_Description))]
    public int NeedRollMaximum { get; set; } = 200;

    /// <summary>
    /// Gets or sets the highest number of the dice of a player who chose greed.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_GreedRollMaximum_Name))]
    public int GreedRollMaximum { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether all dropped items are rolled, not only the excellent items.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_RollAllItems_Name), Description = nameof(PlugInResources.DungeonConfiguration_RollAllItems_Description))]
    public bool RollAllItems { get; set; }
}
