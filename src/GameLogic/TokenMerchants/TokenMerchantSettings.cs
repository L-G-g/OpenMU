// <copyright file="TokenMerchantSettings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The settings of the token merchant of one dungeon of the <see cref="TokenMerchantConfiguration"/>:
/// the NPC which sells the best items of the dungeon for the tokens which the players get for completing it.
/// </summary>
public class TokenMerchantSettings
{
    /// <summary>
    /// Gets or sets the number of the dungeon (<see cref="DungeonSettings.Number"/>) whose tokens the merchant takes and whose items it sells.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantSettings_Dungeon_Name), Description = nameof(PlugInResources.TokenMerchantSettings_Dungeon_Description))]
    public byte Dungeon { get; set; }

    /// <summary>
    /// Gets or sets the designation of the NPC. The NPC is recognized by its designation instead of its number,
    /// because it can use the number (and with it the look in the game client) of another NPC.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantSettings_NpcDesignation_Name), Description = nameof(PlugInResources.TokenMerchantSettings_NpcDesignation_Description))]
    public string NpcDesignation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the token, which the players see in the messages.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantSettings_TokenName_Name))]
    public string TokenName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the price of a piece of the armor set, in tokens.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantSettings_SetPiecePrice_Name))]
    public int SetPiecePrice { get; set; } = 10;

    /// <summary>
    /// Gets or sets the price of a weapon or a shield, in tokens.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantSettings_WeaponPrice_Name))]
    public int WeaponPrice { get; set; } = 15;

    /// <inheritdoc />
    public override string ToString() => $"{this.Dungeon}: {this.NpcDesignation} ({this.TokenName})";
}
