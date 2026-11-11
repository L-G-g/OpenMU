// <copyright file="PortalServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extensions to add the public web portal to the services of the server process.
/// </summary>
public static class PortalServiceCollectionExtensions
{
    /// <summary>
    /// Adds the portal. It only starts listening when <see cref="PortalSettings.Urls"/> is configured.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="section">The configuration section <see cref="PortalSettings.SectionName"/>.</param>
    /// <param name="getServerTimeZone">Gets the time zone in which the event schedules are defined.</param>
    /// <returns>The same services, for chaining.</returns>
    public static IServiceCollection AddPeroniaPortal(this IServiceCollection services, IConfiguration section, Func<TimeZoneInfo>? getServerTimeZone = null)
    {
        var settings = new PortalSettings();
        section.Bind(settings);
        return services.AddHostedService(provider => new PortalHost(provider, settings, getServerTimeZone ?? (() => TimeZoneInfo.Utc), provider.GetRequiredService<ILogger<PortalHost>>()));
    }
}
