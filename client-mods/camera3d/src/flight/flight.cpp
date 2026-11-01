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
//     (0x00503770). Se suben y se devuelven igual que el personaje.
//   - Alas: el paso del bug (0x00501790) elige las acciones de vuelo del Dinorant (bug 0x143: 1, 3, 5, 7
//     en vez de 0, 2, 4, 6) cuando el mundo ([0x00E61E18]) es 8 (Tarkan) o 10 (Icarus). Si el dueno vuela,
//     durante esa llamada el mundo es 10 y despues vuelve a ser el de antes.
//   - Linea recta: volando, el camino se arma en linea recta (pasos de 8 direcciones, hasta 15 puntos con
//     el de inicio, como lo escribe 0x00542D20) en vez del A* del juego, que zigzaguea. Si la linea pasa
//     por una baldosa sin suelo (0x08) se usa el A* con pared 8.
//   - Tecla: [Flight] Key (Shift + V por defecto) manda /volar por el chat, solo con el foco en la ventana
//     del juego (no escribiendo en el chat).

#include "../shared.h"

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

    constexpr DWORD CharKey = 0x7E, CharPath = 0x274, CharObject = 0x308, CharLive = 0x30C, CharPosition = 0x404;

    // --- Montura y mascotas (bugs, OBJECT de 0x288 bytes) ---
    BYTE* const Bugs = reinterpret_cast<BYTE*>(0x08791550);
    constexpr int BugCount = 10;
    constexpr DWORD BugSize = 0x288;
    constexpr DWORD BugLive = 0x04, BugPosition = 0xFC, BugOwner = 0x1D8;

    // --- Paquetes ---
    constexpr int CodeOutOfScope = 0x14, CodeFlight = 0xFE;
    constexpr WORD IdMask = 0x7FFF; // 0x8000 marca a los recien aparecidos
    constexpr int FlyingWall = 8;   // umbral de pared volando: se cruza 0x04, no 0x08

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using SpeedType = float(__cdecl*)(BYTE*);
    using PathType = bool(__cdecl*)(int, int, int, int, BYTE*, float, int);
    using BugMoveType = char(__cdecl*)(BYTE*, char);
    using SendChatType = void(__cdecl*)(const char*);

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    bool g_flying[IdMask + 1] = {};

    float g_speedFactor = 1.5f;
    float g_height = 350.0f;      // unidades del mundo (una baldosa son 100)
    float g_climbPerSecond = 250.0f;
    int g_key = 'V';
    bool g_keyShift = true;
    BYTE g_suppressedKey = 0;
    bool g_sendCommand = false;

    // Altura actual de cada lugar de personaje (sube y baja de a poco) y para que numero de objeto es.
    float g_lift[CharacterCount] = {};
    WORD g_liftKey[CharacterCount] = {};
    float g_savedZ[CharacterCount] = {};
    bool g_lifted[CharacterCount] = {};
    float g_bugSavedZ[BugCount] = {};
    bool g_bugLifted[BugCount] = {};
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
        return character != nullptr && g_flying[*reinterpret_cast<const WORD*>(character + CharKey) & IdMask];
    }

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
                    g_flying[id] = packet[5] != 0;
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
                    g_flying[ReadId(packet + header + 1 + i * 2)] = false;
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
        return IsFlying(character) ? speed * g_speedFactor : speed;
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
            }

            const float target = g_flying[key] ? g_height : 0.0f;
            if (g_lift[i] < target)
            {
                g_lift[i] = g_lift[i] + step > target ? target : g_lift[i] + step;
            }
            else if (g_lift[i] > target)
            {
                g_lift[i] = g_lift[i] - step < target ? target : g_lift[i] - step;
            }

            if (g_lift[i] > 0.0f)
            {
                float* position = reinterpret_cast<float*>(character + CharPosition);
                g_savedZ[i] = position[2];
                position[2] += g_lift[i];
                g_lifted[i] = true;
            }
        }

        for (int b = 0; b < BugCount; b++)
        {
            BYTE* bug = Bugs + b * BugSize;
            g_bugLifted[b] = false;
            if (!bug[BugLive])
            {
                continue;
            }

            const BYTE* owner = *reinterpret_cast<BYTE* const*>(bug + BugOwner);
            const int i = owner != nullptr ? CharacterIndex(owner - CharObject) : -1;
            if (i >= 0 && g_lifted[i])
            {
                float* position = reinterpret_cast<float*>(bug + BugPosition);
                g_bugSavedZ[b] = position[2];
                position[2] += g_lift[i];
                g_bugLifted[b] = true;
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

        for (int b = 0; b < BugCount; b++)
        {
            if (g_bugLifted[b])
            {
                reinterpret_cast<float*>(Bugs + b * BugSize + BugPosition)[2] = g_bugSavedZ[b];
                g_bugLifted[b] = false;
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
    ReadKey(host->iniPath);
    QueryPerformanceFrequency(&g_frequency);

    // Otras DLL (healthbars, elites, resets...) pueden haber redirigido la llamada antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0
        || !Matches(SpeedFunction, SpeedPrologue, sizeof(SpeedPrologue))
        || !Matches(PathFunction, PathPrologue, sizeof(PathPrologue))
        || !Matches(BugMoveFunction, BugMovePrologue, sizeof(BugMovePrologue)))
    {
        g_log("Flight: el main.exe no coincide; desactivado.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!JumpAtStart(SpeedFunction, &SpeedHook)
        || !JumpAtStart(PathFunction, &PathHook)
        || !JumpAtStart(BugMoveFunction, &BugMoveHook, sizeof(BugMovePrologue))
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
