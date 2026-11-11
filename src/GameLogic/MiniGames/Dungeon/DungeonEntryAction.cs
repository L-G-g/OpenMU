// <copyright file="DungeonEntryAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// Action to enter the dungeon with the whole party.
/// </summary>
/// <remarks>
/// The party leader starts it at the NPC of the dungeon. All members of the party enter, wherever they are,
/// but only if all of them can enter: otherwise nobody enters and all of them see why.
/// A character can enter once per <see cref="DungeonConfiguration.EntryCooldown"/>; the time of the last entry
/// is kept in the attribute <see cref="Stats.DungeonLastEntry"/> of the character.
/// </remarks>
public class DungeonEntryAction
{
    /// <summary>
    /// The time from which the <see cref="Stats.DungeonLastEntry"/> counts the minutes.
    /// The minutes fit exactly into the float value of the attribute for more than 30 years.
    /// </summary>
    public static readonly DateTime EntryTimeEpoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const byte NoTicketSlot = 0xFF;

    private readonly EnterMiniGameAction _enterAction = new();

    /// <summary>
    /// Tries to enter the dungeon with the party of the player.
    /// </summary>
    /// <param name="leader">The player who talked to the NPC, the party leader or a player without party.</param>
    public async ValueTask TryEnterAsync(Player leader)
    {
        var gameContext = leader.GameContext;
        var configuration = DungeonFeaturePlugIn.GetConfiguration(gameContext);
        var definition = configuration is null
            ? null
            : gameContext.Configuration.MiniGameDefinitions.FirstOrDefault(d => d.Type == MiniGameType.Dungeon && d.GameLevel == configuration.GameLevel);
        var lastEntryAttribute = gameContext.Configuration.Attributes.FirstOrDefault(a => a.Id == Stats.DungeonLastEntry.Id);
        if (configuration is null || definition is null || lastEntryAttribute is null)
        {
            await leader.ShowBlueMessageAsync(DungeonMessages.NotAvailable).ConfigureAwait(false);
            return;
        }

        if (leader.GetPlayerParty() is { } party && !ReferenceEquals(party.PartyMaster, leader))
        {
            await leader.ShowBlueMessageAsync(DungeonMessages.OnlyPartyLeader).ConfigureAwait(false);
            return;
        }

        var members = leader.GetPlayerParty()?.PartyList.OfType<Player>().ToList() ?? new List<Player> { leader };
        var now = DateTime.UtcNow;
        var problems = members
            .Select(member => (Member: member, Problem: GetEntryProblem(member, definition, configuration, now)))
            .Where(entry => entry.Problem is not null)
            .Select(entry => $"{entry.Member.Name}: {entry.Problem}")
            .ToList();
        if (problems.Count > 0)
        {
            foreach (var member in members)
            {
                await member.ShowBlueMessageAsync(DungeonMessages.EntryCancelled).ConfigureAwait(false);
                foreach (var problem in problems)
                {
                    await member.ShowBlueMessageAsync(problem).ConfigureAwait(false);
                }
            }

            return;
        }

        // The leader enters first, so that the instance of the party is created with the leader as its owner.
        foreach (var member in members.OrderBy(member => ReferenceEquals(member, leader) ? 0 : 1))
        {
            await this._enterAction.TryEnterMiniGameAsync(member, MiniGameType.Dungeon, definition.GameLevel, NoTicketSlot).ConfigureAwait(false);
            if (member.CurrentMiniGame is DungeonContext)
            {
                SetLastEntry(member, lastEntryAttribute, now);
            }
            else
            {
                await member.ShowBlueMessageAsync(DungeonMessages.EntryFailed).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Gets the time of the last entry of the character into the dungeon.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The time of the last entry, or <c>null</c> if it never entered.</returns>
    internal static DateTime? GetLastEntry(Character character)
    {
        var minutes = character.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.DungeonLastEntry.Id)?.Value ?? 0;
        return minutes > 0 ? EntryTimeEpoch.AddMinutes(minutes) : null;
    }

    private static string? GetEntryProblem(Player member, MiniGameDefinition definition, DungeonConfiguration configuration, DateTime now)
    {
        if (member.SelectedCharacter is not { } character || member.Attributes is not { } attributes || member.CurrentMap is null)
        {
            return DungeonMessages.ProblemNotInGame;
        }

        if (member.CurrentMiniGame is not null)
        {
            return DungeonMessages.ProblemInEvent;
        }

        if (!member.IsAlive)
        {
            return DungeonMessages.ProblemDead;
        }

        var isSpecialCharacter = character.IsSpecialCharacter();
        var level = attributes[Stats.Level];
        var minimumLevel = isSpecialCharacter ? definition.MinimumSpecialCharacterLevel : definition.MinimumCharacterLevel;
        var maximumLevel = isSpecialCharacter ? definition.MaximumSpecialCharacterLevel : definition.MaximumCharacterLevel;
        if (level < minimumLevel || level > maximumLevel)
        {
            return string.Format(DungeonMessages.ProblemLevelFormat, minimumLevel, maximumLevel);
        }

        if (!definition.ArePlayerKillersAllowedToEnter && character.State >= HeroState.PlayerKiller1stStage)
        {
            return DungeonMessages.ProblemPlayerKiller;
        }

        var ignoresCooldown = configuration.GameMastersIgnoreCooldown && character.CharacterStatus == CharacterStatus.GameMaster;
        if (!ignoresCooldown && GetLastEntry(character) is { } lastEntry && now - lastEntry < configuration.EntryCooldown)
        {
            var remaining = configuration.EntryCooldown - (now - lastEntry);
            return string.Format(DungeonMessages.ProblemCooldownFormat, (int)remaining.TotalHours, remaining.Minutes);
        }

        return null;
    }

    private static void SetLastEntry(Player member, AttributeDefinition lastEntryAttribute, DateTime now)
    {
        if (member.SelectedCharacter is not { } character)
        {
            return;
        }

        var stat = character.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.DungeonLastEntry.Id);
        if (stat is null)
        {
            stat = member.PersistenceContext.CreateNew<StatAttribute>(lastEntryAttribute, 0);
            character.Attributes.Add(stat);
        }

        stat.Value = (float)Math.Floor((now - EntryTimeEpoch).TotalMinutes);
    }
}
