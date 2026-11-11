// <copyright file="AddExperienceLargePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The implementation of the <see cref="IAddExperiencePlugIn"/> for the season 6 client, which sends the
/// experience with the <see cref="ExperienceGainedLarge"/> packet.
/// Unlike <see cref="ExperienceGained"/>, it carries more than <see cref="ushort.MaxValue"/> experience points,
/// so the client shows the gained experience in one message instead of one message per 65535 points.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AddExperienceLargePlugIn_Name), Description = nameof(PlugInResources.AddExperienceLargePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7E3B9A41-58C2-4D6F-A0E7-3C9D1B2F8A64")]
[MinimumClient(6, 3, ClientLanguage.Invariant)]
public class AddExperienceLargePlugIn : IAddExperiencePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddExperienceLargePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public AddExperienceLargePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask AddExperienceAsync(int exp, IAttackable? obj, ExperienceType experienceType)
    {
        if (exp <= 0)
        {
            return;
        }

        ushort damage = 0;
        if (obj is not null && obj.Id != obj.LastDeath?.KillerId)
        {
            damage = (ushort)Math.Min(obj.LastDeath?.FinalHit.HealthDamage ?? 0, ushort.MaxValue);
        }

        var id = (ushort)(obj.GetId(this._player) | 0x8000);
        var addedExperience = (uint)exp;
        await this._player.Connection.SendExperienceGainedLargeAsync(id, (ushort)(addedExperience >> 16), (ushort)addedExperience, damage).ConfigureAwait(false);
    }
}
