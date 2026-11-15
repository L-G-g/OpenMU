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
    Point? WalksTo = null);

/// <summary>
/// A line of a <see cref="StoryMemory"/>.
/// </summary>
/// <param name="Actor">The index of the actor who says it.</param>
/// <param name="Text">The text (Latin-1).</param>
public sealed record StoryMemoryLine(int Actor, string Text);

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
public sealed record StoryMemory(
    string Title,
    Point ViewerSpot,
    StoryMemoryCamera Camera,
    IReadOnlyList<StoryMemoryActor> Actors,
    IReadOnlyList<StoryMemoryLine> Lines,
    string EndLabel,
    string? Sound = null)
{
    /// <summary>
    /// The name of the NPC of Atlans which shows the memories.
    /// </summary>
    public const string SeerName = "Vidente";

    /// <summary>
    /// The number of the map of the <see cref="Sacrifice"/>: Devias.
    /// </summary>
    public const short SacrificeMapNumber = 2;

    private const int Muren = 0;
    private const int Etramu = 1;
    private const int Mephis = 2;

    /// <summary>
    /// Gets what the seer says before it shows the <see cref="Sacrifice"/>.
    /// </summary>
    public static IReadOnlyList<string> SeerText { get; } =
    [
        "Las aguas de Atlans guardan recuerdos más viejos que cualquier reino que conozcas. Yo solo aprendí a escucharlas.",
        "Hay uno que no deja de volver: una sala de piedra en el norte helado, un rey en su trono y una decisión que cambió el destino del continente. Puedo mostrártelo, si quieres verlo.",
    ];

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
        new StoryMemoryCamera(new Point(223, 230), 165, 60, 48.5f, 1500),
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
        "Volver a Atlans",
        "Data\\Sound\\iBloodCastle.wav"); // the music of the Blood Castle
}
