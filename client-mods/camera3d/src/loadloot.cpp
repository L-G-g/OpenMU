// Carga de la ventana de botin de las dungeons (loot.dll).
//
// Si loot.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Loot] Enabled=0), se carga
// despues de stacks.dll y se le pasa el log. loot.dll muestra las tiradas de need, greed y pass que manda
// el servidor y contesta con los comandos de chat /need, /greed y /pass.
//
// Sin loot.dll el cliente funciona igual que antes (las tiradas se eligen escribiendo los comandos).

#include "mods.h"

void LoadLoot(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadLoot), &module)
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
    if (GetPrivateProfileIntA("Loot", "Enabled", 1, path) == 0)
    {
        log("Botin: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "loot.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin loot.dll
    }

    const HMODULE loot = LoadLibraryA(path);
    const auto start = loot != nullptr
        ? reinterpret_cast<StartLootFunction>(GetProcAddress(loot, StartLootName))
        : nullptr;
    if (start == nullptr)
    {
        log("Botin: no se pudo cargar %s.", path);
        return;
    }

    static LootHost host{};
    host.version = LootHostVersion;
    host.log = log;
    start(&host);
}
