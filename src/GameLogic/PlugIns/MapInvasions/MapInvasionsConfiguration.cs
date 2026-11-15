// <copyright file="MapInvasionsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.MapInvasions;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="MapInvasionsPlugIn"/>.
/// </summary>
public class MapInvasionsConfiguration
{
    /// <summary>
    /// Gets or sets the time between two invasions.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Interval_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_Interval_Description))]
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(24);

    /// <summary>
    /// Gets or sets the time between the start of the game server and the first invasion.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_FirstInvasionDelay_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_FirstInvasionDelay_Description))]
    public TimeSpan FirstInvasionDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the distance around a monster spot of the map within which an invasion monster appears.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_SpawnDistance_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_SpawnDistance_Description))]
    public byte SpawnDistance { get; set; } = 6;

    /// <summary>
    /// Gets or sets the message which is shown to all players when an invasion starts.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_StartMessage_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_StartMessage_Description))]
    public LocalizedString StartMessage { get; set; } = new("{monster} invadio {map}!");

    /// <summary>
    /// Gets or sets the invasions. Each one happens once per cycle, in a random order.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Invasions_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_Invasions_Description))]
    public ICollection<MapInvasion> Invasions { get; set; } = new List<MapInvasion>();

    /// <summary>
    /// The invasion of a map.
    /// </summary>
    public class MapInvasion
    {
        /// <summary>
        /// Gets or sets the number of the map.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_MapNumber_Name))]
        public short MapNumber { get; set; }

        /// <summary>
        /// Gets or sets the monster.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Monster_Name))]
        public MonsterDefinition? Monster { get; set; }

        /// <summary>
        /// Gets or sets the number of monsters of an invasion.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Count_Name))]
        public int Count { get; set; } = 10;

        /// <summary>
        /// Gets or sets the drops which each monster always drops, in addition to its normal drop.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Drops_Name), Description = nameof(PlugInResources.MapInvasionsConfiguration_Drops_Description))]
        public ICollection<InvasionDrop> Drops { get; set; } = new List<InvasionDrop>();

        /// <inheritdoc />
        public override string ToString() => $"Map {this.MapNumber}: {this.Count} x {this.Monster?.Designation.ValueInNeutralLanguage}";
    }

    /// <summary>
    /// A drop of a monster of an invasion: between <see cref="MinimumCount"/> and <see cref="MaximumCount"/> items,
    /// each one chosen at random from <see cref="Items"/>.
    /// </summary>
    public class InvasionDrop
    {
        /// <summary>
        /// Gets or sets the items from which each dropped item is chosen at random. An item which is listed more than once is more likely.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_Items_Name))]
        public ICollection<InvasionDropItem> Items { get; set; } = new List<InvasionDropItem>();

        /// <summary>
        /// Gets or sets the minimum number of dropped items.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_MinimumCount_Name))]
        public int MinimumCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of dropped items. Each number from the minimum to the maximum is equally likely.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_MaximumCount_Name))]
        public int MaximumCount { get; set; } = 1;

        /// <inheritdoc />
        public override string ToString() => $"{this.MinimumCount}-{this.MaximumCount} of {string.Join(", ", this.Items)}";
    }

    /// <summary>
    /// An item which can be dropped.
    /// </summary>
    public class InvasionDropItem
    {
        /// <summary>
        /// Gets or sets the group of the item.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_ItemGroup_Name))]
        public byte Group { get; set; }

        /// <summary>
        /// Gets or sets the number of the item.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_ItemNumber_Name))]
        public short Number { get; set; }

        /// <summary>
        /// Gets or sets the level of the item.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapInvasionsConfiguration_ItemLevel_Name))]
        public byte Level { get; set; }

        /// <inheritdoc />
        public override string ToString() => $"({this.Group}, {this.Number}) +{this.Level}";
    }
}
