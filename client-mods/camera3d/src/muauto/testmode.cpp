// Modo de prueba: garantiza que el cliente solo se conecte al servidor de prueba.
//
// Si el cliente no encuentra la conexion en el registro (por ejemplo, abierto sin el launcher), usa
// el servidor oficial que trae adentro. Para pruebas automaticas eso no puede pasar: con este modo
// activo, toda conexion a una direccion que no sea local (127.x.x.x) se redirige al servidor de
// prueba configurado.
//
// Solo se activa si camera3d.ini, junto a la DLL, lo pide (el cliente normal no lo tiene):
//   [Test]
//   Server=127.127.127.127:45405

#include <winsock2.h>

#include "muauto.h"
#include <stdio.h>
#include <stdlib.h>

#pragma comment(lib, "ws2_32.lib")
#pragma warning(disable : 4996) // inet_ntoa e inet_addr alcanzan para IPv4

namespace
{
    constexpr DWORD ConnectImport = 0x00D2279C; // main.exe: WS2_32.dll ordinal 4 (connect)

    using ConnectFunction = int(WSAAPI*)(SOCKET, const sockaddr*, int);

    LogFunction g_log = nullptr;
    ConnectFunction g_originalConnect = nullptr;
    sockaddr_in g_testServer{};

    bool IsLoopback(const sockaddr_in& address)
    {
        return (ntohl(address.sin_addr.s_addr) >> 24) == 127;
    }

    int WSAAPI ConnectHook(SOCKET socket, const sockaddr* name, int length)
    {
        if (name != nullptr && name->sa_family == AF_INET && length >= static_cast<int>(sizeof(sockaddr_in)))
        {
            const sockaddr_in& target = *reinterpret_cast<const sockaddr_in*>(name);
            if (!IsLoopback(target))
            {
                g_log("Modo de prueba: conexion a %s:%d redirigida al servidor de prueba.",
                    inet_ntoa(target.sin_addr), ntohs(target.sin_port));
                return g_originalConnect(socket, reinterpret_cast<const sockaddr*>(&g_testServer), sizeof(g_testServer));
            }

            g_log("Modo de prueba: conexion a %s:%d.", inet_ntoa(target.sin_addr), ntohs(target.sin_port));
        }

        return g_originalConnect(socket, name, length);
    }

    bool ReadTestServer(sockaddr_in& server)
    {
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&ReadTestServer), &module);

        char path[MAX_PATH];
        GetModuleFileNameA(module, path, MAX_PATH);
        char* slash = strrchr(path, '\\');
        if (slash == nullptr)
        {
            return false;
        }

        strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.ini");
        char value[64] = {};
        GetPrivateProfileStringA("Test", "Server", "", value, sizeof(value), path);
        char* colon = strchr(value, ':');
        if (colon == nullptr)
        {
            return false;
        }

        *colon = '\0';
        server.sin_family = AF_INET;
        server.sin_addr.s_addr = inet_addr(value);
        server.sin_port = htons(static_cast<u_short>(atoi(colon + 1)));
        return server.sin_addr.s_addr != INADDR_NONE && server.sin_port != 0 && IsLoopback(server);
    }
}

bool InstallTestMode(LogFunction log)
{
    g_log = log;
    if (!ReadTestServer(g_testServer))
    {
        return false;
    }

    const HMODULE ws2 = GetModuleHandleA("ws2_32.dll");
    const FARPROC connect = ws2 != nullptr ? GetProcAddress(ws2, MAKEINTRESOURCEA(4)) : nullptr;
    DWORD* const slot = reinterpret_cast<DWORD*>(ConnectImport);
    if (connect == nullptr || *slot != reinterpret_cast<DWORD>(connect))
    {
        // Sin poder garantizar la redireccion, mejor que el cliente no se conecte a ningun lado.
        g_log("Modo de prueba: no se encontro connect en main.exe; se cierra el juego por seguridad.");
        ExitProcess(1);
    }

    g_originalConnect = reinterpret_cast<ConnectFunction>(connect);
    const DWORD hook = reinterpret_cast<DWORD>(&ConnectHook);
    if (!WriteCode(ConnectImport, &hook, sizeof(hook)))
    {
        g_log("Modo de prueba: no se pudo enganchar connect; se cierra el juego por seguridad.");
        ExitProcess(1);
    }

    g_log("Modo de prueba: toda conexion externa va a %s:%d.", inet_ntoa(g_testServer.sin_addr), ntohs(g_testServer.sin_port));
    return true;
}
