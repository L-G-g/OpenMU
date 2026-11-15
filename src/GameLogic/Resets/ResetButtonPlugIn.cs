// <copyright file="ResetButtonPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tells the clients every second whether their character can do a reset right now (<see cref="ResetAvailability"/>),
/// so that the PeroniaMU client (resetbutton.dll) shows its reset button only then. The button sends the chat command /reset.
/// </summary>
/// <remarks>
/// The state is sent when it changes and, while the character can reset, again every few seconds,
/// because the client forgets it when it gets the character information again (e.g. after entering the game).
/// It does nothing without an active reset feature.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetButtonPlugIn_Name), Description = nameof(PlugInResources.ResetButtonPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5C1E8B7A-2F3D-4E9A-B6C4-8D7A1F2E3B90")]
public class ResetButtonPlugIn : IPeriodicTaskPlugIn
{
    private const int RepeatSeconds = 3;

    private readonly ConditionalWeakTable<Player, SentState> _sentStates = new();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (gameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>() is null)
        {
            return;
        }

        await gameContext.ForEachPlayerAsync(async player =>
        {
            if (player.PlayerState.CurrentState.IsDisconnectedOrFinished() || player.SelectedCharacter is null)
            {
                this._sentStates.Remove(player);
                return;
            }

            var available = ResetAvailability.CanResetNow(player);
            var state = this._sentStates.GetOrCreateValue(player);
            if (state.Character == player.SelectedCharacter
                && state.Available == available
                && (!available || state.SecondsSinceSent < RepeatSeconds))
            {
                state.SecondsSinceSent++;
                return;
            }

            state.Character = player.SelectedCharacter;
            state.Available = available;
            state.SecondsSinceSent = 1;
            await player.InvokeViewPlugInAsync<IShowResetAvailablePlugIn>(p => p.ShowResetAvailableAsync(available)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        // do nothing.
    }

    private sealed class SentState
    {
        public object? Character { get; set; }

        public bool Available { get; set; }

        public int SecondsSinceSent { get; set; }
    }
}
