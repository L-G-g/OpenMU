// muauto.dll - Herramientas de prueba para el cliente MU 1.04d con camera3d.dll.
//
// No es parte del cliente de los jugadores: camera3d.dll la carga solo si muauto.dll esta en la misma
// carpeta (ver testtools.cpp de camera3d). Contiene:
//   - el modo de prueba (testmode.cpp): toda conexion externa va al servidor de prueba;
//   - la automatizacion (automation.cpp, gamecommands.cpp): manejar el cliente con comandos desde un
//     archivo, desde mover el mouse hasta caminar, atacar o usar habilidades;
//   - el diagnostico del dibujo (fpsdiag.cpp).
// Todo se activa con camera3d.ini; sin esas opciones la DLL no hace nada.

#include "muauto.h"

namespace
{
    TestHost g_host{};
    char g_folder[MAX_PATH] = {};
}

const TestHost& Host()
{
    return g_host;
}

const char* ClientFolder()
{
    return g_folder;
}

extern "C" __declspec(dllexport) bool StartTestTools(const TestHost* host, TestTools* tools)
{
    if (host == nullptr || tools == nullptr || host->version != TestHostVersion)
    {
        return false;
    }

    g_host = *host;
    const LogFunction log = g_host.log;
    if (InstallTestMode(log)) // antes de cualquier conexion
    {
        if (InstallAutomation(log))
        {
            InstallGameCommands(log);
            tools->tick = &AutomationTick;
        }
    }

    InstallRenderDiagnostics(log);
    log("Herramientas de prueba (muauto.dll) cargadas.");
    return true;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        GetModuleFileNameA(module, g_folder, MAX_PATH);
        char* slash = strrchr(g_folder, '\\');
        if (slash != nullptr)
        {
            slash[1] = '\0';
        }
    }

    return TRUE;
}
