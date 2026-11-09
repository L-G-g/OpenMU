// Carga de las quests de skills (quests.dll).
//
// Si quests.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Quests] Enabled=0), se carga
// despues de tabmap.dll y se le pasa el log y la ruta de camera3d.ini. quests.dll dibuja los signos de quest
// sobre los NPC, la ventana de la quest y el seguimiento de las quests activas.
//
// Sin quests.dll el cliente funciona igual (sin signos ni ventana de quests de skills).

#include "mods.h"

void LoadQuests(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadQuests), &module)
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
    if (GetPrivateProfileIntA("Quests", "Enabled", 1, iniPath) == 0)
    {
        log("Quests: desactivadas en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "quests.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin quests.dll
    }

    const HMODULE quests = LoadLibraryA(path);
    const auto start = quests != nullptr
        ? reinterpret_cast<StartQuestsFunction>(GetProcAddress(quests, StartQuestsName))
        : nullptr;
    if (start == nullptr)
    {
        log("Quests: no se pudo cargar %s.", path);
        return;
    }

    static QuestsHost host{};
    host.version = QuestsHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
