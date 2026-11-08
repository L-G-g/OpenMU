// <copyright file="SkillQuestsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the skill quests of PeroniaMU, see <see cref="SkillQuestsTier1"/>.
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
    internal const string PlugInDescription = "NPCs teach the skills of each class by quests (plugin 'Skill quests' and quests.dll in the client). Tier 1: kill 100 Budge Dragons, 100 Polluted Butterflies and 100 Chain Scorpions. Hanzo: Twisting Slash (warriors, magic gladiators). Pasi: Fire Ball, Ice, Poison, Power Wave, Lightning, Meteorite (wizards, magic gladiators; summoners get Fire Ball, Ice, Power Wave and Meteorite). Elf Lala: Penetration, Heal, Summon Goblin, Summon Stone Golem. Pet Trainer: Fire Burst. Eo: Chain Drive. These skills can't be learned with items anymore: their scrolls and orbs don't drop and aren't sold anymore.";

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
        await SkillQuestsTier1.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}
