// <copyright file="EventDifficultyReferenceMap.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

/// <summary>
/// A hunting map with the resets it needs, see <see cref="EventReworkConfiguration.DifficultyReferenceMaps"/>.
/// The monsters of the events get the stats which the monsters of these maps have at the same resets.
/// </summary>
public class EventDifficultyReferenceMap
{
    /// <summary>
    /// Gets or sets the name, only to recognize the row in the admin panel.
    /// </summary>
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of the map.
    /// </summary>
    [Display(Name = "Map number")]
    public short MapNumber { get; set; }

    /// <summary>
    /// Gets or sets the resets which the map needs.
    /// </summary>
    [Display(Name = "Resets")]
    public int Resets { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{this.Name} ({this.MapNumber}): {this.Resets} resets";
}
