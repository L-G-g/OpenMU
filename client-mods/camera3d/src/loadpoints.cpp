// Carga de los puntos libres de mas de 65535 en la ventana del personaje (points.dll).
//
// Si points.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Points] Enabled=0), se carga
// despues de tabmap.dll y se le pasa el log y la ruta de camera3d.ini. points.dll encadena la llamada del
// reparto de paquetes que parchean las otras DLL.
//
// Sin points.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadPoints(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadPoints), &module)
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
    if (GetPrivateProfileIntA("Points", "Enabled", 1, iniPath) == 0)
    {
        log("Puntos: desactivados en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "points.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin points.dll
    }

    const HMODULE points = LoadLibraryA(path);
    const auto start = points != nullptr
        ? reinterpret_cast<StartPointsFunction>(GetProcAddress(points, StartPointsName))
        : nullptr;
    if (start == nullptr)
    {
        log("Puntos: no se pudo cargar %s.", path);
        return;
    }

    static PointsHost host{};
    host.version = PointsHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
