// <copyright file="PortalCommunityService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.CastleSiege;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The public, read-only pages of the portal: character and guild profiles, who is online,
/// upcoming events, statistics and news. Nothing here changes the game state.
/// </summary>
public sealed class PortalCommunityService
{
    private static readonly TimeSpan ShortCache = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LongCache = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EventWindow = TimeSpan.FromHours(24);

    /// <summary>The names of the known scheduled plug-ins, by their type name.</summary>
    private static readonly IReadOnlyDictionary<string, string> EventNames = new Dictionary<string, string>
    {
        ["BloodCastleStartPlugIn"] = "Blood Castle",
        ["DevilSquareStartPlugIn"] = "Devil Square",
        ["ChaosCastleStartPlugIn"] = "Chaos Castle",
        ["KanturuStartPlugIn"] = "Kanturu",
        ["HappyHourPlugIn"] = "Happy Hour (más exp)",
        ["GoldenInvasionPlugIn"] = "Invasión dorada",
        ["RedDragonInvasionPlugIn"] = "Invasión del Dragón Rojo",
        ["WhiteWizardInvasionPlugIn"] = "Invasión del White Wizard",
        ["SkeletonKingInvasionPlugIn"] = "Invasión del Skeleton King",
        ["WanderingMerchantsPlugIn"] = "Mercaderes ambulantes",
        ["MapInvasionsPlugIn"] = "Invasión de mapa",
    };

    private readonly IPersistenceContextProvider _persistence;
    private readonly IDataSource<GameConfiguration> _configuration;
    private readonly IFriendServer _friendServer;
    private readonly IServerProvider _serverProvider;
    private readonly PlugInManager? _plugInManager;
    private readonly Func<TimeZoneInfo> _getServerTimeZone;
    private readonly PortalSettings _settings;
    private readonly ILogger<PortalCommunityService> _logger;
    private readonly ConcurrentDictionary<string, (DateTime CreatedAt, object Value)> _cache = new();
    private readonly SemaphoreSlim _lock = new(1);

    private (DateTime WrittenAt, IReadOnlyList<PortalNewsItem> Items)? _news;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalCommunityService"/> class.
    /// </summary>
    /// <param name="persistence">The persistence context provider.</param>
    /// <param name="configuration">The game configuration source.</param>
    /// <param name="friendServer">The friend server, which knows the online characters.</param>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="plugInManager">The plug-in manager with the scheduled events.</param>
    /// <param name="getServerTimeZone">Gets the time zone of the event schedules.</param>
    /// <param name="settings">The portal settings.</param>
    /// <param name="logger">The logger.</param>
    public PortalCommunityService(
        IPersistenceContextProvider persistence,
        IDataSource<GameConfiguration> configuration,
        IFriendServer friendServer,
        IServerProvider serverProvider,
        PlugInManager? plugInManager,
        Func<TimeZoneInfo> getServerTimeZone,
        PortalSettings settings,
        ILogger<PortalCommunityService> logger)
    {
        this._persistence = persistence;
        this._configuration = configuration;
        this._friendServer = friendServer;
        this._serverProvider = serverProvider;
        this._plugInManager = plugInManager;
        this._getServerTimeZone = getServerTimeZone;
        this._settings = settings;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the public profile of a character.
    /// </summary>
    /// <param name="name">The character name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile; or <c>null</c>, if the character doesn't exist.</returns>
    public async ValueTask<PortalCharacterProfile?> GetCharacterAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 10)
        {
            return null;
        }

        CharacterSummary? summary;
        using (var context = this._persistence.CreateNewPlayerContext(new GameConfiguration()))
        {
            summary = await context.GetCharacterSummaryAsync(name, Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, cancellationToken).ConfigureAwait(false);
        }

        if (summary is null)
        {
            return null;
        }

