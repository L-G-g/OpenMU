// <copyright file="TokenMerchantConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The configuration of the tokens of the dungeons and of their merchants, see <see cref="TokenMerchantFeaturePlugIn"/>.
/// </summary>
public class TokenMerchantConfiguration
{
    /// <summary>
    /// The designation of Zienna, the merchant of the tokens of the dungeon of Devias.
    /// </summary>
    public const string DeviasNpcDesignation = "Zienna The Weapons Merchant";

    /// <summary>
    /// The designation of the merchant of the tokens of the dungeon of Tarkan.
    /// </summary>
    public const string TarkanNpcDesignation = "Tarkan Token Merchant";

    /// <summary>
    /// The designation of the merchant of the tokens of the dungeon of Kanturu.
    /// </summary>
    public const string KanturuNpcDesignation = "Kanturu Token Merchant";

    /// <summary>
    /// Gets or sets the number of tokens which each player gets for completing the normal version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantConfiguration_NormalTokens_Name))]
    public int NormalTokens { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of tokens which each player gets for completing the extreme version of a dungeon.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantConfiguration_ExtremeTokens_Name))]
    public int ExtremeTokens { get; set; } = 2;

    /// <summary>
    /// Gets or sets a value indicating whether the sold items have luck.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantConfiguration_ItemsHaveLuck_Name))]
    public bool ItemsHaveLuck { get; set; } = true;

    /// <summary>
    /// Gets or sets the merchants, one for each dungeon which gives tokens.
    /// </summary>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.TokenMerchantConfiguration_Merchants_Name), Description = nameof(PlugInResources.TokenMerchantConfiguration_Merchants_Description))]
    public ICollection<TokenMerchantSettings> Merchants { get; set; } = CreateDefaultMerchants();

    /// <summary>
    /// Creates the default merchants: Zienna in Devias, and the merchants of Tarkan and Kanturu.
    /// </summary>
    /// <returns>The default merchants.</returns>
    public static List<TokenMerchantSettings> CreateDefaultMerchants() =>
    [
        new() { Dungeon = DungeonDefaults.DeviasDungeonNumber, NpcDesignation = DeviasNpcDesignation, TokenName = "Gelid Token" },
        new() { Dungeon = DungeonDefaults.TarkanDungeonNumber, NpcDesignation = TarkanNpcDesignation, TokenName = "Arid Token" },
        new() { Dungeon = DungeonDefaults.KanturuDungeonNumber, NpcDesignation = KanturuNpcDesignation, TokenName = "Ancient Token" },
    ];

    /// <summary>
    /// Finds the merchant of the tokens of a dungeon.
    /// </summary>
    /// <param name="dungeonNumber">The number of the dungeon.</param>
    /// <returns>The merchant, or <c>null</c> if the dungeon gives no tokens.</returns>
    public TokenMerchantSettings? FindByDungeon(byte dungeonNumber) => this.Merchants.FirstOrDefault(m => m.Dungeon == dungeonNumber);

    /// <summary>
    /// Finds the merchant by the designation of its NPC.
    /// </summary>
    /// <param name="npcDesignation">The designation of the NPC.</param>
    /// <returns>The merchant, or <c>null</c> if the NPC is not a token merchant.</returns>
    public TokenMerchantSettings? FindByNpc(string? npcDesignation)
    {
        return string.IsNullOrEmpty(npcDesignation)
            ? null
            : this.Merchants.FirstOrDefault(m => m.NpcDesignation == npcDesignation);
    }
}
