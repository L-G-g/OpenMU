// <copyright file="StoryTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

/// <summary>
/// The characters which tell the story.
/// </summary>
public enum StoryActor
{
    /// <summary>
    /// Lumen the Barmaid of Lorencia.
    /// </summary>
    Lumen,

    /// <summary>
    /// The stranger of Lorencia, who is Artorias.
    /// </summary>
    Stranger,

    /// <summary>
    /// Sebina the Priestess of Devias.
    /// </summary>
    Sebina,
}

/// <summary>
/// What a player does in a step of the story.
/// </summary>
public enum StoryStepKind
{
    /// <summary>
    /// The player listens to the actor; the step is finished with the button of the last page.
    /// </summary>
    Talk,

    /// <summary>
    /// The player listens to the actor and duels it; the step is finished by winning the duel.
    /// </summary>
    Duel,
}

/// <summary>
/// The flags of a character in the story mode, kept in the attribute <see cref="Attributes.Stats.StoryFlags"/>.
/// </summary>
[Flags]
public enum StoryFlags
{
    /// <summary>
    /// No flag.
    /// </summary>
    None = 0,

    /// <summary>
    /// The character finished the dungeon of Devias (it was there when the last boss fell).
    /// </summary>
    DungeonFinished = 1,
}

/// <summary>
/// The family of a character class, which decides some texts of the story.
/// </summary>
public enum StoryClassFamily
{
    /// <summary>Dark Wizard, Soul Master, Grand Master.</summary>
    Wizard,

    /// <summary>Dark Knight, Blade Knight, Blade Master.</summary>
    Knight,

    /// <summary>Fairy Elf, Muse Elf, High Elf.</summary>
    Elf,

    /// <summary>Magic Gladiator, Duel Master.</summary>
    MagicGladiator,

    /// <summary>Dark Lord, Lord Emperor.</summary>
    DarkLord,

    /// <summary>Summoner, Bloody Summoner, Dimension Master.</summary>
    Summoner,

    /// <summary>Rage Fighter, Fist Master.</summary>
    RageFighter,
}

/// <summary>
/// What a text of the story may depend on.
/// </summary>
/// <param name="PlayerName">The name of the character.</param>
/// <param name="Family">The family of its class.</param>
/// <param name="HasClassChange">If set to <c>true</c>, the class has the first class change quest of Sebina.</param>
/// <param name="Teacher">The NPC with the first skill quest of the class, e.g. "Hanzo".</param>
/// <param name="TeacherPlace">The map of the <paramref name="Teacher"/>, e.g. "Lorencia".</param>
public sealed record StoryTextContext(string PlayerName, StoryClassFamily Family, bool HasClassChange, string Teacher, string TeacherPlace);

/// <summary>
/// A step of the story: a scene with an actor, which a player can play when it finished the previous steps and meets
/// the requirements of the <see cref="StoryConfiguration"/>.
/// </summary>
/// <param name="Number">The number, starting with 1; the steps are played in this order.</param>
/// <param name="Chapter">The title of the chapter, e.g. for the tracker.</param>
/// <param name="Actor">The actor of the scene.</param>
/// <param name="Kind">What the player does.</param>
/// <param name="AcceptLabel">The text of the button which finishes the step, or starts the duel.</param>
/// <param name="Text">The paragraphs which the actor says.</param>
/// <param name="LockedText">What the actor says when the player doesn't meet the requirements yet.</param>
/// <param name="Reward">The message when the step is finished (without accents: it's a message of the chat), or <c>null</c>.</param>
public sealed record StoryStep(
    int Number,
    string Chapter,
    StoryActor Actor,
    StoryStepKind Kind,
    string AcceptLabel,
    Func<StoryTextContext, IReadOnlyList<string>> Text,
    string LockedText,
    string? Reward);

/// <summary>
/// A button of a <see cref="StoryScene"/>.
/// </summary>
/// <param name="Id">The id which the client sends back with <c>/historia</c>; 0 just closes the scene.</param>
/// <param name="Label">The text.</param>
public readonly record struct StoryButton(byte Id, string Label)
{
    /// <summary>
    /// The id of the button which closes the scene without telling the server.
    /// </summary>
    public const byte CloseId = 0;

    /// <summary>
    /// The id of the button which finishes a <see cref="StoryStepKind.Talk"/> step.
    /// </summary>
    public const byte FinishId = 1;

    /// <summary>
    /// The id of the button which starts the duel of a <see cref="StoryStepKind.Duel"/> step.
    /// </summary>
    public const byte DuelId = 2;
}

/// <summary>
/// A scene of the story, which the client (cinematicas.dll) shows in first person: the actor says the pages one after
/// another, and the last page has the buttons.
/// </summary>
/// <param name="Title">The name of the actor.</param>
/// <param name="Pages">The pages of text.</param>
/// <param name="Buttons">The buttons of the last page.</param>
/// <param name="Note">A note below the text of the last page, e.g. the missing requirement, or <c>null</c>.</param>
public sealed record StoryScene(string Title, IReadOnlyList<string> Pages, IReadOnlyList<StoryButton> Buttons, string? Note);
