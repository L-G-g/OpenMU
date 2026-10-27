// <copyright file="SpectreChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command which handles the spectres of a player (see <see cref="SpectreFeaturePlugIn"/>):
/// <c>/espectro guerrero|mago|elfa</c> summons or dismisses a spectre, <c>/espectro fuera</c> dismisses all,
/// <c>/espectro info</c> shows them, <c>/espectro reset &lt;kind&gt;</c> resets one, and
/// <c>/espectro puntos &lt;kind&gt; &lt;str&gt; &lt;agi&gt; &lt;vit&gt; &lt;ene&gt;</c> sets the percentages by which it spends its points,
/// <c>/espectro equipar &lt;kind&gt; &lt;inventory slot&gt;</c> equips an item of the inventory on it,
/// <c>/espectro quitar &lt;kind&gt; &lt;equipment slot&gt;</c> takes an item off, and <c>/espectro panel</c>
/// sends the state of the spectres to the spectre window of the client.
/// </summary>
[Guid("8E2F4A6C-1B3D-4C5E-8F7A-9B0C1D2E3F40")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SpectreChatCommandPlugIn_Name), Description = nameof(PlugInResources.SpectreChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class SpectreChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/espectro";

    private const string Usage = "Uso: /espectro guerrero|mago|elfa, /espectro fuera, /espectro info, /espectro reset <clase>, /espectro puntos <clase> <fue> <agi> <vit> <ene>, /espectro equipar <clase> <lugar>, /espectro quitar <clase> <lugar>";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<SpectreFeaturePlugIn>() is not { } feature)
        {
            await player.ShowBlueMessageAsync("Los espectros no estan activados.").ConfigureAwait(false);
            return;
        }

        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).ToList();
        var action = arguments.FirstOrDefault()?.ToLowerInvariant();
        switch (action)
        {
            case "fuera":
                await SpectreFeaturePlugIn.DismissAllAsync(player).ConfigureAwait(false);
                await player.ShowBlueMessageAsync("Tus espectros se fueron.").ConfigureAwait(false);
                await SpectreFeaturePlugIn.SendPanelAsync(player).ConfigureAwait(false);
                return;
            case "info":
                await feature.ShowInfoAsync(player).ConfigureAwait(false);
                return;
            case "reset" when arguments.Count == 2 && TryParseKind(arguments[1], out var kind):
                await feature.ResetAsync(player, kind).ConfigureAwait(false);
                return;
            case "panel":
                await SpectreFeaturePlugIn.SendPanelAsync(player).ConfigureAwait(false);
                return;
            case "equipar" when arguments.Count == 3 && TryParseKind(arguments[1], out var kind) && byte.TryParse(arguments[2], out var slot):
                await feature.EquipAsync(player, kind, slot).ConfigureAwait(false);
                return;
            case "quitar" when arguments.Count == 3 && TryParseKind(arguments[1], out var kind) && byte.TryParse(arguments[2], out var slot):
                await feature.UnequipAsync(player, kind, slot).ConfigureAwait(false);
                return;
            case "puntos" when arguments.Count == 6 && TryParseKind(arguments[1], out var kind):
                var percentages = arguments.Skip(2).Select(a => int.TryParse(a, out var value) ? value : -1).ToList();
                await feature.SetStatPercentagesAsync(player, kind, percentages).ConfigureAwait(false);
                return;
            case not null when arguments.Count == 1 && TryParseKind(action, out var kind):
                await feature.ToggleAsync(player, kind).ConfigureAwait(false);
                return;
            default:
                await player.ShowBlueMessageAsync(Usage).ConfigureAwait(false);
                return;
        }
    }

    private static bool TryParseKind(string text, out SpectreKind kind)
    {
        switch (text.ToLowerInvariant())
        {
            case "guerrero" or "g":
                kind = SpectreKind.Warrior;
                return true;
            case "mago" or "m":
                kind = SpectreKind.Mage;
                return true;
            case "elfa" or "e":
                kind = SpectreKind.Elf;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
