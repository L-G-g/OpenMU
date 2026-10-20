// <copyright file="IMonsterItemDroppingPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called when a killed monster is about to drop a generated item.
/// </summary>
/// <remarks>
/// It's called before the item is added to the map, so the item can still be changed, e.g. its number of sockets.
/// After the item was added, the <see cref="IMonsterItemDroppedPlugIn"/> is called.
/// </remarks>
[Guid("570E4AAD-CB9E-47BC-82C0-A93EDCEFB7C9")]
[PlugInPoint("Monster item dropping", "Plugins which will be executed when a killed monster is about to drop an item, so that the item can be changed.")]
public interface IMonsterItemDroppingPlugIn
{
    /// <summary>
    /// Is called when a killed monster is about to drop an item to the ground.
    /// </summary>
    /// <param name="monster">The killed monster.</param>
    /// <param name="killer">The player who killed the monster.</param>
    /// <param name="item">The generated item, which isn't on the map yet.</param>
    ValueTask MonsterItemDroppingAsync(AttackableNpcBase monster, Player killer, Item item);
}
