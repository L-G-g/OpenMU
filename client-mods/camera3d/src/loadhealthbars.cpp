// Carga de las barras de vida de los monstruos (healthbars.dll).
//
// Si healthbars.dll esta junto a camera3d.dll y camera3d.ini no las desactiva ([HealthBars] Enabled=0),
// se carga despues de verificar el main.exe y se le pasa el log. Las barras parchean sus propios lugares
// del juego (la llamada que reparte los paquetes recibidos y el comienzo de la interfaz de la escena
// principal), que no toca camera3d.dll.
//
// Sin healthbars.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadHealthBars(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadHealthBars), &module)
        || GetModuleFileNameA(module, path, MAX_PATH) == 0)
    {
        return;
    }

    char* slash = strrchr(path, '\\');
    if (slash == nullptr)
    {
        return;
    }

    const size_t room = MAX_PATH - (slash + 1 - path);
    strcpy_s(slash + 1, room, "camera3d.ini");
    if (GetPrivateProfileIntA("HealthBars", "Enabled", 1, path) == 0)
    {
        log("Barras de vida: desactivadas en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "healthbars.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin barras de vida
    }

    const HMODULE bars = LoadLibraryA(path);
    const auto start = bars != nullptr
        ? reinterpret_cast<StartHealthBarsFunction>(GetProcAddress(bars, StartHealthBarsName))
        : nullptr;
    if (start == nullptr)
    {
        log("Barras de vida: no se pudo cargar %s.", path);
        return;
    }

    static HealthBarsHost host{};
    host.version = HealthBarsHostVersion;
    host.log = log;
    start(&host);
}
