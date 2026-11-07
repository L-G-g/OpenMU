// <copyright file="PortalService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The data access of the portal. It only reads what the game holds, and changes an account only while it's offline,
/// like the admin panel does.
/// </summary>
public sealed partial class PortalService
{
    /// <summary>The ranking changes slowly and is cheap to cache.</summary>
    private static readonly TimeSpan RankingCacheDuration = TimeSpan.FromSeconds(60);

    private readonly IPersistenceContextProvider _persistence;
    private readonly IDataSource<GameConfiguration> _configuration;
    private readonly ILoginServer _loginServer;
    private readonly IFriendServer _friendServer;
    private readonly IServerProvider _serverProvider;
    private readonly PortalSettings _settings;
    private readonly ILogger<PortalService> _logger;
    private readonly SemaphoreSlim _rankingLock = new(1);

    private (DateTime CreatedAt, IReadOnlyList<PortalCharacter> Ranking)? _rankingCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalService"/> class.
    /// </summary>
    /// <param name="persistence">The persistence context provider.</param>
    /// <param name="configuration">The game configuration source.</param>
    /// <param name="loginServer">The login server, which knows the logged in accounts.</param>
    /// <param name="friendServer">The friend server, which knows the online characters.</param>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="settings">The portal settings.</param>
    /// <param name="logger">The logger.</param>
    public PortalService(
        IPersistenceContextProvider persistence,
        IDataSource<GameConfiguration> configuration,
        ILoginServer loginServer,
        IFriendServer friendServer,
        IServerProvider serverProvider,
        PortalSettings settings,
        ILogger<PortalService> logger)
    {
        this._persistence = persistence;
        this._configuration = configuration;
        this._loginServer = loginServer;
        this._friendServer = friendServer;
        this._serverProvider = serverProvider;
        this._settings = settings;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the public state of the server.
    /// </summary>
    /// <returns>The state.</returns>
    public PortalServerInfo GetServerInfo()
    {
        var servers = this._serverProvider.Servers
            .OfType<IGameServer>()
            .OrderBy(s => s.Id)
            .Select(s => new PortalGameServer(s.Id, s.Description, s.ServerState == ServerState.Started, s.CurrentConnections, s.MaximumConnections))
            .ToList();
        return new PortalServerInfo(
            this._settings.ServerName,
            servers.Where(s => s.IsOnline).Sum(s => s.Players),
            servers,
            this._settings.GetInviteCodes().Count > 0,
            this._settings.ClientDownloadUrl);
    }

    /// <summary>
    /// Registers a new account.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the account was created; otherwise, the error code.</returns>
    public async ValueTask<string?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var codes = this._settings.GetInviteCodes();
        if (codes.Count == 0)
        {
            return "registration_closed";
        }

        if (!codes.Any(code => FixedTimeEquals(code, request.InviteCode?.Trim() ?? string.Empty)))
        {
            return "invalid_invite_code";
        }

        if (!IsValidLoginName(request.LoginName))
        {
            return "invalid_login_name";
        }

        if (!IsValidPassword(request.Password))
        {
            return "invalid_password";
        }

        var email = string.IsNullOrWhiteSpace(request.EMail) ? null : request.EMail.Trim();
        if (email is not null && (email.Length > 100 || !MailAddress.TryCreate(email, out _)))
        {
            return "invalid_email";
        }

        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = this._persistence.CreateNewPlayerContext(config);
        if (await context.GetAccountByLoginNameAsync(request.LoginName, cancellationToken).ConfigureAwait(false) is not null)
        {
            return "login_name_taken";
        }

        var account = context.CreateNew<Account>();
        account.LoginName = request.LoginName;
        account.EMail = email ?? string.Empty;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        account.State = AccountState.Normal;
        account.RegistrationDate = DateTime.UtcNow;
        try
        {
            if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
            {
                return "save_failed";
            }
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Registration of account {loginName} failed.", request.LoginName);
            return "login_name_taken";
        }

        this._logger.LogInformation("Portal: account {loginName} registered.", request.LoginName);
        return null;
    }

