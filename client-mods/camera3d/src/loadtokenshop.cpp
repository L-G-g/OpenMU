// Carga de las solapas de los mercaderes de tokens (tokenshop.dll).
//
// Si tokenshop.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([TokenShop] Enabled=0), se carga
// despues de tokenshop.dll y se le pasa el log y la ruta de camera3d.ini. tokenshop.dll encadena el reparto de
// paquetes, el dibujo y el mouse que ya parchearon las otras DLL.
//
// Sin tokenshop.dll el cliente funciona igual que antes (la solapa se cambia escribiendo /tab 1 a 7).

#include "mods.h"

void LoadTokenShop(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadTokenShop), &module)
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
    if (GetPrivateProfileIntA("TokenShop", "Enabled", 1, iniPath) == 0)
    {
        log("Solapas de tokens: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "tokenshop.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin tokenshop.dll
    }

    const HMODULE shop = LoadLibraryA(path);
    const auto start = shop != nullptr
        ? reinterpret_cast<StartTokenShopFunction>(GetProcAddress(shop, StartTokenShopName))
        : nullptr;
    if (start == nullptr)
    {
        log("Solapas de tokens: no se pudo cargar %s.", path);
        return;
    }

    static TokenShopHost host{};
    host.version = TokenShopHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    start(&host);
}
