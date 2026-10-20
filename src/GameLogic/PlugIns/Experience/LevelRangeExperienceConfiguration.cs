// <copyright file="LevelRangeExperienceConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Experience;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration for the <see cref="LevelRangeExperiencePlugIn"/>.
/// </summary>
public class LevelRangeExperienceConfiguration
{
    /// <summary>
    /// Gets or sets the level ranges with their experience multiplier.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LevelRangeExperienceConfiguration_Ranges_Name), Description = nameof(PlugInResources.LevelRangeExperienceConfiguration_Ranges_Description))]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<LevelRange> Ranges { get; set; } = [new LevelRange { MinimumLevel = 300, MaximumLevel = 399, Multiplier = 5 }];

    /// <summary>
    /// A range of character levels with its experience multiplier.
    /// </summary>
    public class LevelRange
    {
        /// <summary>
        /// Gets or sets the lowest character level of the range.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LevelRange_MinimumLevel_Name))]
        public int MinimumLevel { get; set; }

        /// <summary>
        /// Gets or sets the highest character level of the range.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LevelRange_MaximumLevel_Name))]
        public int MaximumLevel { get; set; }

        /// <summary>
        /// Gets or sets the multiplier of the experience which characters of this range gain for a kill.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LevelRange_Multiplier_Name))]
        public float Multiplier { get; set; } = 1;
    }
}
