// Carga del mapa del comando M (warpmap.dll).
//
// Si warpmap.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([WarpMap] Enabled=0), se carga
// despues de elites.dll y se le pasa el log. El mapa cambia funciones de la ventana del comando M, que no
// toca ningun otro mod.
//
// Sin warpmap.dll el cliente funciona igual que antes (la ventana original con la lista).

#include "mods.h"

void LoadWarpMap(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadWarpMap), &module)
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
    if (GetPrivateProfileIntA("WarpMap", "Enabled", 1, path) == 0)
    {
        log("Mapa del comando M: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "warpmap.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin warpmap.dll
    }

    const HMODULE warpMap = LoadLibraryA(path);
    const auto start = warpMap != nullptr
        ? reinterpret_cast<StartWarpMapFunction>(GetProcAddress(warpMap, StartWarpMapName))
        : nullptr;
    if (start == nullptr)
    {
        log("Mapa del comando M: no se pudo cargar %s.", path);
        return;
    }

    static WarpMapHost host{};
    host.version = WarpMapHostVersion;
    host.log = log;
    start(&host);
}
