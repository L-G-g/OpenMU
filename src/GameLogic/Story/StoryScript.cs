// <copyright file="StoryScript.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

/// <summary>
/// The script of the story mode of PeroniaMU: its steps and what the actors say.
/// </summary>
/// <remarks>
/// The story, as the players find it out: a thousand years ago the wizard Mephis Kundun of the Empire of Mu sealed the
/// dark titan Secneum within himself. His apprentice and lover Lemuria broke the seal, which woke up a Kundun corrupted
/// by the titan. Emperor Muren Rexmillon sent General Gaion against him; Gaion died, and the wizard of light Etramu
/// Renos sealed Kundun again, for a thousand years. The emperor built Lorencia far away and erased the memory of its
/// people, who later founded Noria and Elbeland. Lemuria gave her magic for immortality and waits in Lorencia, now as
/// Lumen the Barmaid, to lead its people to the old world so that Kundun comes back. Artorias, the son of Gaion, was
/// sent to this time by the dimension wizard Runedil and the sacrifice of the emperor, and looks for allies to destroy
/// Kundun for good: he's "the stranger".
/// <para>
/// The first part has seven steps. A paragraph of a text is a page of the scene, unless it's longer than
/// <see cref="MaximumPageLength"/> (see <see cref="Paginate"/>). The texts of the scenes are Latin-1.
/// </para>
/// </remarks>
public static class StoryScript
{
    /// <summary>
    /// The maximum length of a page of a scene: what fits into the dialogue box of the client.
    /// </summary>
    public const int MaximumPageLength = 440;

    /// <summary>
    /// The number of the step of the first talk to Lumen.
    /// </summary>
    public const int Awakening = 1;

    /// <summary>
    /// The number of the step of the duel against the stranger.
    /// </summary>
    public const int StrangerDuel = 2;

    /// <summary>
    /// The number of the step in which the stranger tells about Devias.
    /// </summary>
    public const int Devias = 3;

    /// <summary>
    /// The number of the step in which the stranger tells its name and about the Dungeon.
    /// </summary>
    public const int Dungeon = 4;

    /// <summary>
    /// The number of the step in which Artorias sends the player to Sebina.
    /// </summary>
    public const int OldPacts = 5;

    /// <summary>
    /// The number of the step in which Sebina tells about the pact of the class and opens the Lost Tower.
    /// </summary>
    public const int SchoolOfLight = 6;

    /// <summary>
    /// The number of the step in which Lumen opens the old world.
    /// </summary>
    public const int OldWorld = 7;

    /// <summary>
    /// The name of the stranger before the player knows who it is.
    /// </summary>
    public const string StrangerName = "Forastero";

    /// <summary>
    /// The name of the stranger for the players who know who it is.
    /// </summary>
    public const string StrangerRealName = "Artorias";

