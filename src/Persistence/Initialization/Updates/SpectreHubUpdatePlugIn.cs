// <copyright file="SpectreHubUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the spectre hub, where the players unlock and evolve their spectres, see <see cref="SpectreHubSetup"/>.
/// </summary>
/// <remarks>
/// It's optional, because it adds content instead of correcting data.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.SpectreHubUpdatePlugIn_Name), Description = nameof(PlugInResources.SpectreHubUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7A3C9E1B-5D2F-4B8A-A6C4-0E9D2F4B6A83")]
public class SpectreHubUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Spectre Hub";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the spectre hub: the Guardian of the spectres in Lorencia lets a player enter its own hub (the world of the character selection), where a rival of each class waits. Beating a rival in a duel in the Duel Arena unlocks its spectre, and beating it again makes the spectre evolve. Requires the plugin of the spectres and the PeroniaMU client.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        SpectreHubSetup.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
