// <copyright file="FlightConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Flight;

/// <summary>
/// The configuration of the <see cref="FlightPlugIn"/>.
/// </summary>
public class FlightConfiguration
{
    /// <summary>
    /// The maps of the default configuration: the regular hunting maps, without events, Castle Siege,
    /// Kalima, the spectre hall (Duel Arena) and Icarus, which keeps its own rules.
    /// </summary>
    public const string DefaultAllowedMapNumbers = "0,1,2,3,4,7,8,33,34,37,38,51,56,57,63,80,81";

    /// <summary>
    /// Gets or sets the factor which is applied to the movement speed while flying.
    /// The flight.dll client mod applies the same factor, so the client and the server stay in step.
    /// </summary>
    [Display(Name = "Speed factor", Description = "Factor of the movement speed while flying. flight.dll uses the same value (camera3d.ini [Flight] SpeedFactor).")]
    public float SpeedFactor { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets the seconds after the last hit (given or received) during which a player is in combat
    /// and can't start flying.
    /// </summary>
    [Display(Name = "Combat cooldown (seconds)", Description = "Seconds after the last hit, given or received, during which a player can't start flying.")]
    public int CombatCooldownSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the comma separated numbers of the maps on which flying is allowed.
    /// </summary>
    [Display(Name = "Allowed map numbers", Description = "Comma separated map numbers on which players can fly. Never inside a mini game (events, dungeons).")]
    public string AllowedMapNumbers { get; set; } = DefaultAllowedMapNumbers;

    /// <summary>
    /// Determines whether flying is allowed on the specified map.
    /// </summary>
    /// <param name="mapNumber">The map number.</param>
    /// <returns><c>True</c>, if flying is allowed on the map.</returns>
    public bool IsMapAllowed(int mapNumber)
    {
        foreach (var part in (this.AllowedMapNumbers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var number) && number == mapNumber)
            {
                return true;
            }
        }

        return false;
    }
}
