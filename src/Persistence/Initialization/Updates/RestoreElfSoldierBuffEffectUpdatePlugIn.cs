// <copyright file="RestoreElfSoldierBuffEffectUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Creates the effect of the buff of the Elf Soldier again where it's missing. The first version of the
/// <see cref="ResetExperienceSchemeUpdatePlugIn"/> deleted it together with the buff of the Elf Soldier,
/// so the characters below 10 resets got no buff at all.
/// </summary>
/// <remarks>
/// It's mandatory, because it repairs data. Where the effect exists, it doesn't change anything.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.RestoreElfSoldierBuffEffectUpdatePlugIn_Name), Description = nameof(PlugInResources.RestoreElfSoldierBuffEffectUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6C2F0B7E-3D1A-4E58-9B64-2A7D5C8E1F03")]
public class RestoreElfSoldierBuffEffectUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Restore Elf Soldier Buff Effect";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Creates the effect of the buff of the Elf Soldier again if it's missing. The first version of the update \"Reset Level Ramp And Fixed Experience\" deleted it, so the characters below 10 resets didn't get their permanent buff.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ResetExperienceScheme.RestoreElfSoldierBuffEffect(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
