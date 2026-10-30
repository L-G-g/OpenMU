// <copyright file="SpectreRivals.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Buffers.Binary;
using System.Security.Cryptography;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The characters of the rivals of the spectre hub. Each owner character has its own rivals, one per kind, in a hidden
/// account per owner account (like the spectres, see <see cref="SpectreFeaturePlugIn.GetLoginName"/>). A rival is created
/// for its tier: level 400, the points of two resets per tier, the equipment of the tier (<see cref="SpectreEvolution.GetRivalEquipment"/>)
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
    internal const int ResetsPerTier = 2;

    /// <summary>
    /// The marker in the key configuration of a rival character, followed by its tier.
    /// </summary>
    private const byte TierMarker = (byte)'R';

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
            BotResetHandler.GetResetConfiguration(gameContext));
        character.Inventory!.Money = 0;
        character.KeyConfiguration = [TierMarker, (byte)tier];
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

    private static int GetTier(Character character)
        => character.KeyConfiguration is [TierMarker, var tier, ..] ? tier : -1;

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
        var equipment = SpectreEvolution.GetRivalEquipment(kind, tier).ToList();
        if (equipment.Any(e => e.Group == ElfWeaponGroup) && kind == SpectreKind.Elf)
        {
            equipment.Add((ArrowsGroup, ArrowsNumber));
        }

        foreach (var (group, number) in equipment)
        {
            if (configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } definition
                || definition.ItemSlot?.ItemSlots is not { Count: > 0 } slots
                || !definition.QualifiedCharacters.Contains(character.CharacterClass!))
            {
                // E.g. the gladiator has no helm.
                continue;
            }

            var freeSlots = slots.Select(s => (byte)s).Where(s => inventory.Items.All(i => i.ItemSlot != s)).ToList();
            if (freeSlots.Count == 0)
            {
                continue;
            }

            var slot = freeSlots[0];

            var itemLevel = (byte)(group == ArrowsGroup && number == ArrowsNumber ? 0 : level);
            inventory.Items.Add(SpectreGrowth.CreateItem(context, definition, itemLevel, slot));
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