        string? guildName = null;
        string? guildPosition = null;
        using (var guildContext = this._persistence.CreateNewGuildContext())
        {
            if (await guildContext.GetGuildMembershipAsync(summary.Name).ConfigureAwait(false) is { } membership)
            {
                var names = await guildContext.GetPersistentGuildNamesAsync([membership.GuildId]).ConfigureAwait(false);
                guildName = names.GetValueOrDefault(membership.GuildId);
                guildPosition = PositionName(membership.Position);
            }
        }

        var online = (await this.GetOnlinePlayersAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(p => string.Equals(p.Name, summary.Name, StringComparison.OrdinalIgnoreCase));
        return new PortalCharacterProfile(
            summary.Name,
            Neutral(summary.CharacterClassName),
            online?.Level ?? summary.Level,
            online?.MasterLevel ?? summary.MasterLevel,
            online?.Resets ?? summary.Resets,
            online is not null,
            online?.ServerName,
            online?.MapName,
            guildName,
            guildPosition);
    }

    /// <summary>
    /// Gets the characters which are playing right now, read from the running game servers.
    /// Invisible game masters are left out.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The online players, best first.</returns>
    public ValueTask<IReadOnlyList<PortalOnlinePlayer>> GetOnlinePlayersAsync(CancellationToken cancellationToken)
        => this.GetCachedAsync<IReadOnlyList<PortalOnlinePlayer>>("online", ShortCache, this.LoadOnlinePlayersAsync, cancellationToken);

    /// <summary>
    /// Gets the guilds, with their members count and master.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The guilds, biggest first.</returns>
    public ValueTask<IReadOnlyList<PortalGuild>> GetGuildsAsync(CancellationToken cancellationToken)
        => this.GetCachedAsync<IReadOnlyList<PortalGuild>>("guilds", LongCache, this.LoadGuildsAsync, cancellationToken);

