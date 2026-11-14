// <copyright file="TokenMerchantFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.TokenMerchants;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.MiniGames.Dungeon;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Feature plugin of the tokens of the dungeons: each player gets tokens for completing a dungeon (more in its extreme
/// version), and the merchant of the dungeon sells the best armor set, shield and weapons of the dungeon for them.
/// The shop of a merchant has a tab for each character class; the sold items are usual excellent items with luck
/// and the excellent options of a dropped one.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TokenMerchantFeaturePlugIn_Name), Description = nameof(PlugInResources.TokenMerchantFeaturePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C7A1E5D3-8B42-4F96-A0D7-3E5B9C1F6A28")]
public class TokenMerchantFeaturePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<TokenMerchantConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly TokenMerchantShop _shop = new();
    private TokenMerchantConfiguration? _configuration;

    /// <inheritdoc/>
    public TokenMerchantConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;
            this._shop.Reset();
        }
    }

    /// <summary>
    /// Gets the shop of the merchants.
    /// </summary>
    internal TokenMerchantShop Shop => this._shop;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TokenMerchantConfiguration();

    /// <summary>
    /// Gets the plugin of the game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plugin, or <c>null</c> if the token merchants are not active.</returns>
    internal static TokenMerchantFeaturePlugIn? Get(IGameContext gameContext)
    {
        if (gameContext.FeaturePlugIns.GetPlugIn<TokenMerchantFeaturePlugIn>() is not { } plugIn)
        {
            return null;
        }

        plugIn._configuration ??= new TokenMerchantConfiguration();
        return plugIn;
    }

    /// <summary>
    /// Finds the token merchant of an NPC.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="npc">The definition of the NPC.</param>
    /// <returns>The plugin and the merchant, or <c>null</c> if the NPC is not a token merchant.</returns>
    internal static (TokenMerchantFeaturePlugIn PlugIn, TokenMerchantSettings Merchant)? Find(IGameContext gameContext, MonsterDefinition? npc)
    {
        return npc is not null
               && Get(gameContext) is { } plugIn
               && plugIn._configuration?.FindByNpc(npc.Designation.ValueInNeutralLanguage) is { } merchant
            ? (plugIn, merchant)
            : null;
    }

    /// <summary>
    /// Gives the tokens for completing a dungeon to a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <param name="isExtreme">If set to <c>true</c>, the extreme version was completed.</param>
    internal static async ValueTask GiveDungeonTokensAsync(Player player, DungeonSettings dungeon, bool isExtreme)
    {
        if (Get(player.GameContext) is not { _configuration: { } configuration }
            || configuration.FindByDungeon(dungeon.Number) is not { } merchant)
        {
            return;
        }

        var amount = isExtreme ? configuration.ExtremeTokens : configuration.NormalTokens;
        if (TokenWallet.AddTokens(player, dungeon.Number, amount) is { } total)
        {
            await player.ShowGoldenMessageAsync(string.Format(TokenMerchantMessages.TokensGainedFormat, amount, merchant.TokenName, total)).ConfigureAwait(false);
            await player.ShowBlueMessageAsync(string.Format(TokenMerchantMessages.TokensGainedFormat, amount, merchant.TokenName, total)).ConfigureAwait(false);
        }
    }
}
