// <copyright file="PortalDtos.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

/// <summary>A game server as shown on the portal.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The description of the server.</param>
/// <param name="IsOnline">If set to <c>true</c>, the server accepts players.</param>
/// <param name="Players">The number of connected players.</param>
/// <param name="MaximumPlayers">The maximum number of players.</param>
public sealed record PortalGameServer(int Id, string Name, bool IsOnline, int Players, int MaximumPlayers);

/// <summary>The public state of the server.</summary>
/// <param name="ServerName">The name of the server.</param>
/// <param name="PlayersOnline">The number of connected players over all game servers.</param>
/// <param name="GameServers">The game servers.</param>
/// <param name="RegistrationOpen">If set to <c>true</c>, new accounts can be registered with an invite code.</param>
/// <param name="ClientDownloadUrl">The url from which the client can be downloaded.</param>
public sealed record PortalServerInfo(string ServerName, int PlayersOnline, IReadOnlyList<PortalGameServer> GameServers, bool RegistrationOpen, string? ClientDownloadUrl);

/// <summary>A character in the ranking or of an account.</summary>
/// <param name="Name">The name.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="IsOnline">If set to <c>true</c>, the character is playing right now.</param>
/// <param name="MapName">The name of the map where the character is; only for the own characters.</param>
public sealed record PortalCharacter(string Name, string ClassName, int Level, int MasterLevel, int Resets, bool IsOnline, string? MapName);

/// <summary>The account of the logged in user.</summary>
/// <param name="LoginName">The login name.</param>
/// <param name="EMail">The e-mail address.</param>
/// <param name="RegisteredAt">The registration date.</param>
/// <param name="WCoin">The WCoin (C) balance.</param>
/// <param name="IsOnline">If set to <c>true</c>, the account is logged in at the game.</param>
/// <param name="Characters">The characters.</param>
public sealed record PortalAccount(string LoginName, string EMail, DateTime RegisteredAt, int WCoin, bool IsOnline, IReadOnlyList<PortalCharacter> Characters);

/// <summary>A registration request.</summary>
public sealed class RegisterRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional e-mail address.</summary>
    public string? EMail { get; set; }

    /// <summary>Gets or sets the invite code.</summary>
    public string InviteCode { get; set; } = string.Empty;
}

/// <summary>A login request.</summary>
public sealed class LoginRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>A request to change the password.</summary>
public sealed class ChangePasswordRequest
{
    /// <summary>Gets or sets the current password.</summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the new password.</summary>
    public string NewPassword { get; set; } = string.Empty;
}
