// <copyright file="ResetExperienceSchemeUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Applies a scheme of resets and experience, see <see cref="ResetExperienceScheme"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetExperienceSchemeUpdatePlugIn_Name), Description = nameof(PlugInResources.ResetExperienceSchemeUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("EA6E2BD0-A211-4BD3-8F8D-E5F9706107B2")]
public class ResetExperienceSchemeUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Reset Level Ramp And Fixed Experience";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The resets 1 to 10 require level 300, then the required level rises by 10 every 10 resets up to 400 from the 101st reset on (configured as required level tiers of the reset feature, the other reset settings stay as they are). Characters with less than 10 resets have the buff of the Elf Soldier permanently, and the Elf Soldier doesn't give it anymore. Characters of level 300 to 399 gain 5 times the experience. A monster always gives the same experience, because the random experience multipliers are set to 1.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>It sets the random experience multipliers and removes the buff of the Elf Soldier, which these updates add.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddRandomExperienceConfigAttributesPlugInSeason6).GUID, typeof(AddElfSoldierBuffPlugIn).GUID];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ResetExperienceScheme.Apply(context, gameConfiguration);
        await ResetExperienceScheme.RemoveElfSoldierBuffAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}
