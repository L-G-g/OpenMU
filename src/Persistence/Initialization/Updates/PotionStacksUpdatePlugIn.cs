// <copyright file="PotionStacksUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the potions stack up to 255 pieces and lets the merchants sell full stacks of them, see <see cref="PotionStacks"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.PotionStacksUpdatePlugIn_Name), Description = nameof(PlugInResources.PotionStacksUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B1C2E7A-9D34-4F61-A8E2-3C7D90B4F215")]
public class PotionStacksUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Potion Stacks Of 255";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The potions stack up to 255 pieces instead of 3 or 10: apple, healing, mana, shield and complex potions, antidote, siege potions and the potions of the Halloween and Cherry Blossom events. Every merchant which sells one of them also sells a full stack of 255 pieces of it.";

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
        PotionStacks.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