    /// <summary>
    /// Gets a guild with its members.
    /// </summary>
    /// <param name="name">The guild name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The guild; or <c>null</c>, if it doesn't exist.</returns>
    public async ValueTask<PortalGuildDetail?> GetGuildAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 8)
        {
            return null;
        }

        var guild = (await this.GetGuildsAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (guild is null)
        {
            return null;
        }

        var members = new List<PortalGuildMember>();
        using (var guildContext = this._persistence.CreateNewGuildContext())
        {
            var found = await guildContext.SearchGuildsAsync(guild.Name, 0, 5, cancellationToken).ConfigureAwait(false);
            var entity = found.FirstOrDefault(g => string.Equals(g.Name, guild.Name, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                return null;
            }

            var memberNames = await guildContext.GetMemberNamesAsync(entity.Id).ConfigureAwait(false);
            using var context = this._persistence.CreateNewPlayerContext(new GameConfiguration());
            foreach (var member in entity.Members)
            {
                if (!memberNames.TryGetValue(member.Id, out var memberName))
                {
                    continue;
                }

                var summary = await context.GetCharacterSummaryAsync(memberName, Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, cancellationToken).ConfigureAwait(false);
                var isOnline = await this._friendServer.GetOnlineServerIdAsync(memberName).ConfigureAwait(false) is not null;
                members.Add(new PortalGuildMember(
                    memberName,
                    PositionName(member.Status),
                    Neutral(summary?.CharacterClassName),
                    summary?.Level ?? 0,
                    summary?.MasterLevel ?? 0,
                    summary?.Resets ?? 0,
                    isOnline));
            }
        }

        return new PortalGuildDetail(
            guild,
            members
                .OrderBy(m => PositionOrder(m.Position))
                .ThenByDescending(m => m.Resets)
                .ThenByDescending(m => m.MasterLevel)
                .ThenByDescending(m => m.Level)
                .ToList());
    }

    /// <summary>
    /// Gets the event starts of the next 24 hours.
    /// </summary>
    /// <returns>The events, in the order of their start.</returns>
    public IReadOnlyList<PortalEvent> GetUpcomingEvents()
    {
        // The events run per game server, with the plug-in configuration of its game context.
        var gameContext = this.GetMainGameContext();
        var plugInManager = gameContext?.PlugInManager ?? this._plugInManager;
        if (plugInManager is null)
        {
            return [];
        }

        var timeZone = gameContext?.ServerTimeZone ?? this._getServerTimeZone();
        var now = DateTime.UtcNow;
        var end = now + EventWindow;
        var result = new List<PortalEvent>();
        foreach (var plugIn in plugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<IScheduledPlugIn>())
        {
            var type = plugIn.GetType();
            var key = type.Name;
            var name = EventNames.GetValueOrDefault(key) ?? GetDisplayName(type);
            var cursor = now;
            for (var i = 0; i < 200; i++)
            {
                if (plugIn.GetNextStartUtc(cursor, timeZone) is not { } start || start > end || start <= cursor)
                {
                    break;
                }

                result.Add(new PortalEvent(key.Replace("StartPlugIn", string.Empty).Replace("PlugIn", string.Empty), name, start));
                cursor = start;
            }
        }

        return result.OrderBy(e => e.StartsAtUtc).ToList();
    }

    /// <summary>
    /// Gets the state of the Castle Siege: owner, current phase, next battle and registered guilds.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state; <see cref="PortalCastleSiege.IsActive"/> is <c>false</c> when the event isn't running.</returns>
    public ValueTask<PortalCastleSiege> GetCastleSiegeAsync(CancellationToken cancellationToken)
        => this.GetCachedAsync("siege", ShortCache, this.LoadCastleSiegeAsync, cancellationToken);

    /// <summary>
    /// Gets numbers about the server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    public ValueTask<PortalStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
        => this.GetCachedAsync("stats", LongCache, this.LoadStatisticsAsync, cancellationToken);

    /// <summary>
    /// Gets the news from the news file.
    /// </summary>
    /// <returns>The news, newest first as written in the file.</returns>
    public IReadOnlyList<PortalNewsItem> GetNews()
    {
        var path = this._settings.NewsFile;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        try
        {
            var writtenAt = File.GetLastWriteTimeUtc(path);
            if (this._news is { } news && news.WrittenAt == writtenAt)
            {
                return news.Items;
            }

            var items = ParseNews(File.ReadAllLines(path));
            this._news = (writtenAt, items);
            return items;
        }
        catch (IOException ex)
        {
            this._logger.LogWarning(ex, "Portal: can't read the news file {path}.", path);
            return this._news?.Items ?? [];
        }
    }

    /// <summary>
    /// Parses the news file. An entry starts with <c>## date | title</c>; blank lines separate paragraphs.
    /// </summary>
    /// <param name="lines">The lines of the file.</param>
    /// <returns>The news entries.</returns>
    internal static IReadOnlyList<PortalNewsItem> ParseNews(IEnumerable<string> lines)
    {
        var result = new List<PortalNewsItem>();
        string? date = null;
        string? title = null;
        var paragraphs = new List<string>();
        var current = new List<string>();

        void EndParagraph()
        {
            if (current.Count > 0)
            {
                paragraphs.Add(string.Join(' ', current));
                current.Clear();
            }
        }

        void EndEntry()
        {
            EndParagraph();
            if (title is not null)
            {
                result.Add(new PortalNewsItem(date ?? string.Empty, title, paragraphs.ToList()));
            }

            paragraphs.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                EndEntry();
                var header = line[3..];
                var separator = header.IndexOf('|');
                date = separator >= 0 ? header[..separator].Trim() : string.Empty;
                title = separator >= 0 ? header[(separator + 1)..].Trim() : header.Trim();
            }
            else if (title is null || line.StartsWith('#'))
            {
                // Text before the first entry and other headings are comments for whoever edits the file.
            }
            else if (line.Length == 0)
            {
                EndParagraph();
            }
            else
            {
                current.Add(line);
            }
        }

        EndEntry();
        return result.Take(30).ToList();
    }

    private static string Neutral(string? localized)
        => string.IsNullOrEmpty(localized) ? string.Empty : new LocalizedString(localized).ValueInNeutralLanguage;

    private static string PositionName(GuildPosition position) => position switch
    {
        GuildPosition.GuildMaster => "Guild Master",
        GuildPosition.AssistantMaster => "Asistente",
        GuildPosition.BattleMaster => "Battle Master",
        _ => "Miembro",
    };

    private static int PositionOrder(string position) => position switch
    {
        "Guild Master" => 0,
        "Asistente" => 1,
        "Battle Master" => 2,
        _ => 3,
    };

    private static string GetDisplayName(Type type)
    {
        var display = type.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault();
        try
        {
            return display?.GetDescription() ?? display?.GetName() ?? type.Name;
        }
        catch (InvalidOperationException)
        {
            return type.Name;
        }
    }

    private async ValueTask<T> GetCachedAsync<T>(string key, TimeSpan duration, Func<CancellationToken, ValueTask<T>> load, CancellationToken cancellationToken)
        where T : class
    {
        if (this._cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.CreatedAt < duration)
        {
            return (T)entry.Value;
        }

        await this._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._cache.TryGetValue(key, out entry) && DateTime.UtcNow - entry.CreatedAt < duration)
            {
                return (T)entry.Value;
            }

            var value = await load(cancellationToken).ConfigureAwait(false);
            this._cache[key] = (DateTime.UtcNow, value);
            return value;
        }
        finally
        {
            this._lock.Release();
        }
    }

    private async ValueTask<IReadOnlyList<PortalOnlinePlayer>> LoadOnlinePlayersAsync(CancellationToken cancellationToken)
    {
        var result = new List<PortalOnlinePlayer>();
        foreach (var server in this._serverProvider.Servers.OfType<IGameServer>())
        {
            if (server is not IGameServerContextProvider { Context: { } context } || server.ServerState != ServerState.Started)
            {
                continue;
            }

            foreach (var player in await context.GetPlayersAsync().ConfigureAwait(false))
            {
                if (player.SelectedCharacter is not { } character
                    || player.Attributes is not { } attributes
                    || player.IsInvisible
                    || player.IsTemplatePlayer)
                {
                    continue;
                }

                result.Add(new PortalOnlinePlayer(
                    character.Name,
                    character.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
                    (int)attributes[Stats.Level],
                    (int)attributes[Stats.MasterLevel],
                    (int)attributes[Stats.Resets],
                    player.CurrentMap?.Definition.Name.ValueInNeutralLanguage,
                    server.Description));
            }
        }

        return result
            .OrderByDescending(p => p.Resets)
            .ThenByDescending(p => p.MasterLevel)
            .ThenByDescending(p => p.Level)
            .ToList();
    }

    private async ValueTask<IReadOnlyList<PortalGuild>> LoadGuildsAsync(CancellationToken cancellationToken)
    {
        using var guildContext = this._persistence.CreateNewGuildContext();
        var guilds = await guildContext.GetGuildsOrderedByNameAsync(0, 200, cancellationToken).ConfigureAwait(false);
        var masters = await guildContext.GetGuildMasterNamesAsync(cancellationToken).ConfigureAwait(false);
        return guilds
            .Where(g => !string.IsNullOrEmpty(g.Name))
            .Select(g => new PortalGuild(
                g.Name!,
                masters.GetValueOrDefault(g.Id),
                g.Members?.Count ?? 0,
                g.Score,
                g.Logo is { Length: > 0 } logo ? Convert.ToHexString(logo) : null,
                g.AllianceGuild?.Name))
            .OrderByDescending(g => g.MemberCount)
            .ThenByDescending(g => g.Score)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IGameServerContext? GetMainGameContext()
        => this._serverProvider.Servers
            .OfType<IGameServer>()
            .Where(s => s.ServerState == ServerState.Started)
            .OrderBy(s => s.Id)
            .OfType<IGameServerContextProvider>()
            .Select(s => s.Context)
            .FirstOrDefault();

    private async ValueTask<PortalCastleSiege> LoadCastleSiegeAsync(CancellationToken cancellationToken)
    {
        foreach (var server in this._serverProvider.Servers.OfType<IGameServer>().Where(s => s.ServerState == ServerState.Started).OrderBy(s => s.Id))
        {
            if (server is not IGameServerContextProvider { Context: { } context }
                || context.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<CastleSiegePlugIn>().FirstOrDefault() is not { } plugIn)
            {
                continue;
            }

            var snapshot = await new CastleSiegeAdministration(plugIn).GetSnapshotAsync(context).ConfigureAwait(false);
            if (snapshot is null)
            {
                continue;
            }

            string? ownerLogo = null;
            if (snapshot.IsOccupied && snapshot.OwnerGuildName is { } ownerName)
            {
                ownerLogo = (await this.GetGuildsAsync(cancellationToken).ConfigureAwait(false))
                    .FirstOrDefault(g => string.Equals(g.Name, ownerName, StringComparison.OrdinalIgnoreCase))?.Logo;
            }

            var schedule = context.Configuration.CastleSiegeConfiguration?.StateSchedule;
            var nextBattle = snapshot.State == CastleSiegeState.Start
                ? snapshot.StateStartTimeUtc
                : NextWeeklyStart(schedule?.FirstOrDefault(e => e.State == CastleSiegeState.Start), context.ServerTimeZone);
            return new PortalCastleSiege(
                true,
                snapshot.IsOccupied ? snapshot.OwnerGuildName : null,
                ownerLogo,
                snapshot.State.ToString(),
                SiegeStateName(snapshot.State),
                snapshot.StateEndTimeUtc,
                nextBattle,
                snapshot.Registrations
                    .Where(r => !r.IsGuildDeleted)
                    .Select(r => new PortalSiegeGuild(r.GuildName, r.Marks))
                    .ToList());
        }

        return new PortalCastleSiege(false, null, null, null, null, null, null, []);
    }

    private static DateTime? NextWeeklyStart(CastleSiegeStateScheduleEntry? entry, TimeZoneInfo timeZone)
    {
        if (entry is null)
        {
            return null;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        for (var day = 0; day <= 7; day++)
        {
            var date = nowLocal.Date.AddDays(day);
            if (date.DayOfWeek != entry.DayOfWeek)
            {
                continue;
            }

            var local = DateTime.SpecifyKind(date.AddHours(entry.Hour).AddMinutes(entry.Minute), DateTimeKind.Unspecified);
            if (local > nowLocal)
            {
                return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
            }
        }

        return null;
    }

    private static string SiegeStateName(CastleSiegeState state) => state switch
    {
        CastleSiegeState.RegisterGuild => "Inscripción de guilds",
        CastleSiegeState.RegisterMark => "Entrega de Signs of Lord",
        CastleSiegeState.Notify => "Anuncio de las guilds que pelean",
        CastleSiegeState.Ready => "Preparación para la batalla",
        CastleSiegeState.Start => "¡Batalla en curso!",
        CastleSiegeState.End => "Terminó la batalla",
        CastleSiegeState.EndCycle => "Fin del ciclo",
        _ => "En espera",
    };

        private async ValueTask<PortalStatistics> LoadStatisticsAsync(CancellationToken cancellationToken)
    {
        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        AccountStatistics accounts;
        using (var context = this._persistence.CreateNewPlayerContext(new GameConfiguration()))
        {
            accounts = await context.GetAccountStatisticsAsync(DateTime.UtcNow.AddDays(-7), cancellationToken).ConfigureAwait(false);
        }

        GuildStatistics guilds;
        using (var guildContext = this._persistence.CreateNewGuildContext())
        {
            guilds = await guildContext.GetGuildStatisticsAsync(cancellationToken).ConfigureAwait(false);
        }

        var online = this._serverProvider.Servers.OfType<IGameServer>()
            .Where(s => s.ServerState == ServerState.Started)
            .Sum(s => s.CurrentConnections);
        return new PortalStatistics(
            accounts.AccountCount,
            accounts.NewAccountCount,
            accounts.CharacterCount,
            guilds.GuildCount,
            online,
            config.ExperienceRate,
            config.MasterExperienceRate,
            config.MaximumLevel,
            config.MaximumMasterLevel);
    }
}
