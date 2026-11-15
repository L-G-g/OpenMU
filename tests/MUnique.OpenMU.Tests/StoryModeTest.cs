// <copyright file="StoryModeTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.SkillQuests;
using MUnique.OpenMU.GameLogic.Story;
using MUnique.OpenMU.GameServer.RemoteView.World;

/// <summary>
/// Tests the story mode: <see cref="StoryScript"/>, <see cref="StoryModeFeaturePlugIn"/> and the packet of the scenes.
/// </summary>
[TestFixture]
public class StoryModeTest
{
    private const short DeviasNumber = 2;
    private const short DungeonNumber = 1;
    private const short LostTowerNumber = 4;
    private const short AtlansNumber = 7;
    private const short NoriaNumber = 3;

    /// <summary>
    /// Tests that every page of every step fits into the dialogue box of the client and into Latin-1.
    /// </summary>
    [Test]
    public void PagesFitIntoTheDialogueBox()
    {
        var latin1 = Encoding.GetEncoding("ISO-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        Assert.That(StoryScript.Steps.Select(s => s.Number), Is.EqualTo(Enumerable.Range(1, StoryScript.Steps.Count)));
        foreach (var step in StoryScript.Steps)
        {
            foreach (var family in Enum.GetValues<StoryClassFamily>())
            {
                foreach (var hasClassChange in new[] { true, false })
                {
                    var context = new StoryTextContext("Abcdefghij", family, hasClassChange, "Wandering Merchant Zyro", "Lorencia");
                    var pages = StoryScript.Paginate(step.Text(context).Append(step.LockedText));
                    Assert.That(pages, Is.Not.Empty);
                    foreach (var page in pages)
                    {
                        Assert.That(page.Length, Is.LessThanOrEqualTo(StoryScript.MaximumPageLength), $"step {step.Number}: {page}");
                        Assert.DoesNotThrow(() => latin1.GetBytes(page), $"step {step.Number}: {page}");
                    }
                }
            }

            if (step.Reward is { } reward)
            {
                Assert.That(reward.All(c => c < 128), Is.True, "the messages of the chat have no accents: " + reward);
            }
        }
    }

    /// <summary>
    /// Tests that a long paragraph continues on the next page, after the end of a sentence.
    /// </summary>
    [Test]
    public void LongParagraphIsSplitAtSentence()
    {
        var sentence = new string('a', 250) + ".";
        var pages = StoryScript.Paginate([$"{sentence} {sentence} {sentence}"]);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.All(p => p == sentence), Is.True);
    }

