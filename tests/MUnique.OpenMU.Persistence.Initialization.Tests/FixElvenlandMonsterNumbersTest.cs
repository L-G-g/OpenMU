// <copyright file="FixElvenlandMonsterNumbersTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the monsters of Elvenland have the numbers of the monster table of the original client,
/// and that <see cref="FixElvenlandMonsterNumbersUpdatePlugIn"/> corrects existing databases.
/// </summary>
[TestFixture]
internal class FixElvenlandMonsterNumbersTest
{
    private const short HideousRabbitNumber = 419;
    private const short WerewolfNumber = 420;
    private const short PollutedButterflyNumber = 421;

    /// <summary>
    /// Tests that a new season 6 database has the corrected numbers.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasClientMonsterNumbersAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertCorrectedData(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update corrects a database with the previous numbers,
    /// and that applying it twice doesn't change anything else.
    /// </summary>
    [Test]
    public async Task UpdateCorrectsExistingDatabaseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        RestorePreviousData(gameConfiguration);

        var update = new FixElvenlandMonsterNumbersUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertCorrectedData(gameConfiguration);

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertCorrectedData(gameConfiguration);
    }

    private static void AssertCorrectedData(GameConfiguration gameConfiguration)
    {
        var elvenland = gameConfiguration.Maps.Single(m => m.Number == 51);
        var summonerKillRequirements = GetSummonerKillRequirements(gameConfiguration).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(GetMaximumHealth(gameConfiguration, HideousRabbitNumber), Is.EqualTo(520));
            Assert.That(GetMaximumHealth(gameConfiguration, WerewolfNumber), Is.EqualTo(720));
            Assert.That(GetMaximumHealth(gameConfiguration, PollutedButterflyNumber), Is.EqualTo(230));
            Assert.That(gameConfiguration.Monsters.Count(m => m.Number is HideousRabbitNumber or WerewolfNumber or PollutedButterflyNumber), Is.EqualTo(3));

            // The butterflies keep their own spawn area (0-251, 128-245), the rabbits and werewolves share theirs (0-128, 0-128).
            // Their monsters are grouped into spots, which lie in the squares of these areas.
            Assert.That(GetSpawnAreas(elvenland, PollutedButterflyNumber).Select(a => (int)a.Y1), Is.Not.Empty.And.All.GreaterThanOrEqualTo(128 - MonsterSpotGrouping.SpotDistance));
            Assert.That(GetSpawnAreas(elvenland, HideousRabbitNumber).Select(a => Math.Max(a.X1, a.Y1)), Is.Not.Empty.And.All.LessThanOrEqualTo(128 + MonsterSpotGrouping.SpotDistance));
            Assert.That(GetSpawnAreas(elvenland, WerewolfNumber).Select(a => Math.Max(a.X1, a.Y1)), Is.Not.Empty.And.All.LessThanOrEqualTo(128 + MonsterSpotGrouping.SpotDistance));

            // The summoner quests require the monster number of the original quest data.
            Assert.That(summonerKillRequirements, Is.Not.Empty);
            Assert.That(summonerKillRequirements.Select(r => r.Monster?.Number), Is.All.EqualTo(HideousRabbitNumber));
        });
    }

    /// <summary>
    /// Brings the monsters into the state of the previous initialization data, which derived the id of a
    /// definition from its previous number.
    /// </summary>
    private static void RestorePreviousData(GameConfiguration gameConfiguration)
    {
        var butterfly = GetMonster(gameConfiguration, PollutedButterflyNumber);
        var rabbit = GetMonster(gameConfiguration, HideousRabbitNumber);
        var werewolf = GetMonster(gameConfiguration, WerewolfNumber);

        foreach (var requirement in GetSummonerKillRequirements(gameConfiguration))
        {
            requirement.Monster = butterfly;
        }

        SetNumber(butterfly, 419);
        SetNumber(rabbit, 420);
        SetNumber(werewolf, 421);
    }

    private static void SetNumber(MonsterDefinition monster, short number)
    {
        monster.Number = number;
        ((Persistence.BasicModel.MonsterDefinition)monster).Id = GuidHelper.CreateGuid<MonsterDefinition>(number);
    }

    private static IEnumerable<DataModel.Configuration.Quests.QuestMonsterKillRequirement> GetSummonerKillRequirements(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters
            .SelectMany(m => m.Quests)
            .Where(q => q.QualifiedCharacter?.Number == (byte)CharacterClassNumber.Summoner && q.Group == 18)
            .SelectMany(q => q.RequiredMonsterKills)
            .Where(r => r.Monster?.Number is HideousRabbitNumber or WerewolfNumber or PollutedButterflyNumber);
    }

    private static MonsterDefinition GetMonster(GameConfiguration gameConfiguration, short number)
    {
        return gameConfiguration.Monsters.Single(m => m.Number == number);
    }

    private static float GetMaximumHealth(GameConfiguration gameConfiguration, short number)
    {
        return GetMonster(gameConfiguration, number).Attributes.Single(a => a.AttributeDefinition == Stats.MaximumHealth).Value;
    }

    private static IEnumerable<MonsterSpawnArea> GetSpawnAreas(GameMapDefinition map, short number)
    {
        return map.MonsterSpawns.Where(s => s.MonsterDefinition?.Number == number);
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}
