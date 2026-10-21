// <copyright file="StrongGoldenMobsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes the golden invasion monsters stronger and lets them always drop a Box of Kundun which always gives an item, see <see cref="StrongGoldenMobs"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.StrongGoldenMobsUpdatePlugIn_Name), Description = nameof(PlugInResources.StrongGoldenMobsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("83431E65-688F-45FB-BD4F-FC1A521C9C47")]
public class StrongGoldenMobsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Strong Golden Invasion Mobs";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The monsters of the golden invasion get the health, damage and defense of the classic monster table (2 to 10 times stronger, the level, attack rate and defense rate stay as they are). Each of them always drops its Box of Kundun: +1 for the Golden Budge Dragon (instead of the Box of Luck), Golden Goblin and Golden Soldier, +2 for the Golden Titan and Golden Vepar, +3 for the Golden Wheel and Golden Dragon, +4 for the Golden Lizard King and +5 for the Golden Tantallos. The Box of Kundun+1 to +5 always gives an excellent item instead of money.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        StrongGoldenMobs.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
