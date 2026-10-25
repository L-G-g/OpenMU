// <copyright file="DungeonConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

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
    /// Gets or sets the designation of the monster definition of the boss.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDesignation_Name), Description = nameof(PlugInResources.DungeonConfiguration_BossDesignation_Description))]
    public string BossDesignation { get; set; } = DungeonDefaults.BossDesignation;

    /// <summary>
    /// Gets or sets the time after the start of the dungeon when the boss appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossSpawnDelay_Name))]
    public TimeSpan BossSpawnDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the x coordinate where the boss appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossX_Name))]
    public byte BossX { get; set; } = 128;

    /// <summary>
    /// Gets or sets the y coordinate where the boss appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossY_Name))]
    public byte BossY { get; set; } = 129;

    /// <summary>
    /// Gets or sets the multiplier for the health of the boss, applied to its monster definition.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossHealthMultiplier_Name))]
    public float BossHealthMultiplier { get; set; } = 10;

    /// <summary>
    /// Gets or sets the multiplier for the damage of the boss, applied to its monster definition.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDamageMultiplier_Name))]
    public float BossDamageMultiplier { get; set; } = 5;

    /// <summary>
    /// Gets or sets the multiplier for the defense of the boss, applied to its monster definition.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDefenseMultiplier_Name))]
    public float BossDefenseMultiplier { get; set; } = 5;

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
    /// Gets or sets a value indicating whether all dropped items are rolled, not only the excellent and ancient items and the jewels.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_RollAllItems_Name), Description = nameof(PlugInResources.DungeonConfiguration_RollAllItems_Description))]
    public bool RollAllItems { get; set; }
}