    /// <summary>
    /// Gets the steps of the story, in their order.
    /// </summary>
    public static IReadOnlyList<StoryStep> Steps { get; } =
    [
        new(
            Awakening,
            "El despertar",
            StoryActor.Lumen,
            StoryStepKind.Talk,
            "Lo prometo",
            c =>
            [
                $"¡Mira quién se ha levantado al fin! Ya no eres ningún niño, {c.PlayerName}. En Lorencia, cuando alguien alcanza tu edad, deja de vivir de la sopa de los demás y sale a ganarse la vida. Hoy te toca a ti.",
                "No pongas esa cara. Llevo tantos años detrás de esta barra que he visto partir a todos: a tus padres, a tus abuelos... Nadie recuerda desde cuándo estoy aquí, y es mejor así. Digamos que Lorencia y yo somos viejas amigas.",
                $"Fuera de las murallas hay arañas, toros y cosas peores. Antes de alejarte, busca a {c.Teacher} en {c.TeacherPlace}. Dile que vas de mi parte: te pondrá a prueba y te enseñará a pelear como es debido.",
                "Y ahora, mi precio por el consejo. No quiero zen: quiero una promesa. Algún día, muy lejos de aquí, quizás oigas un nombre: Mephis. Si llegas a encontrarlo, no le temas, digan lo que digan de él. Prométeme que lo salvarás.",
                $"No preguntes quién es. Todavía no. Solo recuerda su nombre cuando el mundo te parezca más grande de lo que te contaron. Anda, que {c.Teacher} no espera a nadie.",
            ],
            "Todavía no tengo nada para ti.",
            null),
        new(
            StrangerDuel,
            "El forastero",
            StoryActor.Stranger,
            StoryStepKind.Duel,
            "Duelo",
            _ =>
            [
                "Tú. Sí, tú. Te he visto marcharte y volver, una y otra vez, más fuerte de lo que te fuiste. Aquí lo llaman renacer y nadie se pregunta por qué ocurre. Yo sí me lo pregunto.",
                "No soy de Lorencia. No me preguntes de dónde vengo, ni de cuándo: no puedo decir demasiado. En este pueblo hay oídos que llevan siglos escuchando, y me cuido de ellos.",
                "Solo te diré esto: se acerca un terror antiguo, algo que este pueblo fue construido para olvidar. Y el mundo es mucho más grande de lo que te enseñaron. Lorencia, Noria, Elbeland... son apenas el patio de una casa enorme.",
                "Pero la verdad pesa, y no se la entrego a quien no puede cargarla. Desenvaina. Si logras vencerme, te revelaré un secreto.",
            ],
            "No te conozco, y tú no me conoces a mí. Sigue tu camino... y vuelve cuando hayas renacido un par de veces. Entonces hablaremos.",
            null),
        new(
            Devias,
            "El secreto del forastero",
            StoryActor.Stranger,
            StoryStepKind.Talk,
            "Continuar",
            _ =>
            [
                "Basta. Es suficiente... Hacía mucho tiempo que nadie me obligaba a retroceder. Tienes la mano de los que no se conforman.",
                "Lo prometido: al norte, más allá de las montañas, hay un bosque de hielo que tus mapas no muestran. Se llama Devias. Los guardias te dirán que allí no hay nada. Mienten, o les enseñaron a mentir sin que lo sepan.",
                "Desde hoy el camino está abierto para ti. Si logras sobrevivir al bosque de hielo, y a la reina que lo gobierna, podrás conocer qué les ocurre a aquellos que buscan la verdad. Vuelve a verme cuando lo hayas visto con tus propios ojos.",
            ],
            "Aún me debes un duelo.",
            "Se abrio el camino a Devias."),
        new(
            Dungeon,
            "Los que buscaron la verdad",
            StoryActor.Stranger,
            StoryStepKind.Talk,
            "Continuar",
            c =>
            [
                $"Has vuelto. Y todavía traes el frío de la Reina de Hielo en la armadura. Eres digno, {c.PlayerName}. Mi nombre es Artorias.",
                "El Imperio, aquí, está corrupto. Los soldados que llevan su estandarte ya no sirven a nadie. No dudes ante ellos. Desde hoy, el camino a su calabozo está abierto para ti: la entrada está al sur de Lorencia.",
                "Busco aliados para volver a mi tierra y evitar la devastación de un ser llamado Kundun. Hazte más fuerte. Cuando llegue el momento, volveremos a hablar.",
            ],
            "Aún no. Sobrevive al bosque de hielo y a su reina, renace unas veces más, y vuelve a verme.",
            "Se abrio el camino a Dungeon."),
        new(
            OldPacts,
            "La sacerdotisa de Devias",
            StoryActor.Stranger,
            StoryStepKind.Talk,
            "Continuar",
            c =>
            [
                $"Has crecido, {c.PlayerName}. Ya eres el aliado que buscaba. Pero para enfrentar a Kundun no alcanza con la fuerza: necesitas recuperar lo que tu sangre olvidó.",
                "En Devias vive Sebina, sacerdotisa de la escuela de la Luz. Su familia guarda la memoria de los pactos antiguos. Ve a verla y dile que te envía Artorias.",
            ],
            "Todavía no estás listo para el resto de la historia. Hazte más fuerte y vuelve.",
            null),
        new(
            SchoolOfLight,
            "Los viejos pactos",
            StoryActor.Sebina,
            StoryStepKind.Talk,
            "Continuar",
            c =>
            [
                "Así que Artorias te envía... Mi familia guardó esa profecía durante mil años sin poder pronunciarla. Soy Sebina, de la escuela de la Luz. Por mis venas corre la sangre de Etramu Renos.",
                "El olvido de Lorencia no nos alcanzó del todo: los discípulos de Etramu juramos recordar. Y recordamos que tu oficio no nació en ese pueblo. Nació de un pacto con el Imperio de Mu.",
                GetPact(c.Family),
                c.HasClassChange
                    ? "Para reconectarte con ese pacto tendrás que superar las pruebas de tu orden. Te pediré reliquias de los viejos tiempos; algunas duermen en la Torre Perdida, que desde hoy ya no te está vedada. Cuando estés listo, háblame y comenzaremos."
                    : "No tengo pruebas para ti, pero sí un consejo: desde hoy la Torre Perdida ya no te está vedada. Sube por ella. Lo que hay en su cima es más viejo que Lorencia.",
            ],
            "Que la Luz te acompañe. Todavía no es momento de que hablemos.",
            "Se abrio el camino a Lost Tower."),
        new(
            OldWorld,
            "El camino al viejo mundo",
            StoryActor.Lumen,
            StoryStepKind.Talk,
            "Continuar",
            c =>
            [
                $"Mírate, {c.PlayerName}. Saliste de aquí con un arma prestada y vuelves con el paso de los que ya no le temen a nada. Te has vuelto muy poderoso. Más de lo que este pueblo puede contener.",
                "Sé que te abrieron la Torre Perdida. Si la subes entera, hasta lo más alto, llegarás al viejo mundo. Al salir de la torre te espera un reino sumergido: Atlans. Está lleno de peligros, pero ya no queda tiempo para tener miedo. El tiempo se acaba.",
                "Y sé también con quién has estado hablando. Ese forastero... Artorias. Escúchame bien: es un traidor al Imperio. Llena de mentiras la cabeza de los jóvenes para que le hagan el trabajo sucio. No debes creerle.",
                "Tú me hiciste una promesa. Más allá del mar, en el viejo mundo, está Mephis. Lleva mil años esperando. Ve, hazte fuerte, y cuando lo encuentres... sálvalo. Desde hoy, todos los caminos están abiertos para ti.",
            ],
            "Sigue haciéndote fuerte. Cuando llegue el momento, tendré algo más que decirte.",
            "Se abrieron todos los caminos del viejo mundo."),
    ];

