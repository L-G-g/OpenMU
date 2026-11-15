// Carga del boton de reset (resetbutton.dll).
//
// Si resetbutton.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([ResetButton] Enabled=0), se carga
// despues de tabmap.dll y se le pasa el log y la ruta de camera3d.ini. resetbutton.dll encadena el reparto de
// paquetes, el dibujo y el mouse que ya parchearon las otras DLL.
//
// Sin resetbutton.dll el cliente funciona igual que antes (el reset se hace escribiendo /reset).

#include "mods.h"

void LoadResetButton(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadResetButton), &module)
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
    if (GetPrivateProfileIntA("ResetButton", "Enabled", 1, iniPath) == 0)
    {
        log("Boton de reset: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "resetbutton.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin resetbutton.dll
    }

    const HMODULE button = LoadLibraryA(path);
    const auto start = button != nullptr
        ? reinterpret_cast<StartResetButtonFunction>(GetProcAddress(button, StartResetButtonName))
        : nullptr;
    if (start == nullptr)
    {
        log("Boton de reset: no se pudo cargar %s.", path);
        return;
    }

    static ResetButtonHost host{};
    host.version = ResetButtonHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
