// <copyright file="SpectreFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The spectre system: every character can summon spectres of the seven character classes, after unlocking them in the
/// spectre hub (see <see cref="SpectreProgress"/>). A spectre is a character of its own, which is stored in a hidden account
/// of the owner's account (see <see cref="GetLoginName"/>); each character of the owner has its own spectres. A summoned spectre
/// is in a party with its owner (see <see cref="SpectreParty"/>), follows its owner, buffs the party and hunts the monsters
/// around the owner (see <see cref="SpectreIntelligence"/>); it can't be hurt and doesn't duel. Its skills come from the
/// evolutions which the owner unlocked (see <see cref="SpectreEvolution"/>).
/// Inside safezones, mini games and duels, the spectres of a player disappear until the player leaves them.
/// The kills of a spectre count as kills of the owner; the spectres of a player share the experience which the
/// player gets for a kill, and spend their level up points by the percentages which the player chose.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SpectreFeaturePlugIn_Name), Description = nameof(PlugInResources.SpectreFeaturePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B0E3C1A-8D24-4F6B-9E7A-1C2D3E4F5A6B")]
public class SpectreFeaturePlugIn : IFeaturePlugIn, IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<SpectreConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>
    /// The prefix of the login names of the hidden accounts of the spectres.
    /// </summary>
    internal const string LoginPrefix = "sp";

    private const byte StatPercentagesMarker = (byte)'S';

    private static readonly ConcurrentDictionary<Player, OwnerState> Owners = new();

    private static readonly IncreaseStatsAction IncreaseStatsAction = new();

    /// <inheritdoc />
    public SpectreConfiguration? Configuration { get; set; }

    private static AttributeDefinition[] StatAttributes => SpectreClasses.StatAttributes;

    /// <summary>
    /// Gets the summoned spectres of a player.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The summoned spectres of the player.</returns>
    public static IReadOnlyCollection<SpectrePlayer> GetSpectres(Player owner)
        => Owners.TryGetValue(owner, out var state) ? state.Spectres.Values.ToList() : [];

    /// <summary>
    /// Gives the spectres of the players which got experience for a kill their share of it: the spectres of a
    /// player share the configured percentage of the experience which the player got.
    /// A player who gets master experience (a master class at the maximum level) gets much less of it than of the
    /// normal experience, so then the spectres share the normal experience which they would get for the kill.
    /// </summary>
    /// <param name="shares">The experience which the players got for the kill.</param>
    /// <param name="killed">The killed object.</param>
    /// <returns>The task.</returns>
    public static ValueTask ShareExperienceAsync(IEnumerable<ExperienceShare> shares, IAttackable killed)
    {
        if (Owners.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var share in shares)
        {
            if (!Owners.TryGetValue(share.Player, out var state)
                || state.Spectres.Values.ToList() is not { Count: > 0 } spectres)
            {
                continue;
            }

            var ownerGetsMasterExperience = GetsMasterExperience(share.Player);
            if (share.Experience <= 0 && !ownerGetsMasterExperience)
            {
                continue;
            }

            var percent = spectres[0].Configuration.ExperiencePercent;
            var count = spectres.Count;
            foreach (var spectre in spectres)
            {
                // Added within the tick of the spectre, so that the level-up doesn't run concurrently to its combat.
                spectre.PendingBotActions.Enqueue(async () =>
                {
                    long experience = ownerGetsMasterExperience
                        ? await spectre.CalculateExpAfterKillAsync(killed).ConfigureAwait(false)
                        : share.Experience;
                    experience = Math.Min(int.MaxValue, experience * percent / 100 / count);
                    if (experience > 0)
                    {
                        await spectre.AddExperienceAsync((int)experience, killed).ConfigureAwait(false);
                    }
                });
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new SpectreConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        foreach (var (owner, state) in Owners)
        {
            if (owner.GameContext != gameContext || !state.TryEnter())
            {
                continue;
            }

            try
            {
                if (!IsInGame(owner) || this.Configuration is null)
                {
                    await DismissAllAsync(owner).ConfigureAwait(false);
                }
                else if (MustHide(owner))
                {
                    await HideAsync(owner, state).ConfigureAwait(false);
                }
                else if (state.Spectres.Values.Any(spectre => spectre.Party is null || spectre.Party != owner.Party)
                         || owner.Party?.PartyList.Any(member => member != owner && member is not SpectrePlayer) is true)
                {
                    // The owner left the party, kicked a spectre or is in a party with other players.
                    await DismissAllAsync(owner).ConfigureAwait(false);
                    await SendPanelAsync(owner).ConfigureAwait(false);
                    await owner.ShowBlueMessageAsync("Tus espectros se fueron porque dejaron de estar en tu party.").ConfigureAwait(false);
                }
                else if (!state.HiddenKinds.IsEmpty && owner.CurrentMap is not null && owner.PlayerState.CurrentState == PlayerState.EnteredWorld)
                {
                    await ShowAgainAsync(owner, state, this.Configuration).ConfigureAwait(false);
                }
                else
                {
                    // Nothing to do.
                }
            }
            catch (Exception ex)
            {
                owner.Logger.LogError(ex, "Error while maintaining the spectres of {Owner}.", owner.Name);
            }
            finally
            {
                state.Exit();
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        // The task runs every second anyway.
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (player is not SpectrePlayer && !IsInGame(player) && Owners.ContainsKey(player))
        {
            await DismissAllAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Spends the level up points of a spectre by the percentages which its owner chose: the points which are
    /// invested in each stat approach the chosen percentage of all invested points.
    /// </summary>
    /// <param name="spectre">The spectre.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask SpendStatPointsAsync(SpectrePlayer spectre)
    {
        if (spectre.SelectedCharacter is not { LevelUpPoints: > 0 } character)
        {
            return;
        }

        var points = character.LevelUpPoints;
        var percentages = GetStatPercentages(character, spectre.Kind);

        // The points go to the stats which are furthest below their percentage of all invested points,
        // so that the build follows the percentages exactly, even when they change later.
        var invested = StatAttributes
            .Select(stat => (spectre.Attributes?[stat] ?? 0) - (character.CharacterClass?.StatAttributes.FirstOrDefault(a => a.Attribute == stat)?.BaseValue ?? 0))
            .Select(value => Math.Max(0L, (long)value))
            .ToArray();
        var total = invested.Sum() + points;
        var amounts = new int[StatAttributes.Length];
        for (var point = 0; point < points; point++)
        {
            var best = -1;
            var bestDeficit = 0.0;
            for (var i = 0; i < StatAttributes.Length; i++)
            {
                var deficit = (total * percentages[i] / 100.0) - invested[i] - amounts[i];
                if (percentages[i] > 0 && (best < 0 || deficit > bestDeficit))
                {
                    best = i;
                    bestDeficit = deficit;
                }
            }

            if (best < 0)
            {
                break;
            }

            amounts[best]++;
        }

        for (var i = 0; i < StatAttributes.Length; i++)
        {
            if (amounts[i] > 0)
            {
                await IncreaseStatsAction.IncreaseStatsAsync(spectre, StatAttributes[i], (ushort)Math.Min(amounts[i], ushort.MaxValue)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Dismisses all summoned spectres of a player.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask DismissAllAsync(Player owner)
    {
        if (!Owners.TryRemove(owner, out var state))
        {
            return;
        }

        foreach (var kind in state.Spectres.Keys.ToList())
        {
            if (state.Spectres.TryRemove(kind, out var spectre))
            {
                await StopAsync(spectre).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Gets the name of a kind of spectres, as shown to the players.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    internal static string GetKindName(SpectreKind kind) => SpectreClasses.GetName(kind);

    /// <summary>
    /// Gets the login name of the hidden account which holds the spectres of the characters of an account.
    /// </summary>
    /// <param name="ownerAccount">The account of the owner.</param>
    /// <returns>The login name.</returns>
    internal static string GetLoginName(Account ownerAccount)
    {
        // A hash of the whole id, because ids may differ in a few bytes only. The login name has up to 10 characters.
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(ownerAccount.GetId().ToByteArray()));
        var name = new char[8];
        for (var i = 0; i < name.Length; i++)
        {
            name[i] = "0123456789abcdefghijklmnopqrstuvwxyz"[(int)(hash % 36)];
            hash /= 36;
        }

        return LoginPrefix + new string(name);
    }

    /// <summary>
    /// Sends the state of the spectres of a player to the spectre window of the player's client.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask SendPanelAsync(Player owner)
    {
        if (owner is SpectrePlayer)
        {
            return;
        }

        var spectres = Owners.TryGetValue(owner, out var state) ? state.Spectres : null;
        var character = owner.SelectedCharacter;
        var entries = SpectreClasses.AllKinds
            .Select(kind =>
            {
                var victories = character is null ? 0 : SpectreProgress.GetVictories(character, kind);
                return (spectres?.TryGetValue(kind, out var spectre) == true ? CreatePanelEntry(spectre, victories) : null)
                       ?? SpectrePanelEntry.NotSummoned(kind, victories);
            })
            .ToList();
        await owner.InvokeViewPlugInAsync<IShowSpectresPlugIn>(p => p.ShowSpectrePanelAsync(entries)).ConfigureAwait(false);
    }

    /// <summary>
    /// Summons a spectre of the player, or dismisses it, if it's already summoned.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ToggleAsync(Player owner, SpectreKind kind)
    {
        if (Owners.TryGetValue(owner, out var existing) && (existing.Spectres.ContainsKey(kind) || existing.HiddenKinds.TryRemove(kind, out _)))
        {
            await DismissAsync(owner, kind).ConfigureAwait(false);
            await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} se fue.").ConfigureAwait(false);
            await SendPanelAsync(owner).ConfigureAwait(false);
            return;
        }

        await this.SummonAsync(owner, kind).ConfigureAwait(false);
        await SendPanelAsync(owner).ConfigureAwait(false);
    }

    /// <summary>
    /// Summons a spectre of the player.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <returns>The task.</returns>
    internal async ValueTask SummonAsync(Player owner, SpectreKind kind)
    {
        if (this.Configuration is not { } configuration || owner is SpectrePlayer || owner.Account is null || owner.SelectedCharacter is null)
        {
            return;
        }

        if (IsInMiniGameOrDuel(owner) || owner.CurrentMap is null)
        {
            await owner.ShowBlueMessageAsync("No podes invocar espectros aca.").ConfigureAwait(false);
            return;
        }

        if (!SpectreProgress.IsUnlocked(owner.SelectedCharacter, kind))
        {
            await owner.ShowBlueMessageAsync($"Primero tenes que vencer al espectro {GetKindName(kind)} en el refugio de los espectros (Guardian en Lorencia).").ConfigureAwait(false);
            return;
        }

        if (owner.GetPlayerParty() is not null)
        {
            await owner.ShowBlueMessageAsync("No podes invocar espectros estando en party con otros jugadores.").ConfigureAwait(false);
            return;
        }

        var state = Owners.GetOrAdd(owner, _ => new OwnerState());
        if (owner.IsAtSafezone())
        {
            // The spectre appears when the owner leaves the safezone (see ExecuteTaskAsync).
            if (state.Spectres.Count + state.HiddenKinds.Count >= configuration.MaximumActiveSpectres)
            {
                await owner.ShowBlueMessageAsync($"Ya tenes {configuration.MaximumActiveSpectres} espectros invocados.").ConfigureAwait(false);
                return;
            }

            state.HiddenKinds[kind] = true;
            await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} va a aparecer cuando salgas de la zona segura.").ConfigureAwait(false);
            return;
        }

        if (!state.TryEnter())
        {
            return;
        }

        try
        {
            if (state.Spectres.ContainsKey(kind))
            {
                return;
            }

            if (state.Spectres.Count >= configuration.MaximumActiveSpectres)
            {
                await owner.ShowBlueMessageAsync($"Ya tenes {configuration.MaximumActiveSpectres} espectros invocados.").ConfigureAwait(false);
                return;
            }

            if (await SpawnAsync(owner, kind, configuration).ConfigureAwait(false) is not { } spectre)
            {
                await owner.ShowBlueMessageAsync("No se pudo invocar el espectro.").ConfigureAwait(false);
                return;
            }

            await owner.ShowBlueMessageAsync($"Invocaste a tu espectro {GetKindName(kind)} {spectre.Name}.").ConfigureAwait(false);
        }
        finally
        {
            state.Exit();
        }
    }

    /// <summary>
    /// Shows the summoned spectres of a player, with their level, resets and stat percentages.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ShowInfoAsync(Player owner)
    {
        var spectres = GetSpectres(owner);
        if (spectres.Count == 0)
        {
            await owner.ShowBlueMessageAsync("No tenes espectros invocados.").ConfigureAwait(false);
            return;
        }

        foreach (var spectre in spectres.OrderBy(s => s.Kind))
        {
            var character = spectre.SelectedCharacter!;
            var percentages = GetStatPercentages(character, spectre.Kind);
            var hasLeadership = SpectreClasses.HasLeadership(spectre.Kind);
            await owner.ShowBlueMessageAsync(
                $"{GetKindName(spectre.Kind)} {spectre.Name}: nivel {spectre.Level} ({character.Experience} exp), resets {(int)(spectre.Attributes?[Stats.Resets] ?? 0)}, "
                + $"FUE {Stat(Stats.BaseStrength)} AGI {Stat(Stats.BaseAgility)} VIT {Stat(Stats.BaseVitality)} ENE {Stat(Stats.BaseEnergy)}"
                + (hasLeadership ? $" CMD {Stat(Stats.BaseLeadership)}" : string.Empty)
                + $", reparte {FormatPercentages(spectre.Kind, percentages)}").ConfigureAwait(false);

            int Stat(AttributeDefinition attribute) => (int)(spectre.Attributes?[attribute] ?? 0);
        }
    }

    /// <summary>
    /// Sets the stat percentages by which a summoned spectre spends its level up points.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <param name="percentages">The percentages of strength, agility, vitality, energy and (only for the lord) leadership.</param>
    /// <returns>The task.</returns>
    internal async ValueTask SetStatPercentagesAsync(Player owner, SpectreKind kind, IReadOnlyList<int> percentages)
    {
        var hasLeadership = SpectreClasses.HasLeadership(kind);
        var values = percentages.Count == StatAttributes.Length - 1 ? [.. percentages, 0] : percentages.ToList();
        if (values.Count != StatAttributes.Length || values.Any(p => p < 0) || values.Sum() != 100 || (!hasLeadership && values[^1] != 0))
        {
            await owner.ShowBlueMessageAsync(hasLeadership
                ? "Los porcentajes de FUE, AGI, VIT, ENE y CMD tienen que sumar 100."
                : "Los porcentajes de FUE, AGI, VIT y ENE tienen que sumar 100.").ConfigureAwait(false);
            return;
        }

        if (!Owners.TryGetValue(owner, out var state) || !state.Spectres.TryGetValue(kind, out var spectre))
        {
            await owner.ShowBlueMessageAsync($"Primero invoca a tu espectro {GetKindName(kind)}.").ConfigureAwait(false);
            return;
        }

        spectre.PendingBotActions.Enqueue(async () =>
        {
            if (spectre.SelectedCharacter is { } character)
            {
                character.KeyConfiguration = [StatPercentagesMarker, .. values.Select(p => (byte)p)];
                await SpendStatPointsAsync(spectre).ConfigureAwait(false);
                await SendPanelAsync(owner).ConfigureAwait(false);
            }
        });

        await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} reparte sus puntos: {FormatPercentages(kind, values.Select(p => (byte)p).ToArray())}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Resets a summoned spectre, by the rules of the reset feature. The owner pays the costs.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ResetAsync(Player owner, SpectreKind kind)
    {
        if (BotResetHandler.GetResetConfiguration(owner.GameContext) is not { } resetConfiguration)
        {
            await owner.ShowBlueMessageAsync("Los resets no estan activados.").ConfigureAwait(false);
            return;
        }

        if (!Owners.TryGetValue(owner, out var state) || !state.Spectres.TryGetValue(kind, out var spectre) || spectre.Attributes is not { } attributes)
        {
            await owner.ShowBlueMessageAsync($"Primero invoca a tu espectro {GetKindName(kind)}.").ConfigureAwait(false);
            return;
        }

        if (!BotResetHandler.IsResetDue(spectre, resetConfiguration))
        {
            var required = ResetProgressionCalculator.GetRequiredLevel(resetConfiguration, (int)attributes[Stats.Resets] + 1);
            await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} necesita nivel {required} para resetear.").ConfigureAwait(false);
            return;
        }

        var progression = ResetProgressionCalculator.Calculate((int)attributes[Stats.Resets], (int)attributes[Stats.PointsPerReset], resetConfiguration);
        if (!await TryConsumeResetCostsAsync(owner, resetConfiguration, progression).ConfigureAwait(false))
        {
            return;
        }

        spectre.PendingBotActions.Enqueue(async () =>
        {
            if (await BotResetHandler.TryResetAsync(spectre, resetConfiguration, false).ConfigureAwait(false))
            {
                await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} ahora tiene {(int)(spectre.Attributes?[Stats.Resets] ?? 0)} resets.").ConfigureAwait(false);
                await SendPanelAsync(owner).ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// Equips an item of the inventory of a player on a summoned spectre of the player.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <param name="inventorySlot">The slot of the item in the inventory of the owner.</param>
    /// <returns>The task.</returns>
    internal ValueTask EquipAsync(Player owner, SpectreKind kind, byte inventorySlot)
        => ChangeEquipmentAsync(owner, kind, spectre => SpectreEquipment.EquipAsync(spectre, inventorySlot));

    /// <summary>
    /// Takes an item off a summoned spectre of a player and puts it into the inventory of the player.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind of the spectre.</param>
    /// <param name="equipmentSlot">The equipment slot of the spectre.</param>
    /// <returns>The task.</returns>
    internal ValueTask UnequipAsync(Player owner, SpectreKind kind, byte equipmentSlot)
        => ChangeEquipmentAsync(owner, kind, spectre => SpectreEquipment.UnequipAsync(spectre, equipmentSlot));

    private static bool IsInGame(Player player)
    {
        var state = player.PlayerState.CurrentState;
        return player.SelectedCharacter is not null
               && state != PlayerState.Finished
               && state != PlayerState.Disconnected
               && state != PlayerState.CharacterSelection
               && state != PlayerState.Authenticated
               && state != PlayerState.LoginScreen
               && state != PlayerState.Initial;
    }

    private static string FormatPercentages(SpectreKind kind, IReadOnlyList<byte> percentages)
        => $"FUE {percentages[0]}% AGI {percentages[1]}% VIT {percentages[2]}% ENE {percentages[3]}%"
           + (SpectreClasses.HasLeadership(kind) ? $" CMD {percentages[4]}%" : string.Empty);

    private static SpectrePanelEntry? CreatePanelEntry(SpectrePlayer spectre, int victories)
    {
        if (spectre.SelectedCharacter is not { } character || spectre.Attributes is not { } attributes || spectre.Inventory is not { } inventory)
        {
            return null;
        }

        var equipment = Enumerable.Range(0, SpectrePanelEntry.EquipmentSlots).Select(slot => inventory.GetItem((byte)slot)).ToArray();
        return new SpectrePanelEntry(
            spectre.Kind,
            victories,
            true,
            spectre.Name,
            (int)attributes[Stats.Level],
            (int)attributes[Stats.Resets],
            character.LevelUpPoints,
            StatAttributes.Select(stat => (int)attributes[stat]).ToArray(),
            GetStatPercentages(character, spectre.Kind),
            equipment,
            equipment.Select(item => item is null ? string.Empty : SpectreEquipment.Describe(item)).ToArray());
    }

    private static async ValueTask ChangeEquipmentAsync(Player owner, SpectreKind kind, Func<SpectrePlayer, ValueTask<string>> change)
    {
        if (!Owners.TryGetValue(owner, out var state) || !state.Spectres.TryGetValue(kind, out var spectre))
        {
            await owner.ShowBlueMessageAsync($"Primero invoca a tu espectro {GetKindName(kind)}.").ConfigureAwait(false);
            return;
        }

        // Within the tick of the spectre, so that its equipment doesn't change during its combat.
        spectre.PendingBotActions.Enqueue(async () =>
        {
            string message;
            try
            {
                message = await owner.RunPersistenceExclusiveAsync(() => change(spectre)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                owner.Logger.LogError(ex, "Error while changing the equipment of the spectre {Name}.", spectre.Name);
                message = "No se pudo cambiar el equipo del espectro.";
            }

            await owner.ShowBlueMessageAsync(message).ConfigureAwait(false);
            await SendPanelAsync(owner).ConfigureAwait(false);
        });
    }

    private static bool GetsMasterExperience(Player player)
        => player.SelectedCharacter?.CharacterClass?.IsMasterClass == true
           && player.Level >= player.GameContext.Configuration.MaximumLevel;

    private static bool IsInMiniGameOrDuel(Player owner) => owner.CurrentMiniGame is not null || owner.DuelRoom is not null;

    private static bool MustHide(Player owner) => IsInMiniGameOrDuel(owner) || owner.IsAtSafezone();

    private static byte[] GetStatPercentages(Character character, SpectreKind kind)
    {
        // The first version of the spectres stored four percentages, without leadership.
        if (character.KeyConfiguration is { Length: 5 or 6 } data && data[0] == StatPercentagesMarker)
        {
            return data.Length == 6 ? data[1..] : [.. data[1..], 0];
        }

        return SpectreClasses.GetDefaultStatPercentages(kind);
    }

    private static async ValueTask HideAsync(Player owner, OwnerState state)
    {
        foreach (var kind in state.Spectres.Keys.ToList())
        {
            if (state.Spectres.TryRemove(kind, out var spectre))
            {
                state.HiddenKinds[kind] = true;
                await StopAsync(spectre).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask ShowAgainAsync(Player owner, OwnerState state, SpectreConfiguration configuration)
    {
        foreach (var kind in state.HiddenKinds.Keys.ToList())
        {
            state.HiddenKinds.TryRemove(kind, out _);
            if (state.Spectres.Count < configuration.MaximumActiveSpectres)
            {
                await SpawnAsync(owner, kind, configuration).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask DismissAsync(Player owner, SpectreKind kind)
    {
        if (Owners.TryGetValue(owner, out var state) && state.Spectres.TryRemove(kind, out var spectre))
        {
            await StopAsync(spectre).ConfigureAwait(false);
        }
    }

    private static async ValueTask StopAsync(SpectrePlayer spectre)
    {
        try
        {
            await SpectreParty.LeaveAsync(spectre).ConfigureAwait(false);
            await spectre.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            spectre.Logger.LogError(ex, "Error while stopping the spectre {Name}.", spectre.Name);
        }
        finally
        {
            await spectre.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask<SpectrePlayer?> SpawnAsync(Player owner, SpectreKind kind, SpectreConfiguration configuration)
    {
        if (!Owners.TryGetValue(owner, out var state))
        {
            return null;
        }

        var characterName = await GetOrCreateCharacterAsync(owner, kind).ConfigureAwait(false);
        if (characterName is null)
        {
            return null;
        }

        var spectre = new SpectrePlayer(owner.GameContext, owner, kind, configuration);
        if (!state.Spectres.TryAdd(kind, spectre))
        {
            await spectre.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        if (!await spectre.InitializeAsync(GetLoginName(owner.Account!), characterName).ConfigureAwait(false))
        {
            state.Spectres.TryRemove(kind, out _);
            await spectre.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        if (!await SpectreParty.JoinOwnerAsync(spectre).ConfigureAwait(false))
        {
            state.Spectres.TryRemove(kind, out _);
            await StopAsync(spectre).ConfigureAwait(false);
            await owner.ShowBlueMessageAsync("Tu espectro no pudo entrar a tu party.").ConfigureAwait(false);
            return null;
        }

        // Points which were earned while the spectre wasn't summoned, e.g. after a change of the percentages.
        BotSkillProgressionPlugIn.CatchUpPendingProgress(spectre);
        return spectre;
    }

    private static async ValueTask<string?> GetOrCreateCharacterAsync(Player owner, SpectreKind kind)
    {
        var gameContext = owner.GameContext;
        var ownerAccount = owner.Account!;
        var ownerCharacter = owner.SelectedCharacter!;
        var loginName = GetLoginName(ownerAccount);
        var slot = SpectreClasses.GetCharacterSlot(ownerCharacter.CharacterSlot, kind);
        var highestTier = SpectreProgress.GetVictories(ownerCharacter, kind) - 1;

        using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
        if (account is null)
        {
            account = context.CreateNew<Account>();
            account.LoginName = loginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 4);
            account.EMail = ownerAccount.LoginName;
            account.Vault = context.CreateNew<ItemStorage>();
        }
        else if (account.EMail != ownerAccount.LoginName)
        {
            owner.Logger.LogError("The spectre account {LoginName} belongs to {Other}, not to {Owner}.", loginName, account.EMail, ownerAccount.LoginName);
            return null;
        }
        else if (account.Characters.FirstOrDefault(c => c.CharacterSlot == slot) is { } existing)
        {
            await GrowAsync(owner, context, existing, kind, highestTier).ConfigureAwait(false);
            return await context.SaveChangesAsync().ConfigureAwait(false) ? existing.Name : null;
        }
        else
        {
            // The spectre is created below.
        }

        var characterClass = gameContext.Configuration.CharacterClasses.FirstOrDefault(c => c.Number == SpectreClasses.GetClassNumber(kind));
        if (characterClass is null)
        {
            owner.Logger.LogError("The character class of the spectre {Kind} doesn't exist.", kind);
            return null;
        }

        var name = await GenerateNameAsync(context, ownerCharacter.Name, kind).ConfigureAwait(false);
        var generator = new BotGenerator(gameContext, owner.Logger);
        var character = generator.CreateCharacter(context, account, name, characterClass, 1, slot, gameContext.ExperienceTable, 0, 0, false, null);
        character.Inventory!.Money = 0;
        character.KeyConfiguration = [StatPercentagesMarker, .. SpectreClasses.GetDefaultStatPercentages(kind)];
        await GrowAsync(owner, context, character, kind, highestTier).ConfigureAwait(false);
        if (!await context.SaveChangesAsync().ConfigureAwait(false))
        {
            return null;
        }

        owner.Logger.LogInformation("Created the spectre {Name} of {Owner}.", name, ownerCharacter.Name);
        return name;
    }

    private static async ValueTask GrowAsync(Player owner, IPlayerContext context, Character character, SpectreKind kind, int highestTier)
    {
        var blocked = await SpectreGrowth.ApplyAsync(context, owner.GameContext.Configuration, character, kind, highestTier).ConfigureAwait(false);
        foreach (var pet in Enum.GetValues<SpectreGrantedPet>().Where(pet => pet != SpectreGrantedPet.None && blocked.HasFlag(pet)))
        {
            var petName = pet switch
            {
                SpectreGrantedPet.DarkRaven => "el Dark Raven (quitale lo que tiene en la otra mano)",
                SpectreGrantedPet.DarkHorse => "el Dark Horse (quitale la mascota)",
                _ => "el Dinorant (quitale la mascota)",
            };
            await owner.ShowBlueMessageAsync($"Tu espectro {GetKindName(kind)} no pudo recibir {petName}.").ConfigureAwait(false);
        }
    }

    private static async ValueTask<string> GenerateNameAsync(IPlayerContext context, string ownerName, SpectreKind kind)
    {
        var suffix = SpectreClasses.GetLetter(kind);

        var baseName = ownerName.Length > 7 ? ownerName[..7] : ownerName;
        for (var i = 0; ; i++)
        {
            var name = i == 0 ? $"{baseName}_{suffix}" : $"{baseName}_{suffix}{i}";
            if (await context.GetAccountIdByCharacterNameAsync(name).ConfigureAwait(false) is null)
            {
                return name;
            }
        }
    }

    private static async ValueTask<bool> TryConsumeResetCostsAsync(Player owner, ResetConfiguration configuration, ResetProgression progression)
    {
        IList<Item> requiredItems = [];
        if (progression.RequiredItemAmount > 0 && configuration.RequiredResetItem is { } requiredDefinition)
        {
            requiredItems = owner.Inventory?.Items
                .Where(item => item.Definition is { } definition
                               && definition.Group == requiredDefinition.Group
                               && definition.Number == requiredDefinition.Number)
                .Take(progression.RequiredItemAmount)
                .ToList() ?? [];
            if (requiredItems.Count < progression.RequiredItemAmount)
            {
                await owner.ShowBlueMessageAsync($"Necesitas {progression.RequiredItemAmount} {requiredDefinition.Name} para resetear al espectro.").ConfigureAwait(false);
                return false;
            }
        }

        if (owner.Money < progression.RequiredZen
            || (progression.RequiredZen > 0 && !owner.TryRemoveMoney(progression.RequiredZen)))
        {
            await owner.ShowBlueMessageAsync($"Necesitas {progression.RequiredZen} de zen para resetear al espectro.").ConfigureAwait(false);
            return false;
        }

        foreach (var item in requiredItems)
        {
            await owner.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// The spectres of an owner.
    /// </summary>
    private sealed class OwnerState
    {
        private int _busy;

        /// <summary>
        /// Gets the summoned spectres, by kind.
        /// </summary>
        public ConcurrentDictionary<SpectreKind, SpectrePlayer> Spectres { get; } = new();

        /// <summary>
        /// Gets the kinds of the spectres which were hidden, because the owner entered a mini game or a duel.
        /// </summary>
        public ConcurrentDictionary<SpectreKind, bool> HiddenKinds { get; } = new();

        /// <summary>
        /// Tries to get exclusive access to the state.
        /// </summary>
        /// <returns><c>true</c>, if the access was granted.</returns>
        public bool TryEnter() => Interlocked.CompareExchange(ref this._busy, 1, 0) == 0;

        /// <summary>
        /// Releases the exclusive access.
        /// </summary>
        public void Exit() => Interlocked.Exchange(ref this._busy, 0);
    }
}
