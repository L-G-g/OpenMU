// <copyright file="SkillQuestsData.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The skill quests of PeroniaMU (see <see cref="SkillQuestFeaturePlugIn"/>): four tiers of quests of the NPCs which
/// teach the skills of each class.
/// </summary>
/// <remarks>
/// The NPCs: Hanzo (knights and magic gladiators), Pasi (wizards, magic gladiators, and the first tier of the summoners),
/// Elf Lala (elves), the Pet Trainer (dark lords), Eo (rage fighters), Rhea (summoners, from the second tier) and the
/// Wandering Merchant Zyro (magic gladiators, from the third tier). A player gets the reward skills which its class can
/// use, and does the tiers in order. The requirements:
/// <list type="number">
///   <item>Kill 30 Budge Dragons, 30 Polluted Butterflies and 30 Chain Scorpions.</item>
///   <item>Level 150; kill 100 Skeleton Warriors (Lorencia), 100 Stone Golems (Noria) and 100 Captain Grizzlies (Elbeland).</item>
///   <item>10 resets (see <see cref="SkillQuestConfiguration.TierRequirements"/>); kill 300 Elite Yetis (Devias) and 300 Dark Knights (Dungeon).</item>
///   <item>25 resets and the first class change; kill 300 Shadows, 300 Bahamuts and 300 Mutants. It teaches the remaining skills.</item>
/// </list>
/// <para>
/// The skills of the quests can only be learned by the quests: their scrolls and orbs don't drop anymore and aren't sold
/// anymore, and the consume handler refuses them. Weapons and shields don't give skills anymore (except the books of the
/// summoners), and new characters don't get skills anymore.
/// </para>
/// </remarks>
internal static class SkillQuestsData
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

    /// <summary>
    /// The quest group of Rhea.
    /// </summary>
    internal const short RheaGroup = SkillQuestGroups.First + 6;

    /// <summary>
    /// The quest group of the Wandering Merchant Zyro.
    /// </summary>
    internal const short ZyroGroup = SkillQuestGroups.First + 7;

    private const byte OrbGroup = 12;
    private const byte ScrollGroup = 15;
    private const short OrbOfSummoningNumber = 11;
    private const byte ShieldGroup = 6;

    private static readonly string[] TierNames = ["I", "II", "III", "IV"];

    private static readonly short[] SummonerBookSkills = [(short)SkillNumber.Explosion223, (short)SkillNumber.Requiem, (short)SkillNumber.Pollution];

    private static readonly Type[] InitialSkillPlugIns =
    [
        typeof(AddEnergyBallForDarkWizard),
        typeof(AddCrescentMoonSlashForDarkKnight),
        typeof(AddStarfallForFairyElf),
        typeof(AddManaRaysForMagicGladiator),
        typeof(AddSpiralSlashForMagicGladiator),
        typeof(AddForceForDarkLord),
        typeof(AddFireBlastForDarkLord),
        typeof(AddLanceForSummoner),
        typeof(AddChargeForRageFighter),
    ];

    /// <summary>
    /// The tiers: the minimum level and the monsters to kill (number of the original monster and amount).
    /// </summary>
    private static readonly (int MinimumLevel, (short Monster, int Amount)[] Kills)[] Tiers =
    [
        (1, [(2, 30), (421, 30), (27, 30)]), // Budge Dragon, Polluted Butterfly, Chain Scorpion
        (150, [(14, 100), (32, 100), (425, 100)]), // Skeleton Warrior, Stone Golem, Captain Grizzly
        (150, [(20, 300), (10, 300)]), // Elite Yeti, Dark Knight
        (150, [(36, 300), (45, 300), (62, 300)]), // Shadow, Bahamut, Mutant
    ];

    /// <summary>
    /// The NPCs and the reward skills of each tier (empty: the NPC has no quest of this tier).
    /// </summary>
    private static readonly (short Group, short NpcNumber, string Name, CharacterClassNumber[] Classes, SkillNumber[][] Skills)[] Quests =
    [
        (HanzoGroup, 251, "Lecciones de Hanzo", [CharacterClassNumber.DarkKnight, CharacterClassNumber.MagicGladiator],
        [
            [SkillNumber.FallingSlash, SkillNumber.Lunge, SkillNumber.Uppercut, SkillNumber.Cyclone, SkillNumber.Slash, SkillNumber.Defense],
            [SkillNumber.TwistingSlash],
            [SkillNumber.RagefulBlow],
            [SkillNumber.Impale, SkillNumber.SwellLife, SkillNumber.DeathStab, SkillNumber.StrikeofDestruction],
        ]),
        (PasiGroup, 254, "Lecciones de Pasi", [CharacterClassNumber.DarkWizard, CharacterClassNumber.MagicGladiator, CharacterClassNumber.Summoner],
        [
            [SkillNumber.FireBall, SkillNumber.Ice, SkillNumber.PowerWave, SkillNumber.Meteorite],
            [SkillNumber.Lightning, SkillNumber.Poison, SkillNumber.Flame, SkillNumber.Twister, SkillNumber.EvilSpirit],
            [SkillNumber.Cometfall, SkillNumber.AquaBeam, SkillNumber.Inferno, SkillNumber.Hellfire],
            [SkillNumber.EnergyBall, SkillNumber.Nova, SkillNumber.IceStorm, SkillNumber.Decay, SkillNumber.Teleport, SkillNumber.SoulBarrier, SkillNumber.TeleportAlly, SkillNumber.ExpansionofWizardry],
        ]),
        (LalaGroup, 242, "Lecciones de Lala", [CharacterClassNumber.FairyElf],
        [
            [SkillNumber.Penetration, SkillNumber.Heal, SkillNumber.SummonGoblin],
            [SkillNumber.TripleShot, SkillNumber.GreaterDefense, SkillNumber.SummonStoneGolem, SkillNumber.SummonAssassin],
            [SkillNumber.GreaterDamage, SkillNumber.SummonEliteYeti, SkillNumber.SummonDarkKnight],
            [SkillNumber.SummonBali, SkillNumber.SummonSoldier, SkillNumber.MultiShot, SkillNumber.IceArrow, SkillNumber.Recovery, SkillNumber.InfinityArrow],
        ]),
        (TrainerGroup, 226, "Lecciones del Entrenador", [CharacterClassNumber.DarkLord],
        [
            [SkillNumber.FallingSlash, SkillNumber.Lunge, SkillNumber.Uppercut, SkillNumber.Cyclone, SkillNumber.Slash, SkillNumber.Defense],
            [SkillNumber.ForceWave, SkillNumber.FireBurst],
            [SkillNumber.Force, SkillNumber.ElectricSpike, SkillNumber.FireScream],
            [SkillNumber.ChaoticDiseier, SkillNumber.IncreaseCriticalDamage, SkillNumber.Summon],
        ]),
        (EoGroup, 243, "Lecciones de Eo", [CharacterClassNumber.RageFighter],
        [
            [SkillNumber.FallingSlash, SkillNumber.KillingBlow, SkillNumber.BeastUppercut, SkillNumber.PhoenixShot],
            [SkillNumber.ChainDrive, SkillNumber.DragonRoar, SkillNumber.IncreaseHealth],
            [SkillNumber.DragonSlasher, SkillNumber.IncreaseBlock],
            [SkillNumber.DarkSide, SkillNumber.IgnoreDefense, SkillNumber.Charge],
        ]),
        (RheaGroup, 416, "Lecciones de Rhea", [CharacterClassNumber.Summoner],
        [
            [],
            [SkillNumber.DrainLife, SkillNumber.ChainLightning],
            [SkillNumber.DamageReflection, SkillNumber.Berserker],
            [SkillNumber.LightningShock, SkillNumber.Sleep, SkillNumber.Weakness, SkillNumber.Innovation],
        ]),
        (ZyroGroup, 568, "Lecciones de Zyro", [CharacterClassNumber.MagicGladiator],
        [
            [],
            [],
            [SkillNumber.PowerSlash],
            [SkillNumber.FireSlash, SkillNumber.GiganticStorm, SkillNumber.FlameStrike],
        ]),
    ];

    /// <summary>
    /// Creates the quests (if they don't exist yet), takes their skills out of the drops and the shops, takes the skills
    /// off the weapons and disables the skills of new characters.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, npcNumber, name, classes, skillsPerTier) in Quests)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == npcNumber && m.ObjectKind != NpcObjectKind.Monster) is not { } npc)
            {
                continue;
            }

            for (var tier = 0; tier < Tiers.Length; tier++)
            {
                var number = (short)(tier + 1);
                if (skillsPerTier[tier].Length == 0)
                {
                    continue;
                }

                foreach (var classNumber in classes)
                {
                    if (gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == (byte)classNumber) is not { } characterClass
                        || npc.Quests.Any(q => q.Group == group && q.Number == number && q.QualifiedCharacter == characterClass))
                    {
                        continue;
                    }

                    if (!CanUseAny(gameConfiguration, characterClass, skillsPerTier[tier]))
                    {
                        continue; // e.g. Rageful Blow of Hanzo for the magic gladiators
                    }

                    CreateQuest(context, gameConfiguration, npc, characterClass, group, number, $"{name} {TierNames[tier]}", skillsPerTier[tier]);
                }
            }
        }

        await RemoveQuestSkillItemsAsync(context, gameConfiguration).ConfigureAwait(false);
        RemoveWeaponSkills(gameConfiguration);
        DisableInitialSkills(context, gameConfiguration);
    }

    /// <summary>
    /// Determines whether a class or one of its next generations can use at least one of the skills.
    /// </summary>
    private static bool CanUseAny(GameConfiguration gameConfiguration, CharacterClass characterClass, SkillNumber[] skills)
    {
        var family = new List<CharacterClass>();
        for (var current = characterClass; current is not null && !family.Contains(current); current = current.NextGenerationClass)
        {
            family.Add(current);
        }

        return gameConfiguration.Skills
            .Where(s => skills.Contains((SkillNumber)s.Number))
            .Any(s => s.QualifiedCharacters.Any(family.Contains));
    }

    /// <summary>
    /// Creates a quest for a class family (the class and its next generations, see <see cref="QuestDefinition.QualifiedCharacter"/>).
    /// </summary>
    private static void CreateQuest(IContext context, GameConfiguration gameConfiguration, MonsterDefinition npc, CharacterClass characterClass, short group, short number, string name, SkillNumber[] skills)
    {
        var tier = Tiers[number - 1];
        var quest = context.CreateNew<QuestDefinition>();
        quest.SetGuid(group, number, characterClass.Number);
        npc.Quests.Add(quest);
        quest.QuestGiver = npc;
        quest.QualifiedCharacter = characterClass;
        quest.Name = name;
        quest.Group = group;
        quest.Number = number;
        quest.StartingNumber = number;
        quest.MinimumCharacterLevel = tier.MinimumLevel;

        foreach (var (monsterNumber, amount) in tier.Kills)
        {
            if (MapLevelLadder.GetOriginalMonster(gameConfiguration, monsterNumber) is not { } monster)
            {
                continue;
            }

            var requirement = context.CreateNew<QuestMonsterKillRequirement>();
            requirement.SetGuid(group, (short)((number * 1000) + monsterNumber), characterClass.Number);
            requirement.Monster = monster;
            requirement.MinimumNumber = amount;
            quest.RequiredMonsterKills.Add(requirement);
        }

        foreach (var skillNumber in skills)
        {
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)skillNumber) is not { } skill)
            {
                continue;
            }

            var reward = context.CreateNew<QuestReward>();
            reward.SetGuid(group, (short)((number * 1000) + (short)skillNumber), characterClass.Number);
            reward.RewardType = QuestRewardType.Skill;
            reward.SkillReward = skill;
            quest.Rewards.Add(reward);
        }
    }

    /// <summary>
    /// Weapons and shields don't give skills anymore (the skills are learned by the quests), except the books of the
    /// summoners.
    /// </summary>
    private static void RemoveWeaponSkills(GameConfiguration gameConfiguration)
    {
        foreach (var item in gameConfiguration.Items)
        {
            if (item.Group <= ShieldGroup && item.Skill is { } skill && !SummonerBookSkills.Contains(skill.Number))
            {
                item.Skill = null;
            }
        }
    }

    /// <summary>
    /// New characters don't get skills anymore: the plugins which add them are disabled.
    /// </summary>
    private static void DisableInitialSkills(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var plugInType in InitialSkillPlugIns)
        {
            if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInType.GUID) is not { } plugInConfiguration)
            {
                plugInConfiguration = context.CreateNew<PlugInConfiguration>();
                plugInConfiguration.SetGuid(plugInType.GUID);
                plugInConfiguration.TypeId = plugInType.GUID;
                gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
            }

            plugInConfiguration.IsActive = false;
        }
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
            if (IsSkillItem(item) && questSkills.Contains(item.Skill!.Number))
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
