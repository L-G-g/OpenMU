// <copyright file="PortalHost.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Hosts the public web portal as its own small web application on its own port, inside the process of the servers.
/// </summary>
/// <remarks>
/// It's a separate application on purpose: the admin panel and its API stay on their own port, which is never
/// published to the internet, while only this portal is put behind the public reverse proxy.
/// </remarks>
public sealed class PortalHost : IHostedService, IAsyncDisposable
{
    private const string AuthPolicy = "auth";

    private readonly IServiceProvider _rootServices;
    private readonly PortalSettings _settings;
    private readonly Func<TimeZoneInfo> _getServerTimeZone;
    private readonly ILogger<PortalHost> _logger;
    private WebApplication? _app;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalHost"/> class.
    /// </summary>
    /// <param name="rootServices">The services of the server process, which the portal shares.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="getServerTimeZone">Gets the time zone in which the event schedules are defined.</param>
    /// <param name="logger">The logger.</param>
    public PortalHost(IServiceProvider rootServices, PortalSettings settings, Func<TimeZoneInfo> getServerTimeZone, ILogger<PortalHost> logger)
    {
        this._rootServices = rootServices;
        this._settings = settings;
        this._getServerTimeZone = getServerTimeZone;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this._settings.IsEnabled)
        {
            this._logger.LogInformation("Portal is disabled; set Portal__Urls to enable it.");
            return;
        }

        var webRoot = Path.Combine(AppContext.BaseDirectory, "portal-wwwroot");
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Directory.Exists(webRoot) ? webRoot : null,
        });
        builder.WebHost.UseUrls(this._settings.Urls!);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<ILoggerFactory>());
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        builder.Services.AddSingleton(this._settings);
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<IPersistenceContextProvider>());
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<IDataSource<GameConfiguration>>());
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<ILoginServer>());
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<IFriendServer>());
        builder.Services.AddSingleton(this._rootServices.GetRequiredService<IServerProvider>());
        builder.Services.AddSingleton<PortalService>();
        builder.Services.AddSingleton(provider => new PortalCommunityService(
            provider.GetRequiredService<IPersistenceContextProvider>(),
            provider.GetRequiredService<IDataSource<GameConfiguration>>(),
            provider.GetRequiredService<IFriendServer>(),
            provider.GetRequiredService<IServerProvider>(),
            this._rootServices.GetService<PlugInManager>(),
            this._getServerTimeZone,
            this._settings,
            provider.GetRequiredService<ILogger<PortalCommunityService>>()));

        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PeroniaPortal");
        if (!string.IsNullOrWhiteSpace(this._settings.KeyDirectory))
        {
            try
            {
                Directory.CreateDirectory(this._settings.KeyDirectory);
                dataProtection.PersistKeysToFileSystem(new DirectoryInfo(this._settings.KeyDirectory));
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Portal: can't keep the login keys in {dir}; logins end with a restart.", this._settings.KeyDirectory);
            }
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // The portal is only reachable through the reverse proxy in front of it.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "peronia_session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.Use(async (ctx, next) =>
        {
            var headers = ctx.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "same-origin";
            await next(ctx).ConfigureAwait(false);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.WebRootPath is { } root)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        MapEndpoints(app);

        if (app.Environment.WebRootPath is { } spaRoot)
        {
            app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(spaRoot) });
        }

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        this._app = app;
        this._logger.LogInformation("Portal started on {urls}, web files: {root}", this._settings.Urls, app.Environment.WebRootPath ?? "(none)");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this._app is { } app)
        {
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._app is { } app)
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/server", (PortalService portal) => Results.Ok(portal.GetServerInfo()));

        api.MapGet("/ranking", async (PortalService portal, int? count, CancellationToken ct)
            => Results.Ok(await portal.GetRankingAsync(count ?? 50, ct).ConfigureAwait(false)));

        api.MapPost("/register", async (PortalService portal, RegisterRequest request, CancellationToken ct) =>
        {
            var error = await portal.RegisterAsync(request, ct).ConfigureAwait(false);
            return error is null ? Results.Created() : Results.BadRequest(new { error });
        }).RequireRateLimiting(AuthPolicy);

        api.MapPost("/login", async (PortalService portal, LoginRequest request, HttpContext http, CancellationToken ct) =>
        {
            var error = await portal.AuthenticateAsync(request, ct).ConfigureAwait(false);
            if (error is not null)
            {
                return Results.BadRequest(new { error });
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, request.LoginName)], CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireRateLimiting(AuthPolicy);

        api.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return Results.NoContent();
        });

        api.MapGet("/me", async (PortalService portal, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var account = await portal.GetAccountAsync(user.Identity!.Name!, ct).ConfigureAwait(false);
            return account is null ? Results.Unauthorized() : Results.Ok(account);
        }).RequireAuthorization();

        api.MapPost("/me/password", async (PortalService portal, ClaimsPrincipal user, ChangePasswordRequest request, CancellationToken ct) =>
        {
            var error = await portal.ChangePasswordAsync(user.Identity!.Name!, request, ct).ConfigureAwait(false);
            return error is null ? Results.NoContent() : Results.BadRequest(new { error });
        }).RequireAuthorization().RequireRateLimiting(AuthPolicy);

        api.MapGet("/online", async (PortalCommunityService community, CancellationToken ct)
            => Results.Ok(await community.GetOnlinePlayersAsync(ct).ConfigureAwait(false)));

        api.MapGet("/characters/{name}", async (PortalCommunityService community, string name, CancellationToken ct)
            => await community.GetCharacterAsync(name, ct).ConfigureAwait(false) is { } profile ? Results.Ok(profile) : Results.NotFound());

        api.MapGet("/guilds", async (PortalCommunityService community, CancellationToken ct)
            => Results.Ok(await community.GetGuildsAsync(ct).ConfigureAwait(false)));

        api.MapGet("/guilds/{name}", async (PortalCommunityService community, string name, CancellationToken ct)
            => await community.GetGuildAsync(name, ct).ConfigureAwait(false) is { } guild ? Results.Ok(guild) : Results.NotFound());

        api.MapGet("/events", (PortalCommunityService community) => Results.Ok(community.GetUpcomingEvents()));

        api.MapGet("/stats", async (PortalCommunityService community, CancellationToken ct)
            => Results.Ok(await community.GetStatisticsAsync(ct).ConfigureAwait(false)));

        api.MapGet("/news", (PortalCommunityService community) => Results.Ok(community.GetNews()));

        api.MapFallback(() => Results.NotFound());
    }
}
