// Carga del nivel y los resets en la ventana del personaje (resets.dll).
//
// Si resets.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Resets] Enabled=0), se carga
// despues de elites.dll y se le pasa el log y la ruta de camera3d.ini. resets.dll encadena la llamada del
// reparto de paquetes que parchean healthbars.dll y elites.dll.
//
// Sin resets.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadResets(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadResets), &module)
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
    if (GetPrivateProfileIntA("Resets", "Enabled", 1, iniPath) == 0)
    {
        log("Resets: desactivados en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "resets.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin resets.dll
    }

    const HMODULE resets = LoadLibraryA(path);
    const auto start = resets != nullptr
        ? reinterpret_cast<StartResetsFunction>(GetProcAddress(resets, StartResetsName))
        : nullptr;
    if (start == nullptr)
    {
        log("Resets: no se pudo cargar %s.", path);
        return;
    }

    static ResetsHost host{};
    host.version = ResetsHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
