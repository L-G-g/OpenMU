// <copyright file="EventRework.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.GameLogic.PlugIns.EventRework;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The rework of the events of PeroniaMU, see <see cref="EventReworkPlugIn"/>: activates the plugin, and lets Devil Square,
/// Blood Castle and Chaos Castle follow each other in a cycle of 65 minutes (Devil Square, 10 minutes later Blood Castle,
/// 10 minutes later Chaos Castle, 45 minutes later Devil Square again), with golden announcements every minute from
/// 5 minutes before each opening.
/// </summary>
internal static class EventRework
{
    /// <summary>
    /// The length of the cycle of the events.
    /// </summary>
    internal static readonly TimeSpan CycleLength = TimeSpan.FromMinutes(65);

    /// <summary>
    /// The minutes before the opening of an event, from which it's announced every minute.
    /// </summary>
    internal const int PreOpenAnnouncementMinutes = 5;

    /// <summary>
    /// Applies the rework.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        Configure<DevilSquareStartPlugIn, DevilSquareStartConfiguration>(context, gameConfiguration, DevilSquareStartConfiguration.Default, "Devil Square", TimeSpan.Zero);
        Configure<BloodCastleStartPlugIn, BloodCastleStartConfiguration>(context, gameConfiguration, BloodCastleStartConfiguration.Default, "Blood Castle", TimeSpan.FromMinutes(10));
        Configure<ChaosCastleStartPlugIn, ChaosCastleStartConfiguration>(context, gameConfiguration, ChaosCastleStartConfiguration.Default, "Chaos Castle", TimeSpan.FromMinutes(20));

        var rework = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(EventReworkPlugIn).GUID);
        rework.IsActive = true;
        if (rework.GetConfiguration<EventReworkConfiguration>(null) is null)
        {
            rework.SetConfiguration(new EventReworkConfiguration(), null);
        }

        GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(EventBoardChatCommandPlugIn).GUID).IsActive = true;
    }

    private static void Configure<TPlugIn, TConfiguration>(IContext context, GameConfiguration gameConfiguration, TConfiguration defaultConfiguration, string eventName, TimeSpan offset)
        where TConfiguration : MiniGameStartConfiguration
    {
        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(TPlugIn).GUID);
        var configuration = plugInConfiguration.GetConfiguration<TConfiguration>(null) ?? defaultConfiguration;
        configuration.CycleLength = CycleLength;
        configuration.CycleOffset = offset;
        configuration.Timetable = new List<TimeOnly>();
        configuration.PreStartMessageDelay = TimeSpan.Zero;
        configuration.PreOpenAnnouncementMinutes = PreOpenAnnouncementMinutes;
        configuration.PreOpenMessage = $"Faltan {{0}} min para que abra {eventName}.";
        configuration.EntranceOpenedMessage = $"{eventName} esta abierto, cierra en {{0}} min. Entrada unica por resets.";
        configuration.EntranceClosedMessage = $"Se cerro la entrada a {eventName}.";
        plugInConfiguration.SetConfiguration(configuration, null);
    }

    private static PlugInConfiguration GetOrCreatePlugInConfiguration(IContext context, GameConfiguration gameConfiguration, Guid typeId)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId) is { } existing)
        {
            return existing;
        }

        var plugInConfiguration = context.CreateNew<PlugInConfiguration>();
        plugInConfiguration.SetGuid(typeId);
        plugInConfiguration.TypeId = typeId;
        plugInConfiguration.IsActive = true;
        gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        return plugInConfiguration;
    }
}
