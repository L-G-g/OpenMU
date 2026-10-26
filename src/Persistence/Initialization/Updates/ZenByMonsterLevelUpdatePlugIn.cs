// <copyright file="ZenByMonsterLevelUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the monsters drop money by their level and makes the excellent option for more money additive, see <see cref="ZenByMonsterLevel"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ZenByMonsterLevelUpdatePlugIn_Name), Description = nameof(PlugInResources.ZenByMonsterLevelUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A25A2FAC-84E5-4A5F-8A5D-B6F07BB8C13A")]
public class ZenByMonsterLevelUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Zen By Monster Level";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Monsters drop money by their level (0.019 × level ^ 3.5, e.g. 749,000 zen at level 148 and 16,800 at level 50) instead of by the experience of the kill, with the plugin \"Money drop by monster level\", whose numbers can be changed later. The excellent option for more money adds 40 % per item instead of multiplying the money by 1.4 per item: five items give 3 times the money.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ZenByMonsterLevel.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
