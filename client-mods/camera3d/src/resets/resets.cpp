// resets.dll - Nivel y resets en la ventana del personaje (tecla C) del cliente MU 1.04d con camera3d.dll.
//
// El cliente original no sabe cuantos resets tiene el personaje, y a un personaje de tercera clase (despues
// de la ultima quest) le muestra "Master" en lugar del nivel y una linea de guiones en lugar de la
// experiencia. El servidor (OpenMU, con el reset activado) manda el paquete C1 07 FB [resets, 4 bytes big
// endian] justo despues de la informacion del personaje (C3 F3 03), al entrar al juego y despues de un
// reset. El main.exe original ignora el codigo 0xFB: su reparto de paquetes (0x006639D0) manda los
// codigos mayores a 0xF9 al caso por defecto, que devuelve 1 sin anotar nada ni desconectar.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79) ya puede estar redirigida por healthbars.dll y
//     elites.dll: se lee a donde apunta y se llama a eso. El paquete 0xFB se queda aca y no llega al juego.
//     Con C3 F3 03 (informacion del personaje, otro personaje o el mismo despues de un reset) se olvidan
//     los resets hasta que llegue el 0xFB siguiente; ese paquete sigue al juego como siempre.
//   - Los textos de la ventana se arman en 0x0077F990. Si la clase es de tercera clase (0x005878E0 con
//     CHARACTER_ATTRIBUTE +0x0B) salta a escribir "Master" (texto 1745) y los guiones; el salto
//     (0x0077F9B7, JNZ) pasa a ser siempre el otro camino: "Level: %d" (texto 200) y la experiencia
//     (texto 201) como con cualquier personaje.
//   - El nivel se escribe con sprintf (0x0077FA10, CALL 0x0050D100 con el lugar del texto de 32 bytes, el
//     formato y el nivel). Esa llamada pasa por LevelTextHook: escribe el nivel y, si los resets se
//     conocen, agrega el texto de [Resets] Format de camera3d.ini.

#include "../shared.h"

#include <stdio.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;  // CALL al reparto al recibir cada paquete
    constexpr DWORD MasterJump = 0x0077F9B7;        // JNZ 0x0077F9EE: no es tercera clase -> "Level: %d"
    const BYTE MasterJumpOriginal[] = { 0x75, 0x35 };
    const BYTE MasterJumpAlways[] = { 0xEB, 0x35 };
    constexpr DWORD LevelTextCallSite = 0x0077FA10; // CALL sprintf(texto, "Level: %d", nivel)
    constexpr DWORD SprintfFunction = 0x0050D100;
    constexpr size_t LevelTextSize = 32;            // [EBP-0x128] hasta [EBP-0x108]

    // --- Paquetes ---
    constexpr int CodeCharacterInfo = 0xF3, SubCodeCharacterInfo = 0x03, CodeResets = 0xFB;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    char g_format[64] = " | Resets: %d";
    long g_resets = -1; // -1: desconocidos (servidor sin resets o todavia no llego el paquete)

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr)
        {
            const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
            if (code == CodeResets)
            {
                if (packet[0] == 0xC1 && size >= 7)
                {
                    const DWORD resets = (static_cast<DWORD>(packet[3]) << 24) | (packet[4] << 16) | (packet[5] << 8) | packet[6];
                    g_resets = resets > 0x7FFFFFFF ? 0x7FFFFFFF : static_cast<long>(resets);
                }

                return 1; // el juego no lo conoce
            }

            if (code == CodeCharacterInfo && size > header && packet[header] == SubCodeCharacterInfo)
            {
                g_resets = -1;
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    int __cdecl LevelTextHook(char* text, const char* format, int level)
    {
        int length = _snprintf_s(text, LevelTextSize, _TRUNCATE, format, level);
        if (length >= 0 && g_resets >= 0)
        {
            const int added = _snprintf_s(text + length, LevelTextSize - length, _TRUNCATE, g_format, g_resets);
            length = added >= 0 ? length + added : static_cast<int>(strlen(text));
        }

        return length;
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

    // [Resets] Format: texto que se agrega despues del nivel; %d son los resets. Tiene que llevar un solo %d.
    void ReadFormat(const char* iniPath)
    {
        char text[sizeof(g_format)];
        GetPrivateProfileStringA("Resets", "Format", "", text, sizeof(text), iniPath);
        const char* percent = strchr(text, '%');
        if (percent != nullptr && percent[1] == 'd' && strchr(percent + 1, '%') == nullptr)
        {
            // GetPrivateProfileString quita las comillas de los extremos, asi se pueden dejar espacios al principio.
            strcpy_s(g_format, text);
        }
    }
}

extern "C" __declspec(dllexport) bool StartResets(const ResetsHost* host)
{
    if (host == nullptr || host->version != ResetsHostVersion)
    {
        return false;
    }

    g_log = host->log;
    ReadFormat(host->iniPath);

    // healthbars.dll y elites.dll pueden haber redirigido la llamada antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0
        || CallTarget(LevelTextCallSite) != SprintfFunction
        || memcmp(reinterpret_cast<const void*>(MasterJump), MasterJumpOriginal, sizeof(MasterJumpOriginal)) != 0)
    {
        g_log("Resets: el main.exe no coincide; desactivados.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!RedirectCall(DispatchCallSite, &PacketHook)
        || !RedirectCall(LevelTextCallSite, &LevelTextHook)
        || !WriteCode(MasterJump, MasterJumpAlways, sizeof(MasterJumpAlways)))
    {
        g_log("Resets: no se pudieron instalar.");
        return false;
    }

    g_log("Resets listos (formato \"%s\").", g_format);
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
