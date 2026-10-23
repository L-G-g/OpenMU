// Carga de los stacks de pociones (stacks.dll).
//
// Si stacks.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Stacks] Enabled=0), se carga
// despues de elites.dll y se le pasa el log. stacks.dll deja juntar arrastrando las pociones hasta 255.
//
// Sin stacks.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadStacks(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadStacks), &module)
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
    if (GetPrivateProfileIntA("Stacks", "Enabled", 1, path) == 0)
    {
        log("Stacks: desactivados en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "stacks.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin stacks.dll
    }

    const HMODULE stacks = LoadLibraryA(path);
    const auto start = stacks != nullptr
        ? reinterpret_cast<StartStacksFunction>(GetProcAddress(stacks, StartStacksName))
        : nullptr;
    if (start == nullptr)
    {
        log("Stacks: no se pudo cargar %s.", path);
        return;
    }

    static StacksHost host{};
    host.version = StacksHostVersion;
    host.log = log;
    start(&host);
}
