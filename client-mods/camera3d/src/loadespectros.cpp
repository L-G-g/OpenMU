// Carga de los espectros (espectros.dll).
//
// Si espectros.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Espectros] Enabled=0), se carga
// despues de loot.dll y se le pasa el log, la ruta de camera3d.ini, el aviso al empezar cada cuadro y el filtro
// del click derecho del inventario. espectros.dll muestra la ventana de espectros (tecla S) y dibuja a los
// espectros transparentes.
//
// Sin espectros.dll el cliente funciona igual que antes (los espectros se manejan con el comando /espectro).

#include "mods.h"

void LoadEspectros(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadEspectros), &module)
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
    if (GetPrivateProfileIntA("Espectros", "Enabled", 1, iniPath) == 0)
    {
        log("Espectros: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "espectros.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin espectros.dll
    }

    const HMODULE loot = LoadLibraryA(path);
    const auto start = loot != nullptr
        ? reinterpret_cast<StartEspectrosFunction>(GetProcAddress(loot, StartEspectrosName))
        : nullptr;
    if (start == nullptr)
    {
        log("Espectros: no se pudo cargar %s.", path);
        return;
    }

    static EspectrosHost host{};
    host.version = EspectrosHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    host.setBeforeRender = &SetEspectrosBeforeRender;
    host.setRightClickFilter = &SetRightClickFilter;
    start(&host);
}

namespace
{
    BeforeRenderCallback g_espectrosBeforeRender = nullptr;
}

void SetEspectrosBeforeRender(BeforeRenderCallback callback)
{
    g_espectrosBeforeRender = callback;
}

void EspectrosBeforeRender()
{
    if (g_espectrosBeforeRender != nullptr)
    {
        g_espectrosBeforeRender();
    }
}
