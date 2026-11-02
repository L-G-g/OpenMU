// flight.dll - Monturas voladoras: el Dinorant vuela por encima de paredes, arboles y casas en el cliente
// MU 1.04d con camera3d.dll.
//
// El servidor (plugin "Flight" de OpenMU, comando /volar) decide quien vuela: deja caminar sobre baldosas
// bloqueadas (no sobre el vacio) a la velocidad x1,5 y avisa con el paquete C1 06 FE [numero del objeto, 2
// bytes big endian] [1 vuela, 0 no] cuando alguien despega o aterriza y cuando uno que vuela entra a la vista.
// El main.exe original ignora el codigo 0xFE (su reparto, 0x006639D0, manda los codigos mayores a 0xF9 al
// caso por defecto).
//
// Como funciona, para cada personaje que vuela (numero de objeto, +0x7E del personaje, marcado por el 0xFE):
//   - Velocidad: 0x00564B20 es float __cdecl Velocidad(personaje) (12 caminando, 15 con Dinorant, 17-19
//     caballo/Fenrir, x0,5 con hielo). Se salta al principio de la funcion (los primeros 6 bytes van a un
//     trampolin) y se multiplica por [Flight] SpeedFactor. La usa el paso de movimiento (0x00564DD0) de
//     todos los personajes, tambien los de otros jugadores.
//   - Camino: 0x00542D20 es la busqueda de camino (x, y, destino x, y, PATH*, distancia, pared). Una baldosa
//     se puede pisar si su atributo (0x082C6AA0) es menor que "pared" (2 normalmente). Volando se pasa 8:
//     se cruza 0x04 (no se camina) pero no 0x08 (sin suelo). El PATH es +0x274 del personaje. La usan el
//     click en el terreno y el movimiento de los otros jugadores que manda el servidor.
//   - Altura: el juego pone a cada personaje sobre el terreno (+30 con Dinorant) en muchos lugares, asi que
//     la altura de vuelo se suma solo para dibujar: camera3d.dll llama antes y despues del dibujo de la
//     escena (despues del movimiento fluido) y aca se sube la posicion (+0x404) y despues se devuelve el
//     valor exacto. La camara sigue al personaje subido. Al despegar y aterrizar la altura cambia de a poco.
//   - Montura y mascotas: son "bugs" aparte (10 OBJECT en 0x08791550), que copian la posicion del dueno
//     (+0x1D8, el OBJECT del personaje, +0x308) en el paso de logica (0x00501790), no al dibujar
//     (0x00503770). Se suben y se devuelven igual que el personaje, y tambien los efectos con ese dueno
//     (0x08967FE8 y 0x097622F0, por ejemplo el buff del Elf Soldier). Volando, el suelo dibujado sigue al
//     terreno de a poco, para que no rebote con el relieve.
//   - Jinete: 0x0054F710 elige la accion al caminar; volando ve el mundo 10 (en Icarus el jinete del Dinorant
//     no usa la accion de correr montado, que sube y baja).
//   - Alas: el paso del bug (0x00501790) elige las acciones de vuelo del Dinorant (bug 0x143: 1, 3, 5, 7
//     en vez de 0, 2, 4, 6) cuando el mundo ([0x00E61E18]) es 8 (Tarkan) o 10 (Icarus). Si el dueno vuela,
//     durante esa llamada el mundo es 10 y despues vuelve a ser el de antes.
//   - Linea recta: volando, el camino se arma en linea recta (pasos de 8 direcciones, hasta 15 puntos con
//     el de inicio, como lo escribe 0x00542D20) en vez del A* del juego, que zigzaguea. Si la linea pasa
//     por una baldosa sin suelo (0x08) se usa el A* con pared 8.
//   - Tecla: [Flight] Key (Shift + V por defecto) manda /volar por el chat, solo con el foco en la ventana
//     del juego (no escribiendo en el chat).

