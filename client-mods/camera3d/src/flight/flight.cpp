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
    constexpr DWORD CharKey = 0x7E, CharPath = 0x274, CharLive = 0x30C, CharPosition = 0x404;

    // --- Paquetes ---
    constexpr int CodeOutOfScope = 0x14, CodeFlight = 0xFE;
    constexpr WORD IdMask = 0x7FFF; // 0x8000 marca a los recien aparecidos
    constexpr int FlyingWall = 8;   // umbral de pared volando: se cruza 0x04, no 0x08

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using SpeedType = float(__cdecl*)(BYTE*);
    using PathType = bool(__cdecl*)(int, int, int, int, BYTE*, float, int);
    using SendChatType = void(__cdecl*)(const char*);

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    bool g_flying[IdMask + 1] = {};

    float g_speedFactor = 1.5f;
    float g_height = 200.0f;      // unidades del mundo (una baldosa son 100)
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

    bool __cdecl PathHook(int sx, int sy, int tx, int ty, BYTE* path, float distance, int wall)
    {
        if (path != nullptr && wall < FlyingWall)
        {
            BYTE* character = path - CharPath;
            if (CharacterIndex(character) >= 0 && IsFlying(character))
            {
                wall = FlyingWall;
            }
        }

        return g_pathOriginal(sx, sy, tx, ty, path, distance, wall);
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

    // Reemplaza el prologo de 6 bytes de la funcion por un salto al gancho (y un NOP).
    bool JumpAtStart(DWORD function, const void* target)
    {
        BYTE jump[6] = { 0xE9, 0, 0, 0, 0, 0x90 };
        *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(target) - (function + 5);
        return WriteCode(function, jump, sizeof(jump));
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
    g_height = ReadFloat(host->iniPath, "Height", 200.0f, 0.0f, 1000.0f);
    g_climbPerSecond = ReadFloat(host->iniPath, "ClimbPerSecond", 250.0f, 10.0f, 10000.0f);
    ReadKey(host->iniPath);
    QueryPerformanceFrequency(&g_frequency);

    // Otras DLL (healthbars, elites, resets...) pueden haber redirigido la llamada antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0
        || !Matches(SpeedFunction, SpeedPrologue, sizeof(SpeedPrologue))
        || !Matches(PathFunction, PathPrologue, sizeof(PathPrologue)))
    {
        g_log("Flight: el main.exe no coincide; desactivado.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!JumpAtStart(SpeedFunction, &SpeedHook)
        || !JumpAtStart(PathFunction, &PathHook)
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
