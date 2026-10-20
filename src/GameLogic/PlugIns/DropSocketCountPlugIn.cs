// <copyright file="DropSocketCountPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the number of sockets of the socket items which monsters drop, by the rules of its configuration.
/// Without a matching rule, the number of sockets stays random between one and the maximum of the item.
/// </summary>
/// <remarks>
/// A rule of the killed monster wins over a rule of the map. The number of sockets never exceeds the maximum of the item.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.DropSocketCountPlugIn_Name), Description = nameof(PlugInResources.DropSocketCountPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F8A44E01-E009-42CB-B55F-504B482590D6")]
public class DropSocketCountPlugIn : IMonsterItemDroppingPlugIn, ISupportCustomConfiguration<DropSocketCountConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    public DropSocketCountConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new DropSocketCountConfiguration();
    }

    /// <inheritdoc />
    public ValueTask MonsterItemDroppingAsync(AttackableNpcBase monster, Player killer, Item item)
    {
        var maximumSockets = item.Definition?.MaximumSockets ?? 0;
        if (maximumSockets <= 0 || this.Configuration?.Rules is not { Count: > 0 } rules)
        {
            return ValueTask.CompletedTask;
        }

        var mapNumber = monster.CurrentMap?.Definition.Number;
        var rule = rules.FirstOrDefault(r => r.MonsterNumber == monster.Definition.Number)
                   ?? rules.FirstOrDefault(r => r.MonsterNumber is null && r.MapNumber is not null && r.MapNumber == mapNumber);
        if (rule is null)
        {
            return ValueTask.CompletedTask;
        }

        var maximum = Math.Min(rule.MaximumSockets, maximumSockets);
        var minimum = Math.Clamp(rule.MinimumSockets, 1, maximum);
        item.SocketCount = Rand.NextInt(minimum, maximum + 1);
        return ValueTask.CompletedTask;
    }
}
