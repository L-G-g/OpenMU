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
    volatile LONG g_crashesLogged = 0;

    // Anota en camera3d.log donde fallo el juego (accesos invalidos), para saber si fue una DLL nuestra.
    LONG CALLBACK CrashLogger(EXCEPTION_POINTERS* exception)
    {
        const EXCEPTION_RECORD* record = exception->ExceptionRecord;
        if (record->ExceptionCode != EXCEPTION_ACCESS_VIOLATION || InterlockedIncrement(&g_crashesLogged) > 5)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        const DWORD address = reinterpret_cast<DWORD>(record->ExceptionAddress);
        HMODULE module = nullptr;
        char name[MAX_PATH] = "?";
        if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCSTR>(address), &module))
        {
            GetModuleFileNameA(module, name, MAX_PATH);
        }

        g_host.log("Error: acceso invalido en 0x%08X (%s + 0x%X), %s de 0x%08X.", address, name,
            address - reinterpret_cast<DWORD>(module), record->ExceptionInformation[0] ? "escritura" : "lectura",
            static_cast<DWORD>(record->ExceptionInformation[1]));
        return EXCEPTION_CONTINUE_SEARCH;
    }
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

    AddVectoredExceptionHandler(0, &CrashLogger);
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
