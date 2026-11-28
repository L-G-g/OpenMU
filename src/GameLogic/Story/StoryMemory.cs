// <copyright file="StoryMemory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

using MUnique.OpenMU.GameLogic.Spectres;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// An actor of a <see cref="StoryMemory"/>: a character with its class and equipment, see <see cref="RivalPlayer"/>.
/// </summary>
/// <param name="Name">The short name, which is the name of the character for the clients (at most 10 characters).</param>
/// <param name="Title">The name which the client shows above the actor and in the dialogue box.</param>
/// <param name="Kind">The kind, which is the class.</param>
/// <param name="ItemLevel">The level of the items.</param>
/// <param name="Equipment">The items, as group and number.</param>
/// <param name="Spot">Where the actor stands when it appears.</param>
/// <param name="LooksAt">The point which the actor faces when it appears.</param>
/// <param name="IsSitting">If set to <c>true</c>, the actor sits (on the chair of its spot).</param>
/// <param name="EntersAtLine">The index of the line at which the actor appears; 0 if it's there from the start.</param>
/// <param name="WalksTo">Where the actor walks to after it appeared, or <c>null</c>.</param>
/// <param name="NpcNumber">If above 0, the actor is an NPC with the look of this monster number (for example an NPC of a town)
/// instead of a character; then <paramref name="Kind"/>, <paramref name="ItemLevel"/> and <paramref name="Equipment"/> don't matter,
/// and the NPC doesn't move.</param>
public sealed record StoryMemoryActor(
    string Name,
    string Title,
    SpectreKind Kind,
    byte ItemLevel,
    IReadOnlyList<(byte Group, short Number)> Equipment,
    Point Spot,
    Point LooksAt,
    bool IsSitting = false,
    int EntersAtLine = 0,
    Point? WalksTo = null,
    short NpcNumber = 0)
{
    /// <summary>
    /// Gets a value indicating whether the actor is an NPC, see <see cref="NpcNumber"/>.
    /// </summary>
    public bool IsNpc => this.NpcNumber > 0;
}

/// <summary>
/// A line of a <see cref="StoryMemory"/>.
/// </summary>
/// <param name="Actor">The index of the actor who says it.</param>
/// <param name="Text">The text (Latin-1).</param>
/// <param name="Sound">The sound of the client which starts when the line is shown (it replaces the one before), or <c>null</c>
/// to keep the one which plays. The sound of the first line is <see cref="StoryMemory.Sound"/>.</param>
/// <param name="LoopsSound">If set to <c>false</c>, the <paramref name="Sound"/> plays once and silence follows.</param>
public sealed record StoryMemoryLine(int Actor, string Text, string? Sound = null, bool LoopsSound = true);

/// <summary>
/// What an actor does in a <see cref="StoryMemoryCue"/>.
/// </summary>
public enum StoryMemoryCueKind
{
    /// <summary>
    /// The actor plays an animation (<see cref="StoryMemoryCue.Animation"/>). If it's a pose which lasts, like kneeling,
    /// it's shown again to the clients which see the actor later.
    /// </summary>
    Animate,

    /// <summary>
    /// The actor gets up from its pose and stands.
    /// </summary>
    StandUp,

    /// <summary>
    /// The actor turns towards <see cref="StoryMemoryCue.Target"/>, without moving.
    /// </summary>
    Turn,

    /// <summary>
    /// The actor walks to <see cref="StoryMemoryCue.Target"/>.
    /// </summary>
    Walk,

    /// <summary>
    /// The actor walks to <see cref="StoryMemoryCue.Target"/> and disappears there, for example through a door.
    /// </summary>
    Leave,
}

/// <summary>
/// Something which an actor of a <see cref="StoryMemory"/> does when the client shows a line.
/// </summary>
/// <param name="Line">The index of the line.</param>
/// <param name="Actor">The index of the actor.</param>
/// <param name="Kind">What the actor does.</param>
/// <param name="Target">Where the actor walks to (<see cref="StoryMemoryCueKind.Walk"/>, <see cref="StoryMemoryCueKind.Leave"/>) or looks at (<see cref="StoryMemoryCueKind.Turn"/>).</param>
/// <param name="Animation">The animation, for <see cref="StoryMemoryCueKind.Animate"/>.</param>
/// <param name="Holds">If set to <c>true</c>, the <paramref name="Animation"/> is a pose which the actor keeps until the next cue.</param>
public sealed record StoryMemoryCue(int Line, int Actor, StoryMemoryCueKind Kind, Point? Target = null, byte Animation = 0, bool Holds = false);

