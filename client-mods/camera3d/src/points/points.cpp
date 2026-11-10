// points.dll - Puntos libres de mas de 65535 en la ventana del personaje (tecla C) del cliente MU 1.04d con
// camera3d.dll.
//
// El cliente original guarda los puntos libres en un WORD (CHARACTER_ATTRIBUTE +0x74): no sabe de mas de
// 65535, y al repartir los descuenta el mismo (C1 F3 06, uno por punto). Con mas puntos llegaba a 0 aunque
// al personaje le quedaban. El servidor (OpenMU) manda C1 08 FF 20 [puntos, 4 bytes big endian] cuando el
// personaje tiene mas de 65535 (o tenia, antes de repartir): despues de la informacion del personaje
// (C3 F3 03), de subir de nivel (C3 F3 05) y de repartir puntos. El main.exe original ignora el codigo 0xFF:
// su reparto de paquetes (0x006639D0) manda los codigos mayores a 0xF9 al caso por defecto. 0xFF lo comparten
// varias DLL por subcodigo (resetbutton.dll 0x00, quests.dll 0x10 a 0x12); 0xFE es de flight.dll.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79) ya puede estar redirigida por otras DLL: se lee a donde
//     apunta y se llama a eso. El paquete 0xFF 0x20 se queda aca y no llega al juego: el WORD del cliente pasa a
//     ser min(puntos, 65535), asi se pueden seguir repartiendo, y se recuerdan los puntos reales. Con
//     C3 F3 03 (otro personaje, o el mismo despues de un reset) se olvidan hasta el 0xFF 0x20 siguiente.
//   - El texto "Point: %d" de la ventana (texto 217) se escribe con sprintf (0x0077FC06, CALL 0x0050D100 con
//     el lugar del texto de 128 bytes, el formato y el WORD). Esa llamada pasa por PointTextHook: si se
//     conocen los puntos reales, escribe esos, mas lo que cambio el WORD desde el ultimo paquete (los
//     puntos que el cliente ya desconto antes de que llegue el siguiente).

#include "../shared.h"

#include <stdio.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;  // CALL al reparto al recibir cada paquete
    constexpr DWORD PointTextCallSite = 0x0077FC06; // CALL sprintf(texto, "Point: %d", puntos)
    constexpr DWORD SprintfFunction = 0x0050D100;
    constexpr size_t PointTextSize = 128;           // [EBP-0x238] hasta [EBP-0x1B8]
    BYTE** const CharacterAttribute = reinterpret_cast<BYTE**>(0x08128AC8);
    constexpr DWORD LevelUpPointsOffset = 0x74;      // WORD

    // --- Paquetes ---
    constexpr int CodeCharacterInfo = 0xF3, SubCodeCharacterInfo = 0x03, CodeInterface = 0xFF;
    constexpr BYTE SubCodePoints = 0x20;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    bool g_known = false;   // false: el servidor no mando puntos de mas (o todavia no llego el paquete)
    DWORD g_points = 0;     // puntos reales del ultimo paquete de puntos
    WORD g_clientPoints = 0; // lo que se puso en el WORD del cliente con ese paquete

    WORD* ClientPoints()
    {
        BYTE* attribute = *CharacterAttribute;
        return attribute != nullptr ? reinterpret_cast<WORD*>(attribute + LevelUpPointsOffset) : nullptr;
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr)
        {
            const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
            if (code == CodeInterface && packet[0] == 0xC1 && size >= 8 && packet[3] == SubCodePoints)
            {
                WORD* clientPoints = ClientPoints();
                if (clientPoints != nullptr)
                {
                    g_points = (static_cast<DWORD>(packet[4]) << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7];
                    g_clientPoints = g_points > 0xFFFF ? 0xFFFF : static_cast<WORD>(g_points);
                    *clientPoints = g_clientPoints;
                    g_known = true;
                }

                return 1; // el juego no lo conoce
            }

            if (code == CodeCharacterInfo && size > header && packet[header] == SubCodeCharacterInfo)
            {
                g_known = false;
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    int __cdecl PointTextHook(char* text, const char* format, int points)
    {
        long long shown = points;
        if (g_known)
        {
            const long long real = static_cast<long long>(g_points) + points - g_clientPoints;
            if (real > shown)
            {
                shown = real > 0x7FFFFFFF ? 0x7FFFFFFF : real;
            }
        }

        const int length = _snprintf_s(text, PointTextSize, _TRUNCATE, format, static_cast<int>(shown));
        return length >= 0 ? length : static_cast<int>(strlen(text));
    }

    // Destino de un CALL rel32, o 0 si no es un CALL.
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
}

extern "C" __declspec(dllexport) bool StartPoints(const PointsHost* host)
{
    if (host == nullptr || host->version != PointsHostVersion)
    {
        return false;
    }

    g_log = host->log;

    // Otras DLL pueden haber redirigido la llamada del reparto antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0 || CallTarget(PointTextCallSite) != SprintfFunction)
    {
        g_log("Puntos: el main.exe no coincide; desactivados.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!RedirectCall(DispatchCallSite, &PacketHook) || !RedirectCall(PointTextCallSite, &PointTextHook))
    {
        g_log("Puntos: no se pudieron instalar.");
        return false;
    }

    g_log("Puntos listos.");
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
