// <copyright file="MarketDepotNpcUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Mercader, which opens the market depot, see <see cref="MarketDepotNpc"/>.
/// </summary>
/// <remarks>
/// It's optional, because it's a custom feature of the market of the web portal.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.MarketDepotNpcUpdatePlugIn_Name), Description = nameof(PlugInResources.MarketDepotNpcUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6E1B3F0A-6D52-4E1C-9C1F-2B7E5A9D4C31")]
public class MarketDepotNpcUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Market Depot NPC";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Mercader next to the vault keeper of Lorencia, shown by the client as a second Market Union Member Julia. Talking to her opens the market depot of the account, a second vault for the items of the web market, which only takes tradable items. The original Julia keeps warping to the Loren Market.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MarketDepotNpc.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
