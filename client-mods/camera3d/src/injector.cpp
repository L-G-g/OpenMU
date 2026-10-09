// Camera3D.exe - Carga camera3d.dll en el cliente de MU (main.exe) cuando se abre.
// Uso: abrir Camera3D.exe, dejarlo abierto, y abrir el juego como siempre (con el launcher).
// camera3d.dll tiene que estar en la misma carpeta que este programa.

#include <windows.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <set>

namespace
{
    bool HasModule(DWORD processId, const char* moduleName)
    {
        const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        MODULEENTRY32 entry{ sizeof(entry) };
        bool found = false;
        for (BOOL ok = Module32First(snapshot, &entry); ok; ok = Module32Next(snapshot, &entry))
        {
            if (_stricmp(entry.szModule, moduleName) == 0)
            {
                found = true;
                break;
            }
        }

        CloseHandle(snapshot);
        return found;
    }

    bool Inject(DWORD processId, const char* dllPath)
    {
        const HANDLE process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, FALSE, processId);
        if (process == nullptr)
        {
            printf("  No se pudo abrir el proceso (error %lu). Proba abrir Camera3D como administrador.\n", GetLastError());
            return false;
        }

        const size_t size = strlen(dllPath) + 1;
        void* remote = VirtualAllocEx(process, nullptr, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        bool ok = remote != nullptr && WriteProcessMemory(process, remote, dllPath, size, nullptr);
        if (ok)
        {
            // Este programa es de 32 bits, igual que main.exe, asi que LoadLibraryA esta en la misma direccion.
            const auto loadLibrary = reinterpret_cast<LPTHREAD_START_ROUTINE>(GetProcAddress(GetModuleHandleA("kernel32.dll"), "LoadLibraryA"));
            const HANDLE thread = CreateRemoteThread(process, nullptr, 0, loadLibrary, remote, 0, nullptr);
            ok = thread != nullptr;
            if (ok)
            {
                WaitForSingleObject(thread, 10000);
                DWORD exitCode = 0;
                GetExitCodeThread(thread, &exitCode);
                ok = exitCode != 0;
                CloseHandle(thread);
            }
        }

        if (remote != nullptr)
        {
            VirtualFreeEx(process, remote, 0, MEM_RELEASE);
        }

        CloseHandle(process);
        return ok;
    }
}

namespace
{
    // Ruta completa del ejecutable de un proceso, o cadena vacia si no se puede leer.
    bool GetProcessPath(DWORD processId, char* path, DWORD size)
    {
        const HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (process == nullptr)
        {
            return false;
        }

        const BOOL ok = QueryFullProcessImageNameA(process, 0, path, &size);
        CloseHandle(process);
        return ok != FALSE;
    }
}

// Opcional: "--only <ruta de main.exe>" para cargarla solo en ese cliente.
int main(int argc, char** argv)
{
    const char* onlyPath = (argc >= 3 && _stricmp(argv[1], "--only") == 0) ? argv[2] : nullptr;

    char dllPath[MAX_PATH];
    GetModuleFileNameA(nullptr, dllPath, MAX_PATH);
    char* slash = strrchr(dllPath, '\\');
    strcpy_s(slash + 1, MAX_PATH - (slash + 1 - dllPath), "camera3d.dll");
    if (GetFileAttributesA(dllPath) == INVALID_FILE_ATTRIBUTES)
    {
        printf("No encuentro camera3d.dll al lado de este programa:\n  %s\n", dllPath);
        system("pause");
        return 1;
    }

    SetConsoleTitleA("Camara 3D para MU");
    printf("Camara 3D activa. Deja esta ventana abierta y abri el juego como siempre.\n");
    printf("Controles: rueda = zoom, boton del medio + arrastrar = girar/inclinar, click del medio = reset.\n\n");

    std::set<DWORD> handled;
    for (;;)
    {
        const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        PROCESSENTRY32 entry{ sizeof(entry) };
        for (BOOL ok = Process32First(snapshot, &entry); ok; ok = Process32Next(snapshot, &entry))
        {
            if (_stricmp(entry.szExeFile, "main.exe") != 0 || handled.count(entry.th32ProcessID) != 0)
            {
                continue;
            }

            if (onlyPath != nullptr)
            {
                char path[MAX_PATH];
                if (!GetProcessPath(entry.th32ProcessID, path, MAX_PATH))
                {
                    continue; // puede que todavia no se pueda leer; se reintenta
                }

                if (_stricmp(path, onlyPath) != 0)
                {
                    handled.insert(entry.th32ProcessID);
                    continue;
                }
            }

            handled.insert(entry.th32ProcessID);
            if (HasModule(entry.th32ProcessID, "camera3d.dll"))
            {
                continue;
            }

            printf("Juego detectado (proceso %lu)... ", entry.th32ProcessID);
            printf(Inject(entry.th32ProcessID, dllPath) ? "camara 3D cargada.\n" : "no se pudo cargar.\n");
        }

        CloseHandle(snapshot);
        Sleep(200);
    }
}