#include "../shared.h"

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;  // CALL al reparto al recibir cada paquete
    constexpr DWORD SpeedFunction = 0x00564B20;     // float __cdecl (CHARACTER*)
    constexpr DWORD PathFunction = 0x00542D20;      // bool __cdecl (sx, sy, tx, ty, PATH*, float, int pared)
    const BYTE SpeedPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x10 }; // PUSH EBP, MOV EBP,ESP, SUB ESP,0x10
    const BYTE PathPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x1C };  // PUSH EBP, MOV EBP,ESP, SUB ESP,0x1C
    constexpr DWORD BugMoveFunction = 0x00501790;   // char __cdecl (OBJECT* bug, char)
    const BYTE BugMovePrologue[] = { 0x55, 0x8B, 0xEC, 0x81, 0xEC, 0x8C, 0x01, 0x00, 0x00 }; // ... SUB ESP,0x18C
    int* const World = reinterpret_cast<int*>(0x00E61E18);
    constexpr int IcarusWorld = 10;
    const WORD* const TerrainAttributes = reinterpret_cast<const WORD*>(0x082C6AA0); // 256x256, y * 256 + x
    constexpr WORD NoGround = 0x08;
    constexpr DWORD PathStepFunction = 0x005428B0;  // char __cdecl (CHARACTER*, char girar): avanza por el PATH
    const BYTE PathStepPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x68 }; // PUSH EBP, MOV EBP,ESP, SUB ESP,0x68
    constexpr DWORD AngleToFunction = 0x00540C30;   // float __cdecl (x, y, destino x, y)
    constexpr DWORD AngleDiffFunction = 0x005410A0; // float __cdecl (angulo, destino, 1)
    constexpr DWORD TurnFunction = 0x00540EA0;      // float __cdecl (angulo, destino, paso)
    constexpr float TileSize = 100.0f;              // como 0x005428B0: (baldosa + 0,5) * 100
    constexpr float ArriveDistance = 20.0f;         // 0x00D23600
    constexpr float TurnLimit = 45.0f;              // 0x00D2AF68
    constexpr DWORD SendChatFunction = 0x005BDE40;  // void __cdecl (const char* texto)
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);
    constexpr int ChatReady = 0x32;                 // el chat sale si la espera es como mucho esto
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // [+8]: los 400 personajes
    DWORD* const Hero = reinterpret_cast<DWORD*>(0x07BC4F04);              // personaje propio
    constexpr BYTE KeyUp = 0, KeyPressed = 2, KeyHeld = 3;

    // --- Personajes (CHARACTER, 0x598 bytes) ---
    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    // --- Camino (PATH: +2 cantidad de puntos, +3 las x, +0x12 las y) ---
    constexpr int PathMaxPoints = 15;
    constexpr DWORD PathCount = 2, PathX = 3, PathY = 0x12;

    constexpr DWORD CharKey = 0x7E, CharMoving = 0x78, CharTargetX = 0xAC, CharTargetY = 0xB0, CharWings = 0x208,
        CharHelper = 0x22C, CharPath = 0x274, CharObject = 0x308, CharLive = 0x30C, CharPosition = 0x404,
        CharHeading = 0x418;
    constexpr short DinorantHelper = 0x1E96;     // +0x22C con el Dinorant (0x00564B20, 0x0054F710)
    constexpr short UniriaHelper = 0x1E95, DarkHorseHelper = 0x1E97, FenrirHelper = 0x1EB8;
    const BYTE StopActionPrologue[] = { 0x55, 0x8B, 0xEC, 0x6A, 0xFF }; // PUSH EBP, MOV EBP,ESP, PUSH -1
    constexpr DWORD CharRun = 0x25;              // contador de correr (0x0054F710: con alas, 40 o mas vuela)
    constexpr BYTE FlyRun = 0x28;
    constexpr DWORD StopActionFunction = 0x0054EA80; // void __cdecl (CHARACTER*): accion quieto (0x7A-0x7D con Fenrir)

    // --- Objetos con dueno (OBJECT de 0x288 bytes): montura y mascotas (bugs) y efectos ---
    constexpr DWORD ObjectSize = 0x288;
    constexpr DWORD ObjectLive = 0x04, ObjectPosition = 0xFC, ObjectOwner = 0x1D8;
    constexpr DWORD BugLive = ObjectLive, BugOwner = ObjectOwner;

    struct AttachedArray
    {
        BYTE* base;
        int count;
        DWORD size;  // bytes de cada elemento
        DWORD live;  // byte "vivo"
        DWORD owner; // puntero al OBJECT del dueno
        bool hide;   // en vez de subirlos, no se dibujan mientras el dueno vuela
    };

    const AttachedArray AttachedArrays[] = {
        { reinterpret_cast<BYTE*>(0x08791550), 10, ObjectSize, ObjectLive, ObjectOwner, false },  // bugs (0x00503720)
        { reinterpret_cast<BYTE*>(0x08967FE8), 200, ObjectSize, ObjectLive, ObjectOwner, false }, // efectos (0x00721620)
        { reinterpret_cast<BYTE*>(0x097622F0), 100, ObjectSize, ObjectLive, ObjectOwner, false }, // efectos (0x0074ABC0)
        // Particulas con estela (0x007711C0; 0x007716D0 borra las de un dueno, tipo 0x7D02 en el buff del Elf
        // Soldier) y uniones (0x087C5708, 0xA18 bytes; 0x00735DC0 borra las de un dueno): el remolino del buff.
        // Se dibujan con las posiciones anteriores que guardan en el paso de logica, asi que subir la posicion
        // actual no las mueve. Volando no se dibujan.
        { reinterpret_cast<BYTE*>(0x09778748), 1000, ObjectSize, ObjectLive, ObjectOwner, true },
        { reinterpret_cast<BYTE*>(0x087C5708), 500, 0xA18, 0x00, 0x54, true },
    };
    constexpr int AttachedCount = 1810;
    constexpr BYTE AttachedNone = 0, AttachedLifted = 1, AttachedHidden = 2;

    // Volando, el suelo dibujado sigue al terreno con esta rapidez (fraccion por segundo).
    constexpr float GroundFollowPerSecond = 3.0f;

    // --- Pose del jinete ---
    constexpr DWORD RiderWalkFunction = 0x0054F710; // void __cdecl (CHARACTER*): accion al caminar
    const BYTE RiderWalkPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x44 }; // PUSH EBP, MOV EBP,ESP, SUB ESP,0x44

    // --- Paquetes ---
    constexpr int CodeOutOfScope = 0x14, CodeFlight = 0xFE;
    constexpr WORD IdMask = 0x7FFF; // 0x8000 marca a los recien aparecidos
    constexpr int FlyingWall = 8;   // umbral de pared volando: se cruza 0x04, no 0x08

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using SpeedType = float(__cdecl*)(BYTE*);
    using PathType = bool(__cdecl*)(int, int, int, int, BYTE*, float, int);
    using BugMoveType = char(__cdecl*)(BYTE*, char);
    using RiderWalkType = void(__cdecl*)(BYTE*);
    using PathStepType = char(__cdecl*)(BYTE*, char);
    using AngleToType = float(__cdecl*)(float, float, float, float);
    using AngleDiffType = float(__cdecl*)(float, float, int);
    using TurnType = float(__cdecl*)(float, float, float);
    using SendChatType = void(__cdecl*)(const char*);

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    // Por numero de objeto: 0 no vuela; si no, el factor de velocidad en porcentaje (150 = x1,5) que manda el
    // servidor (1: el de [Flight] SpeedFactor, servidores viejos).
    BYTE g_flying[IdMask + 1] = {};

    float g_speedFactor = 1.5f;
    float g_height = 350.0f;      // unidades del mundo (una baldosa son 100)
    float g_climbPerSecond = 250.0f;
    float FlightForwardSign = 1.0f; // [Flight] ForwardSign: 1 o -1, para que Superman mire hacia donde avanza
    int g_key = 'V';
    bool g_keyShift = true;
    BYTE g_suppressedKey = 0;
    bool g_sendCommand = false;

    // Altura actual de cada lugar de personaje (sube y baja de a poco) y para que numero de objeto es.
    float g_lift[CharacterCount] = {};
    WORD g_liftKey[CharacterCount] = {};
    float g_savedZ[CharacterCount] = {};
    bool g_lifted[CharacterCount] = {};
    float g_target[CharacterCount] = {};

    // Vuelo con alas: fases del despegue y del aterrizaje y cuanto de cada pose se aplica a los huesos.
    enum Phase : BYTE { PhaseGround, PhaseCrouch, PhaseRise, PhaseAir, PhaseBrake, PhaseDrop };

    struct Pose
    {
        float crouch = 0.0f;  // agachado antes del salto
        float stretch = 0.0f; // estirado subiendo (brazos arriba en V)
        float super = 0.0f;   // Superman: horizontal, brazos adelante, piernas atras
        float brake = 0.0f;   // frenando: cuerpo hacia atras, piernas adelante, brazos abiertos
    };

    Phase g_phase[CharacterCount] = {};
    float g_phaseTime[CharacterCount] = {};
    float g_dropFrom[CharacterCount] = {};
    Pose g_pose[CharacterCount] = {};
    float g_swayTime = 0.0f;
    float g_ground[CharacterCount] = {};
    float g_offset[CharacterCount] = {};
    float g_attachedSavedZ[AttachedCount] = {};
    BYTE g_attachedState[AttachedCount] = {}; // AttachedNone, AttachedLifted o AttachedHidden
    LARGE_INTEGER g_frequency{};
    LARGE_INTEGER g_lastFrame{};

    WORD ReadId(const BYTE* data)
    {
        return static_cast<WORD>(((data[0] << 8) | data[1]) & IdMask);
    }

    BYTE* Characters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    bool IsFlying(const BYTE* character)
    {
        return character != nullptr && g_flying[*reinterpret_cast<const WORD*>(character + CharKey) & IdMask] != 0;
    }

    float SpeedFactorOf(const BYTE* character)
    {
        const BYTE percent = g_flying[*reinterpret_cast<const WORD*>(character + CharKey) & IdMask];
        return percent == 0 ? 1.0f : percent == 1 ? g_speedFactor : percent / 100.0f;
    }

    // Vuela con alas (no montado en el Dinorant, que tiene su propia pose de jinete).
    bool HasWingsWithoutDinorant(const BYTE* character)
    {
        return *reinterpret_cast<const short*>(character + CharWings) != -1
            && *reinterpret_cast<const short*>(character + CharHelper) != DinorantHelper;
    }

    // Monturas que no son el Dinorant (+0x22C: 0x1E93 + numero del item 13/N): con alas, volando no se dibujan
    // y el personaje usa las poses de volar.
    bool IsOtherMount(short helper)
    {
        return helper == UniriaHelper || helper == DarkHorseHelper || helper == FenrirHelper;
    }

    int CharacterIndex(const BYTE* character);

    // Con alas y sin el Dinorant, volando o todavia en el aire al aterrizar: usa las poses de volar.
    bool UsesWingPose(const BYTE* character)
    {
        if (!HasWingsWithoutDinorant(character))
        {
            return false;
        }

        const int i = CharacterIndex(character);
        return IsFlying(character) || (i >= 0 && g_lift[i] > 0.0f);
    }

    bool HidesMount(const BYTE* character)
    {
        return UsesWingPose(character) && IsOtherMount(*reinterpret_cast<const short*>(character + CharHelper));
    }

    // Mientras existe, el personaje no tiene montura para el juego (las acciones se eligen como sin ella).
    struct WithoutMount
    {
        BYTE* character;
        short helper;

        explicit WithoutMount(BYTE* target)
            : character(HidesMount(target) ? target : nullptr),
              helper(character != nullptr ? *reinterpret_cast<short*>(target + CharHelper) : 0)
        {
            if (character != nullptr)
            {
                *reinterpret_cast<short*>(character + CharHelper) = -1;
            }
        }

        ~WithoutMount()
        {
            if (character != nullptr)
            {
                *reinterpret_cast<short*>(character + CharHelper) = helper;
            }
        }
    };

    // Indice del personaje en la lista, o -1 si el puntero no es el principio de un personaje.
    int CharacterIndex(const BYTE* character)
    {
        const BYTE* characters = Characters();
        if (characters == nullptr || character < characters || character >= characters + CharacterCount * CharacterSize)
        {
            return -1;
        }

        const DWORD offset = static_cast<DWORD>(character - characters);
        return offset % CharacterSize == 0 ? static_cast<int>(offset / CharacterSize) : -1;
    }

    // --- Paquetes ---
    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr)
        {
            const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
            if (code == CodeFlight)
            {
                if (packet[0] == 0xC1 && size >= 6)
                {
                    const WORD id = ReadId(packet + 3);
                    g_flying[id] = packet[5];
                    const DWORD hero = *Hero;
                    if (packet[5] == 0 && hero != 0 && (*reinterpret_cast<const WORD*>(hero + CharKey) & IdMask) == id)
                    {
                        // El propio aterrizo o cambio de mapa: los demas que se veian volar ya no estan a la vista.
                        memset(g_flying, 0, sizeof(g_flying));
                    }
                }

                return 1; // el juego no lo conoce
            }

            if (code == CodeOutOfScope)
            {
                const int count = size > header ? packet[header] : 0;
                for (int i = 0; i < count && header + 1 + i * 2 + 2 <= size; i++)
                {
                    g_flying[ReadId(packet + header + 1 + i * 2)] = 0;
                }
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // --- Velocidad ---
    // Los primeros 6 bytes de 0x00564B20 y un salto al resto.
    __declspec(naked) float __cdecl SpeedOriginal(BYTE*)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x10
            push 0x00564B26 // SpeedFunction + 6
            ret
        }
    }

    // Por un puntero volatil: el compilador no puede suponer nada de los registros de la funcion desnuda.
    SpeedType volatile g_speedOriginal = &SpeedOriginal;

    float __cdecl SpeedHook(BYTE* character)
    {
        const float speed = g_speedOriginal(character);
        return IsFlying(character) ? speed * SpeedFactorOf(character) : speed;
    }

    // --- Camino ---
    __declspec(naked) bool __cdecl PathOriginal(int, int, int, int, BYTE*, float, int)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x1C
            push 0x00542D26 // PathFunction + 6
            ret
        }
    }

    PathType volatile g_pathOriginal = &PathOriginal;

    // Camino en linea recta de (sx, sy) a (tx, ty), con el punto de inicio, cortado a PathMaxPoints. Falso si
    // no hay donde ir o la linea pasa por una baldosa sin suelo.
    bool StraightPath(int sx, int sy, int tx, int ty, BYTE* path)
    {
        if ((sx == tx && sy == ty) || sx < 0 || sy < 0 || tx < 0 || ty < 0 || sx > 255 || sy > 255 || tx > 255 || ty > 255)
        {
            return false;
        }

        BYTE xs[PathMaxPoints];
        BYTE ys[PathMaxPoints];
        int count = 0;
        int x = sx;
        int y = sy;
        xs[count] = static_cast<BYTE>(x);
        ys[count] = static_cast<BYTE>(y);
        count++;

        // Bresenham: en cada paso se avanza en x, en y o en las dos (diagonal), asi los puntos quedan pegados.
        const int dx = abs(tx - sx);
        const int dy = abs(ty - sy);
        const int stepX = sx < tx ? 1 : -1;
        const int stepY = sy < ty ? 1 : -1;
        int error = dx - dy;
        while ((x != tx || y != ty) && count < PathMaxPoints)
        {
            const int doubled = 2 * error;
            if (doubled > -dy)
            {
                error -= dy;
                x += stepX;
            }

            if (doubled < dx)
            {
                error += dx;
                y += stepY;
            }

            if (TerrainAttributes[y * 256 + x] & NoGround)
            {
                return false;
            }

            xs[count] = static_cast<BYTE>(x);
            ys[count] = static_cast<BYTE>(y);
            count++;
        }

        path[0] = 0;
        path[1] = 0;
        path[PathCount] = static_cast<BYTE>(count);
        memcpy(path + PathX, xs, count);
        memcpy(path + PathY, ys, count);
        return true;
    }

    bool __cdecl PathHook(int sx, int sy, int tx, int ty, BYTE* path, float distance, int wall)
    {
        if (path != nullptr && wall < FlyingWall)
        {
            BYTE* character = path - CharPath;
            if (CharacterIndex(character) >= 0 && IsFlying(character))
            {
                if (StraightPath(sx, sy, tx, ty, path))
                {
                    return true;
                }

                wall = FlyingWall;
            }
        }

        return g_pathOriginal(sx, sy, tx, ty, path, distance, wall);
    }

    // --- Avance recto ---
    // 0x005428B0 sigue el PATH con una curva por cada 4 puntos (+1 del PATH es el tramo, 0 a 3) y gira hacia
    // ese objetivo; con una linea en escalera el objetivo va y viene y el personaje zigzaguea. Volando se gira
    // hacia el ultimo punto, el punto actual (+0 del PATH) pasa al mas cercano (solo hacia adelante) y a menos
    // de 20 del final se llega, como en el original.
    __declspec(naked) char __cdecl PathStepOriginal(BYTE*, char)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x68
            push 0x005428B6 // PathStepFunction + 6
            ret
        }
    }

    PathStepType volatile g_pathStepOriginal = &PathStepOriginal;

    float TileCenter(BYTE tile)
    {
        return (static_cast<float>(tile) + 0.5f) * TileSize;
    }

    char __cdecl PathStepHook(BYTE* character, char turn)
    {
        BYTE* path = character != nullptr ? character + CharPath : nullptr;
        if (path == nullptr || CharacterIndex(character) < 0 || !IsFlying(character) || path[0] >= path[PathCount])
        {
            return g_pathStepOriginal(character, turn);
        }

        const int count = path[PathCount];
        float* position = reinterpret_cast<float*>(character + CharPosition);
        const float endX = TileCenter(path[PathX + count - 1]);
        const float endY = TileCenter(path[PathY + count - 1]);

        int nearest = path[0];
        float nearestDistance = 0.0f;
        for (int i = path[0]; i < count; i++)
        {
            const float dx = position[0] - TileCenter(path[PathX + i]);
            const float dy = position[1] - TileCenter(path[PathY + i]);
            const float distance = dx * dx + dy * dy;
            if (i == path[0] || distance < nearestDistance)
            {
                nearest = i;
                nearestDistance = distance;
            }
        }

        path[0] = static_cast<BYTE>(nearest);
        path[1] = 0;
        const int next = nearest + 1 < count ? nearest + 1 : count - 1;
        *reinterpret_cast<DWORD*>(character + CharTargetX) = path[PathX + next];
        *reinterpret_cast<DWORD*>(character + CharTargetY) = path[PathY + next];

        const float toEndX = position[0] - endX;
        const float toEndY = position[1] - endY;
        if (toEndX * toEndX + toEndY * toEndY < ArriveDistance * ArriveDistance)
        {
            path[0] = static_cast<BYTE>(count - 1);
            *reinterpret_cast<DWORD*>(character + CharTargetX) = path[PathX + count - 1];
            *reinterpret_cast<DWORD*>(character + CharTargetY) = path[PathY + count - 1];
            position[0] = endX;
            position[1] = endY;
            return 1;
        }

        if (turn)
        {
            float* heading = reinterpret_cast<float*>(character + CharHeading);
            const float target = reinterpret_cast<AngleToType>(AngleToFunction)(position[0], position[1], endX, endY);
            const float difference = reinterpret_cast<AngleDiffType>(AngleDiffFunction)(*heading, target, 1);
            *heading = difference < TurnLimit
                ? reinterpret_cast<TurnType>(TurnFunction)(*heading, target, difference * 0.5f)
                : target;
        }

        return 0;
    }

    // --- Alas del Dinorant ---
    // Los primeros 9 bytes de 0x00501790 y un salto al resto.
    __declspec(naked) char __cdecl BugMoveOriginal(BYTE*, char)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x18C
            push 0x00501799 // BugMoveFunction + 9
            ret
        }
    }

    BugMoveType volatile g_bugMoveOriginal = &BugMoveOriginal;

    char __cdecl BugMoveHook(BYTE* bug, char flag)
    {
        const BYTE* owner = bug != nullptr && bug[BugLive] ? *reinterpret_cast<BYTE* const*>(bug + BugOwner) : nullptr;
        BYTE* character = owner != nullptr ? const_cast<BYTE*>(owner) - CharObject : nullptr;
        if (character == nullptr || CharacterIndex(character) < 0 || !IsFlying(character))
        {
            return g_bugMoveOriginal(bug, flag);
        }

        // Como en Icarus: el Dinorant abre las alas y usa sus acciones de vuelo.
        const int world = *World;
        *World = IcarusWorld;
        const char result = g_bugMoveOriginal(bug, flag);
        *World = world;
        return result;
    }

    // --- Pose del jinete ---
    // 0x0054F710 elige la accion del personaje al caminar; con el Dinorant, en Tarkan e Icarus no le pone la de
    // correr montado (que sube y baja). Volando ve el mundo 10 durante la llamada, como el paso del bug.
    __declspec(naked) void __cdecl RiderWalkOriginal(BYTE*)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x44
            push 0x0054F716 // RiderWalkFunction + 6
            ret
        }
    }

    RiderWalkType volatile g_riderWalkOriginal = &RiderWalkOriginal;

    void __cdecl RiderWalkHook(BYTE* character)
    {
        if (character == nullptr || CharacterIndex(character) < 0 || !UsesWingPose(character) && !IsFlying(character))
        {
            g_riderWalkOriginal(character);
            return;
        }

        // Con alas, la accion de volar (0x22/0x23, el cuerpo horizontal) se usa recien cuando el contador de
        // correr (+0x25) llega a 40; volando se usa siempre.
        if (UsesWingPose(character) && character[CharRun] < FlyRun)
        {
            character[CharRun] = FlyRun;
        }

        const WithoutMount withoutMount(character);
        const int world = *World;
        *World = IcarusWorld;
        g_riderWalkOriginal(character);
        *World = world;
    }

    // 0x0054EA80 elige la accion quieto. Con alas y otra montura, volando, se elige como sin montura (parado).
    __declspec(naked) void __cdecl StopActionOriginal(BYTE*)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            push -1
            push 0x0054EA85 // StopActionFunction + 5
            ret
        }
    }

    RiderWalkType volatile g_stopActionOriginal = &StopActionOriginal;

    void __cdecl StopActionHook(BYTE* character)
    {
        if (character == nullptr || CharacterIndex(character) < 0)
        {
            g_stopActionOriginal(character);
            return;
        }

        const WithoutMount withoutMount(character);
        g_stopActionOriginal(character);
    }

    // --- Vuelo con alas: fases ---
    constexpr float CrouchSeconds = 0.25f; // agachado antes del salto
    constexpr float RiseSeconds = 0.7f;    // subida del salto, rapida al principio
    constexpr float BrakeSeconds = 0.45f;  // freno en el aire antes de bajar
    constexpr float DropSeconds = 0.55f;   // bajada al suelo, rapida al final
    constexpr float SuperPerSecond = 3.0f; // rapidez con la que entra y sale la pose de Superman

    float Clamp01(float value)
    {
        return value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
    }

    float EaseOut(float t)
    {
        t = Clamp01(t);
        return 1.0f - (1.0f - t) * (1.0f - t);
    }

    float EaseIn(float t)
    {
        t = Clamp01(t);
        return t * t;
    }

    float Approach(float value, float target, float step)
    {
        return value < target ? (value + step > target ? target : value + step) : (value - step < target ? target : value - step);
    }

    void StandStill(BYTE* character)
    {
        if (!character[CharMoving])
        {
            reinterpret_cast<RiderWalkType>(StopActionFunction)(character);
        }
    }

    // Despegue: agachado, salto (sube rapido estirado); en el aire: quieto parado o Superman al moverse;
    // aterrizaje: freno arriba y despues baja. Fija g_lift y g_pose del lugar.
    void UpdateWingFlight(int i, BYTE* character, bool flying, float seconds)
    {
        float& t = g_phaseTime[i];
        t += seconds;
        Pose pose{};
        switch (g_phase[i])
        {
        case PhaseGround:
            g_lift[i] = 0.0f;
            if (flying)
            {
                g_phase[i] = PhaseCrouch;
                t = 0.0f;
                StandStill(character); // baja de la montura (si tiene otra que no es el Dinorant) antes de agacharse
            }

            break;
        case PhaseCrouch:
            g_lift[i] = 0.0f;
            pose.crouch = EaseOut(t / CrouchSeconds);
            if (!flying)
            {
                g_phase[i] = PhaseGround;
            }
            else if (t >= CrouchSeconds)
            {
                g_phase[i] = PhaseRise;
                t = 0.0f;
            }

            break;
        case PhaseRise:
            g_lift[i] = g_height * EaseOut(t / RiseSeconds);
            pose.stretch = 1.0f - EaseIn(t / RiseSeconds);
            pose.crouch = 1.0f - Clamp01(t / 0.12f);
            if (!flying)
            {
                g_phase[i] = PhaseBrake;
                t = 0.0f;
            }
            else if (t >= RiseSeconds)
            {
                g_phase[i] = PhaseAir;
                t = 0.0f;
                StandStill(character);
            }

            break;
        case PhaseAir:
            g_lift[i] = g_height;
            if (!flying)
            {
                g_phase[i] = PhaseBrake;
                t = 0.0f;
            }

            break;
        case PhaseBrake:
            pose.brake = EaseOut(t / (BrakeSeconds * 0.5f));
            if (flying)
            {
                g_phase[i] = PhaseAir;
                t = 0.0f;
            }
            else if (t >= BrakeSeconds)
            {
                g_phase[i] = PhaseDrop;
                g_dropFrom[i] = g_lift[i];
                t = 0.0f;
            }

            break;
        case PhaseDrop:
            g_lift[i] = g_dropFrom[i] * (1.0f - EaseIn(t / DropSeconds));
            pose.brake = 1.0f - EaseIn(t / DropSeconds);
            if (flying)
            {
                g_phase[i] = PhaseRise;
                t = 0.0f;
            }
            else if (t >= DropSeconds)
            {
                g_phase[i] = PhaseGround;
                g_lift[i] = 0.0f;
                t = 0.0f;
                StandStill(character);
            }

            break;
        }

        // Superman entra de a poco al moverse en el aire y sale al quedarse quieto.
        const bool super = g_phase[i] == PhaseAir && character[CharMoving];
        pose.super = Approach(g_pose[i].super, super ? 1.0f : 0.0f, seconds * SuperPerSecond);
        g_pose[i] = pose;
    }

    // --- Vuelo con alas: huesos ---
    // 0x005440B0 es BMD::Animation (thiscall: BoneTransform, cuadro, cuadro anterior, accion anterior, Angle,
    // HeadAngle, padre, trasladar): arma la matriz 3x4 de cada hueso (0x30 bytes) en el espacio del modelo,
    // padre * local. Despues se ajustan los huesos del personaje que vuela con alas: se "apunta" cada miembro
    // hacia una direccion (girando el hueso y todo lo que cuelga de el alrededor de su articulacion) y se
    // inclina el cuerpo desde la pelvis. El esqueleto de Player.bmd: 60 huesos (BMD +0x22 cantidad, +0x2C los
    // huesos de 0x8C bytes: nombre, +0x20 padre, +0x22 hueso vacio).
    constexpr DWORD AnimationFunction = 0x005440B0;
    const BYTE AnimationPrologue[] = { 0x55, 0x8B, 0xEC, 0x81, 0xEC, 0xA4, 0x00, 0x00, 0x00 }; // ... SUB ESP,0xA4
    constexpr int PlayerBoneCount = 60;
    constexpr int BonePelvis = 2, BoneLThigh = 3, BoneLCalf = 4, BoneLFoot = 5, BoneRThigh = 10, BoneRCalf = 11,
        BoneRFoot = 12, BoneRClavicle = 25, BoneRUpperArm = 26, BoneRForearm = 27, BoneRHand = 28,
        BoneLClavicle = 34, BoneLUpperArm = 35, BoneLForearm = 36, BoneLHand = 37;
    constexpr DWORD BmdBoneCount = 0x22, BmdBones = 0x2C, BoneSize = 0x8C, BoneParent = 0x20, BoneDummy = 0x22;
    constexpr float Pi = 3.14159265f;

    using AnimationType = void(__fastcall*)(BYTE*, void*, float*, float, float, DWORD, float*, float*, DWORD, DWORD);

    struct Vec3
    {
        float x, y, z;
    };

    Vec3 operator+(Vec3 a, Vec3 b) { return { a.x + b.x, a.y + b.y, a.z + b.z }; }
    Vec3 operator-(Vec3 a, Vec3 b) { return { a.x - b.x, a.y - b.y, a.z - b.z }; }
    Vec3 operator*(Vec3 a, float s) { return { a.x * s, a.y * s, a.z * s }; }
    float Dot(Vec3 a, Vec3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    Vec3 Cross(Vec3 a, Vec3 b) { return { a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x }; }

    Vec3 Normalize(Vec3 v)
    {
        const float length = sqrtf(Dot(v, v));
        return length > 0.0001f ? v * (1.0f / length) : Vec3{ 0.0f, 0.0f, 0.0f };
    }

    struct Skeleton
    {
        float (*bones)[3][4];
        const BYTE* bmd;

        Vec3 Position(int bone) const { return { bones[bone][0][3], bones[bone][1][3], bones[bone][2][3] }; }

        bool IsInside(int bone, int ancestor) const
        {
            const BYTE* records = *reinterpret_cast<BYTE* const*>(bmd + BmdBones);
            for (int guard = 0; bone >= 0 && bone < PlayerBoneCount && guard < PlayerBoneCount; guard++)
            {
                if (bone == ancestor)
                {
                    return true;
                }

                if (records[bone * BoneSize + BoneDummy] != 0)
                {
                    return false;
                }

                bone = *reinterpret_cast<const short*>(records + bone * BoneSize + BoneParent);
            }

            return false;
        }

        // Gira el hueso y todo lo que cuelga de el alrededor de "pivot" (eje normalizado, angulo en radianes).
        void Rotate(int root, Vec3 pivot, Vec3 axis, float angle) const
        {
            if (angle == 0.0f)
            {
                return;
            }

            const float c = cosf(angle), s = sinf(angle), k = 1.0f - c;
            const float r[3][3] = {
                { c + axis.x * axis.x * k, axis.x * axis.y * k - axis.z * s, axis.x * axis.z * k + axis.y * s },
                { axis.y * axis.x * k + axis.z * s, c + axis.y * axis.y * k, axis.y * axis.z * k - axis.x * s },
                { axis.z * axis.x * k - axis.y * s, axis.z * axis.y * k + axis.x * s, c + axis.z * axis.z * k },
            };

            const BYTE* records = *reinterpret_cast<BYTE* const*>(bmd + BmdBones);
            for (int b = 0; b < PlayerBoneCount; b++)
            {
                if (records[b * BoneSize + BoneDummy] != 0 || !IsInside(b, root))
                {
                    continue;
                }

                float (&m)[3][4] = bones[b];
                float result[3][4];
                const float t[3] = { m[0][3] - pivot.x, m[1][3] - pivot.y, m[2][3] - pivot.z };
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        result[row][col] = r[row][0] * m[0][col] + r[row][1] * m[1][col] + r[row][2] * m[2][col];
                    }

                    result[row][3] = r[row][0] * t[0] + r[row][1] * t[1] + r[row][2] * t[2];
                }

                for (int row = 0; row < 3; row++)
                {
                    m[row][0] = result[row][0];
                    m[row][1] = result[row][1];
                    m[row][2] = result[row][2];
                }

                m[0][3] = result[0][3] + pivot.x;
                m[1][3] = result[1][3] + pivot.y;
                m[2][3] = result[2][3] + pivot.z;
            }
        }

        // Apunta el hueso (de su articulacion a la del hijo) hacia "direction", en la medida "weight".
        void Aim(int bone, int child, Vec3 direction, float weight) const
        {
            if (weight <= 0.0f)
            {
                return;
            }

            const Vec3 pivot = Position(bone);
            const Vec3 current = Normalize(Position(child) - pivot);
            const Vec3 target = Normalize(direction);
            const Vec3 axis = Normalize(Cross(current, target));
            const float cosine = Dot(current, target);
            if (Dot(axis, axis) < 0.5f)
            {
                return; // ya apunta ahi (o justo al reves)
            }

            Rotate(bone, pivot, axis, acosf(cosine < -1.0f ? -1.0f : cosine > 1.0f ? 1.0f : cosine) * weight);
        }

        void Translate(int root, Vec3 offset) const
        {
            const BYTE* records = *reinterpret_cast<BYTE* const*>(bmd + BmdBones);
            for (int b = 0; b < PlayerBoneCount; b++)
            {
                if (records[b * BoneSize + BoneDummy] == 0 && IsInside(b, root))
                {
                    bones[b][0][3] += offset.x;
                    bones[b][1][3] += offset.y;
                    bones[b][2][3] += offset.z;
                }
            }
        }
    };

    bool IsPlayerSkeleton(const BYTE* bmd)
    {
        static const BYTE* checked = nullptr;
        if (bmd == checked)
        {
            return true;
        }

        const BYTE* records = *reinterpret_cast<BYTE* const*>(bmd + BmdBones);
        if (*reinterpret_cast<const short*>(bmd + BmdBoneCount) != PlayerBoneCount || records == nullptr
            || strcmp(reinterpret_cast<const char*>(records + BoneRUpperArm * BoneSize), "Bip01 R UpperArm") != 0
            || strcmp(reinterpret_cast<const char*>(records + BoneLThigh * BoneSize), "Bip01 L Thigh") != 0)
        {
            return false;
        }

        checked = bmd;
        return true;
    }

    // Una pierna o un brazo: apunta el hueso de arriba y despues el de abajo.
    void AimLimb(const Skeleton& skeleton, int upper, int lower, int end, Vec3 upperDirection, Vec3 lowerDirection, float weight)
    {
        skeleton.Aim(upper, lower, upperDirection, weight);
        skeleton.Aim(lower, end, lowerDirection, weight);
    }

    void ApplyPose(const Skeleton& skeleton, const Pose& pose, float yawDegrees)
    {
        if (pose.crouch <= 0.0f && pose.stretch <= 0.0f && pose.super <= 0.0f && pose.brake <= 0.0f)
        {
            return;
        }

        // Ejes del cuerpo parado. El juego avanza por (0, -1, 0) girado por el angulo del personaje (0x00564DD0)
        // y los huesos ya incluyen ese giro: adelante es (sen giro, -cos giro, 0).
        const Vec3 up{ 0.0f, 0.0f, 1.0f };
        const float yaw = yawDegrees * Pi / 180.0f;
        const Vec3 forward = Vec3{ sinf(yaw), -cosf(yaw), 0.0f } * FlightForwardSign;
        const Vec3 right = Cross(forward, up);

        // Superman: brazos estirados por encima de la cabeza (al inclinar el cuerpo quedan adelante), piernas
        // juntas y derechas.
        AimLimb(skeleton, BoneRUpperArm, BoneRForearm, BoneRHand, up + forward * 0.15f + right * 0.12f, up + forward * 0.1f + right * 0.05f, pose.super);
        AimLimb(skeleton, BoneLUpperArm, BoneLForearm, BoneLHand, up + forward * 0.15f - right * 0.12f, up + forward * 0.1f - right * 0.05f, pose.super);
        AimLimb(skeleton, BoneRThigh, BoneRCalf, BoneRFoot, up * -1.0f + right * 0.03f, up * -1.0f + forward * -0.05f, pose.super);
        AimLimb(skeleton, BoneLThigh, BoneLCalf, BoneLFoot, up * -1.0f - right * 0.03f, up * -1.0f + forward * -0.05f, pose.super);

        // Subiendo: brazos arriba en V, piernas derechas.
        AimLimb(skeleton, BoneRUpperArm, BoneRForearm, BoneRHand, up + right * 0.45f, up + right * 0.35f, pose.stretch);
        AimLimb(skeleton, BoneLUpperArm, BoneLForearm, BoneLHand, up - right * 0.45f, up - right * 0.35f, pose.stretch);
        AimLimb(skeleton, BoneRThigh, BoneRCalf, BoneRFoot, up * -1.0f + right * 0.05f, up * -1.0f, pose.stretch);
        AimLimb(skeleton, BoneLThigh, BoneLCalf, BoneLFoot, up * -1.0f - right * 0.05f, up * -1.0f, pose.stretch);

        // Agachado: rodillas adelante, brazos atras y abajo para tomar impulso.
        AimLimb(skeleton, BoneRThigh, BoneRCalf, BoneRFoot, forward * 0.8f - up * 0.6f + right * 0.1f, forward * -0.45f - up * 0.9f, pose.crouch);
        AimLimb(skeleton, BoneLThigh, BoneLCalf, BoneLFoot, forward * 0.8f - up * 0.6f - right * 0.1f, forward * -0.45f - up * 0.9f, pose.crouch);
        AimLimb(skeleton, BoneRUpperArm, BoneRForearm, BoneRHand, forward * -0.5f - up * 0.7f + right * 0.3f, forward * -0.4f - up * 0.8f, pose.crouch);
        AimLimb(skeleton, BoneLUpperArm, BoneLForearm, BoneLHand, forward * -0.5f - up * 0.7f - right * 0.3f, forward * -0.4f - up * 0.8f, pose.crouch);

        // Frenando: piernas adelante, brazos adelante y abiertos.
        AimLimb(skeleton, BoneRThigh, BoneRCalf, BoneRFoot, up * -0.85f + forward * 0.55f + right * 0.08f, up * -1.0f + forward * 0.1f, pose.brake);
        AimLimb(skeleton, BoneLThigh, BoneLCalf, BoneLFoot, up * -0.85f + forward * 0.55f - right * 0.08f, up * -1.0f + forward * 0.1f, pose.brake);
        AimLimb(skeleton, BoneRUpperArm, BoneRForearm, BoneRHand, forward * 0.6f + up * 0.25f + right * 0.75f, forward * 0.7f + up * 0.3f + right * 0.6f, pose.brake);
        AimLimb(skeleton, BoneLUpperArm, BoneLForearm, BoneLHand, forward * 0.6f + up * 0.25f - right * 0.75f, forward * 0.7f + up * 0.3f - right * 0.6f, pose.brake);

        // Todo el cuerpo desde la pelvis: Superman casi horizontal con un vaiven lento, adelante al agacharse,
        // hacia atras al frenar. Agachado baja la pelvis para que los pies sigan en el suelo.
        const Vec3 pelvis = skeleton.Position(BonePelvis);
        const float sway = sinf(g_swayTime * 1.3f);
        const float pitch = (80.0f + 4.0f * sinf(g_swayTime * 0.9f)) * pose.super + 25.0f * pose.crouch + 8.0f * pose.stretch - 28.0f * pose.brake;
        // Girar alrededor de "derecha" con angulo positivo lleva arriba hacia atras: se gira al reves.
        skeleton.Rotate(BonePelvis, pelvis, right, -pitch * Pi / 180.0f);
        skeleton.Rotate(BonePelvis, pelvis, Normalize(Cross(right, up)), 6.0f * sway * pose.super * Pi / 180.0f);
        skeleton.Translate(BonePelvis, up * (-35.0f * pose.crouch));
    }

    __declspec(naked) void __fastcall AnimationOriginal(BYTE*, void*, float*, float, float, DWORD, float*, float*, DWORD, DWORD)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0xA4
            push 0x005440B9 // AnimationFunction + 9
            ret
        }
    }

    AnimationType volatile g_animationOriginal = &AnimationOriginal;

    void __fastcall AnimationHook(BYTE* bmd, void* edx, float* boneTransform, float frame, float priorFrame,
        DWORD priorAction, float* angle, float* headAngle, DWORD parent, DWORD translate)
    {
        g_animationOriginal(bmd, edx, boneTransform, frame, priorFrame, priorAction, angle, headAngle, parent, translate);

        // El Angle que recibe es el del OBJECT (+0x108) del personaje que se dibuja.
        BYTE* character = angle != nullptr ? reinterpret_cast<BYTE*>(angle) - 0x108 - CharObject : nullptr;
        const int i = CharacterIndex(character);
        if (i < 0 || boneTransform == nullptr || bmd == nullptr || !IsPlayerSkeleton(bmd))
        {
            return;
        }

        const Skeleton skeleton{ reinterpret_cast<float(*)[3][4]>(boneTransform), bmd };
        ApplyPose(skeleton, g_pose[i], angle[2]);
    }

    // --- Altura (solo para dibujar) ---
    void __cdecl BeforeRender()
    {
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        float seconds = g_lastFrame.QuadPart != 0
            ? static_cast<float>(now.QuadPart - g_lastFrame.QuadPart) / static_cast<float>(g_frequency.QuadPart)
            : 0.0f;
        g_lastFrame = now;
        if (seconds > 0.25f)
        {
            seconds = 0.25f; // despues de una pausa larga (minimizado) no salta de golpe
        }

        BYTE* characters = Characters();
        if (characters == nullptr)
        {
            return;
        }

        const float step = g_climbPerSecond * seconds;
        g_swayTime += seconds;
        for (int i = 0; i < CharacterCount; i++)
        {
            BYTE* character = characters + i * CharacterSize;
            g_lifted[i] = false;
            if (!character[CharLive])
            {
                g_lift[i] = 0.0f;
                continue;
            }

            const WORD key = *reinterpret_cast<const WORD*>(character + CharKey) & IdMask;
            if (g_liftKey[i] != key)
            {
                // Otro personaje en ese lugar: empieza en el suelo (o ya arriba, si aparecio volando).
                g_liftKey[i] = key;
                g_lift[i] = g_flying[key] ? g_height : 0.0f;
                g_target[i] = g_lift[i];
                g_phase[i] = g_flying[key] ? PhaseAir : PhaseGround;
                g_phaseTime[i] = 0.0f;
                g_pose[i] = Pose{};
                g_ground[i] = reinterpret_cast<const float*>(character + CharPosition)[2];
            }

            const float target = g_flying[key] ? g_height : 0.0f;
            g_target[i] = target;
            if (HasWingsWithoutDinorant(character))
            {
                // Con alas: salto al despegar, freno al aterrizar y pose de Superman al moverse (ver UpdateWingFlight).
                UpdateWingFlight(i, character, g_flying[key] != 0, seconds);
            }
            else
            {
                g_phase[i] = g_flying[key] ? PhaseAir : PhaseGround;
                g_pose[i] = Pose{};
                if (g_lift[i] < target)
                {
                    g_lift[i] = g_lift[i] + step > target ? target : g_lift[i] + step;
                }
                else if (g_lift[i] > target)
                {
                    g_lift[i] = g_lift[i] - step < target ? target : g_lift[i] - step;
                }
            }

            float* position = reinterpret_cast<float*>(character + CharPosition);
            if (g_lift[i] <= 0.0f)
            {
                g_ground[i] = position[2];
                continue;
            }

            // El suelo dibujado sigue al terreno de a poco: volando no se notan los pozos ni los bordes del relieve.
            const float follow = seconds * GroundFollowPerSecond;
            g_ground[i] += (position[2] - g_ground[i]) * (follow > 1.0f ? 1.0f : follow);
            g_savedZ[i] = position[2];
            g_offset[i] = g_ground[i] + g_lift[i] - position[2];
            position[2] += g_offset[i];
            g_lifted[i] = true;
        }

        // La montura, las mascotas y los efectos (por ejemplo el buff del Elf Soldier) suben igual que su dueno.
        int slot = 0;
        for (const AttachedArray& attached : AttachedArrays)
        {
            for (int a = 0; a < attached.count; a++, slot++)
            {
                BYTE* object = attached.base + a * attached.size;
                g_attachedState[slot] = AttachedNone;
                if (!object[attached.live])
                {
                    continue;
                }

                const BYTE* owner = *reinterpret_cast<BYTE* const*>(object + attached.owner);
                const int i = owner != nullptr ? CharacterIndex(owner - CharObject) : -1;
                // Otra montura: no se dibuja desde que empieza el vuelo (tambien agachado, todavia en el suelo).
                const bool otherMount = attached.base == AttachedArrays[0].base && i >= 0 && HidesMount(owner - CharObject);
                if (i >= 0 && (otherMount || (g_lifted[i] && attached.hide)))
                {
                    g_attachedSavedZ[slot] = object[attached.live]; // aca se guarda el valor de "vivo"
                    object[attached.live] = 0;
                    g_attachedState[slot] = AttachedHidden;
                }
                else if (i >= 0 && g_lifted[i])
                {
                    float* position = reinterpret_cast<float*>(object + ObjectPosition);
                    g_attachedSavedZ[slot] = position[2];
                    position[2] += g_offset[i];
                    g_attachedState[slot] = AttachedLifted;
                }
            }
        }
    }

    void __cdecl AfterRender()
    {
        BYTE* characters = Characters();
        if (characters == nullptr)
        {
            return;
        }

        for (int i = 0; i < CharacterCount; i++)
        {
            if (g_lifted[i])
            {
                reinterpret_cast<float*>(characters + i * CharacterSize + CharPosition)[2] = g_savedZ[i];
                g_lifted[i] = false;
            }
        }

        int slot = 0;
        for (const AttachedArray& attached : AttachedArrays)
        {
            for (int a = 0; a < attached.count; a++, slot++)
            {
                if (g_attachedState[slot] != AttachedNone)
                {
                    BYTE* object = attached.base + a * attached.size;
                    if (g_attachedState[slot] == AttachedHidden)
                    {
                        object[attached.live] = static_cast<BYTE>(g_attachedSavedZ[slot]);
                    }
                    else
                    {
                        reinterpret_cast<float*>(object + ObjectPosition)[2] = g_attachedSavedZ[slot];
                    }

                    g_attachedState[slot] = AttachedNone;
                }
            }
        }
    }

    // --- Tecla ---
    bool IsDown(const BYTE* keyStates, int key)
    {
        return keyStates[key] == KeyPressed || keyStates[key] == KeyHeld;
    }

    void __cdecl KeyTick(BYTE* keyStates)
    {
        if (g_sendCommand && *ChatWait <= ChatReady)
        {
            g_sendCommand = false;
            reinterpret_cast<SendChatType>(SendChatFunction)("/volar");
        }

        if (g_suppressedKey != 0)
        {
            if (IsDown(keyStates, g_suppressedKey))
            {
                keyStates[g_suppressedKey] = KeyUp;
                return;
            }

            g_suppressedKey = 0;
        }

        if (g_key == 0 || GetFocus() != *MainWindow)
        {
            return; // escribiendo en el chat (o el juego no tiene el foco)
        }

        if (keyStates[g_key] == KeyPressed && (!g_keyShift || IsDown(keyStates, VK_SHIFT)))
        {
            g_suppressedKey = static_cast<BYTE>(g_key);
            keyStates[g_key] = KeyUp;
            g_sendCommand = true;
        }
    }

    // --- Instalacion ---
    DWORD CallTarget(DWORD callSite)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(callSite);
        return code[0] == 0xE8 ? callSite + 5 + *reinterpret_cast<const DWORD*>(code + 1) : 0;
    }

    bool RedirectCall(DWORD callSite, const void* target)
    {
        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(target) - (callSite + 5);
        return WriteCode(callSite, call, sizeof(call));
    }

    bool Matches(DWORD address, const BYTE* expected, size_t size)
    {
        return memcmp(reinterpret_cast<const void*>(address), expected, size) == 0;
    }

    // Reemplaza el prologo de la funcion (6 bytes o mas) por un salto al gancho y NOPs.
    bool JumpAtStart(DWORD function, const void* target, size_t prologueSize = 6)
    {
        BYTE jump[16] = { 0xE9 };
        memset(jump + 5, 0x90, sizeof(jump) - 5);
        *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(target) - (function + 5);
        return WriteCode(function, jump, prologueSize);
    }

    float ReadFloat(const char* iniPath, const char* name, float fallback, float minimum, float maximum)
    {
        char text[32];
        GetPrivateProfileStringA("Flight", name, "", text, sizeof(text), iniPath);
        if (text[0] == 0)
        {
            return fallback;
        }

        const float value = static_cast<float>(atof(text));
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    // [Flight] Key: una letra o numero (V) o un codigo de tecla virtual en hexadecimal (0x56); 0 sin tecla.
    // KeyShift=1: hay que apretar Shift tambien.
    void ReadKey(const char* iniPath)
    {
        char text[16];
        GetPrivateProfileStringA("Flight", "Key", "V", text, sizeof(text), iniPath);
        if (text[0] != 0 && text[1] == 0 && ((text[0] >= 'A' && text[0] <= 'Z') || (text[0] >= 'a' && text[0] <= 'z') || (text[0] >= '0' && text[0] <= '9')))
        {
            g_key = text[0] >= 'a' && text[0] <= 'z' ? text[0] - 'a' + 'A' : text[0];
        }
        else
        {
            const long code = strtol(text, nullptr, 0);
            g_key = code > 0 && code < 256 ? static_cast<int>(code) : 0;
        }

        g_keyShift = GetPrivateProfileIntA("Flight", "KeyShift", 1, iniPath) != 0;
    }
}