    /// <summary>
    /// Tests the packet of a scene.
    /// </summary>
    [Test]
    public void ScenePacket()
    {
        var scene = new StoryScene("Lumen", ["Hola.", "Adiós."], [new(StoryButton.DuelId, "Duelo"), new(StoryButton.CloseId, "Luego")], "Nota");
        var packet = StorySceneViewPlugIn.Serialize(0x1234, scene);
        var expected = new byte[] { 0xC2, 0, 0, 0xFF, 0x30, 0x12, 0x34, 2, 2 }
            .Concat(Encoding.Latin1.GetBytes("Lumen\0Nota\0Hola.\0Adiós.\0"))
            .Concat(new byte[] { 2 }).Concat(Encoding.Latin1.GetBytes("Duelo\0"))
            .Concat(new byte[] { 0 }).Concat(Encoding.Latin1.GetBytes("Luego\0"))
            .ToArray();
        expected[1] = (byte)(expected.Length >> 8);
        expected[2] = (byte)expected.Length;
        Assert.That(packet, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that the maps open step by step.
    /// </summary>
    [Test]
    public async ValueTask MapsOpenStepByStepAsync()
    {
        var (story, player) = await CreateAsync().ConfigureAwait(false);

        Assert.That(story.GetMapLock(player, NoriaNumber), Is.Null);
        Assert.That(story.GetMapLock(player, DeviasNumber)?.Number, Is.EqualTo(StoryScript.Devias));
        Assert.That(story.GetMapLock(player, DungeonNumber)?.Number, Is.EqualTo(StoryScript.Dungeon));
        Assert.That(story.GetMapLock(player, LostTowerNumber)?.Number, Is.EqualTo(StoryScript.SchoolOfLight));
        Assert.That(story.GetMapLock(player, AtlansNumber)?.Number, Is.EqualTo(StoryScript.OldWorld));

        Assert.That(StoryProgress.SetFinishedStep(player, StoryScript.Devias), Is.True);
        Assert.That(story.GetMapLock(player, DeviasNumber), Is.Null);
        Assert.That(story.GetMapLock(player, DungeonNumber), Is.Not.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.SchoolOfLight);
        Assert.That(story.GetMapLock(player, LostTowerNumber), Is.Null);
        Assert.That(story.GetMapLock(player, AtlansNumber), Is.Not.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.OldWorld);
        Assert.That(story.GetMapLock(player, AtlansNumber), Is.Null);
        Assert.That(story.GetNextStep(player), Is.Null);
    }

    /// <summary>
    /// Tests that game masters and a disabled lock let everyone in.
    /// </summary>
    [Test]
    public async ValueTask GameMastersAndDisabledLockAsync()
    {
        var (story, player) = await CreateAsync().ConfigureAwait(false);
        player.SelectedCharacter!.CharacterStatus = CharacterStatus.GameMaster;
        Assert.That(story.GetMapLock(player, DeviasNumber), Is.Null);

        player.SelectedCharacter!.CharacterStatus = CharacterStatus.Normal;
        Assert.That(story.GetMapLock(player, DeviasNumber), Is.Not.Null);
        story.Configuration!.LockMaps = false;
        Assert.That(story.GetMapLock(player, DeviasNumber), Is.Null);
    }

    /// <summary>
    /// Tests the requirements of the steps: resets and the dungeon of Devias.
    /// </summary>
    [Test]
    public async ValueTask RequirementsAsync()
    {
        var (story, player) = await CreateAsync().ConfigureAwait(false);
        Assert.That(story.GetNextStep(player)?.Number, Is.EqualTo(StoryScript.Awakening));
        Assert.That(story.GetMissingRequirement(player, story.GetNextStep(player)!), Is.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.Awakening);
        var duel = story.GetNextStep(player)!;
        Assert.That(duel.Kind, Is.EqualTo(StoryStepKind.Duel));
        player.Attributes![Stats.Resets] = 1;
        Assert.That(story.GetMissingRequirement(player, duel), Is.Not.Null);
        player.Attributes[Stats.Resets] = 2;
        Assert.That(story.GetMissingRequirement(player, duel), Is.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.Devias);
        var dungeon = story.GetNextStep(player)!;
        player.Attributes[Stats.Resets] = 5;
        Assert.That(story.GetMissingRequirement(player, dungeon), Does.Contain("dungeon"));
        await story.OnDungeonFinishedAsync(player).ConfigureAwait(false);
        Assert.That(StoryProgress.GetFlags(player.SelectedCharacter!), Is.EqualTo(StoryFlags.DungeonFinished));
        Assert.That(story.GetMissingRequirement(player, dungeon), Is.Null);
        player.Attributes[Stats.Resets] = 4;
        Assert.That(story.GetMissingRequirement(player, dungeon), Does.Contain("5 resets"));
    }

    /// <summary>
    /// Tests the marks above the actors and the lines of the tracker.
    /// </summary>
    [Test]
    public async ValueTask MarksAndTrackerAsync()
    {
        var (story, player) = await CreateAsync().ConfigureAwait(false);
        var lumen = CreateNpc(StoryModeFeaturePlugIn.LumenNumber, "Lumen the Barmaid");
        var stranger = CreateNpc(479, StoryScript.StrangerName);
        var titus = CreateNpc(479, "Doorkeeper Titus");
        var sebina = CreateNpc(StoryModeFeaturePlugIn.SebinaNumber, "Sebina the Priestess");
        Assert.That(StoryModeFeaturePlugIn.GetActor(titus), Is.Null);

        Assert.That(story.GetMark(player, lumen), Is.EqualTo(SkillQuestMark.Available));
        Assert.That(story.GetMark(player, stranger), Is.EqualTo(SkillQuestMark.None));
        var lines = new List<SkillQuestLine>();
        story.AddTrackerLines(player, lines);
        Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[] { "Historia: El despertar", "  Hablá con Lumen (Lorencia)" }));

        StoryProgress.SetFinishedStep(player, StoryScript.Awakening);
        Assert.That(story.GetMark(player, lumen), Is.EqualTo(SkillQuestMark.None));
        Assert.That(story.GetMark(player, stranger), Is.EqualTo(SkillQuestMark.Locked));
        lines.Clear();
        story.AddTrackerLines(player, lines);
        Assert.That(lines[1].Text, Is.EqualTo("  Necesitás 2 resets"));

        player.Attributes![Stats.Resets] = 20;
        Assert.That(story.GetMark(player, stranger), Is.EqualTo(SkillQuestMark.Available));
        StoryProgress.SetFinishedStep(player, StoryScript.StrangerDuel);
        Assert.That(story.GetMark(player, stranger), Is.EqualTo(SkillQuestMark.Ready), "after the duel");

        StoryProgress.SetFinishedStep(player, StoryScript.OldPacts);
        Assert.That(story.GetMark(player, sebina), Is.EqualTo(SkillQuestMark.Available));
        lines.Clear();
        story.AddTrackerLines(player, lines);
        Assert.That(lines[1].Text, Is.EqualTo("  Hablá con Sebina (Devias)"));

        StoryProgress.SetFinishedStep(player, StoryScript.OldWorld);
        lines.Clear();
        story.AddTrackerLines(player, lines);
        Assert.That(lines, Is.Empty);
    }

