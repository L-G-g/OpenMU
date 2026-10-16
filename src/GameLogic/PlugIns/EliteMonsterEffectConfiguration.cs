// <copyright file="EliteMonsterEffectConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="EliteMonsterEffectPlugIn"/>.
/// </summary>
public class EliteMonsterEffectConfiguration
{
    /// <summary>
    /// Gets or sets the suffix of the designation of the elite monsters.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.EliteMonsterEffectConfiguration_DesignationSuffix_Name), Description = nameof(PlugInResources.EliteMonsterEffectConfiguration_DesignationSuffix_Description))]
    public string DesignationSuffix { get; set; } = "Elite";

    /// <summary>
    /// Gets or sets the number of the magic effect which the client shows on the elite monsters.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.EliteMonsterEffectConfiguration_EffectNumber_Name), Description = nameof(PlugInResources.EliteMonsterEffectConfiguration_EffectNumber_Description))]
    public short EffectNumber { get; set; } = 1;
}