// Altura de vuelo que se dibuja ahora para el personaje propio (0 en el suelo). camera3d.dll la usa para
// agrandar el terreno visible: desde mas arriba se ve mas lejos.
extern "C" __declspec(dllexport) float FlightHeroLift()
{
    const int i = CharacterIndex(reinterpret_cast<const BYTE*>(*Hero));
    return i >= 0 ? g_lift[i] : 0.0f;
}

extern "C" __declspec(dllexport) bool StartFlight(const FlightHost* host)
{
    if (host == nullptr || host->version != FlightHostVersion)
    {
        return false;
    }

    g_log = host->log;
    g_speedFactor = ReadFloat(host->iniPath, "SpeedFactor", 1.5f, 1.0f, 3.0f);
    g_height = ReadFloat(host->iniPath, "Height", 350.0f, 0.0f, 1000.0f);
    g_climbPerSecond = ReadFloat(host->iniPath, "ClimbPerSecond", 250.0f, 10.0f, 10000.0f);
    FlightForwardSign = ReadFloat(host->iniPath, "ForwardSign", 1.0f, -1.0f, 1.0f) < 0.0f ? -1.0f : 1.0f;
    ReadKey(host->iniPath);
    QueryPerformanceFrequency(&g_frequency);

    // Otras DLL (healthbars, elites, resets...) pueden haber redirigido la llamada antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0
        || !Matches(SpeedFunction, SpeedPrologue, sizeof(SpeedPrologue))
        || !Matches(PathFunction, PathPrologue, sizeof(PathPrologue))
        || !Matches(BugMoveFunction, BugMovePrologue, sizeof(BugMovePrologue))
        || !Matches(PathStepFunction, PathStepPrologue, sizeof(PathStepPrologue))
        || !Matches(RiderWalkFunction, RiderWalkPrologue, sizeof(RiderWalkPrologue))
        || !Matches(StopActionFunction, StopActionPrologue, sizeof(StopActionPrologue))
        || !Matches(AnimationFunction, AnimationPrologue, sizeof(AnimationPrologue)))
    {
        g_log("Flight: el main.exe no coincide; desactivado.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!JumpAtStart(SpeedFunction, &SpeedHook)
        || !JumpAtStart(PathFunction, &PathHook)
        || !JumpAtStart(BugMoveFunction, &BugMoveHook, sizeof(BugMovePrologue))
        || !JumpAtStart(PathStepFunction, &PathStepHook)
        || !JumpAtStart(RiderWalkFunction, &RiderWalkHook)
        || !JumpAtStart(StopActionFunction, &StopActionHook, sizeof(StopActionPrologue))
        || !JumpAtStart(AnimationFunction, &AnimationHook, sizeof(AnimationPrologue))
        || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Flight: no se pudo instalar.");
        return false;
    }

    host->setRenderHooks(&BeforeRender, &AfterRender);
    host->setKeyTick(&KeyTick);
    g_log("Flight listo (velocidad x%.2f, altura %.0f, tecla %s0x%02X).", g_speedFactor, g_height, g_keyShift ? "Shift+" : "", g_key);
    return true;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}
