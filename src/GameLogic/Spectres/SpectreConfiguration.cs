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
    [Range(1, 3)]
    public int MaximumActiveSpectres { get; set; } = 3;

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
}