    /// <summary>
    /// Checks the credentials of an account.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the credentials are correct and the account may log in; otherwise, the error code.</returns>
    public async ValueTask<string?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidLoginName(request.LoginName) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 20)
        {
            return "wrong_credentials";
        }

        using var context = this._persistence.CreateNewPlayerContext(new GameConfiguration());
        var state = await context.AuthenticateAsync(request.LoginName, request.Password, cancellationToken).ConfigureAwait(false);
        return state switch
        {
            null => "wrong_credentials",
            AccountState.Banned or AccountState.TemporarilyBanned => "banned",
            _ => null,
        };
    }

    /// <summary>
    /// Gets the account of the logged in user with its characters.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account, or <c>null</c> if it doesn't exist anymore.</returns>
    public async ValueTask<PortalAccount?> GetAccountAsync(string loginName, CancellationToken cancellationToken)
    {
        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = this._persistence.CreateNewPlayerContext(config);
        var account = await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        var characters = new List<PortalCharacter>();
        foreach (var character in account.Characters.OrderBy(c => c.CharacterSlot))
        {
            var summary = await context.GetCharacterSummaryAsync(character.Name, Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, cancellationToken).ConfigureAwait(false);
            var isOnline = await this._friendServer.GetOnlineServerIdAsync(character.Name).ConfigureAwait(false) is not null;
            characters.Add(new PortalCharacter(
                character.Name,
                summary?.CharacterClassName ?? character.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
                summary?.Level ?? 0,
                summary?.MasterLevel ?? 0,
                summary?.Resets ?? 0,
                isOnline,
                character.CurrentMap?.Name.ValueInNeutralLanguage));
        }

        return new PortalAccount(
            account.LoginName,
            account.EMail,
            account.RegistrationDate,
            account.WCoinC,
            await this.IsAccountOnlineAsync(account.LoginName).ConfigureAwait(false),
            characters);
    }

    /// <summary>
    /// Changes the password of an account, which is not logged in at the game.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>null</c>, if the password was changed; otherwise, the error code.</returns>
    public async ValueTask<string?> ChangePasswordAsync(string loginName, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidPassword(request.NewPassword))
        {
            return "invalid_password";
        }

        if (await this.AuthenticateAsync(new LoginRequest { LoginName = loginName, Password = request.CurrentPassword }, cancellationToken).ConfigureAwait(false) is not null)
        {
            return "wrong_current_password";
        }

        if (await this.IsAccountOnlineAsync(loginName).ConfigureAwait(false))
        {
            return "account_online";
        }

        var config = await this._configuration.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = this._persistence.CreateNewPlayerContext(config);
        var account = await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return "wrong_current_password";
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) ? null : "save_failed";
    }

    /// <summary>
    /// Gets the best characters, by resets, master level and level.
    /// </summary>
    /// <param name="count">The maximum number of characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ranking.</returns>
    public async ValueTask<IReadOnlyList<PortalCharacter>> GetRankingAsync(int count, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 100);
        await this._rankingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._rankingCache is not { } cache || DateTime.UtcNow - cache.CreatedAt > RankingCacheDuration)
            {
                using var context = this._persistence.CreateNewPlayerContext(new GameConfiguration());
                var summaries = await context.GetCharacterRankingAsync(Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, 100, cancellationToken).ConfigureAwait(false);
                var ranking = new List<PortalCharacter>(summaries.Count);
                foreach (var s in summaries)
                {
                    var isOnline = await this._friendServer.GetOnlineServerIdAsync(s.Name).ConfigureAwait(false) is not null;
                    ranking.Add(new PortalCharacter(s.Name, s.CharacterClassName, s.Level, s.MasterLevel, s.Resets, isOnline, null));
                }

                cache = (DateTime.UtcNow, ranking);
                this._rankingCache = cache;
            }

            return cache.Ranking.Take(count).ToList();
        }
        finally
        {
            this._rankingLock.Release();
        }
    }

    private static bool IsValidLoginName(string? loginName)
        => loginName is { Length: >= 3 and <= 10 } && LoginNameRegex().IsMatch(loginName);

    private static bool IsValidPassword(string? password)
        => password is { Length: >= 3 and <= 20 } && PasswordRegex().IsMatch(password);

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    [GeneratedRegex(@"^[\x21-\x7E]+$")]
    private static partial Regex LoginNameRegex();

    [GeneratedRegex(@"^[\x20-\x7E]+$")]
    private static partial Regex PasswordRegex();

    private async ValueTask<bool> IsAccountOnlineAsync(string loginName)
        => (await this._loginServer.GetSnapshotAsync().ConfigureAwait(false)).Keys.Contains(loginName, StringComparer.OrdinalIgnoreCase);
}
