// <copyright file="SkillQuestsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the skill quests of PeroniaMU, see <see cref="SkillQuestsData"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C9E1F3A5-2B7D-4C8E-A6F1-3D5B9E7A1C28")]
public class SkillQuestsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Skill Quests";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "NPCs teach the skills of each class by four tiers of quests (plugin 'Skill quests' and quests.dll in the client): Hanzo (knights, magic gladiators), Pasi (wizards, magic gladiators, first tier of summoners), Elf Lala, Pet Trainer, Eo, Rhea (summoners) and the Wandering Merchant Zyro (magic gladiators). Tier 1: kill 30 Budge Dragons, Polluted Butterflies and Chain Scorpions. Tier 2: level 150, 100 Skeleton Warriors, Stone Golems and Captain Grizzlies. Tier 3: 10 resets, 300 Elite Yetis and Dark Knights. Tier 4: 25 resets and the first class change, 300 Shadows, Bahamuts and Mutants. These skills can only be learned by the quests: their scrolls and orbs don't drop and aren't sold anymore. Weapons and shields don't give skills anymore (except the books of the summoners) and new characters don't get skills anymore.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await SkillQuestsData.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}
