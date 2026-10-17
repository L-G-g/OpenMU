// <copyright file="PreLearnedSkillsAndEventItemShopUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Changes the data for a game in which the characters know their skills from the start, and buy the items
/// to enter the events at Lumen the Barmaid instead of finding them.
/// See <see cref="PreLearnedSkillsAndEventItemShop"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// The skills are added to new characters by the plugin "Add Learnable Skills For New Characters", which has to be activated separately.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.PreLearnedSkillsAndEventItemShopUpdatePlugIn_Name), Description = nameof(PlugInResources.PreLearnedSkillsAndEventItemShopUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E6B400BD-AF97-4EA7-A5D3-FBD53FA8EC9B")]
public class PreLearnedSkillsAndEventItemShopUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Pre-Learned Skills And Event Items At Lumen";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "The requirements of the skill items (scrolls, orbs, parchments) become requirements of their skills, and the skill items don't drop from monsters anymore. The items to enter Blood Castle, Devil Square, Illusion Temple, Kalima, Doppelganger and Imperial Guardian don't drop from monsters anymore, Lumen the Barmaid in Lorencia sells all of them (adds Old Scroll +6, Illusion Sorcerer Covenant +6, Lost Map +1 to +7, Mirror of Dimensions and Gaion's Order). Activate the plugin 'Add Learnable Skills For New Characters' to give new characters all skills of their class.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        PreLearnedSkillsAndEventItemShop.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
