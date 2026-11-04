// <copyright file="SpectreConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The configuration of the <see cref="SpectreFeaturePlugIn"/>.
/// </summary>
public class SpectreConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of spectres which a player can have summoned at the same time.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_MaximumActiveSpectres_Name), Description = nameof(PlugInResources.SpectreConfiguration_MaximumActiveSpectres_Description))]
    [Range(1, 4)]
    public int MaximumActiveSpectres { get; set; } = 4;

    /// <summary>
    /// Gets or sets the experience which the summoned spectres of a player share, in percent of the experience
    /// which the player gets for a kill.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_ExperiencePercent_Name), Description = nameof(PlugInResources.SpectreConfiguration_ExperiencePercent_Description))]
    [Range(0, 1000)]
    public int ExperiencePercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets the distance to its owner above which a spectre is warped next to the owner, instead of walking.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_WarpDistance_Name), Description = nameof(PlugInResources.SpectreConfiguration_WarpDistance_Description))]
    [Range(5, 50)]
    public int WarpDistance { get; set; } = 12;

    /// <summary>
    /// Gets or sets a value indicating whether the spectres have unlimited mana.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_UnlimitedMana_Name), Description = nameof(PlugInResources.SpectreConfiguration_UnlimitedMana_Description))]
    public bool UnlimitedMana { get; set; } = true;

    /// <summary>
    /// Gets or sets the designation of the NPC which tells the story of the spectres and lets the players enter the spectre hub.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_GuardianDesignation_Name), Description = nameof(PlugInResources.SpectreConfiguration_GuardianDesignation_Description))]
    public string GuardianDesignation { get; set; } = SpectreHub.GuardianDesignation;

    /// <summary>
    /// Gets or sets the multiplier of the maximum health and shield of the rivals of the spectre hub.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_RivalHealthMultiplier_Name), Description = nameof(PlugInResources.SpectreConfiguration_RivalHealthMultiplier_Description))]
    public float RivalHealthMultiplier { get; set; } = 3;

    /// <summary>
    /// Gets or sets how many victories in the spectre hub (spectres and their evolutions) a player may have per reset:
    /// with 1, one reset allows the first spectre, two resets a second one or the first evolution, and so on. 0 means no limit.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_VictoriesPerReset_Name), Description = nameof(PlugInResources.SpectreConfiguration_VictoriesPerReset_Description))]
    [Range(0, 100)]
    public int VictoriesPerReset { get; set; } = 1;

    /// <summary>
    /// Gets or sets how often a player may win in the spectre hub per day (in the time of Argentina). 0 means no limit.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_MaximumVictoriesPerDay_Name), Description = nameof(PlugInResources.SpectreConfiguration_MaximumVictoriesPerDay_Description))]
    [Range(0, 100)]
    public int MaximumVictoriesPerDay { get; set; } = 3;

    /// <summary>
    /// Gets or sets the names, separated by commas, of which the male spectres get one when their story is finished.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_MaleNames_Name), Description = nameof(PlugInResources.SpectreConfiguration_MaleNames_Description))]
    public string MaleNames { get; set; } = SpectreNames.DefaultMaleNames;

    /// <summary>
    /// Gets or sets the names, separated by commas, of which the female spectres (elf and summoner) get one when their story is finished.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SpectreConfiguration_FemaleNames_Name), Description = nameof(PlugInResources.SpectreConfiguration_FemaleNames_Description))]
    public string FemaleNames { get; set; } = SpectreNames.DefaultFemaleNames;
}
