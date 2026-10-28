// Carga del mapa del TAB completo (tabmap.dll).
//
// Si tabmap.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([TabMap] Enabled=0), se carga
// despues de espectros.dll y se le pasa el log, la ruta de camera3d.ini y el paso de logica. tabmap.dll
// dibuja el mapa entero de la zona en el centro de la pantalla y hace caminar al personaje hasta el punto
// elegido con el mouse.
//
// Sin tabmap.dll el TAB abre el mapa original (centrado en el personaje).

#include "mods.h"

void LoadTabMap(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadTabMap), &module)
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
    strcpy_s(iniPath, path);
    if (GetPrivateProfileIntA("TabMap", "Enabled", 1, iniPath) == 0)
    {
        log("Mapa del TAB: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "tabmap.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin tabmap.dll
    }

    const HMODULE tabMap = LoadLibraryA(path);
    const auto start = tabMap != nullptr
        ? reinterpret_cast<StartTabMapFunction>(GetProcAddress(tabMap, StartTabMapName))
        : nullptr;
    if (start == nullptr)
    {
        log("Mapa del TAB: no se pudo cargar %s.", path);
        return;
    }

    static TabMapHost host{};
    host.version = TabMapHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    host.setKeyTick = &SetTabMapKeyTick;
    start(&host);
}
