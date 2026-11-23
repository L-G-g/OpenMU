// <copyright file="EventRework.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.GameLogic.PlugIns.EventRework;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The rework of the events of PeroniaMU, see <see cref="EventReworkPlugIn"/>: activates the plugin, and lets Devil Square,
/// Blood Castle and Chaos Castle follow each other in a cycle: Blood Castle opens 10 minutes after Devil Square ended,
/// Chaos Castle 10 minutes after Blood Castle ended, and Devil Square 45 minutes after Chaos Castle ended. An event ends
/// when its players are taken out (entrance, countdown, game and exit durations of its definitions, rounded up to minutes).
/// The events are announced in golden messages every minute from 5 minutes before each opening.
/// </summary>
internal static class EventRework
{
    /// <summary>
    /// The pause after Devil Square and after Blood Castle until the next event opens.
    /// </summary>
    internal static readonly TimeSpan ShortPause = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The pause after Chaos Castle until Devil Square opens again.
    /// </summary>
    internal static readonly TimeSpan LongPause = TimeSpan.FromMinutes(45);

    /// <summary>
    /// The countdown of a mini game between the closing of the entrance and the start of the game.
    /// </summary>
    private static readonly TimeSpan CountdownDuration = TimeSpan.FromSeconds(30);

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
        var devilSquare = GetEventDuration(gameConfiguration, MiniGameType.DevilSquare);
        var bloodCastle = GetEventDuration(gameConfiguration, MiniGameType.BloodCastle);
        var chaosCastle = GetEventDuration(gameConfiguration, MiniGameType.ChaosCastle);
        var bloodCastleOffset = devilSquare + ShortPause;
        var chaosCastleOffset = bloodCastleOffset + bloodCastle + ShortPause;
        var cycleLength = chaosCastleOffset + chaosCastle + LongPause;
        Configure<DevilSquareStartPlugIn, DevilSquareStartConfiguration>(context, gameConfiguration, DevilSquareStartConfiguration.Default, "Devil Square", cycleLength, TimeSpan.Zero);
        Configure<BloodCastleStartPlugIn, BloodCastleStartConfiguration>(context, gameConfiguration, BloodCastleStartConfiguration.Default, "Blood Castle", cycleLength, bloodCastleOffset);
        Configure<ChaosCastleStartPlugIn, ChaosCastleStartConfiguration>(context, gameConfiguration, ChaosCastleStartConfiguration.Default, "Chaos Castle", cycleLength, chaosCastleOffset);

        var rework = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(EventReworkPlugIn).GUID);
        rework.IsActive = true;
        if (rework.GetConfiguration<EventReworkConfiguration>(ReferenceHandler()) is null)
        {
            rework.SetConfiguration(new EventReworkConfiguration(), ReferenceHandler());
        }

        GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(EventBoardChatCommandPlugIn).GUID).IsActive = true;
        UseOneTicketPerEvent(gameConfiguration);
        EventMonsterSpots.Apply(context, gameConfiguration);
    }

    /// <summary>
    /// There is one ticket per event, because the level is chosen by the resets: Lumen sells only the +1 of the Devil's Eye and Key
    /// and of the Scroll of Archangel and Blood Bone (the ingredients of the Devil's Invitation +1 and the Cloak of Invisibility +1), and only the Symbol of Kundun +1 drops (from all the monsters which dropped one of the
    /// levels before). Tickets of other levels which the players already have still work.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    private static void UseOneTicketPerEvent(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == EventEntryItemShop.LumenTheBarmaidNumber)?.MerchantStore is { } store)
        {
            foreach (var item in store.Items.Where(IsTicketOfHigherLevel).ToList())
            {
                store.Items.Remove(item);
            }
        }

        var symbols = Enumerable.Range(1, MapLevelLadder.SymbolOfKundunDropLevels.Count)
            .Select(level => (Level: level, Group: gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == GuidHelper.CreateGuid<DropItemGroup>(14, 29, (byte)level))))
            .Where(s => s.Group is not null)
            .ToList();
        if (symbols.FirstOrDefault(s => s.Level == 1).Group is not { } first)
        {
            return;
        }

        first.MinimumMonsterLevel = MapLevelLadder.SymbolOfKundunDropLevels[0].MinimumLevel;
        first.MaximumMonsterLevel = byte.MaxValue;
        foreach (var (_, group) in symbols.Where(s => s.Level > 1))
        {
            // A monster level can't be at least 255 and at most 0 at the same time: it doesn't drop anymore.
            group!.MinimumMonsterLevel = byte.MaxValue;
            group.MaximumMonsterLevel = 0;
        }
    }

    private static bool IsTicketOfHigherLevel(Item item)
    {
        return item.Level > 1
               && item.Definition is { } definition
               && ((definition.Group == 14 && definition.Number is 17 or 18 or 19) // Devil's Eye, Devil's Key, Devil's Invitation
                   || (definition.Group == 13 && definition.Number is 16 or 17 or 18)); // Scroll of Archangel, Blood Bone, Cloak of Invisibility
    }

    /// <summary>
    /// The configurations of the plugins are stored with ids and references (as the other plugin configurations of the initialization).
    /// Reading them without it fails on their collections.
    /// </summary>
    private static ConfigureNpcTalkPlugInsPlugInBase.IdReferenceHandler ReferenceHandler() => new();

    private static void Configure<TPlugIn, TConfiguration>(IContext context, GameConfiguration gameConfiguration, TConfiguration defaultConfiguration, string eventName, TimeSpan cycleLength, TimeSpan offset)
        where TConfiguration : MiniGameStartConfiguration
    {
        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(TPlugIn).GUID);
        var configuration = plugInConfiguration.GetConfiguration<TConfiguration>(ReferenceHandler()) ?? defaultConfiguration;
        configuration.CycleLength = cycleLength;
        configuration.CycleOffset = offset;
        configuration.Timetable = new List<TimeOnly>();
        configuration.PreStartMessageDelay = TimeSpan.Zero;
        configuration.PreOpenAnnouncementMinutes = PreOpenAnnouncementMinutes;
        configuration.PreOpenMessage = $"Faltan {{0}} min para que abra {eventName}.";
        configuration.EntranceOpenedMessage = $"{eventName} esta abierto, cierra en {{0}} min.";
        configuration.EntranceClosedMessage = $"Se cerro la entrada a {eventName}.";
        plugInConfiguration.SetConfiguration(configuration, ReferenceHandler());
    }

    /// <summary>
    /// Gets the time from the opening of an event until its players are taken out, rounded up to whole minutes:
    /// the longest of its definitions.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="type">The type of the event.</param>
    /// <returns>The duration.</returns>
    private static TimeSpan GetEventDuration(GameConfiguration gameConfiguration, MiniGameType type)
    {
        var duration = gameConfiguration.MiniGameDefinitions
            .Where(d => d.Type == type)
            .Select(d => d.EnterDuration + CountdownDuration + d.GameDuration + d.ExitDuration)
            .DefaultIfEmpty(TimeSpan.FromMinutes(30))
            .Max();
        return TimeSpan.FromMinutes(Math.Ceiling(duration.TotalMinutes));
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
