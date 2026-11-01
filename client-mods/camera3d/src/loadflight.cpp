// Carga de las monturas voladoras (flight.dll).
//
// Si flight.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Flight] Enabled=0), se carga
// despues de tabmap.dll y se le pasa el log, la ruta de camera3d.ini, el antes y despues del dibujo de la
// escena principal y el paso de logica. flight.dll sube a los personajes que vuelan, los deja buscar camino por
// encima de las paredes y los hace mas rapidos (el servidor decide quien vuela).
//
// Sin flight.dll el cliente funciona igual que antes (los que vuelan se ven caminando a ras del suelo).

#include "mods.h"

void LoadFlight(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadFlight), &module)
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
    if (GetPrivateProfileIntA("Flight", "Enabled", 1, iniPath) == 0)
    {
        log("Flight: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "flight.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin flight.dll
    }

    const HMODULE flight = LoadLibraryA(path);
    const auto start = flight != nullptr
        ? reinterpret_cast<StartFlightFunction>(GetProcAddress(flight, StartFlightName))
        : nullptr;
    if (start == nullptr)
    {
        log("Flight: no se pudo cargar %s.", path);
        return;
    }

    static FlightHost host{};
    host.version = FlightHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    host.setRenderHooks = &SetFlightRenderHooks;
    host.setKeyTick = &SetFlightKeyTick;
    start(&host);
}

namespace
{
    BeforeRenderCallback g_flightBeforeRender = nullptr;
    BeforeRenderCallback g_flightAfterRender = nullptr;
}

void SetFlightRenderHooks(BeforeRenderCallback before, BeforeRenderCallback after)
{
    g_flightBeforeRender = before;
    g_flightAfterRender = after;
}

void FlightBeforeRender()
{
    if (g_flightBeforeRender != nullptr)
    {
        g_flightBeforeRender();
    }
}

void FlightAfterRender()
{
    if (g_flightAfterRender != nullptr)
    {
        g_flightAfterRender();
    }
}
