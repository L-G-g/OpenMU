// <copyright file="SpectreRivals.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Buffers.Binary;
using System.Security.Cryptography;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The characters of the rivals of the spectre hub. Each owner character has its own rivals, one per kind, in a hidden
/// account per owner account (like the spectres, see <see cref="SpectreFeaturePlugIn.GetLoginName"/>). A rival is created
/// for its tier: level 400, the points of seven resets per tier (spent like the damage builds of the bots, but without their cap of the vitality), the equipment of the tier (<see cref="SpectreEvolution.GetRivalEquipment"/>)
/// and the skills of the tier. When the tier changes, the character is created again.
/// </summary>
internal static class SpectreRivals
{
    /// <summary>
    /// The prefix of the login names of the hidden accounts of the rivals.
    /// </summary>
    internal const string LoginPrefix = "sr";

    /// <summary>
    /// The level of the rivals.
    /// </summary>
    internal const int Level = 400;

    /// <summary>
    /// The number of resets whose points a rival gets per tier.
    /// </summary>
    internal const int ResetsPerTier = 7;

    /// <summary>
    /// The marker in the key configuration of a rival character, followed by its tier.
    /// </summary>
    private const byte TierMarker = (byte)'R';

    /// <summary>
    /// The version of the rules which created the rivals, after the tier. Rivals of older rules are created again.
    /// </summary>
    private const byte RulesVersion = 4;

    /// <summary>
    /// The first byte of the key configuration of a rival of the story mode, see <see cref="GetStoryKey"/>.
    /// </summary>
    private const byte StoryMarker = (byte)'S';

    /// <summary>
    /// The version of the rules by which the rivals of the story mode are created.
    /// </summary>
    private const byte StoryRulesVersion = 1;

    /// <summary>
    /// The first character slot of the rivals of the story mode, above the slots of the rivals of the hub.
    /// </summary>
    private const byte StorySlotOffset = 200;

    /// <summary>
    /// The first character slot of the actors of the scenes of the story mode, above the rivals of the story mode.
    /// </summary>
    private const byte SceneSlotOffset = 220;

    /// <summary>
    /// The number of slots for actors of scenes per owner character.
    /// </summary>
    private const int SceneActorsPerOwner = 6;

    /// <summary>
    /// The first byte of the key configuration of an actor of a scene of the story mode.
    /// </summary>
    private const byte SceneMarker = (byte)'A';

    /// <summary>
    /// The version of the rules by which the actors of the scenes are created.
    /// </summary>
    private const byte SceneRulesVersion = 1;

    /// <summary>
    /// The build variant of the stats of the rivals: the damage build, e.g. the archer elf instead of the support elf.
    /// </summary>
    private const int DamageBuildVariant = 0;

    private const byte ArrowsGroup = 4;
    private const short ArrowsNumber = 15;
    private const short ElfWeaponGroup = 4;

    /// <summary>
    /// Gets the login name of the hidden account of the rivals of an account.
    /// </summary>
    /// <param name="ownerAccount">The account of the owner.</param>
    /// <returns>The login name.</returns>
    internal static string GetLoginName(Account ownerAccount) => LoginPrefix + GetHash(ownerAccount.GetId());

