// <copyright file="FixElvenlandMonsterNumbersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Corrects the numbers of the Polluted Butterfly, the Hideous Rabbit and the Werewolf of Elvenland,
/// so that they match the monster table of the original Season 6 Episode 3 client.
/// </summary>
/// <remarks>
/// The client shows the name and the model of a monster by its number. The definitions had the numbers
/// 419 (Polluted Butterfly), 420 (Hideous Rabbit) and 421 (Werewolf), but in the client 419 is the
/// Hideous Rabbit, 420 the Werewolf and 421 the Polluted Butterfly. So players saw e.g. the stats of the
/// Werewolf on a monster which looked and was named like a Polluted Butterfly.
/// The definitions keep their stats and spawn areas, only their numbers change.
/// The quest kill requirements keep the number which they had before, because it comes from the original
/// quest data: e.g. the summoner quests which required number 419 now require the Hideous Rabbit.
/// </remarks>
[PlugIn]
[Display(Name = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.FixElvenlandMonsterNumbersUpdatePlugIn_Name), Description = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.FixElvenlandMonsterNumbersUpdatePlugIn_Description), ResourceType = typeof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources))]
[Guid("679578A6-B57E-4D3D-AA63-C5FBD9488A87")]
public class FixElvenlandMonsterNumbersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Elvenland Monster Numbers";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Corrects the numbers of the Polluted Butterfly (421), Hideous Rabbit (419) and Werewolf (420), so that the client shows the right name and model for their stats.";

    /// <summary>
    /// The previous and the corrected number of each definition. The definitions are identified by
    /// their id, which the data initialization derived from the previous number.
    /// </summary>
    private static readonly (short PreviousNumber, short CorrectedNumber)[] Corrections =
    [
        (419, 421), // Polluted Butterfly
        (420, 419), // Hideous Rabbit
        (421, 420), // Werewolf
    ];

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var monsters = Corrections
            .Select(c => (c.PreviousNumber, c.CorrectedNumber, Monster: gameConfiguration.Monsters.FirstOrDefault(m => m.GetId() == GuidHelper.CreateGuid<MonsterDefinition>(c.PreviousNumber))))
            .ToList();
        if (monsters.Any(m => m.Monster is null))
        {
            // The owner removed or replaced one of the definitions, so we don't touch them.
            return ValueTask.CompletedTask;
        }

        // The kill requirements refer to the definition which has the number of the original quest data.
        var requirements = gameConfiguration.Monsters
            .SelectMany(m => m.Quests)
            .SelectMany(q => q.RequiredMonsterKills)
            .Where(r => monsters.Any(m => m.Monster == r.Monster))
            .Select(r => (Requirement: r, r.Monster!.Number))
            .ToList();

        foreach (var (_, correctedNumber, monster) in monsters)
        {
            monster!.Number = correctedNumber;
        }

        foreach (var (requirement, number) in requirements)
        {
            requirement.Monster = monsters.First(m => m.Monster!.Number == number).Monster;
        }

        return ValueTask.CompletedTask;
    }
}
