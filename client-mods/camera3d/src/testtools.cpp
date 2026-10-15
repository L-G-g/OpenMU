// Carga de las herramientas de prueba (muauto.dll), que no forman parte de la DLL de los jugadores.
//
// Si muauto.dll esta junto a camera3d.dll, se carga al iniciar camera3d (antes de cualquier conexion,
// porque el modo de prueba tiene que redirigirlas) y se le pasa lo que necesita de camera3d: el log, los
// ajustes de prueba del movimiento fluido y el lugar para el diagnostico del dibujo. Las herramientas no
// parchean nada que camera3d ya parchee: el paso de logica les llega por TestToolsTick, que llama el
// click derecho desde su envoltorio de la actualizacion de teclas.
//
// Sin muauto.dll (el cliente de los jugadores) esto no hace nada.

#include "mods.h"

namespace
{
    TestTools g_tools{};

    const TestHost* Host(LogFunction log)
    {
        static TestHost host{};
        host.version = TestHostVersion;
        host.log = log;
        host.setSmoothMotion = &SetSmoothMotion;
        host.setSmoothTrails = &InterpolationSetSmoothTrails;
        host.setCreatorBlocked = &SetCreatorBlocked;
        host.setRenderDiagnostics = &SetRenderDiagnostics;
        return &host;
    }
}

void LoadTestTools(HMODULE module, LogFunction log)
{
    char path[MAX_PATH];
    GetModuleFileNameA(module, path, MAX_PATH);
    char* slash = strrchr(path, '\\');
    if (slash == nullptr)
    {
        return;
    }

    strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "muauto.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente normal
    }

    const HMODULE tools = LoadLibraryA(path);
    const auto start = tools != nullptr
        ? reinterpret_cast<StartTestToolsFunction>(GetProcAddress(tools, StartTestToolsName))
        : nullptr;
    if (start == nullptr)
    {
        log("Herramientas de prueba: no se pudo cargar %s.", path);
        return;
    }

    TestTools result{};
    if (start(Host(log), &result))
    {
        g_tools = result;
    }
}

void TestToolsTick(BYTE* keyStates)
{
    if (g_tools.tick != nullptr)
    {
        g_tools.tick(keyStates);
    }
}
