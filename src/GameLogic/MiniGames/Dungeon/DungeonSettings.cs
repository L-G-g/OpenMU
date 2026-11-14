// <copyright file="DungeonSettings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The settings of one dungeon of the <see cref="DungeonConfiguration"/>: an instance of a hunting map, which a party
/// enters at an NPC in its normal or in its extreme version. The stages of the dungeon are the
/// <see cref="DungeonConfiguration.Stages"/> with its <see cref="Number"/>.
/// </summary>
public class DungeonSettings
{
    /// <summary>
    /// Gets or sets the number of the dungeon, to which its stages refer (<see cref="DungeonStage.Dungeon"/>).
    /// Each number has its own time of the last entry of a character.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_Number_Name), Description = nameof(PlugInResources.DungeonSettings_Number_Description))]
    public byte Number { get; set; }

    /// <summary>
    /// Gets or sets the name of the dungeon, which the players see in the messages. Without accents, because the game client doesn't show them.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_Name_Name))]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the designation of the NPC at which the party leader enters the normal version of the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_NpcDesignation_Name), Description = nameof(PlugInResources.DungeonSettings_NpcDesignation_Description))]
    public string NpcDesignation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the designation of the NPC at which the party leader enters the extreme version of the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_ExtremeNpcDesignation_Name))]
    public string ExtremeNpcDesignation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the game level of the <see cref="MiniGameDefinition"/> of the normal version, which has the type <see cref="MiniGameType.Dungeon"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_GameLevel_Name), Description = nameof(PlugInResources.DungeonSettings_GameLevel_Description))]
    public byte GameLevel { get; set; }

    /// <summary>
    /// Gets or sets the game level of the <see cref="MiniGameDefinition"/> of the extreme version.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_ExtremeGameLevel_Name))]
    public byte ExtremeGameLevel { get; set; }

    /// <summary>
    /// Gets or sets the x coordinate where the players appear when they enter the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_EntryX_Name), Description = nameof(PlugInResources.DungeonConfiguration_EntryX_Description))]
    public byte EntryX { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate where the players appear when they enter the dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_EntryY_Name))]
    public byte EntryY { get; set; }

    /// <summary>
    /// Gets or sets the numbers of the maps whose zones give the excellent weapons, shields and armor sets of the dungeon,
    /// separated by commas, e.g. "0,3,51,2" for Lorencia, Noria, Elbeland and Devias. Without them, the dungeon drops
    /// the items of the zone of the monster and of all zones before it.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_DropMaps_Name), Description = nameof(PlugInResources.DungeonSettings_DropMaps_Description))]
    public string DropMaps { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many times a character can enter this dungeon per entry cooldown of the plugin. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonSettings_EntriesPerDay_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public int EntriesPerDay { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the health of the monsters of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterHealthMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float MonsterHealthMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the damage of the monsters of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterDamageMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float MonsterDamageMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the defense of the monsters of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_MonsterDefenseMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float MonsterDefenseMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the health of the bosses of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossHealthMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float BossHealthMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the damage of the bosses of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDamageMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float BossDamageMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for the defense of the bosses of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_BossDefenseMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float BossDefenseMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the additional multiplier for the health in the extreme version of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeHealthMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float ExtremeHealthMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the additional multiplier for the damage in the extreme version of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeDamageMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float ExtremeDamageMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the additional multiplier for the defense in the extreme version of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeDefenseMultiplier_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public float ExtremeDefenseMultiplier { get; set; }

    /// <summary>
    /// Gets or sets how much more often than a low item a high item drops in the extreme version of this dungeon. 0 uses the general value of the <see cref="DungeonConfiguration"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonConfiguration_ExtremeHighItemWeight_Name), Description = nameof(PlugInResources.DungeonSettings_Override_Description))]
    public double ExtremeHighItemWeight { get; set; }

    /// <summary>
    /// Gets the numbers of the <see cref="DropMaps"/>.
    /// </summary>
    /// <returns>The numbers of the maps.</returns>
    public IReadOnlyList<short> GetDropMapNumbers()
    {
        return this.DropMaps
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => short.TryParse(part, out var number) ? number : (short)-1)
            .Where(number => number >= 0)
            .Distinct()
            .ToList();
    }

    /// <inheritdoc />
    public override string ToString() => $"{this.Number}: {this.Name}";
}
