// <copyright file="FlightChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Flight;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command <c>/volar</c>, which starts or ends the flight of the player on its Dinorant.
/// The flight.dll client mod sends it with a key, too.
/// </summary>
[Guid("A3C6E9F2-4B71-4D8A-B5E3-6F2A9C1D7E04")]
[PlugIn]
[Display(Name = nameof(PlugInResources.FlightChatCommandPlugIn_Name), Description = nameof(PlugInResources.FlightChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class FlightChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/volar";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<FlightPlugIn>() is not { } feature)
        {
            await player.ShowBlueMessageAsync("Volar no esta activado en este servidor.").ConfigureAwait(false);
            return;
        }

        await feature.ToggleAsync(player).ConfigureAwait(false);
    }
}
