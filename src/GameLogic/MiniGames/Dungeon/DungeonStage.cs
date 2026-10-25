// <copyright file="DungeonStage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// A stage of the dungeon, which ends with a boss. The boss appears when the boss of the stage before is dead,
/// the players killed <see cref="KillCount"/> monsters with the number <see cref="MonsterNumber"/>
/// and <see cref="MinimumTime"/> passed since the start of the dungeon.
/// </summary>
public class DungeonStage
{
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
    /// Gets or sets the golden message which tells the players what to do in this stage.
    /// It's in Spanish without accents, because the game client doesn't show them.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DungeonStage_Hint_Name))]
    public string Hint { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => $"{this.BossDesignation} ({this.BossX}, {this.BossY})";
}
