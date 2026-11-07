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

/// <summary>The public profile of a character.</summary>
/// <param name="Name">The name.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="IsOnline">If set to <c>true</c>, the character is playing right now.</param>
/// <param name="ServerName">The game server on which the character plays right now.</param>
/// <param name="MapName">The map where the character is, while it's online.</param>
/// <param name="GuildName">The name of the guild.</param>
/// <param name="GuildPosition">The position in the guild.</param>
public sealed record PortalCharacterProfile(string Name, string ClassName, int Level, int MasterLevel, int Resets, bool IsOnline, string? ServerName, string? MapName, string? GuildName, string? GuildPosition);

/// <summary>A character which is playing right now.</summary>
/// <param name="Name">The name.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="MapName">The map where the character is.</param>
/// <param name="ServerName">The game server.</param>
public sealed record PortalOnlinePlayer(string Name, string ClassName, int Level, int MasterLevel, int Resets, string? MapName, string ServerName);

/// <summary>A guild in the guild list.</summary>
/// <param name="Name">The name.</param>
/// <param name="MasterName">The name of the guild master.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="Score">The score.</param>
/// <param name="Logo">The guild mark as hex string (64 colors of 4 bit, 8x8).</param>
/// <param name="AllianceName">The name of the master guild of the alliance.</param>
public sealed record PortalGuild(string Name, string? MasterName, int MemberCount, int Score, string? Logo, string? AllianceName);

/// <summary>A member of a guild.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Position">The position in the guild.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="IsOnline">If set to <c>true</c>, the character is playing right now.</param>
public sealed record PortalGuildMember(string Name, string Position, string ClassName, int Level, int MasterLevel, int Resets, bool IsOnline);

/// <summary>A guild with its members.</summary>
/// <param name="Guild">The guild.</param>
/// <param name="Members">The members.</param>
public sealed record PortalGuildDetail(PortalGuild Guild, IReadOnlyList<PortalGuildMember> Members);

/// <summary>An upcoming start of a scheduled event.</summary>
/// <param name="Key">The key of the event kind, e.g. <c>BloodCastle</c>.</param>
/// <param name="Name">The name, in Spanish where it's known.</param>
/// <param name="StartsAtUtc">The start time.</param>
public sealed record PortalEvent(string Key, string Name, DateTime StartsAtUtc);

/// <summary>Numbers about the server.</summary>
/// <param name="Accounts">The number of player accounts.</param>
/// <param name="NewAccounts">The accounts registered in the last 7 days.</param>
/// <param name="Characters">The number of characters.</param>
/// <param name="Guilds">The number of guilds.</param>
/// <param name="PlayersOnline">The players online right now.</param>
/// <param name="ExperienceRate">The experience rate.</param>
/// <param name="MasterExperienceRate">The master experience rate.</param>
/// <param name="MaximumLevel">The maximum level.</param>
/// <param name="MaximumMasterLevel">The maximum master level.</param>
public sealed record PortalStatistics(int Accounts, int NewAccounts, int Characters, int Guilds, int PlayersOnline, float ExperienceRate, float MasterExperienceRate, int MaximumLevel, int MaximumMasterLevel);

/// <summary>A news entry.</summary>
/// <param name="Date">The date, as written in the news file.</param>
/// <param name="Title">The title.</param>
/// <param name="Body">The text paragraphs.</param>
public sealed record PortalNewsItem(string Date, string Title, IReadOnlyList<string> Body);

/// <summary>A guild which is registered for the Castle Siege.</summary>
/// <param name="Name">The guild name.</param>
/// <param name="Marks">The number of handed in Signs of Lord.</param>
public sealed record PortalSiegeGuild(string Name, int Marks);

/// <summary>The state of the Castle Siege.</summary>
/// <param name="IsActive">If set to <c>false</c>, the Castle Siege event isn't running.</param>
/// <param name="OwnerGuildName">The guild which owns the castle.</param>
/// <param name="OwnerGuildLogo">The mark of the owner guild.</param>
/// <param name="State">The key of the current phase.</param>
/// <param name="StateName">The name of the current phase, in Spanish.</param>
/// <param name="StateEndsAtUtc">When the current phase ends.</param>
/// <param name="NextBattleUtc">When the next battle starts, or the current one started.</param>
/// <param name="Guilds">The registered guilds.</param>
public sealed record PortalCastleSiege(bool IsActive, string? OwnerGuildName, string? OwnerGuildLogo, string? State, string? StateName, DateTime? StateEndsAtUtc, DateTime? NextBattleUtc, IReadOnlyList<PortalSiegeGuild> Guilds);
