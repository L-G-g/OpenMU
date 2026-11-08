// <copyright file="SkillQuestsTier1.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// The first tier of the skill quests of PeroniaMU (see <see cref="SkillQuestFeaturePlugIn"/>).
/// </summary>
/// <remarks>
/// Every class has an NPC which teaches its skills: Hanzo (warriors and magic gladiators), Pasi (wizards, summoners and
/// magic gladiators), Elf Lala (elves), the Pet Trainer (dark lords) and Eo (rage fighters). The first quest of each one
/// requires 100 Budge Dragons, 100 Polluted Butterflies and 100 Chain Scorpions. A player gets the reward skills which
/// its class can use.
/// <para>
/// The skills of the quests can only be learned by the quests: their scrolls and orbs don't drop anymore and aren't
/// sold anymore. The Orb of Summoning still drops, because its other levels teach other summons; using it for a summon
/// of a quest is refused by the consume handler.
/// </para>
/// </remarks>
internal static class SkillQuestsTier1
{
    /// <summary>
    /// The quest group of Hanzo the Blacksmith.
    /// </summary>
    internal const short HanzoGroup = SkillQuestGroups.First + 1;

    /// <summary>
    /// The quest group of Pasi the Mage.
    /// </summary>
    internal const short PasiGroup = SkillQuestGroups.First + 2;

    /// <summary>
    /// The quest group of Elf Lala.
    /// </summary>
    internal const short LalaGroup = SkillQuestGroups.First + 3;

    /// <summary>
    /// The quest group of the Pet Trainer.
    /// </summary>
    internal const short TrainerGroup = SkillQuestGroups.First + 4;

    /// <summary>
    /// The quest group of Eo the Craftsman.
    /// </summary>
    internal const short EoGroup = SkillQuestGroups.First + 5;

    private const short BudgeDragon = 2;
    private const short PollutedButterfly = 421;
    private const short ChainScorpion = 27;
    private const int KillsPerMonster = 100;

    private const byte OrbGroup = 12;
    private const byte ScrollGroup = 15;
    private const short OrbOfSummoningNumber = 11;

    private static readonly (short Group, short NpcNumber, string Name, SkillNumber[] Skills)[] Quests =
    [
        (HanzoGroup, 251, "Lecciones de Hanzo I", [SkillNumber.TwistingSlash]),
        (PasiGroup, 254, "Lecciones de Pasi I", [SkillNumber.FireBall, SkillNumber.Ice, SkillNumber.Poison, SkillNumber.PowerWave, SkillNumber.Lightning, SkillNumber.Meteorite]),
        (LalaGroup, 242, "Lecciones de Lala I", [SkillNumber.Penetration, SkillNumber.Heal, SkillNumber.SummonGoblin, SkillNumber.SummonStoneGolem]),
        (TrainerGroup, 226, "Lecciones del Entrenador I", [SkillNumber.FireBurst]),
        (EoGroup, 243, "Lecciones de Eo I", [SkillNumber.ChainDrive]),
    ];

    /// <summary>
    /// Creates the quests (if they don't exist yet) and takes their skills out of the drops and the shops.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var requiredMonsters = new[] { BudgeDragon, PollutedButterfly, ChainScorpion }
            .Select(number => MapLevelLadder.GetOriginalMonster(gameConfiguration, number))
            .OfType<MonsterDefinition>()
            .ToList();

        foreach (var (group, npcNumber, name, skills) in Quests)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == npcNumber && m.ObjectKind != NpcObjectKind.Monster) is not { } npc
                || npc.Quests.Any(q => q.Group == group && q.Number == 1))
            {
                continue;
            }

            var quest = context.CreateNew<QuestDefinition>();
            quest.SetGuid(group, 1, 0);
            npc.Quests.Add(quest);
            quest.QuestGiver = npc;
            quest.Name = name;
            quest.Group = group;
            quest.Number = 1;
            quest.StartingNumber = 1;
            quest.MinimumCharacterLevel = 1;

            foreach (var monster in requiredMonsters)
            {
                var requirement = context.CreateNew<QuestMonsterKillRequirement>();
                requirement.SetGuid(group, monster.Number);
                requirement.Monster = monster;
                requirement.MinimumNumber = KillsPerMonster;
                quest.RequiredMonsterKills.Add(requirement);
            }

            foreach (var skillNumber in skills)
            {
                if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)skillNumber) is not { } skill)
                {
                    continue;
                }

                var reward = context.CreateNew<QuestReward>();
                reward.SetGuid(group, (short)skillNumber);
                reward.RewardType = QuestRewardType.Skill;
                reward.SkillReward = skill;
                quest.Rewards.Add(reward);
            }
        }

        await RemoveQuestSkillItemsAsync(context, gameConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the scrolls and orbs of the skills of the skill quests out of the drops and the shops.
    /// </summary>
    private static async ValueTask RemoveQuestSkillItemsAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var questSkills = gameConfiguration.Monsters
            .SelectMany(m => m.Quests)
            .Where(q => SkillQuestGroups.IsSkillQuestGroup(q.Group))
            .SelectMany(q => q.Rewards)
            .Where(r => r.RewardType == QuestRewardType.Skill && r.SkillReward is not null)
            .Select(r => r.SkillReward!.Number)
            .ToHashSet();

        foreach (var item in gameConfiguration.Items)
        {
            if (IsSkillItem(item) && !IsOrbOfSummoning(item) && questSkills.Contains(item.Skill!.Number))
            {
                item.DropsFromMonsters = false;
            }
        }

        foreach (var store in gameConfiguration.Monsters.Select(m => m.MerchantStore).OfType<ItemStorage>())
        {
            foreach (var storeItem in store.Items.ToList())
            {
                if (storeItem.Definition is not { } definition || !IsSkillItem(definition))
                {
                    continue;
                }

                var skillNumber = IsOrbOfSummoning(definition) ? (short)(definition.Skill!.Number + storeItem.Level) : definition.Skill!.Number;
                if (questSkills.Contains(skillNumber))
                {
                    store.Items.Remove(storeItem);
                    await context.DeleteAsync(storeItem).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool IsSkillItem(ItemDefinition item)
    {
        return item.Skill is not null && item.Group is OrbGroup or ScrollGroup;
    }

    private static bool IsOrbOfSummoning(ItemDefinition item)
    {
        return item.Group == OrbGroup && item.Number == OrbOfSummoningNumber;
    }
}
