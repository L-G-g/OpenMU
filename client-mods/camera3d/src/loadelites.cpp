// Carga de los monstruos elite (elites.dll).
//
// Si elites.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Elites] Enabled=0), se carga
// despues de healthbars.dll y se le pasa el log y la ruta de camera3d.ini. elites.dll encadena las mismas
// llamadas del juego que parchea healthbars.dll (el reparto de paquetes y el comienzo de la interfaz).
//
// Sin elites.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadElites(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadElites), &module)
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
    if (GetPrivateProfileIntA("Elites", "Enabled", 1, iniPath) == 0)
    {
        log("Elites: desactivados en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "elites.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin elites.dll
    }

    const HMODULE elites = LoadLibraryA(path);
    const auto start = elites != nullptr
        ? reinterpret_cast<StartElitesFunction>(GetProcAddress(elites, StartElitesName))
        : nullptr;
    if (start == nullptr)
    {
        log("Elites: no se pudo cargar %s.", path);
        return;
    }

    static ElitesHost host{};
    host.version = ElitesHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
