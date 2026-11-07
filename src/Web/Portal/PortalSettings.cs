// <copyright file="PortalSettings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

/// <summary>
/// The settings of the public web portal, bound from the configuration section <c>Portal</c>
/// (e.g. the environment variables <c>Portal__Urls</c> and <c>Portal__InviteCodes</c>).
/// </summary>
public class PortalSettings
{
    /// <summary>
    /// The name of the configuration section.
    /// </summary>
    public const string SectionName = "Portal";

    /// <summary>
    /// Gets or sets the urls on which the portal listens, e.g. <c>http://+:8090</c>.
    /// When it's empty, the portal is not started.
    /// </summary>
    public string? Urls { get; set; }

    /// <summary>
    /// Gets or sets the invite codes, separated by commas. A new account needs one of them.
    /// When it's empty, nobody can register.
    /// </summary>
    public string? InviteCodes { get; set; }

    /// <summary>
    /// Gets or sets the url from which the game client can be downloaded.
    /// </summary>
    public string? ClientDownloadUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the server, as shown on the portal.
    /// </summary>
    public string ServerName { get; set; } = "PeroniaMU";

    /// <summary>
    /// Gets or sets the directory in which the keys of the login cookies are kept, so that logins survive a restart.
    /// </summary>
    public string? KeyDirectory { get; set; } = "/app/data-protection-keys/portal";

    /// <summary>
    /// Gets or sets the path of the news file. Each news entry starts with a line like
    /// <c>## 2026-11-07 | Title</c>, followed by its text. The file is read again when it changes.
    /// </summary>
    public string? NewsFile { get; set; } = "/app/portal/noticias.md";

    /// <summary>
    /// Gets a value indicating whether the portal is enabled.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(this.Urls);

    /// <summary>
    /// Gets the configured invite codes.
    /// </summary>
    /// <returns>The invite codes.</returns>
    public IReadOnlyList<string> GetInviteCodes()
        => (this.InviteCodes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
