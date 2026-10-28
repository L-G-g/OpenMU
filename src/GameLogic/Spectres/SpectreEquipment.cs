// <copyright file="SpectreEquipment.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Moves items between the inventory of a player and the equipment of the player's spectres.
/// </summary>
/// <remarks>
/// The spectre is a character of another account, with a persistence context of its own. So, an item changes
/// its context like in a trade: it's saved by a separate context while it's detached from the old one, and then
/// attached to the new one.
/// </remarks>
internal static class SpectreEquipment
{
    private const byte LastWeaponGroup = 5;

    private const byte ShieldGroup = 6;

    /// <summary>
    /// Describes an item in a short text, e.g. for the spectre window.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The description.</returns>
    public static string Describe(Item item)
    {
        var name = item.Definition?.Name.ValueInNeutralLanguage ?? "?";
        var text = item.Level > 0 ? $"{name} +{item.Level}" : name;
        var excellentOptions = item.ItemOptions.Count(o => object.Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));
        if (excellentOptions > 0)
        {
            text += $" (exc {excellentOptions})";
        }
        else if (item.IsAncient())
        {
            text += " (ancient)";
        }

        return text;
    }

    /// <summary>
    /// Equips an item of the inventory of the owner of the spectre on the spectre. When the slot of the spectre
    /// is already used, the item which was there goes into the inventory of the owner.
    /// Has to run while holding the persistence locks of the spectre and the owner.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <param name="ownerSlot">The slot of the item in the inventory of the owner.</param>
    /// <returns>The message for the owner.</returns>
    public static async ValueTask<string> EquipAsync(SpectrePlayer spectre, byte ownerSlot)
    {
        var owner = spectre.Owner;
        if (owner.Inventory is not { } ownerInventory || spectre.Inventory is not { } spectreInventory)
        {
            return "No se pudo equipar el item.";
        }

        if (ownerSlot < InventoryConstants.EquippableSlotsCount || ownerInventory.GetItem(ownerSlot) is not { Definition: { } definition } item)
        {
            return "No hay un item en ese lugar del inventario.";
        }

        if (definition.ItemSlot is not { ItemSlots.Count: > 0 }
            || !definition.QualifiedCharacters.Contains(spectre.SelectedCharacter!.CharacterClass!))
        {
            return $"Tu espectro {SpectreFeaturePlugIn.GetKindName(spectre.Kind)} no puede usar {Describe(item)}.";
        }

        if (!spectre.CompliesRequirements(item))
        {
            return $"Tu espectro {SpectreFeaturePlugIn.GetKindName(spectre.Kind)} no cumple los requisitos de {Describe(item)}.";
        }

        var slots = GetTargetSlots(definition).Where(s => !definition.ConflictsWithEquippedHands(spectreInventory, s)).ToList();
        if (slots.Count == 0)
        {
            return "Primero quitale el item de la otra mano.";
        }

        var slot = slots.FirstOrDefault(s => spectreInventory.GetItem(s) is null, slots[0]);
        var replaced = spectreInventory.GetItem(slot);

        // The transfer context only updates items which are already in the database, so items which the
        // owner just got (e.g. by a drop) have to be saved first.
        await owner.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
        if (replaced is not null)
        {
            await spectre.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
        }

        using var transferContext = owner.GameContext.PersistenceContextProvider.CreateNewTradeContext();
        transferContext.Attach(item);
        if (replaced is not null)
        {
            transferContext.Attach(replaced);
        }

        await ownerInventory.RemoveItemAsync(item).ConfigureAwait(false);
        if (replaced is not null)
        {
            await spectreInventory.RemoveItemAsync(replaced).ConfigureAwait(false);
        }

        var replacedSlot = replaced is null ? null : ownerInventory.CheckInvSpace(replaced);
        if ((replaced is not null && (replacedSlot is null || !await ownerInventory.AddItemAsync(replacedSlot.Value, replaced).ConfigureAwait(false)))
            || !await spectreInventory.AddItemAsync(slot, item).ConfigureAwait(false))
        {
            await RollbackAsync(ownerInventory, ownerSlot, item, spectreInventory, slot, replaced).ConfigureAwait(false);
            transferContext.Detach(item);
            if (replaced is not null)
            {
                transferContext.Detach(replaced);
            }

            return replaced is null ? "No se pudo equipar el item." : "No hay lugar en tu inventario para el item que tenia puesto.";
        }

        owner.PersistenceContext.Detach(item);
        if (replaced is not null)
        {
            spectre.PersistenceContext.Detach(replaced);
        }

        try
        {
            await transferContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // Nothing was sent to the client yet, so it's enough to restore the state of the server.
            await RollbackAsync(ownerInventory, ownerSlot, item, spectreInventory, slot, replaced).ConfigureAwait(false);
            owner.PersistenceContext.Attach(item);
            if (replaced is not null)
            {
                spectre.PersistenceContext.Attach(replaced);
            }

            throw;
        }

        spectre.PersistenceContext.Attach(item);
        await owner.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(ownerSlot)).ConfigureAwait(false);
        if (replaced is not null)
        {
            owner.PersistenceContext.Attach(replaced);
            await owner.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(replaced)).ConfigureAwait(false);
        }

        return $"Tu espectro {SpectreFeaturePlugIn.GetKindName(spectre.Kind)} se equipo {Describe(item)}.";
    }

    /// <summary>
    /// Takes off an item of the spectre and puts it into the inventory of the owner.
    /// Has to run while holding the persistence locks of the spectre and the owner.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <param name="equipmentSlot">The equipment slot of the spectre.</param>
    /// <returns>The message for the owner.</returns>
    public static async ValueTask<string> UnequipAsync(SpectrePlayer spectre, byte equipmentSlot)
    {
        var owner = spectre.Owner;
        if (owner.Inventory is not { } ownerInventory || spectre.Inventory is not { } spectreInventory)
        {
            return "No se pudo quitar el item.";
        }

        if (equipmentSlot >= InventoryConstants.EquippableSlotsCount || spectreInventory.GetItem(equipmentSlot) is not { } item)
        {
            return "Tu espectro no tiene un item en ese lugar.";
        }

        if (ownerInventory.CheckInvSpace(item) is not { } ownerSlot)
        {
            return "No hay lugar en tu inventario.";
        }

        await spectre.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
        using var transferContext = owner.GameContext.PersistenceContextProvider.CreateNewTradeContext();
        transferContext.Attach(item);
        await spectreInventory.RemoveItemAsync(item).ConfigureAwait(false);
        if (!await ownerInventory.AddItemAsync(ownerSlot, item).ConfigureAwait(false))
        {
            await spectreInventory.AddItemAsync(equipmentSlot, item).ConfigureAwait(false);
            transferContext.Detach(item);
            return "No se pudo quitar el item.";
        }

        spectre.PersistenceContext.Detach(item);
        try
        {
            await transferContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            await ownerInventory.RemoveItemAsync(item).ConfigureAwait(false);
            await spectreInventory.AddItemAsync(equipmentSlot, item).ConfigureAwait(false);
            spectre.PersistenceContext.Attach(item);
            throw;
        }

        owner.PersistenceContext.Attach(item);
        await owner.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        return $"Le quitaste {Describe(item)} a tu espectro {SpectreFeaturePlugIn.GetKindName(spectre.Kind)}.";
    }

    /// <summary>
    /// Gets the slots into which a spectre equips an item: weapons into the main hand and shields into the
    /// other hand, like the bots; everything else into any of its slots (rings have two).
    /// </summary>
    private static IReadOnlyList<byte> GetTargetSlots(ItemDefinition definition)
    {
        var slots = definition.ItemSlot!.ItemSlots.Select(s => (byte)s).ToList();
        if (definition.Group <= LastWeaponGroup && !definition.IsAmmunition && slots.Contains(InventoryConstants.LeftHandSlot))
        {
            return [InventoryConstants.LeftHandSlot];
        }

        if (definition.Group == ShieldGroup && slots.Contains(InventoryConstants.RightHandSlot))
        {
            return [InventoryConstants.RightHandSlot];
        }

        return slots;
    }

    private static async ValueTask RollbackAsync(IStorage ownerInventory, byte ownerSlot, Item item, IStorage spectreInventory, byte slot, Item? replaced)
    {
        if (spectreInventory.GetItem(slot) == item)
        {
            await spectreInventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        if (replaced is not null)
        {
            if (ownerInventory.Items.Contains(replaced))
            {
                await ownerInventory.RemoveItemAsync(replaced).ConfigureAwait(false);
            }

            await spectreInventory.AddItemAsync(slot, replaced).ConfigureAwait(false);
        }

        await ownerInventory.AddItemAsync(ownerSlot, item).ConfigureAwait(false);
    }
}
