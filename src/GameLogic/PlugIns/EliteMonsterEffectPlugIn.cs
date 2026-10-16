// <copyright file="EliteMonsterEffectPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Shows elite monsters with a visible magic effect, so that players can tell them apart from the normal
/// monsters which look the same. Elite monsters are the ones whose designation ends with
/// <see cref="EliteMonsterEffectConfiguration.DesignationSuffix"/>, e.g. the "Shadow Elite".
/// </summary>
/// <remarks>
/// The effect is only a visual: it has no power-ups. It lasts as long as the monster lives.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.EliteMonsterEffectPlugIn_Name), Description = nameof(PlugInResources.EliteMonsterEffectPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A8727F1E-9BA0-45E7-8538-E1C32481E113")]
public class EliteMonsterEffectPlugIn : IObjectAddedToMapPlugIn, ISupportCustomConfiguration<EliteMonsterEffectConfiguration>, ISupportDefaultCustomConfiguration
{
    private MagicEffectDefinition? _effectDefinition;

    /// <inheritdoc />
    public EliteMonsterEffectConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new EliteMonsterEffectConfiguration();
    }

    /// <inheritdoc />
    public async ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        var configuration = this.Configuration ??= new EliteMonsterEffectConfiguration();
        if (addedObject is not Monster monster
            || configuration.EffectNumber <= 0
            || string.IsNullOrEmpty(configuration.DesignationSuffix)
            || !monster.Definition.Designation.ValueInNeutralLanguage.EndsWith(configuration.DesignationSuffix, StringComparison.OrdinalIgnoreCase)
            || monster.MagicEffectList.ContainsEffect(configuration.EffectNumber))
        {
            return;
        }

        var definition = this.GetEffectDefinition(configuration.EffectNumber);
        await monster.MagicEffectList.AddEffectAsync(new MagicEffect(Timeout.InfiniteTimeSpan, definition)).ConfigureAwait(false);
    }

    private MagicEffectDefinition GetEffectDefinition(short number)
    {
        if (this._effectDefinition is { } definition && definition.Number == number)
        {
            return definition;
        }

        // It's not part of the configuration, because it's only a visual for the client and has no power-ups.
        definition = new MagicEffectDefinition
        {
            Number = number,
            Name = "Elite Monster",
            InformObservers = true,
            StopByDeath = false,
        };
        this._effectDefinition = definition;
        return definition;
    }
}
