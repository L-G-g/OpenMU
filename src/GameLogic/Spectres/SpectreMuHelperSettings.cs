// <copyright file="SpectreMuHelperSettings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.GameLogic.MuHelper;

/// <summary>
/// The MU Helper settings which drive the combat AI of a spectre.
/// A spectre can't be hurt, so it never uses potions and never defends itself; it doesn't pick up
/// items either, because the drops of its kills belong to its owner. It buffs and heals itself, its owner
/// and the other spectres of its owner (see <see cref="SpectrePlayer.GetSupportTargets"/>), and doesn't
/// accept any party request. The raven of a lord attacks the monsters around it.
/// </summary>
internal sealed class SpectreMuHelperSettings : IMuHelperSettings
{
    private readonly Player _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreMuHelperSettings"/> class.
    /// </summary>
    /// <param name="player">The spectre or rival which is driven by the settings.</param>
    public SpectreMuHelperSettings(Player player)
    {
        this._player = player;
    }

    /// <inheritdoc />
    public int BasicSkillId => 0;

    /// <inheritdoc />
    public int ActivationSkill1Id => 0;

    /// <inheritdoc />
    public int ActivationSkill2Id => 0;

    /// <inheritdoc />
    public int DelayMinSkill1 => 0;

    /// <inheritdoc />
    public int DelayMinSkill2 => 0;

    /// <inheritdoc />
    public bool Skill1UseTimer => false;

    /// <inheritdoc />
    public bool Skill1UseCondition => false;

    /// <inheritdoc />
    public bool Skill1ConditionAttacking => false;

    /// <inheritdoc />
    public int Skill1SubCondition => 0;

    /// <inheritdoc />
    public bool Skill2UseTimer => false;

    /// <inheritdoc />
    public bool Skill2UseCondition => false;

    /// <inheritdoc />
    public bool Skill2ConditionAttacking => false;

    /// <inheritdoc />
    public int Skill2SubCondition => 0;

    /// <inheritdoc />
    public bool UseCombo => false;

    /// <inheritdoc />
    public int HuntingRange => SpectreIntelligence.HuntingRange;

    /// <inheritdoc />
    public int MaxSecondsAway => 2;

    /// <inheritdoc />
    public bool LongRangeCounterAttack => false;

    /// <inheritdoc />
    public bool ReturnToOriginalPosition => false;

    /// <inheritdoc />
    public int BuffSkill0Id => 0;

    /// <inheritdoc />
    public int BuffSkill1Id => 0;

    /// <inheritdoc />
    public int BuffSkill2Id => 0;

    /// <inheritdoc />
    public bool BuffOnDuration => true;

    /// <inheritdoc />
    public bool BuffDurationForParty => true;

    /// <inheritdoc />
    public int BuffCastIntervalSeconds => 60;

    /// <inheritdoc />
    public bool AutoHeal => true;

    /// <inheritdoc />
    public int HealThresholdPercent => 70;

    /// <inheritdoc />
    public bool UseDrainLife => false;

    /// <inheritdoc />
    public bool UseHealPotion => false;

    /// <inheritdoc />
    public int PotionThresholdPercent => 60;

    /// <inheritdoc />
    public bool SupportParty => true;

    /// <inheritdoc />
    public bool AutoHealParty => true;

    /// <inheritdoc />
    public int HealPartyThresholdPercent => 70;

    /// <inheritdoc />
    public bool UseDarkRaven => this._player.Inventory?.GetItem(InventoryConstants.RightHandSlot)?.Definition is { Group: SpectreEvolution.PetGroup, Number: 5 };

    /// <inheritdoc />
    public int DarkRavenMode => 1;

    /// <inheritdoc />
    public int ObtainRange => 6;

    /// <inheritdoc />
    public bool PickAllItems => false;

    /// <inheritdoc />
    public bool PickSelectItems => false;

    /// <inheritdoc />
    public bool PickJewel => false;

    /// <inheritdoc />
    public bool PickZen => false;

    /// <inheritdoc />
    public bool PickAncient => false;

    /// <inheritdoc />
    public bool PickExcellent => false;

    /// <inheritdoc />
    public bool PickExtraItems => false;

    /// <inheritdoc />
    public IReadOnlyList<string> ExtraItemNames => Array.Empty<string>();

    /// <inheritdoc />
    public bool RepairItem => false;

    /// <inheritdoc />
    public bool UseSelfDefense => false;

    /// <inheritdoc />
    public bool AutoAcceptFriend => false;

    /// <inheritdoc />
    public bool AutoAcceptGuild => false;

    /// <inheritdoc />
    public bool AutoAcceptAnyone => false;

    /// <inheritdoc />
    public bool FallbackBasicAttack => true;

    /// <inheritdoc />
    public bool AutoSelectBestSkill => true;

    /// <inheritdoc />
    public bool AutoSelectBuffs => true;

    /// <inheritdoc />
    public bool UseManaPotion => false;

    /// <inheritdoc />
    public bool OnlyHuntSafeMonsters => false;

    /// <inheritdoc />
    public bool PickUpgradeItems => false;
}
