// <copyright file="DropSocketCountConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration for the <see cref="DropSocketCountPlugIn"/>.
/// </summary>
public class DropSocketCountConfiguration
{
    /// <summary>
    /// Gets or sets the rules for the number of sockets.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DropSocketCountConfiguration_Rules_Name))]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<SocketCountRule> Rules { get; set; } = new List<SocketCountRule>();

    /// <summary>
    /// The number of sockets of the socket items which drop from a monster or on a map.
    /// </summary>
    public class SocketCountRule
    {
        /// <summary>
        /// Gets or sets the number of the monster. If it's set, the rule applies to this monster on any map.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DropSocketCountConfiguration_MonsterNumber_Name))]
        public short? MonsterNumber { get; set; }

        /// <summary>
        /// Gets or sets the number of the map. If it's set and the monster number isn't, the rule applies to all monsters of this map.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DropSocketCountConfiguration_MapNumber_Name))]
        public short? MapNumber { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of sockets.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DropSocketCountConfiguration_MinimumSockets_Name))]
        public int MinimumSockets { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of sockets.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DropSocketCountConfiguration_MaximumSockets_Name))]
        public int MaximumSockets { get; set; } = 1;
    }
}