/// <summary>
/// The camera of a <see cref="StoryMemory"/>: it doesn't move, and looks at a point of the map like the camera of the game
/// looks at the character, but turned.
/// </summary>
/// <param name="Target">The point of the map at which the camera looks.</param>
/// <param name="Height">The height of the ground at the <paramref name="Target"/>, in units of the client (100 per tile).</param>
/// <param name="Yaw">The direction in which the camera looks, in degrees: 0 looks towards +Y, 90 towards +X. The camera of the game has -45.</param>
/// <param name="Tilt">The angle between the camera and the vertical, in degrees: 0 looks down, 90 to the horizon. The camera of the game has 48.5.</param>
/// <param name="Distance">The distance between the camera and the <paramref name="Target"/>, in units of the client.</param>
public sealed record StoryMemoryCamera(Point Target, short Height, short Yaw, float Tilt, short Distance);

/// <summary>
/// A memory of the story mode: a scene of the past which a player watches, played by actors in an instance of a map
/// (<see cref="StorySceneContext"/>). The client (cinematicas.dll) shows it with a fixed camera and the dialogue box of the
/// story; the player only goes on to the next line.
/// </summary>
/// <param name="Title">The title, which the player sees when the scene starts (without accents: a message of the game).</param>
/// <param name="ViewerSpot">Where the (hidden) character of the player stands: the actors must be in its view range.</param>
/// <param name="Camera">The camera.</param>
/// <param name="Actors">The actors.</param>
/// <param name="Lines">The lines, in their order.</param>
/// <param name="EndLabel">The text of the button after the last line.</param>
/// <param name="Sound">The sound of the client which replaces the music of the map during the memory (a path below the folder of the client), or <c>null</c>.</param>
/// <param name="Cues">What the actors do at the lines, or <c>null</c>.</param>
public sealed record StoryMemory(
    string Title,
    Point ViewerSpot,
    StoryMemoryCamera Camera,
    IReadOnlyList<StoryMemoryActor> Actors,
    IReadOnlyList<StoryMemoryLine> Lines,
    string EndLabel,
    string? Sound = null,
    IReadOnlyList<StoryMemoryCue>? Cues = null)
{
    /// <summary>
    /// The name of the NPC at the entrance of each level of Kalima which shows the memories.
    /// </summary>
    public const string SeerName = "Vidente";

    /// <summary>
    /// The number of the map of the <see cref="Sacrifice"/>: Devias.
    /// </summary>
    public const short SacrificeMapNumber = 2;

    private const int Muren = 0;
    private const int Etramu = 1;
    private const int Mephis = 2;

    private const int WeddingEntrance = 6;
    private const int Groom = 0;
    private const int Bride = 1;
    private const int Seliane = 2;
    private const int Daveth = 3;
    private const int Ilsa = 4;
    private const int Kreol = 5;
    private const int Bishop = 6;

    /// <summary>
    /// The numbers of the maps of Kalima 1 to 7: the seer stands at the entrance of each level, and the memory of a step
    /// is shown on its level (<see cref="StoryStep.KalimaLevel"/>) and on the deeper ones.
    /// </summary>
    public static IReadOnlyList<short> KalimaMapNumbers { get; } = [24, 25, 26, 27, 28, 29, 36];

    /// <summary>
    /// Gets what the seer says before it shows the <see cref="Sacrifice"/>.
    /// </summary>
    public static IReadOnlyList<string> SeerText { get; } =
    [
        "Kalima no es un lugar: es una herida. Son las tierras que el sello arrancó del mundo hace mil años, y sus piedras todavía recuerdan lo que vieron. Cuanto más hondo bajes, más viejos serán los recuerdos que guardan. Yo solo aprendí a escucharlos.",
        "En este primer nivel hay uno que no deja de volver: una sala de piedra en el norte helado, un rey en su trono y una decisión que cambió el destino del continente. Puedo mostrártelo, si quieres verlo.",
    ];

    /// <summary>
    /// Gets what the seer says before it shows the <see cref="Wedding"/>.
    /// </summary>
    public static IReadOnlyList<string> WeddingSeerText { get; } =
    [
        "Bajaste un nivel más, y las piedras hablan de algo más viejo todavía. Antes del trono y del sacrificio hubo una iglesia en Devias, llena de flores, y una promesa que nunca llegó a cumplirse.",
        "Es un recuerdo triste. Pero si quieres entender lo que vino después, tienes que verlo. ¿Te lo muestro?",
    ];

    /// <summary>
    /// Gets what the seer says when the player watches a memory again.
    /// </summary>
    public static IReadOnlyList<string> SeerAgainText { get; } =
    [
        "Los recuerdos no se gastan. Si quieres, las piedras de Kalima pueden mostrártelo otra vez.",
    ];

    /// <summary>
    /// Gets the level of Kalima (1 to 7) of a map.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <returns>The level, or 0 if the map isn't Kalima.</returns>
    public static int GetKalimaLevel(short mapNumber)
    {
        for (var i = 0; i < KalimaMapNumbers.Count; i++)
        {
            if (KalimaMapNumbers[i] == mapNumber)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// The animation of a character which falls on its knees: the one of sitting, which on flat ground is a kneel.
    /// </summary>
    public const byte KneelAnimation = 0x80;

    /// <summary>
    /// The animation of a character which cries.
    /// </summary>
    public const byte CryAnimation = 0x89;

    /// <summary>
    /// The number of the NPC whose look the bishop of the <see cref="Wedding"/> has: the Seed Master.
    /// </summary>
    public const short BishopLookNumber = 452;

    /// <summary>
    /// Gets the first memory: in the throne room of the castle of Devias, a thousand years ago, Emperor Muren Rexmillon and
    /// the wizard Etramu Renos have run out of ways to contain the titan Secneum, and Mephis offers himself as the sacrifice.
    /// </summary>
    /// <remarks>
    /// The throne room: the hall at 218..231 / 225..238 of Devias. Its doors are at 224..227 / 222..224, the carpet runs
    /// along +Y from the doors to the dais with the two thrones (chairs at 225 and 226 / 237, which face the doors).
    /// The camera is turned like in the picture of the request: the dais at the top left, the doors at the bottom right.
    /// </remarks>
    public static StoryMemory Sacrifice { get; } = new(
        "Hace mil anos, en el castillo de Devias...",
        new Point(225, 231),
        new StoryMemoryCamera(new Point(226, 232), 165, 60, 48.5f, 1150),
        [
            // Glorious set; a king needs no weapon on his throne.
            new StoryMemoryActor("Muren", "Rey Muren Rexmillon", SpectreKind.Lord, 13, [(7, 38), (8, 38), (9, 38), (10, 38), (11, 38)], new Point(225, 237), new Point(225, 230), IsSitting: true),

            // Venom Mist set and Platina Staff.
            new StoryMemoryActor("Etramu", "Etramu Renos", SpectreKind.Mage, 13, [(7, 30), (8, 30), (9, 30), (10, 30), (11, 30), (5, 13)], new Point(227, 234), new Point(225, 237)),

            // Legendary set and Legendary Staff. He comes in through the doors.
            new StoryMemoryActor("Mephis", "Mephis", SpectreKind.Mage, 13, [(7, 3), (8, 3), (9, 3), (10, 3), (11, 3), (5, 5)], new Point(225, 219), new Point(225, 237), EntersAtLine: 8, WalksTo: new Point(225, 230)),
        ],
        [
            new(Muren, "Dímelo sin rodeos, Etramu. ¿Cuánto tiempo le queda al sello?"),
            new(Etramu, "Días, majestad. Quizás menos. Las runas de contención se quiebran más rápido de lo que podemos restaurarlas. Secneum está despertando."),
            new(Muren, "Entonces refuércenlas. Tienes a todos los magos del Imperio a tu disposición."),
            new(Etramu, "Ya lo intentamos todo: los sellos de luz, las cadenas rúnicas, las barreras de los antiguos. Nada resiste. No quedan más alternativas."),
            new(Muren, "Siempre queda otra alternativa."),
            new(Etramu, "Esta vez no. El colapso es inminente. Solo nos queda el rito antiguo: encerrar al titán dentro de un recipiente vivo. Un sacrificio."),
            new(Muren, "¿Y quién podría contener a un titán sin ser consumido en el acto?"),
            new(Etramu, "Alguien con un poder que casi no existe en este mundo. Y debemos encontrarlo pronto, antes de que no quede continente que salvar."),
            new(Mephis, "No hace falta que sigan buscando."),
            new(Etramu, "¡Mephis! Esta audiencia no te concierne."),
            new(Mephis, "Me concierne más que a nadie. Puedo sentirla, majestad: la energía oscura invade el continente como una marea, y cada noche está más cerca."),
            new(Mephis, "Sé lo que exige el rito. Y sé que soy el único capaz de evitar la devastación."),
            new(Mephis, "Estoy listo para el sacrificio."),
        ],
        "Volver a Kalima",
        "Data\\Sound\\RecuerdoSacrificio.wav"); // a file of the PeroniaMU client, not of the original one

    /// <summary>
    /// Gets the second memory (shown in Kalima 2, although it happened before the <see cref="Sacrifice"/>): in the church
    /// of Devias, Mephis and Lemuria are getting married, when Mephis feels Secneum awake and leaves the altar.
    /// </summary>
    /// <remarks>
    /// The church: the nave at 13..29 / 24..27 of Devias, between two rows of pillars (y 23 and 28). The altar is at
    /// 12 / 24..27 with room for the bishop behind it (10..11 / 25..26; 10 / 26 is the closest to the middle of the altar on the screen), the door is at its other end (30 / 24..27).
    /// </remarks>
    public static StoryMemory Wedding { get; } = new(
        "Hace mil anos, en la iglesia de Devias...",
        new Point(20, 21),
        new StoryMemoryCamera(new Point(15, 26), 165, -45, 48.5f, 1350),
        [
            // Legendary set, like in the memory of the sacrifice, but no staff at his wedding.
            new StoryMemoryActor("Mephis", "Mephis", SpectreKind.Mage, 13, [(7, 3), (8, 3), (9, 3), (10, 3), (11, 3)], new Point(29, 26), new Point(13, 26), EntersAtLine: WeddingEntrance, WalksTo: new Point(15, 26)),

            // Guardian set.
            new StoryMemoryActor("Lemuria", "Lemuria", SpectreKind.Elf, 13, [(7, 14), (8, 14), (9, 14), (10, 14), (11, 14)], new Point(29, 25), new Point(13, 25), EntersAtLine: WeddingEntrance, WalksTo: new Point(15, 25)),

            // The guests outside the carpet, behind the pillars, who whisper about the bride and the groom.
            new StoryMemoryActor("Seliane", "Dama Seliane", SpectreKind.Elf, 9, [(7, 19), (8, 19), (9, 19), (10, 19), (11, 19)], new Point(18, 22), new Point(19, 22)),
            new StoryMemoryActor("Daveth", "Sir Daveth", SpectreKind.Warrior, 9, [(7, 1), (8, 1), (9, 1), (10, 1), (11, 1)], new Point(19, 22), new Point(18, 22)),
            new StoryMemoryActor("Ilsa", "Ilsa, la invocadora", SpectreKind.Summoner, 9, [(7, 40), (8, 40), (9, 40), (10, 40), (11, 40)], new Point(18, 29), new Point(19, 29)),
            new StoryMemoryActor("Kreol", "Baron Kreol", SpectreKind.Lord, 9, [(7, 25), (8, 25), (9, 25), (10, 25), (11, 25)], new Point(19, 29), new Point(18, 29)),

            // The bishop behind the altar: an NPC with the look of the Seed Master.
            new StoryMemoryActor("Obispo", "Obispo", SpectreKind.Mage, 0, [], new Point(10, 26), new Point(13, 26), NpcNumber: BishopLookNumber),
        ],
        [
            new(Seliane, "¿Ya lo viste? Dicen que el novio es Mephis en persona."),
            new(Daveth, "El mismo. Uno de los magos más poderosos del Imperio, y de los más especiales. Ni los sabios de la torre de Etramu entienden del todo de dónde saca su poder."),
            new(Ilsa, "¿Y la novia? Me contaron que llegó a su torre siendo apenas una aprendiz."),
            new(Kreol, "Lemuria. Primero fue su discípula, después su amante, y hoy será su esposa. Y no te dejes engañar por el vestido: su poder no se queda atrás del de él."),
            new(Seliane, "Que los dioses protejan Devias el día que esos dos discutan..."),
            new(Daveth, "Silencio. Ya empieza."),
            new(Bishop, "Queridos hermanos: bajo la luz del Imperio nos reunimos hoy para unir en matrimonio a Mephis y a Lemuria. Que se acerquen los novios.", "Data\\Sound\\BodaCeremonia.wav"),
            new(Bishop, "Mephis, Lemuria: el camino que los trajo hasta este altar empezó entre libros y hechizos, en una torre donde uno enseñaba y la otra aprendía. Hoy ese camino se vuelve uno solo."),
            new(Bishop, "Que el lazo que hoy los une sea más fuerte que cualquier sello y más duradero que cualquier hechizo."),
            new(Bishop, "Lemuria, ¿aceptas a Mephis como esposo, para caminar a su lado en la luz y en la oscuridad, hasta el final de tus días?"),
            new(Bride, "Acepto. Hoy, mañana y siempre."),
            new(Bishop, "Mephis, ¿aceptas a Lemuria como esposa, para caminar a su lado en la luz y en la oscuridad, hasta el final de tus días?"),
            new(Groom, "Yo... ¡Ngh!", "Data\\Sound\\BodaPresencia.wav"),
            new(Bride, "¡Mephis! ¿Qué te pasa? ¡Mírame!"),
            new(Groom, "Esa presencia... La siento bajo la tierra, como una marea negra que sube. Es Secneum. Está despertando."),
            new(Groom, "Lemuria... perdóname, mi amor. Tengo un deber que cumplir, y no puede esperar."),
            new(Bride, "¿Un deber? ¡Mephis, es nuestra boda! ¡No te vayas!"),
            new(Groom, "Perdóname."),
            new(Bride, "No... no, no... ¡Mephis!", "Data\\Sound\\BodaLlanto.wav"),
            new(Bride, "Te esperé toda mi vida... y te vas así, sin mirar atrás..."),
            new(Bride, "No. No voy a perderte. No me importa lo que cueste ni cuánto tenga que esperar. Te juro que no voy a perderte."),
            new(Bride, "Espérame, Mephis."),
        ],
        "Volver a Kalima",
        "Data\\Sound\\BodaSusurros.wav", // files of the PeroniaMU client, not of the original one
        [
            // The guests stop whispering and look at the carpet, where the bride and the groom come in.
            new(WeddingEntrance, Seliane, StoryMemoryCueKind.Turn, new Point(18, 26)),
            new(WeddingEntrance, Daveth, StoryMemoryCueKind.Turn, new Point(19, 26)),
            new(WeddingEntrance, Ilsa, StoryMemoryCueKind.Turn, new Point(18, 25)),
            new(WeddingEntrance, Kreol, StoryMemoryCueKind.Turn, new Point(19, 25)),

            // When the groom falls, the guests look at the altar.
            new(12, Seliane, StoryMemoryCueKind.Turn, new Point(15, 25)),
            new(12, Daveth, StoryMemoryCueKind.Turn, new Point(15, 25)),
            new(12, Ilsa, StoryMemoryCueKind.Turn, new Point(15, 26)),
            new(12, Kreol, StoryMemoryCueKind.Turn, new Point(15, 26)),

            // He falls on his knees, then gets up, takes a breath and leaves the church.
            new(12, Groom, StoryMemoryCueKind.Animate, Animation: KneelAnimation, Holds: true),
            new(15, Groom, StoryMemoryCueKind.StandUp),
            new(17, Groom, StoryMemoryCueKind.Leave, new Point(30, 26)),

            // She falls on her knees and cries there (the animation of crying would get her up), then gets up and follows him.
            new(18, Bride, StoryMemoryCueKind.Animate, Animation: KneelAnimation, Holds: true),
            new(20, Bride, StoryMemoryCueKind.StandUp),
            new(21, Bride, StoryMemoryCueKind.Leave, new Point(30, 25)),
        ]);
}