    /// <summary>
    /// Tests that Sebina starts the class change only after her scene, and the skill quests wait for Lumen.
    /// </summary>
    [Test]
    public async ValueTask QuestsWaitForTheStoryAsync()
    {
        var (story, player) = await CreateAsync().ConfigureAwait(false);
        var classChange = new Mock<DataModel.Configuration.Quests.QuestDefinition>();
        classChange.SetupAllProperties();
        classChange.Object.QuestGiver = CreateNpc(StoryModeFeaturePlugIn.SebinaNumber, "Sebina the Priestess");

        Assert.That(story.BlocksSkillQuests(player, false), Is.True);
        Assert.That(story.BlocksSkillQuests(player, true), Is.False, "a character from before the story mode");
        Assert.That(story.GetQuestBlock(player, classChange.Object), Is.Not.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.OldPacts);
        Assert.That(story.BlocksSkillQuests(player, false), Is.False);
        Assert.That(story.GetQuestBlock(player, classChange.Object), Is.Not.Null);

        StoryProgress.SetFinishedStep(player, StoryScript.SchoolOfLight);
        Assert.That(story.GetQuestBlock(player, classChange.Object), Is.Null);
    }

    /// <summary>
    /// Tests that the stranger never tells that Kundun was Mephis, so that the players think of two different persons.
    /// </summary>
    [Test]
    public void StrangerKeepsTheSecret()
    {
        var context = new StoryTextContext("Tester", StoryClassFamily.Knight, true, "Hanzo", "Lorencia");
        var texts = StoryScript.Steps
            .Where(step => step.Actor == StoryActor.Stranger)
            .SelectMany(step => step.Text(context).Append(step.LockedText))
            .ToList();
        Assert.That(texts, Has.None.Contains("Mephis"));
        Assert.That(texts, Has.None.Contains("Lemuria"));
        Assert.That(texts, Has.Some.Contains("Kundun"));
        Assert.That(StoryScript.Steps.Single(step => step.Number == StoryScript.Dungeon).Text(context), Has.Some.Contains("Artorias"));
    }

    /// <summary>
    /// Tests that nothing hurts a player who listens to a scene.
    /// </summary>
    [Test]
    public async ValueTask ProtectedDuringTheSceneAsync()
    {
        var (_, player) = await CreateAsync().ConfigureAwait(false);
        var attacker = new Mock<IAttacker>();
        player.Attributes![Stats.CurrentHealth] = Math.Max(player.Attributes[Stats.MaximumHealth], 1);
        var health = player.Attributes[Stats.CurrentHealth];
        Assert.That(health, Is.GreaterThanOrEqualTo(1));

        player.InvulnerableUntilUtc = DateTime.UtcNow.AddMinutes(1);
        Assert.That(player.IsInvulnerable, Is.True);
        await player.ApplyPoisonDamageAsync(attacker.Object, 1).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.CurrentHealth], Is.EqualTo(health));

        player.InvulnerableUntilUtc = default;
        await player.ApplyPoisonDamageAsync(attacker.Object, 1).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.CurrentHealth], Is.EqualTo(health - 1));
    }

    private static MonsterDefinition CreateNpc(short number, string designation)
    {
        var npc = new Mock<MonsterDefinition>();
        npc.SetupAllProperties();
        npc.Object.Number = number;
        npc.Object.Designation = designation;
        npc.Object.ObjectKind = NpcObjectKind.PassiveNpc;
        return npc.Object;
    }

    private static async ValueTask<(StoryModeFeaturePlugIn Story, Player Player)> CreateAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        foreach (var attribute in new[] { Stats.StoryProgress, Stats.StoryFlags })
        {
            gameContext.Configuration.Attributes.Add(new Persistence.BasicModel.AttributeDefinition(attribute.Id, attribute.Designation, attribute.Description));
        }
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        return (new StoryModeFeaturePlugIn { Configuration = new StoryConfiguration() }, player);
    }
}
