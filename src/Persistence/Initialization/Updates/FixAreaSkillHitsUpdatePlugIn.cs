// <copyright file="FixAreaSkillHitsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Fixes area skills which don't hit what the client shows:
/// <list type="bullet">
///   <item><description>
///     Force Wave got the type <see cref="SkillType.AreaSkillAutomaticHits"/> by the <see cref="AddAreaSkillSettingsUpdatePlugIn"/>,
///     but the client sends it as a targeted skill. The targeted skill action then doesn't hit at all.
///   </description></item>
///   <item><description>
///     Flame and Cometfall only hit monsters exactly at the targeted coordinates, while the client shows them,
///     and hits, within a radius of 1.5 coordinates. The hit chance of Flame additionally decreased with
///     the distance between the caster and the target, so that it hardly hit targets which are some steps away.
///   </description></item>
/// </list>
/// The master skills which are based on Flame and Cometfall get the same settings.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.FixAreaSkillHitsUpdatePlugIn_Name), Description = nameof(PlugInResources.FixAreaSkillHitsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6E0C2E43-5B8F-4C7A-9E1D-2F4A8B3C9D71")]
public class FixAreaSkillHitsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Force Wave, Flame and Cometfall hits";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Force Wave hits again, and Flame and Cometfall hit the targets within a radius of 1.5 coordinates around the targeted point, like the client shows it.";

    /// <summary>
    /// The diameter of the area around the targeted point in which Flame and Cometfall hit.
    /// </summary>
    private const float TargetAreaDiameter = 3;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 17, 22, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.ForceWave) is { SkillType: SkillType.AreaSkillAutomaticHits } forceWave)
        {
            forceWave.SkillType = SkillType.DirectHit;
        }

        foreach (var skill in gameConfiguration.Skills)
        {
            if (skill.AreaSkillSettings is not { } settings)
            {
                continue;
            }

            var baseSkill = GetBaseSkill(skill);
            if (baseSkill == SkillNumber.Flame)
            {
                settings.TargetAreaDiameter = TargetAreaDiameter;
                settings.HitChancePerDistanceMultiplier = 1.0f;
            }
            else if (baseSkill == SkillNumber.Cometfall)
            {
                settings.TargetAreaDiameter = TargetAreaDiameter;
            }
        }

        return ValueTask.CompletedTask;
    }

    private static SkillNumber GetBaseSkill(Skill skill)
    {
        var current = skill;
        while (current.MasterDefinition?.ReplacedSkill is { } replacedSkill && replacedSkill != current)
        {
            current = replacedSkill;
        }

        return (SkillNumber)current.Number;
    }
}
