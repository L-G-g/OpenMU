// Carga del board de los proximos eventos (eventboard.dll).
//
// Si eventboard.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([EventBoard] Enabled=0), se carga
// despues de points.dll y se le pasa el log, la ruta de camera3d.ini y el paso de logica (para su tecla, H).
// eventboard.dll muestra los proximos 10 eventos que manda el servidor (/eventos).
//
// Sin eventboard.dll el cliente funciona igual ("/eventos chat" escribe los eventos en el chat).

#include "mods.h"

void LoadEventBoard(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadEventBoard), &module)
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
    if (GetPrivateProfileIntA("EventBoard", "Enabled", 1, iniPath) == 0)
    {
        log("Board de eventos: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "eventboard.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin eventboard.dll
    }

    const HMODULE board = LoadLibraryA(path);
    const auto start = board != nullptr
        ? reinterpret_cast<StartEventBoardFunction>(GetProcAddress(board, StartEventBoardName))
        : nullptr;
    if (start == nullptr)
    {
        log("Board de eventos: no se pudo cargar %s.", path);
        return;
    }

    static EventBoardHost host{};
    host.version = EventBoardHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    host.setKeyTick = &SetEventBoardKeyTick;
    start(&host);
}