    /// <summary>
    /// Gets what an actor says when it has nothing to tell to the player right now.
    /// </summary>
    /// <param name="actor">The actor.</param>
    /// <param name="finishedStep">The last step which the player finished.</param>
    /// <returns>The text.</returns>
    public static string GetIdleText(StoryActor actor, int finishedStep) => actor switch
    {
        StoryActor.Stranger when finishedStep >= Steps.Count => "Lumen te habrá dicho cosas sobre mí. Júzgame por lo que has visto, no por lo que te cuenten. Volveremos a hablar cuando el viejo mundo te haya mostrado sus heridas.",
        StoryActor.Stranger when finishedStep >= Dungeon => "Hazte más fuerte. Nos queda poco tiempo, y un camino muy largo.",
        StoryActor.Stranger when finishedStep >= Devias => "El bosque de hielo te espera al norte. Vuelve cuando hayas visto a su reina.",
        StoryActor.Stranger => "No tengo nada que decirte. Todavía.",
        StoryActor.Sebina => "Que la Luz te acompañe.",
        _ => "Anda, que el mundo no se va a recorrer solo.",
    };

    /// <summary>
    /// Gets the step with a number.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The step, or <c>null</c> if the story has no such step.</returns>
    public static StoryStep? GetStep(int number) => number >= 1 && number <= Steps.Count ? Steps[number - 1] : null;

    /// <summary>
    /// Splits paragraphs into the pages of a scene: a paragraph which is longer than <see cref="MaximumPageLength"/>
    /// continues on the next page, after the end of a sentence if possible.
    /// </summary>
    /// <param name="paragraphs">The paragraphs.</param>
    /// <returns>The pages.</returns>
    public static IReadOnlyList<string> Paginate(IEnumerable<string> paragraphs)
    {
        var pages = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var rest = paragraph.Trim();
            while (rest.Length > MaximumPageLength)
            {
                var cut = rest.LastIndexOfAny(['.', '!', '?', ':'], MaximumPageLength - 1);
                if (cut < MaximumPageLength / 3)
                {
                    cut = rest.LastIndexOf(' ', MaximumPageLength - 1);
                }

                if (cut <= 0)
                {
                    cut = MaximumPageLength - 1;
                }

                pages.Add(rest[..(cut + 1)].Trim());
                rest = rest[(cut + 1)..].Trim();
            }

            if (rest.Length > 0)
            {
                pages.Add(rest);
            }
        }

        return pages;
    }

    private static string GetPact(StoryClassFamily family) => family switch
    {
        StoryClassFamily.Knight => "Los guerreros como tú fueron la guardia imperial. Marchaban bajo el mando del general Gaion y eran el muro entre el Imperio y la oscuridad. Tu espada recuerda ese juramento, aunque tú lo hayas olvidado.",
        StoryClassFamily.Elf => "Las elfas como tú sellaron una alianza con el Imperio: gracias a ellas pudo domar la naturaleza rebelde del continente. Los bosques escuchaban su voz, y sus flechas cuidaban cada camino.",
        StoryClassFamily.Wizard => "Los altos magos como tú prestaron al Imperio su poder y su erudición. Los lideraba el propio Etramu Renos, mi antepasado. De sus torres salió la magia que selló a Kundun.",
        StoryClassFamily.Summoner => "Las invocadoras como tú le explicaron al Imperio cómo manejar la magia dimensional, ese don que solo unos pocos traen de nacimiento. Runedil, el mago que le abrió el camino a Artorias, era uno de los tuyos.",
        StoryClassFamily.MagicGladiator => "Los gladiadores mágicos nacieron después: hijos de guerreros y de magos, herederos de dos pactos a la vez. Por eso no necesitas renovar ninguno: la guardia de Gaion y las torres de Etramu ya viven en ti.",
        StoryClassFamily.DarkLord => "Los señores oscuros descienden de los comandantes del Imperio, los que aprendieron a mandar ejércitos y bestias. Tu linaje nunca rompió su pacto: no tienes nada que renovar, solo que recordarlo.",
        _ => "Los luchadores vienen de los reinos hermanos del Imperio: guerreros de puño y espíritu que pelearon junto a la guardia de Gaion. Tu pacto es de sangre, no de palabra, y no necesita renovarse.",
    };
}
