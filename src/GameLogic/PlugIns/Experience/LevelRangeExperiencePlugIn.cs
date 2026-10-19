// <copyright file="LevelRangeExperiencePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Experience;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Multiplies the experience which a character gains for a kill by the multiplier of its level range,
/// e.g. to make the levels before a high reset level less tedious.
/// </summary>
/// <remarks>
/// It applies to the normal experience only. The master experience is gained at the maximum level, where
/// the character can't level up anymore.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.LevelRangeExperiencePlugIn_Name), Description = nameof(PlugInResources.LevelRangeExperiencePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("BB2FC7E6-D26C-42E6-850C-0E0FF5F2980C")]
public class LevelRangeExperiencePlugIn : IExperienceCalculationPlugIn, ISupportCustomConfiguration<LevelRangeExperienceConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public LevelRangeExperienceConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new LevelRangeExperienceConfiguration();
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        if (args.IsMasterExperience || this.Configuration is not { } configuration)
        {
            return ValueTask.CompletedTask;
        }

        var level = player.Level;
        if (configuration.Ranges.FirstOrDefault(range => range.MinimumLevel <= level && level <= range.MaximumLevel) is { } matchingRange)
        {
            args.Experience *= matchingRange.Multiplier;
        }

        return ValueTask.CompletedTask;
    }
}