    /// <summary>
    /// Gets the character of the rival of a kind at a tier, and creates it if necessary.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The name of the character, or <c>null</c> if it couldn't be created.</returns>
    internal static async ValueTask<string?> GetOrCreateCharacterAsync(Player owner, SpectreKind kind, int tier)
    {
        var gameContext = owner.GameContext;
        var ownerAccount = owner.Account!;
        var ownerCharacter = owner.SelectedCharacter!;
        var loginName = GetLoginName(ownerAccount);
        var slot = SpectreClasses.GetCharacterSlot(ownerCharacter.CharacterSlot, kind);

        using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
        if (account is null)
        {
            account = context.CreateNew<Account>();
            account.LoginName = loginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 4);
            account.EMail = ownerAccount.LoginName;
            account.Vault = context.CreateNew<ItemStorage>();
        }
        else if (account.EMail != ownerAccount.LoginName)
        {
            owner.Logger.LogError("The rival account {LoginName} belongs to {Other}, not to {Owner}.", loginName, account.EMail, ownerAccount.LoginName);
            return null;
        }
        else if (account.Characters.FirstOrDefault(c => c.CharacterSlot == slot) is { } existing)
        {
            if (GetTier(existing) == tier)
            {
                return existing.Name;
            }

            account.Characters.Remove(existing);
            await context.DeleteAsync(existing).ConfigureAwait(false);
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return null;
            }
        }
        else
        {
            // The rival is created below.
        }

        var characterClass = gameContext.Configuration.CharacterClasses.FirstOrDefault(c => c.Number == SpectreClasses.GetClassNumber(kind));
        if (characterClass is null)
        {
            owner.Logger.LogError("The character class of the rival {Kind} doesn't exist.", kind);
            return null;
        }

        var name = $"R{GetHash(ownerCharacter.Id)}{SpectreClasses.GetLetter(kind)}";
        var generator = new BotGenerator(gameContext, owner.Logger);
        var character = generator.CreateCharacter(
            context,
            account,
            name,
            characterClass,
            Level,
            slot,
            gameContext.ExperienceTable,
            ResetsPerTier * tier,
            0,
            false,
            BotResetHandler.GetResetConfiguration(gameContext),
            capVitality: false,
            buildVariant: DamageBuildVariant);
        character.Inventory!.Money = 0;
        character.KeyConfiguration = [TierMarker, (byte)tier, RulesVersion];
        await ClearInventoryAsync(context, character).ConfigureAwait(false);

        // First the class and the pets, then the equipment for the class in the remaining slots.
        await SpectreGrowth.ApplyAsync(context, gameContext.Configuration, character, kind, tier).ConfigureAwait(false);
        Equip(context, gameContext.Configuration, character, kind, tier);
        if (!await context.SaveChangesAsync().ConfigureAwait(false))
        {
            return null;
        }

        owner.Logger.LogInformation("Created the rival {Kind} E{Tier} of {Owner}.", kind, tier, ownerCharacter.Name);
        return name;
    }

    /// <summary>
    /// Gets the name of the character of the rival of a duel of the story mode, and creates it if it doesn't exist yet or
    /// if the duel changed (e.g. its level by the configuration of the story mode).
    /// </summary>
    /// <param name="owner">The player.</param>
    /// <param name="duel">The duel.</param>
    /// <returns>The name of the character, or <c>null</c> if it couldn't be created.</returns>
    internal static ValueTask<string?> GetOrCreateStoryCharacterAsync(Player owner, StoryDuel duel)
    {
        var ownerCharacter = owner.SelectedCharacter!;
        return GetOrCreateStoryCharacterAsync(
            owner,
            (byte)(StorySlotOffset + ownerCharacter.CharacterSlot),
            $"S{GetHash(ownerCharacter.Id)}{SpectreClasses.GetLetter(duel.Kind)}",
            GetStoryKey(duel),
            duel.Kind,
            duel.Level,
            duel.Resets,
            duel.SkillTier,
            duel.ItemLevel,
            duel.Equipment,
            duel.RivalName);
    }

    /// <summary>
    /// Gets the name of the character of an actor of a scene of the story mode (see <see cref="Story.StorySceneContext"/>),
    /// and creates it if it doesn't exist yet or if the actor changed. The actors never fight: they only need their class
    /// and their equipment.
    /// </summary>
    /// <param name="owner">The player who watches the scene.</param>
    /// <param name="index">The index of the actor in its scene, 0 to <see cref="SceneActorsPerOwner"/> - 1.</param>
    /// <param name="name">The name of the actor, for the log.</param>
    /// <param name="kind">The kind, which is the class of the actor.</param>
    /// <param name="itemLevel">The level of the items.</param>
    /// <param name="equipment">The items of the actor, as group and number.</param>
    /// <returns>The name of the character, or <c>null</c> if it couldn't be created.</returns>
    internal static ValueTask<string?> GetOrCreateSceneActorAsync(Player owner, int index, string name, SpectreKind kind, byte itemLevel, IReadOnlyList<(byte Group, short Number)> equipment)
    {
        var ownerCharacter = owner.SelectedCharacter!;
        index = Math.Clamp(index, 0, SceneActorsPerOwner - 1);
        var text = $"{SceneRulesVersion}|{name}|{kind}|{itemLevel}|{string.Join(',', equipment)}";
        byte[] key = [SceneMarker, .. SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))[..8]];
        return GetOrCreateStoryCharacterAsync(
            owner,
            (byte)(SceneSlotOffset + (ownerCharacter.CharacterSlot * SceneActorsPerOwner) + index),
            $"A{GetHash(ownerCharacter.Id)}{index}",
            key,
            kind,
            Level,
            0,
            0,
            itemLevel,
            equipment,
            name);
    }

    private static async ValueTask<string?> GetOrCreateStoryCharacterAsync(Player owner, byte slot, string name, byte[] key, SpectreKind kind, int level, int resets, int skillTier, byte itemLevel, IReadOnlyList<(byte Group, short Number)> equipment, string logName)
    {
        var gameContext = owner.GameContext;
        var ownerAccount = owner.Account!;
        var ownerCharacter = owner.SelectedCharacter!;
        var loginName = GetLoginName(ownerAccount);

        using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
        if (account is null)
        {
            account = context.CreateNew<Account>();
            account.LoginName = loginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 4);
            account.EMail = ownerAccount.LoginName;
            account.Vault = context.CreateNew<ItemStorage>();
        }
        else if (account.EMail != ownerAccount.LoginName)
        {
            owner.Logger.LogError("The rival account {LoginName} belongs to {Other}, not to {Owner}.", loginName, account.EMail, ownerAccount.LoginName);
            return null;
        }
        else if (account.Characters.FirstOrDefault(c => c.CharacterSlot == slot) is { } existing)
        {
            if (existing.KeyConfiguration is { } existingKey && existingKey.AsSpan().SequenceEqual(key))
            {
                return existing.Name;
            }

            account.Characters.Remove(existing);
            await context.DeleteAsync(existing).ConfigureAwait(false);
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return null;
            }
        }
        else
        {
            // The character is created below.
        }

        var characterClass = gameContext.Configuration.CharacterClasses.FirstOrDefault(c => c.Number == SpectreClasses.GetClassNumber(kind));
        if (characterClass is null)
        {
            owner.Logger.LogError("The character class of the story character {Kind} doesn't exist.", kind);
            return null;
        }

        var generator = new BotGenerator(gameContext, owner.Logger);
        var character = generator.CreateCharacter(
            context,
            account,
            name,
            characterClass,
            Math.Clamp(level, 1, Level),
            slot,
            gameContext.ExperienceTable,
            Math.Max(resets, 0),
            0,
            false,
            BotResetHandler.GetResetConfiguration(gameContext),
            capVitality: false,
            buildVariant: DamageBuildVariant);
        character.Inventory!.Money = 0;
        character.KeyConfiguration = key;
        await ClearInventoryAsync(context, character).ConfigureAwait(false);
        await SpectreGrowth.ApplyAsync(context, gameContext.Configuration, character, kind, skillTier).ConfigureAwait(false);
        foreach (var (group, number) in equipment)
        {
            if (gameContext.Configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is { } definition
                && IsQualified(definition, character.CharacterClass!))
            {
                AddToFreeSlot(context, character.Inventory, definition, itemLevel);
            }
        }

        if (!await context.SaveChangesAsync().ConfigureAwait(false))
        {
            return null;
        }

        owner.Logger.LogInformation("Created the story character {Name} of {Owner}.", logName, ownerCharacter.Name);
        return name;
    }

    private static byte[] GetStoryKey(StoryDuel duel)
    {
        // What the character was created from, so that a changed duel creates it again.
        var text = $"{StoryRulesVersion}|{duel.Kind}|{duel.Level}|{duel.Resets}|{duel.SkillTier}|{duel.ItemLevel}|{string.Join(',', duel.Equipment)}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return [StoryMarker, .. hash[..8]];
    }

    private static int GetTier(Character character)
        => character.KeyConfiguration is [TierMarker, var tier, RulesVersion, ..] ? tier : -1;

    private static async ValueTask ClearInventoryAsync(IContext context, Character character)
    {
        // The starter weapon and the potions of a new character.
        var inventory = character.Inventory!;
        foreach (var item in inventory.Items.ToList())
        {
            inventory.Items.Remove(item);
            await context.DeleteAsync(item).ConfigureAwait(false);
        }
    }

    private static void Equip(IContext context, GameConfiguration configuration, Character character, SpectreKind kind, int tier)
    {
        var inventory = character.Inventory!;
        var level = SpectreEvolution.GetRivalItemLevel(tier);
        var excellent = SpectreEvolution.IsRivalEquipmentExcellent(tier);
        foreach (var (group, number) in SpectreEvolution.GetRivalEquipment(kind, tier))
        {
            if (configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is { } definition
                && IsQualified(definition, character.CharacterClass!)
                && AddToFreeSlot(context, inventory, definition, level) is { } item
                && excellent)
            {
                AddAllExcellentOptions(context, item);
            }

            if (kind == SpectreKind.Elf && group == ElfWeaponGroup)
            {
                AddToFreeSlot(context, inventory, configuration.Items.FirstOrDefault(i => i.Group == ArrowsGroup && i.Number == ArrowsNumber), 0);
            }
        }

        // The wings even when the class doesn't qualify for them yet, e.g. the third wings before the master class.
        if (SpectreEvolution.GetRivalWings(kind, tier) is var (wingsGroup, wingsNumber))
        {
            AddToFreeSlot(context, inventory, configuration.Items.FirstOrDefault(i => i.Group == wingsGroup && i.Number == wingsNumber), level);
        }
    }

    private static bool IsQualified(ItemDefinition definition, CharacterClass characterClass)
    {
        // Also the items of the later class generations, e.g. the sets of the Bloody Summoner for a summoner
        // which only evolves at the last tier.
        for (CharacterClass? current = characterClass; current is not null; current = current.NextGenerationClass)
        {
            if (definition.QualifiedCharacters.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    private static Item? AddToFreeSlot(IContext context, ItemStorage inventory, ItemDefinition? definition, byte level)
    {
        // E.g. the gladiator has no helm.
        if (definition?.ItemSlot?.ItemSlots is not { Count: > 0 } slots)
        {
            return null;
        }

        var slot = slots.Select(s => (byte)s).FirstOrDefault(s => inventory.Items.All(i => i.ItemSlot != s), byte.MaxValue);
        if (slot == byte.MaxValue)
        {
            return null;
        }

        var item = SpectreGrowth.CreateItem(context, definition, level, slot);
        inventory.Items.Add(item);
        return item;
    }

    private static void AddAllExcellentOptions(IContext context, Item item)
    {
        var options = item.Definition?.PossibleItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(o => object.Equals(o.OptionType, ItemOptionTypes.Excellent)) ?? [];
        foreach (var option in options)
        {
            var link = context.CreateNew<ItemOptionLink>();
            link.ItemOption = option;
            item.ItemOptions.Add(link);
        }
    }

    private static string GetHash(Guid id)
    {
        // A hash of the whole id, because ids may differ in a few bytes only.
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(id.ToByteArray()));
        var name = new char[8];
        for (var i = 0; i < name.Length; i++)
        {
            name[i] = "0123456789abcdefghijklmnopqrstuvwxyz"[(int)(hash % 36)];
            hash /= 36;
        }

        return new string(name);
    }
}
