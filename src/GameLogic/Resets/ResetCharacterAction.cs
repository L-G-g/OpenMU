// <copyright file="ResetCharacterAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Action to reset a character.
/// </summary>
public class ResetCharacterAction
{
    private readonly Player _player;
    private readonly NonPlayerCharacter? _npc;
    private readonly LogoutAction _logoutAction = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetCharacterAction"/> class.
    /// </summary>
    /// <param name="player">Player to reset.</param>
    /// <param name="npc">NPC which the player talks to to initiate the reset action.</param>
    public ResetCharacterAction(Player player, NonPlayerCharacter? npc = null)
    {
        this._player = player;
        this._npc = npc;
    }

    /// <summary>
    /// Reset specific character.
    /// </summary>
    public async ValueTask ResetCharacterAsync()
    {
        var resetFeature = this._player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>();
        if (resetFeature is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotEnabled)).ConfigureAwait(false);
            return;
        }

        if (this._player.PlayerState.CurrentState != PlayerState.EnteredWorld && this._npc is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.CantResetWithOpenedWindows)).ConfigureAwait(false);
            return;
        }

        if (this._player.Attributes is null || this._player.SelectedCharacter is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnteredTheGame)).ConfigureAwait(false);
            return;
        }

        var configuration = resetFeature.Configuration;
        if (configuration is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotConfigured)).ConfigureAwait(false);
            return;
        }

        var resetProgression = ResetProgressionCalculator.Calculate(this.GetResetCount(), (int)this._player.Attributes[Stats.PointsPerReset], configuration);

        if (this._player.Level < configuration.RequiredLevel)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.RequiredLevelForReset), configuration.RequiredLevel).ConfigureAwait(false);
            return;
        }

        if (configuration.ResetLimit > 0 && resetProgression.NextResetCount > configuration.ResetLimit)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.MaximumResetsReached), configuration.ResetLimit).ConfigureAwait(false);
            return;
        }

        if (!await this.TryConsumeResetCostsAsync(configuration, resetProgression).ConfigureAwait(false))
        {
            return;
        }

        this._player.Attributes[Stats.Resets] = resetProgression.NextResetCount;
        this._player.Attributes[Stats.Level] = configuration.LevelAfterReset;
        this._player.SelectedCharacter.Experience = 0;
        this.UpdateStats(configuration, resetProgression);
        if (configuration.LogOut)
        {
            if (configuration.MoveHome && await this.GetHomeGateAsync().ConfigureAwait(false) is { } homeGate)
            {
                this._player.SelectedCharacter.PositionX = (byte)Rand.NextInt(homeGate.X1, homeGate.X2);
                this._player.SelectedCharacter.PositionY = (byte)Rand.NextInt(homeGate.Y1, homeGate.Y2);
                this._player.SelectedCharacter.CurrentMap = homeGate.Map;
                this._player.Rotation = homeGate.Direction;
            }

            await this._logoutAction.LogoutAsync(this._player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
        }
        else
        {
            await this.UpdateClientAndWarpAsync(configuration).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowMessageAsync(string messageKey, params object?[] args)
    {
        var message = this._player.GetLocalizedMessage(messageKey, args);

        if (this._npc is null)
        {
            await this._player.ShowBlueMessageAsync(message).ConfigureAwait(false);
            return;
        }

        await this._player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(message, this._npc)).ConfigureAwait(false);
    }

    private int GetResetCount()
    {
        return (int)this._player.Attributes![Stats.Resets];
    }

    private async ValueTask<bool> TryConsumeResetCostsAsync(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        var requiredItems = await this.GetRequiredItemsToConsumeAsync(configuration, resetProgression.RequiredItemAmount).ConfigureAwait(false);
        if (requiredItems is null)
        {
            return false;
        }

        if (this._player.Money < resetProgression.RequiredZen)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        if (resetProgression.RequiredZen > 0 && !this._player.TryRemoveMoney(resetProgression.RequiredZen))
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        foreach (var item in requiredItems)
        {
            await this._player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask<IList<Item>?> GetRequiredItemsToConsumeAsync(ResetConfiguration configuration, int requiredItemAmount)
    {
        if (requiredItemAmount <= 0 || configuration.RequiredResetItem is null)
        {
            return [];
        }

        if (this._player.Inventory is null)
        {
            return null;
        }

        var requiredDefinition = configuration.RequiredResetItem;
        var requiredItems = this._player.Inventory.Items
            .Where(item => item.Definition is { } definition
                           && definition.Group == requiredDefinition.Group
                           && definition.Number == requiredDefinition.Number)
            .Take(requiredItemAmount)
            .ToList();
        if (requiredItems.Count < requiredItemAmount)
        {
            await this.ShowMessageAsync(
                nameof(PlayerMessage.NotEnoughItemsForReset),
                requiredItemAmount,
                configuration.RequiredResetItem.Name).ConfigureAwait(false);
            return null;
        }

        return requiredItems;
    }

    private void UpdateStats(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        if (configuration.ResetStats)
        {
            this._player.SelectedCharacter!.CharacterClass!.StatAttributes
                .Where(s => s.IncreasableByPlayer)
                .ForEach(s => this._player.Attributes![s.Attribute] = s.BaseValue);
        }

        if (configuration.ReplacePointsPerReset)
        {
            this._player.SelectedCharacter!.LevelUpPoints = resetProgression.TotalPointsAfterReset;
        }
        else
        {
            this._player.SelectedCharacter!.LevelUpPoints += resetProgression.PointsForReset;
        }
    }

    /// <summary>
    /// Gets the gate in the safezone of the home map of the character class.
    /// </summary>
    private async ValueTask<ExitGate?> GetHomeGateAsync()
    {
        var homeMapDef = this._player.SelectedCharacter!.CharacterClass!.HomeMap;
        if (homeMapDef is null)
        {
            return null;
        }

        return (await this._player.GameContext.GetMapAsync((ushort)homeMapDef.Number).ConfigureAwait(false))?.SafeZoneSpawnGate;
    }

    /// <summary>
    /// Updates the client of a player who stays in the game after the reset, and warps the
    /// player home or to its current position.
    /// </summary>
    /// <remarks>
    /// Clients of season 6 and older don't get to know their new stats through the base stats
    /// update, which is only implemented for the extended client. They have to get the character
    /// information again, which makes them create the own character anew. To not end up with a
    /// second, stale copy of the character, the client is told first that the character is out of
    /// scope, and the warp afterwards brings it back at its (new) position.
    /// </remarks>
    private async ValueTask UpdateClientAndWarpAsync(ResetConfiguration configuration)
    {
        var player = this._player;
        var targetGate = configuration.MoveHome ? await this.GetHomeGateAsync().ConfigureAwait(false) : null;
        if (targetGate is null)
        {
            if (player.CurrentMap is not { } currentMap)
            {
                return;
            }

            targetGate = new Persistence.BasicModel.ExitGate
            {
                Map = currentMap.Definition,
                X1 = player.Position.X,
                X2 = player.Position.X,
                Y1 = player.Position.Y,
                Y2 = player.Position.Y,
            };
        }

        if (configuration.ResetStats)
        {
            await player.InvokeViewPlugInAsync<IUpdateCharacterBaseStatsPlugIn>(p => p.UpdateCharacterBaseStatsAsync()).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IUpdateLevelPlugIn>(p => p.UpdateLevelAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IObjectsOutOfScopePlugIn>(p => p.ObjectsOutOfScopeAsync(player.GetAsEnumerable())).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateCharacterStatsPlugIn>(p => p.UpdateCharacterStatsAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
        await player.WarpToAsync(targetGate).ConfigureAwait(false);
    }
}
