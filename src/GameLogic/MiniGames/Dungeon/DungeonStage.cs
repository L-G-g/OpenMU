// <copyright file="DungeonStage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// A stage of a dungeon, which ends with a boss. The boss appears when the boss of the stage before is dead,
/// the players killed <see cref="KillCount"/> monsters with the number <see cref="MonsterNumber"/>
/// (and <see cref="SecondKillCount"/> monsters with the <see cref="SecondMonsterNumber"/>, if the stage has two kinds of monsters)
/// and <see cref="MinimumTime"/> passed since the start of the dungeon. Escorts can appear together with the boss.
/// </summary>
public class DungeonStage
{
    /// <summary>
    /// Gets or sets the number of the dungeon of this stage, see <see cref="DungeonSettings.Number"/>.
    /// The stages of a dungeon follow each other in the order of the list.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_Dungeon_Name), Description = nameof(PlugInResources.DungeonStage_Dungeon_Description))]
    public byte Dungeon { get; set; } = 1;

    /// <summary>
    /// Gets or sets the designation of the monster definition of the boss.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_BossDesignation_Name))]
    public string BossDesignation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the x coordinate where the boss appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_BossX_Name))]
    public byte BossX { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate where the boss appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_BossY_Name))]
    public byte BossY { get; set; }

    /// <summary>
    /// Gets or sets the number of the monsters which the players have to kill, e.g. 24 for the Worm.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_MonsterNumber_Name), Description = nameof(PlugInResources.DungeonStage_MonsterNumber_Description))]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets how many of the monsters the players have to kill.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_KillCount_Name))]
    public int KillCount { get; set; }

    /// <summary>
    /// Gets or sets the number of a second kind of monsters which the players have to kill, too. 0 if the stage has only one kind.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_SecondMonsterNumber_Name), Description = nameof(PlugInResources.DungeonStage_SecondMonsterNumber_Description))]
    public short SecondMonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets how many of the second kind of monsters the players have to kill.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_SecondKillCount_Name))]
    public int SecondKillCount { get; set; }

    /// <summary>
    /// Gets or sets the time after the start of the dungeon before which the boss doesn't appear.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_MinimumTime_Name))]
    public TimeSpan MinimumTime { get; set; }

    /// <summary>
    /// Gets or sets the chance of the boss to drop an excellent item, from 0 to 1.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_ExcellentDropChance_Name))]
    public double ExcellentDropChance { get; set; } = 1;

    /// <summary>
    /// Gets or sets the designation of the monster definition of the escorts, which appear around the boss. Empty if it comes alone.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_EscortDesignation_Name), Description = nameof(PlugInResources.DungeonStage_EscortDesignation_Description))]
    public string EscortDesignation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many escorts appear around the boss.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_EscortCount_Name))]
    public int EscortCount { get; set; }

    /// <summary>
    /// Gets or sets the chance of each escort to drop an excellent item, from 0 to 1.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_EscortExcellentDropChance_Name))]
    public double EscortExcellentDropChance { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the golden message which tells the players what to do in this stage.
    /// It's in Spanish without accents, because the game client doesn't show them.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_Hint_Name))]
    public string Hint { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => $"{this.Dungeon}: {this.BossDesignation} ({this.BossX}, {this.BossY})";
}
