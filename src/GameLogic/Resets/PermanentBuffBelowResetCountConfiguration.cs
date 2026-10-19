// <copyright file="PermanentBuffBelowResetCountConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Configuration for the <see cref="PermanentBuffBelowResetCountPlugIn"/>.
/// </summary>
public class PermanentBuffBelowResetCountConfiguration
{
    /// <summary>
    /// Gets or sets the number of the magic effect which the characters get, e.g. 3 for the buff of the Elf Soldier.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PermanentBuffBelowResetCountConfiguration_EffectNumber_Name), Description = nameof(PlugInResources.PermanentBuffBelowResetCountConfiguration_EffectNumber_Description))]
    public short EffectNumber { get; set; } = 3;

    /// <summary>
    /// Gets or sets the reset count at which the characters lose the buff. Characters with less resets have it.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PermanentBuffBelowResetCountConfiguration_ResetCount_Name), Description = nameof(PlugInResources.PermanentBuffBelowResetCountConfiguration_ResetCount_Description))]
    public int ResetCount { get; set; } = 10;
}
