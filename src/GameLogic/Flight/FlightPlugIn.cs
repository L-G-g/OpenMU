// <copyright file="FlightPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Flight;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets players fly on their Dinorant (<c>/volar</c>, or the key of the flight.dll client mod):
/// a flying player walks over blocked tiles (walls, trees, houses), but not over tiles without ground,
/// and moves faster by <see cref="FlightConfiguration.SpeedFactor"/>. Flying is movement only: no attacks
/// and no skills, and monsters don't attack a flying player. It can't be started in combat or in a safezone
/// (entering one lands the player), and only on the allowed regular maps, never in a mini game.
/// A player lands only on a walkable tile; when the flight is ended by force (the Dinorant is taken off or
/// breaks), the player is put on the nearest walkable tile.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.FlightPlugIn_Name), Description = nameof(PlugInResources.FlightPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E7B4D2A9-1C58-4F3E-8D6B-2A9F5C3E1B70")]
public sealed class FlightPlugIn : IFeaturePlugIn,
    IAttackableGotHitPlugIn,
    IAttackableGotKilledPlugIn,
    IAttackableMovedPlugIn,
    IObjectRemovedFromMapPlugIn,
    IItemMovedPlugIn,
    IItemDestroyedPlugIn,
    ISupportCustomConfiguration<FlightConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>
    /// How far around the player a walkable tile is searched for a forced landing.
    /// </summary>
    private const int LandingSearchRadius = 30;

    private const byte DinorantGroup = 13;

    private const byte DinorantNumber = 3;

    private readonly ConditionalWeakTable<Player, StrongBox<long>> _lastCombatTicks = new();

    private readonly ConcurrentDictionary<Player, IElement> _speedElements = new();

    private readonly ConcurrentDictionary<Player, bool> _safezoneLandings = new();

    /// <inheritdoc />
    public FlightConfiguration? Configuration { get; set; }

    private FlightConfiguration CurrentConfiguration => this.Configuration ?? new FlightConfiguration();

    /// <summary>
    /// Determines whether a flying player may be on the specified tile: anywhere, except where there's no ground.
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <param name="point">The tile.</param>
    /// <returns><c>True</c>, if a flying player may be on the tile.</returns>
    public static bool IsFlyableTile(GameMapTerrain terrain, Point point)
    {
        return (terrain.AttributeMap[point.X, point.Y] & (byte)TerrainAttributeType.NoGround) == 0;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new FlightConfiguration();

    /// <summary>
    /// Starts the flight of the player, or lands it when it's already flying.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask ToggleAsync(Player player)
    {
        if (player.IsFlying)
        {
            await this.LandAsync(player).ConfigureAwait(false);
        }
        else
        {
            await this.StartAsync(player).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        var now = Environment.TickCount64;
        if (attackable is Player victim)
        {
            this._lastCombatTicks.AddOrUpdate(victim, new StrongBox<long>(now));
        }

        if (attacker is Player attackerPlayer)
        {
            this._lastCombatTicks.AddOrUpdate(attackerPlayer, new StrongBox<long>(now));
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is Player { IsFlying: true } player)
        {
            // The player respawns in a town, so there's no need to look for a landing tile.
            await this.EndFlightAsync(player, notifyObservers: true).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void AttackableMoved(IAttackable attackable)
    {
        if (attackable is Player { IsFlying: true } player
            && player.IsAtSafezone()
            && this._safezoneLandings.TryAdd(player, true))
        {
            // Called by the walker while it moves the player, so the landing (which stops the walk) runs apart.
            _ = Task.Run(() => this.LandInSafezoneSafeAsync(player));
        }
    }

    /// <inheritdoc />
    public async ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is Player { IsFlying: true } player)
        {
            // Warp, map change or logout. The others lose it out of scope; its own client needs to know.
            // A character which logs out above a wall is moved to a walkable tile at the next login.
            await this.EndFlightAsync(player, notifyObservers: false).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IShowFlightStatePlugIn>(p => p.ShowFlightStateAsync(player, false)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask ItemMovedAsync(Player player, Item item)
    {
        if (player.IsFlying && !HasDinorant(player))
        {
            await this.ForceLandingAsync(player).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void ItemDestroyed(Item item)
    {
        if (item.Definition is not { Group: DinorantGroup, Number: DinorantNumber })
        {
            return;
        }

        foreach (var player in this._speedElements.Keys)
        {
            if (!HasDinorant(player))
            {
                _ = this.ForceLandingSafeAsync(player);
            }
        }
    }

    private static async ValueTask ResetSpeedCheckAsync(Player player)
    {
        if (player.GameContext.PlugInManager.GetPlugInPoint<ISpeedHackCheatCheckPlugIn>() is { } speedCheck)
        {
            await speedCheck.ResetMovementStateAsync(player).ConfigureAwait(false);
        }
    }

    private static bool HasDinorant(Player player)
    {
        return player.Inventory?.GetItem(InventoryConstants.PetSlot) is { Durability: > 0.0, Definition: { Group: DinorantGroup, Number: DinorantNumber } };
    }

    private static Point? FindNearestWalkableTile(GameMapTerrain terrain, Point origin)
    {
        if (terrain.WalkMap[origin.X, origin.Y])
        {
            return origin;
        }

        // Rings of growing size around the origin; the first walkable tile of the smallest ring wins.
        for (var radius = 1; radius <= LandingSearchRadius; radius++)
        {
            Point? best = null;
            var bestDistance = double.MaxValue;
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var x = origin.X + dx;
                    var y = origin.Y + dy;
                    if (x < 0 || y < 0 || x > byte.MaxValue || y > byte.MaxValue || !terrain.WalkMap[x, y])
                    {
                        continue;
                    }

                    var candidate = new Point((byte)x, (byte)y);
                    var distance = candidate.EuclideanDistanceTo(origin);
                    if (distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return terrain.AnyWalkableCoordinate;
    }

    private bool IsInCombat(Player player, FlightConfiguration configuration)
    {
        return this._lastCombatTicks.TryGetValue(player, out var lastCombat)
               && Environment.TickCount64 - lastCombat.Value < configuration.CombatCooldownSeconds * 1000L;
    }

    private async ValueTask StartAsync(Player player)
    {
        var configuration = this.CurrentConfiguration;
        if (!player.IsAlive || player.CurrentMap is not { } map || player.Attributes is not { } attributes)
        {
            return;
        }

        if (!HasDinorant(player))
        {
            await player.ShowBlueMessageAsync("Para volar necesitas el Dinorant equipado.").ConfigureAwait(false);
            return;
        }

        if (player.CurrentMiniGame is not null || !configuration.IsMapAllowed(map.MapId))
        {
            await player.ShowBlueMessageAsync("En este mapa no se puede volar.").ConfigureAwait(false);
            return;
        }

        if (player.IsAtSafezone())
        {
            await player.ShowBlueMessageAsync("En la zona segura no se puede volar.").ConfigureAwait(false);
            return;
        }

        if (this.IsInCombat(player, configuration))
        {
            await player.ShowBlueMessageAsync("No podes volar en combate. Espera unos segundos.").ConfigureAwait(false);
            return;
        }

        var speedElement = new ConstantElement(configuration.SpeedFactor > 0 ? configuration.SpeedFactor : 1, AggregateType.Multiplicate);
        if (!this._speedElements.TryAdd(player, speedElement))
        {
            return;
        }

        attributes.AddElement(speedElement, Stats.MovementSpeedFactor);
        player.IsFlying = true;

        // The speed check of the anti-cheat starts fresh with the new speed.
        await ResetSpeedCheckAsync(player).ConfigureAwait(false);
        await player.ForEachWorldObserverAsync<IShowFlightStatePlugIn>(p => p.ShowFlightStateAsync(player, true), true).ConfigureAwait(false);
        await player.ShowBlueMessageAsync("Estas volando. Escribi /volar de nuevo para aterrizar.").ConfigureAwait(false);
    }

    private async ValueTask LandAsync(Player player)
    {
        if (player.CurrentMap is not { } map)
        {
            await this.EndFlightAsync(player, notifyObservers: false).ConfigureAwait(false);
            return;
        }

        await player.StopWalkingAsync().ConfigureAwait(false);
        if (!map.Terrain.WalkMap[player.Position.X, player.Position.Y])
        {
            await player.ShowBlueMessageAsync("Aca no podes aterrizar. Busca un lugar donde se pueda caminar.").ConfigureAwait(false);
            return;
        }

        await this.EndFlightAsync(player, notifyObservers: true).ConfigureAwait(false);
        await player.ShowBlueMessageAsync("Aterrizaste.").ConfigureAwait(false);
    }

    private async Task LandInSafezoneSafeAsync(Player player)
    {
        try
        {
            if (player.IsFlying)
            {
                // Safezone tiles are walkable, so the player lands right there.
                await player.StopWalkingAsync().ConfigureAwait(false);
                await this.EndFlightAsync(player, notifyObservers: true).ConfigureAwait(false);
                await player.ShowBlueMessageAsync("Aterrizaste en la zona segura.").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error during the safezone landing of {Player}", player);
        }
        finally
        {
            this._safezoneLandings.TryRemove(player, out _);
        }
    }

    private async Task ForceLandingSafeAsync(Player player)
    {
        try
        {
            await this.ForceLandingAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error during the forced landing of {Player}", player);
        }
    }

    private async ValueTask ForceLandingAsync(Player player)
    {
        if (!player.IsFlying)
        {
            return;
        }

        await player.StopWalkingAsync().ConfigureAwait(false);
        await this.EndFlightAsync(player, notifyObservers: true).ConfigureAwait(false);
        if (player.CurrentMap is { } map
            && !map.Terrain.WalkMap[player.Position.X, player.Position.Y]
            && FindNearestWalkableTile(map.Terrain, player.Position) is { } landingTile)
        {
            await player.MoveAsync(landingTile).ConfigureAwait(false);
        }

        await player.ShowBlueMessageAsync("Sin el Dinorant no podes seguir volando.").ConfigureAwait(false);
    }

    private async ValueTask EndFlightAsync(Player player, bool notifyObservers)
    {
        if (this._speedElements.TryRemove(player, out var speedElement))
        {
            player.Attributes?.RemoveElement(speedElement, Stats.MovementSpeedFactor);
        }

        if (!player.IsFlying)
        {
            return;
        }

        player.IsFlying = false;
        await ResetSpeedCheckAsync(player).ConfigureAwait(false);
        if (notifyObservers && player.CurrentMap is not null)
        {
            await player.ForEachWorldObserverAsync<IShowFlightStatePlugIn>(p => p.ShowFlightStateAsync(player, false), true).ConfigureAwait(false);
        }
    }
}
