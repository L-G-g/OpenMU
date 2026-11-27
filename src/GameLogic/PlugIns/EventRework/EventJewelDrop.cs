// <copyright file="EventJewelDrop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.EventRework;

/// <summary>
/// A jewel which the monsters of an event drop, with its weight, see <see cref="EventReworkConfiguration.ChaosCastleJewels"/>.
/// </summary>
public class EventJewelDrop
{
    /// <summary>
    /// Gets or sets the name, only to recognize the row in the admin panel.
    /// </summary>
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item group of the jewel.
    /// </summary>
    [Display(Name = "Group")]
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the item number of the jewel.
    /// </summary>
    [Display(Name = "Number")]
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the weight: how often the jewel drops in relation to the others.
    /// </summary>
    [Display(Name = "Weight")]
    public int Weight { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{this.Name} ({this.Group},{this.Number}): {this.Weight}";
}
