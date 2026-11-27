// <copyright file="EventReworkUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Activates the rework of the events, see <see cref="EventRework"/>.
/// </summary>
/// <remarks>
/// It's optional, because it changes how the game plays instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.EventReworkUpdatePlugIn_Name), Description = nameof(PlugInResources.EventReworkUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E1B5C7A3-8F24-4D69-B3E0-6A9D2F4C7B58")]
public class EventReworkUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Event Rework";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Activates the plugin 'Event rework': Devil Square, Blood Castle, Chaos Castle and Kalima by resets (one entrance per event), Devil Square with experience rising from 1.5x to 3x and without drops, Blood Castle with an item for the player who delivers the weapon, Chaos Castle without experience and with a jewel per kill. Devil Square, Blood Castle and Chaos Castle follow each other in a cycle: Blood Castle opens 10 minutes after Devil Square ended, Chaos Castle 10 minutes after Blood Castle ended, and Devil Square 45 minutes after Chaos Castle ended (with the original durations: Devil Square at 0, Blood Castle at 39 and Chaos Castle at 76 minutes of a 138 minute cycle), announced in golden messages every minute from 5 minutes before each opening. Activates the chat command /eventos of the event board (key H). One ticket per event: Lumen sells only the +1 of the ingredients of the Devil's Invitation and the Cloak of Invisibility, only the Symbol of Kundun +1 drops, and Charon and the Messenger of Archangel enter the event of the resets directly (tickets of other levels still work). The monsters of Devil Square, Blood Castle and Kalima get the stats of the hunting maps at the resets of their level, rising during the game (Devil Square, Blood Castle) or along the way to Kundun (Kalima), and the monsters of Devil Square and Kalima are grouped into spots of 5 and hotspots of 7.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    /// <remarks>The rewards of Blood Castle use the drop maps of the dungeons; the store of Lumen and the drops of the Symbol of Kundun come from the map level ladder.</remarks>
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(MoreDungeonsUpdatePlugIn).GUID, (typeof(MapLevelLadderUpdatePlugIn).GUID, 2)];

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        EventRework.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
