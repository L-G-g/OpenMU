// <copyright file="DungeonConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// The configuration of the dungeons, see <see cref="DungeonFeaturePlugIn"/>.
/// </summary>
public class DungeonConfiguration
{
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
    /// Gets or sets the dungeons. Each one is an instance of the entrance map of its mini game definitions,
    /// with a normal and an extreme version.
    /// </summary>
    /// <remarks>
    /// It's an <see cref="ICollection{T}"/>, so that the admin panel shows it as a table of objects, and its rows
    /// are edited in place in that table (<see cref="MemberOfAggregateAttribute"/> and <see cref="ScaffoldColumnAttribute"/>),
    /// because they aren't entities of the database which could be edited on their own page.
    /// </remarks>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_Dungeons_Name), Description = nameof(PlugInResources.DungeonConfiguration_Dungeons_Description))]
    public ICollection<DungeonSettings> Dungeons { get; set; } = DungeonDefaults.CreateDungeons();

    /// <summary>
    /// Gets or sets the stages of all dungeons, see <see cref="DungeonStage.Dungeon"/>. Each stage ends with a boss, which appears
    /// when the boss of the stage before is dead, the players killed enough monsters of the stage and enough time passed.
    /// </summary>
    /// <remarks>
    /// It's a table in the admin panel, like the <see cref="Dungeons"/>. A configuration which was saved when there was only
    /// the dungeon of Devias has just its stages; the update which adds the other dungeons adds their stages.
    /// </remarks>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_Stages_Name), Description = nameof(PlugInResources.DungeonConfiguration_Stages_Description))]
    public ICollection<DungeonStage> Stages { get; set; } = DungeonDefaults.CreateStages();

    /// <summary>
    /// Gets or sets the additional multiplier for the health of the monsters and bosses in the extreme version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeHealthMultiplier_Name), Description = nameof(PlugInResources.DungeonConfiguration_ExtremeMultiplier_Description))]
    public float ExtremeHealthMultiplier { get; set; } = 2;

    /// <summary>
    /// Gets or sets the additional multiplier for the damage of the monsters and bosses in the extreme version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeDamageMultiplier_Name), Description = nameof(PlugInResources.DungeonConfiguration_ExtremeMultiplier_Description))]
    public float ExtremeDamageMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets the additional multiplier for the defense of the monsters and bosses in the extreme version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeDefenseMultiplier_Name), Description = nameof(PlugInResources.DungeonConfiguration_ExtremeMultiplier_Description))]
    public float ExtremeDefenseMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets how much more often than a low item a high item drops in the normal version of a dungeon.
    /// The weapons, shields and armor sets of the <see cref="DungeonSettings.DropMaps"/> are split per character class
    /// into a low and a high half; with an odd number, the low half gets one more.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_HighItemWeight_Name), Description = nameof(PlugInResources.DungeonConfiguration_HighItemWeight_Description))]
    public double HighItemWeight { get; set; } = 1;

    /// <summary>
    /// Gets or sets how much more often than a low item a high item drops in the extreme version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeHighItemWeight_Name), Description = nameof(PlugInResources.DungeonConfiguration_HighItemWeight_Description))]
    public double ExtremeHighItemWeight { get; set; } = 3;

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

    /// <summary>
    /// Gets the dungeon and its version which belong to a game level of the mini game definitions.
    /// </summary>
    /// <param name="gameLevel">The game level.</param>
    /// <returns>The dungeon and whether it's the extreme version; or <c>null</c>, if no dungeon has the game level.</returns>
    public (DungeonSettings Dungeon, bool IsExtreme)? FindByGameLevel(int gameLevel)
    {
        foreach (var dungeon in this.Dungeons)
        {
            if (dungeon.GameLevel == gameLevel)
            {
                return (dungeon, false);
            }

            if (dungeon.ExtremeGameLevel == gameLevel)
            {
                return (dungeon, true);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the dungeon and its version which a party enters at an NPC.
    /// </summary>
    /// <param name="npcDesignation">The designation of the NPC.</param>
    /// <returns>The dungeon and whether it's the extreme version; or <c>null</c>, if the NPC doesn't belong to a dungeon.</returns>
    public (DungeonSettings Dungeon, bool IsExtreme)? FindByNpc(string? npcDesignation)
    {
        if (string.IsNullOrEmpty(npcDesignation))
        {
            return null;
        }

        foreach (var dungeon in this.Dungeons)
        {
            if (dungeon.NpcDesignation == npcDesignation)
            {
                return (dungeon, false);
            }

            if (dungeon.ExtremeNpcDesignation == npcDesignation)
            {
                return (dungeon, true);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the stages of a dungeon, in their order.
    /// </summary>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>The stages.</returns>
    public IReadOnlyList<DungeonStage> GetStages(DungeonSettings dungeon)
    {
        return this.Stages.Where(stage => stage.Dungeon == dungeon.Number).ToList();
    }
}
