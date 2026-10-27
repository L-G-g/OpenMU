// Automatizacion (solo en modo de prueba): manejar el cliente desde afuera sin mouse ni teclado reales.
//
// Funciona aunque la PC este bloqueada o el juego no tenga el foco: los comandos se aplican desde
// adentro del juego, en cada paso de logica, sobre las mismas variables que usa el juego (posicion del
// mouse, estado de botones y teclas), y las capturas se leen de OpenGL antes de mostrar cada cuadro.
//
// Protocolo (carpeta "automation" junto a la DLL):
//   - quien controla escribe commands.txt (un comando por linea) y lo deja listo renombrandolo;
//   - la DLL lo ejecuta en orden, lo borra y escribe done.txt con el resultado de cada comando.
//
// Comandos (coordenadas en pixeles de la ventana, como en una captura):
//   move X Y               mover el mouse
//   click X Y [right]      click (izquierdo por defecto)
//   down X Y [right]       apretar y dejar apretado; up [right] para soltar
//   key TECLA              apretar y soltar: nombre (enter, tab, esc, space, f1..f12, up, ...) o letra;
//                          shift+TECLA con Shift sostenido mientras tanto
//   text TEXTO             escribir texto en la caja de texto activa
//   wait N                 esperar N pasos de logica (40 ms cada uno)
//   shot RUTA.bmp [N]      captura del proximo cuadro (o N cuadros despues)
//   burst RUTA N           captura N cuadros seguidos (RUTA_1.bmp ... RUTA_N.bmp)
//   smooth 0|1             pruebas: movimiento interpolado
//   block N 0|1            pruebas: bloqueo en cuadros extra de la funcion N (0 uniones, 1 particulas, 2 efectos)
//   info                   datos del personaje (mapa, posicion, vida) en done.txt
//   focus                  ventana con foco, la ultima que eligio el juego (SetFocus) y si el juego esta al frente
//
// Comandos de juego (gamecommands.cpp): por intencion, usando la logica del propio juego, sin
// coordenadas de pantalla. Cada uno deja una linea "nombre: clave=valor ..." en done.txt (objects, una
// por objeto). Los que esperan algo (walk ... wait, attack, skill) siguen en los pasos siguientes.
//   state                  mapa, baldosa, vida, mana, SD, AG, nivel, clase, habilidad, objetivo
//   objects [RADIO]        objetos vivos cerca (monstruos, NPCs, jugadores)
//   walk X Y [wait]        caminar a la baldosa (como un click en el terreno); wait: hasta llegar
//   attack N [untildead [SEGUNDOS]]  ataque normal al objeto N (como un click sobre el monstruo)
//   skill S [N | X Y]      habilidad S sobre el objeto N, una baldosa o uno mismo
//   target N               estado del objeto N
//   peek DIRECCION [BYTES] bytes de la memoria del juego, en hexadecimal (para investigar)
//   enter N                seleccion de personaje: entrar al juego con el personaje N (0 el primero)
//
// Se activa con camera3d.ini (solo junto al modo de prueba):
//   [Test]
//   Server=127.127.127.127:45405
//   Automation=1

#include "muauto.h"

#include <gl/GL.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <string>
#include <vector>


namespace
{
    constexpr DWORD SwapBuffersImport = 0x00D22134; // main.exe: GDI32.dll SwapBuffers
    int* const MouseX = reinterpret_cast<int*>(0x0879340C);  // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    BYTE* const LeftButtonFlags = reinterpret_cast<BYTE*>(0x08793384);  // apretado recien, soltado, sostenido
    BYTE* const RightButtonFlags = reinterpret_cast<BYTE*>(0x08793381);
    BYTE* const WindowActive = reinterpret_cast<BYTE*>(0x00E8C5AC); // sin esto el juego ignora teclas y clicks
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    int* const CurrentMap = reinterpret_cast<int*>(0x00E61E18);
    DWORD* const Hero = reinterpret_cast<DWORD*>(0x07BC4F04);
    constexpr BYTE KeyUp = 0, KeyReleased = 1, KeyPressed = 2, KeyHeld = 3;

    using SwapBuffersType = BOOL(WINAPI*)(HDC);
    // OpenGL se busca al capturar (main.exe ya lo cargo), para que muauto.dll no lo importe.
    using PixelStoreType = void(APIENTRY*)(GLenum, GLint);
    using ReadBufferType = void(APIENTRY*)(GLenum);
    using ReadPixelsType = void(APIENTRY*)(GLint, GLint, GLsizei, GLsizei, GLenum, GLenum, GLvoid*);

    LogFunction g_log = nullptr;
    char g_folder[MAX_PATH];
    SwapBuffersType g_originalSwapBuffers = nullptr;

    std::vector<std::string> g_commands;
    size_t g_next = 0;
    std::string g_results;
    bool g_gameCommandPending = false; // un comando de juego sigue en los proximos pasos
    int g_waitTicks = 0;
    char g_shotPath[MAX_PATH] = {};
    int g_shotDelay = 0; // cuadros a esperar antes de la captura
    int g_burstLeft = 0; // rafaga: cuadros seguidos que faltan capturar (RUTA_1.bmp, RUTA_2.bmp, ...)
    int g_burstIndex = 0;
    char g_burstBase[MAX_PATH] = {};

    // Tecla o boton que la automatizacion mantiene: se simula apretar, sostener y soltar.
    int g_heldKey = -1;
    int g_releaseKey = -1;
    bool g_heldShift = false; // key shift+TECLA: Shift se sostiene mientras la tecla

    // Pantallas de login y seleccion de personaje: usan otro lector del mouse (CInput, 0x00E7C228,
    // actualizado por 0x00407C70 desde 0x004D9DCC) que toma el cursor real de Windows y no ve nada si la
    // ventana no esta activa. Despues de su actualizacion se le pone el mouse de la automatizacion.
    constexpr DWORD InputUpdateFunction = 0x00407C70;  // void __thiscall CInput::Update()
    constexpr DWORD InputUpdateCallSite = 0x004D9DCC;
    bool g_mouseSet = false;     // la automatizacion movio el mouse (desde ahi lo maneja ella)
    POINT g_mouse{};             // en pixeles de la ventana
    BYTE* g_keyStates = nullptr;

    std::string Path(const char* name)
    {
        return std::string(g_folder) + name;
    }

    void ToGameMouse(int x, int y)
    {
        g_mouseSet = true;
        g_mouse = { x, y };
        RECT client{};
        GetClientRect(*MainWindow, &client);
        const int width = client.right > 0 ? client.right : 1024;
        const int height = client.bottom > 0 ? client.bottom : 768;
        *MouseX = x * 640 / width;
        *MouseY = y * 480 / height;
    }

    int KeyFromName(const char* name)
    {
        struct Named { const char* name; int key; };
        static const Named names[] = {
            { "enter", VK_RETURN }, { "tab", VK_TAB }, { "esc", VK_ESCAPE }, { "space", VK_SPACE },
            { "back", VK_BACK }, { "up", VK_UP }, { "down", VK_DOWN }, { "left", VK_LEFT }, { "right", VK_RIGHT },
            { "shift", VK_SHIFT }, { "ctrl", VK_CONTROL }, { "alt", VK_MENU },
        };
        for (const Named& named : names)
        {
            if (_stricmp(name, named.name) == 0)
            {
                return named.key;
            }
        }

        if ((name[0] == 'f' || name[0] == 'F') && name[1] >= '1' && name[1] <= '9')
        {
            return VK_F1 + atoi(name + 1) - 1;
        }

        if (name[0] != '\0' && name[1] == '\0')
        {
            return toupper(static_cast<unsigned char>(name[0]));
        }

        return -1;
    }

    void SetButtonFlags(int key, bool down)
    {
        BYTE* flags = key == VK_LBUTTON ? LeftButtonFlags : key == VK_RBUTTON ? RightButtonFlags : nullptr;
        if (flags == nullptr)
        {
            return;
        }

        // Como lo hace la ventana del juego: [0] apretado recien, [1] soltado, [2] sostenido.
        flags[0] = down ? 1 : 0;
        flags[1] = down ? 0 : 1;
        flags[2] = down ? 1 : 0;
    }

    // Ultima ventana a la que el juego le dio el foco (por ejemplo la caja del chat al apretar Enter). Con el
    // juego en segundo plano GetFocus() devuelve nullptr aunque el chat este abierto, y el texto le llegaba a
    // la ventana principal como atajos de teclado (P abria el party) en vez de a la caja.
    using SetFocusType = HWND(WINAPI*)(HWND);
    SetFocusType g_originalSetFocus = nullptr;
    HWND g_gameFocus = nullptr;

    HWND WINAPI SetFocusHook(HWND window)
    {
        g_gameFocus = window;
        return g_originalSetFocus(window);
    }

    // El juego pregunta GetFocus() antes de abrir el chat con Enter y de atender atajos como C o M; en segundo
    // plano la respuesta es nullptr. Mientras la automatizacion manda una tecla o un texto (unos pasos), para el
    // juego el foco es el que el mismo eligio. Solo entonces: el juego lee el teclado real de toda la PC, y con el
    // foco fingido siempre, lo que se escribe en otras ventanas le llegaba como atajos.
    using GetFocusType = HWND(WINAPI*)();
    GetFocusType g_originalGetFocus = nullptr;
    constexpr int FakeFocusTicks = 50;
    int g_fakeFocusTicks = 0;

    HWND WINAPI GetFocusHook()
    {
        const HWND focus = g_originalGetFocus();
        if (focus != nullptr || g_fakeFocusTicks <= 0)
        {
            return focus;
        }

        return g_gameFocus != nullptr && IsWindow(g_gameFocus) ? g_gameFocus : *MainWindow;
    }

    // Entrada de la tabla de importaciones de main.exe que apunta a la funcion, o nullptr.
    DWORD* FindImport(FARPROC function)
    {
        BYTE* const base = reinterpret_cast<BYTE*>(GetModuleHandleA(nullptr));
        const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
        const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
        const IMAGE_DATA_DIRECTORY& imports = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        for (auto* module = reinterpret_cast<const IMAGE_IMPORT_DESCRIPTOR*>(base + imports.VirtualAddress);
             module->Name != 0; module++)
        {
            for (auto* entry = reinterpret_cast<DWORD*>(base + module->FirstThunk); *entry != 0; entry++)
            {
                if (*entry == reinterpret_cast<DWORD>(function))
                {
                    return entry;
                }
            }
        }

        return nullptr;
    }

    // Reemplaza en la tabla de importaciones de main.exe una funcion de user32; devuelve la original o nullptr.
    FARPROC HookUser32Import(const char* name, const void* hook)
    {
        const HMODULE user32 = GetModuleHandleA("user32.dll");
        const FARPROC function = user32 != nullptr ? GetProcAddress(user32, name) : nullptr;
        DWORD* const slot = function != nullptr ? FindImport(function) : nullptr;
        if (slot == nullptr)
        {
            g_log("Automatizacion: no se encontro %s en main.exe.", name);
            return nullptr;
        }

        const DWORD address = reinterpret_cast<DWORD>(hook);
        return WriteCode(reinterpret_cast<DWORD>(slot), &address, sizeof(address)) ? function : nullptr;
    }

    void InstallFocusHooks()
    {
        g_originalSetFocus = reinterpret_cast<SetFocusType>(HookUser32Import("SetFocus", &SetFocusHook));
        g_originalGetFocus = reinterpret_cast<GetFocusType>(HookUser32Import("GetFocus", &GetFocusHook));
    }

    // Adonde mandar teclas y texto: la ventana con foco o, en segundo plano, la que el juego eligio.
    HWND KeyboardTarget()
    {
        const HWND focus = GetFocus();
        if (focus != nullptr)
        {
            return focus;
        }

        return g_gameFocus != nullptr && IsWindow(g_gameFocus) ? g_gameFocus : *MainWindow;
    }

    void SendKeyMessages(int key)
    {
        const HWND focus = KeyboardTarget();
        SendMessageA(focus, WM_KEYDOWN, key, 1);
        if (key == VK_RETURN || key == VK_TAB || key == VK_ESCAPE || key == VK_BACK)
        {
            SendMessageA(focus, WM_CHAR, key == VK_BACK ? 8 : key == VK_TAB ? 9 : key == VK_ESCAPE ? 27 : 13, 1);
        }

        SendMessageA(focus, WM_KEYUP, key, 0xC0000001);
    }

    void Info()
    {
        char line[256];
        const DWORD hero = *Hero;
        if (hero == 0)
        {
            sprintf_s(line, "info: sin personaje (mapa %d)", *CurrentMap);
        }
        else
        {
            const float x = *reinterpret_cast<const float*>(hero + 0x404);
            const float y = *reinterpret_cast<const float*>(hero + 0x408);
            sprintf_s(line, "info: %s mapa %d posicion %d,%d", reinterpret_cast<const char*>(hero + 0x38), *CurrentMap,
                static_cast<int>(x / 100), static_cast<int>(y / 100));
        }

        g_results += line;
    }

    // Ejecuta el comando; devuelve false si tiene que seguir en el proximo paso de logica.
    bool Execute(const std::string& command, BYTE* keyStates)
    {
        char name[32] = {};
        char text[MAX_PATH] = {};
        int x = 0, y = 0;
        sscanf_s(command.c_str(), "%31s", name, static_cast<unsigned>(sizeof(name)));
        const char* rest = command.c_str() + strlen(name);
        while (*rest == ' ')
        {
            rest++;
        }

        const bool right = strstr(rest, "right") != nullptr;
        if (_stricmp(name, "move") == 0 && sscanf_s(rest, "%d %d", &x, &y) == 2)
        {
            ToGameMouse(x, y);
        }
        else if ((_stricmp(name, "click") == 0 || _stricmp(name, "down") == 0) && sscanf_s(rest, "%d %d", &x, &y) == 2)
        {
            ToGameMouse(x, y);
            g_heldKey = right ? VK_RBUTTON : VK_LBUTTON;
            keyStates[g_heldKey] = KeyPressed;
            SetButtonFlags(g_heldKey, true);
            if (_stricmp(name, "click") == 0)
            {
                g_releaseKey = g_heldKey; // se suelta en el proximo paso
            }
        }
        else if (_stricmp(name, "up") == 0)
        {
            g_releaseKey = right ? VK_RBUTTON : VK_LBUTTON;
        }
        else if (_stricmp(name, "key") == 0)
        {
            g_heldShift = _strnicmp(rest, "shift+", 6) == 0;
            if (g_heldShift)
            {
                rest += 6;
                keyStates[VK_SHIFT] = KeyPressed;
            }

            const int key = KeyFromName(rest);
            if (key < 0)
            {
                g_results += "error: tecla desconocida: " + std::string(rest);
                return true;
            }

            keyStates[key] = KeyPressed;
            g_heldKey = key;
            g_releaseKey = key;
            g_fakeFocusTicks = FakeFocusTicks;
            SendKeyMessages(key);
        }
        else if (_stricmp(name, "text") == 0)
        {
            g_fakeFocusTicks = FakeFocusTicks;
            const HWND focus = KeyboardTarget();
            for (const char* c = rest; *c != '\0'; c++)
            {
                SendMessageA(focus, WM_CHAR, static_cast<unsigned char>(*c), 1);
            }
        }
        else if (_stricmp(name, "wait") == 0)
        {
            g_waitTicks = atoi(rest);
        }
        else if (_stricmp(name, "shot") == 0 && *rest != '\0')
        {
            // shot RUTA [N]: captura N cuadros despues (para ver cuadros sin paso de logica).
            strcpy_s(text, rest);
            char* space = strrchr(text, ' ');
            g_shotDelay = 0;
            if (space != nullptr && atoi(space + 1) > 0)
            {
                g_shotDelay = atoi(space + 1);
                *space = '\0';
            }

            strcpy_s(g_shotPath, text);
        }
        else if (_stricmp(name, "burst") == 0 && *rest != '\0')
        {
            // burst RUTA N: captura N cuadros seguidos (RUTA_1.bmp ... RUTA_N.bmp)
            strcpy_s(text, rest);
            char* space = strrchr(text, ' ');
            if (space != nullptr)
            {
                g_burstLeft = atoi(space + 1);
                *space = '\0';
            }

            strcpy_s(g_burstBase, text);
            g_burstIndex = 0;
        }
        else if (_stricmp(name, "smooth") == 0)
        {
            Host().setSmoothMotion(atoi(rest) != 0); // smooth 0|1
        }
        else if (_stricmp(name, "trails") == 0)
        {
            Host().setSmoothTrails(atoi(rest) != 0); // trails 0|1
        }
        else if (_stricmp(name, "block") == 0 && sscanf_s(rest, "%d %d", &x, &y) == 2)
        {
            Host().setCreatorBlocked(x, y != 0); // block N 0|1
        }
        else if (_stricmp(name, "info") == 0)
        {
            Info();
        }
        else if (_stricmp(name, "focus") == 0)
        {
            char line[160];
            sprintf_s(line, "focus: getfocus=%p game=%p main=%p foreground=%d", static_cast<void*>(GetFocus()),
                static_cast<void*>(g_gameFocus), static_cast<void*>(*MainWindow), GetForegroundWindow() == *MainWindow ? 1 : 0);
            g_results += line;
        }
        else
        {
            std::string result;
            if (!StartGameCommand(name, rest, result, g_gameCommandPending))
            {
                g_results += "error: comando desconocido: " + command;
            }

            g_results += result;
            return true;
        }

        if (g_results.empty() || g_results.back() == '\n')
        {
            g_results += "ok: " + command;
        }

        return true;
    }

    void LoadCommands()
    {
        const std::string path = Path("commands.txt");
        FILE* file = nullptr;
        if (fopen_s(&file, path.c_str(), "r") != 0 || file == nullptr)
        {
            return;
        }

        char line[512];
        while (fgets(line, sizeof(line), file) != nullptr)
        {
            line[strcspn(line, "\r\n")] = '\0';
            if (line[0] != '\0' && line[0] != '#')
            {
                g_commands.push_back(line);
            }
        }

        fclose(file);
        DeleteFileA(path.c_str());
        g_next = 0;
        g_results.clear();
        g_log("Automatizacion: %u comandos.", static_cast<unsigned>(g_commands.size()));
    }

    void Finish()
    {
        const std::string temporary = Path("done.tmp");
        FILE* file = nullptr;
        if (fopen_s(&file, temporary.c_str(), "w") == 0 && file != nullptr)
        {
            fputs(g_results.c_str(), file);
            fclose(file);
            MoveFileExA(temporary.c_str(), Path("done.txt").c_str(), MOVEFILE_REPLACE_EXISTING);
        }

        g_commands.clear();
        g_next = 0;
    }

    void SaveShot(const char* path)
    {
        RECT client{};
        GetClientRect(*MainWindow, &client);
        const int width = client.right, height = client.bottom;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        const int stride = (width * 3 + 3) & ~3;
        const HMODULE gl = GetModuleHandleA("opengl32.dll");
        const auto pixelStore = reinterpret_cast<PixelStoreType>(GetProcAddress(gl, "glPixelStorei"));
        const auto readBuffer = reinterpret_cast<ReadBufferType>(GetProcAddress(gl, "glReadBuffer"));
        const auto readPixels = reinterpret_cast<ReadPixelsType>(GetProcAddress(gl, "glReadPixels"));
        if (pixelStore == nullptr || readBuffer == nullptr || readPixels == nullptr)
        {
            return;
        }

        std::vector<BYTE> pixels(static_cast<size_t>(stride) * height);
        pixelStore(GL_PACK_ALIGNMENT, 4);
        readBuffer(GL_BACK);
        readPixels(0, 0, width, height, 0x80E0 /* GL_BGR */, GL_UNSIGNED_BYTE, pixels.data());

        BITMAPFILEHEADER header{};
        BITMAPINFOHEADER info{};
        info.biSize = sizeof(info);
        info.biWidth = width;
        info.biHeight = height; // de abajo hacia arriba, como OpenGL
        info.biPlanes = 1;
        info.biBitCount = 24;
        info.biSizeImage = static_cast<DWORD>(pixels.size());
        header.bfType = 0x4D42;
        header.bfOffBits = sizeof(header) + sizeof(info);
        header.bfSize = header.bfOffBits + info.biSizeImage;

        FILE* file = nullptr;
        if (fopen_s(&file, path, "wb") == 0 && file != nullptr)
        {
            fwrite(&header, sizeof(header), 1, file);
            fwrite(&info, sizeof(info), 1, file);
            fwrite(pixels.data(), 1, pixels.size(), file);
            fclose(file);
        }
    }

    BOOL WINAPI SwapBuffersHook(HDC dc)
    {
        if (g_burstLeft > 0)
        {
            char path[MAX_PATH];
            sprintf_s(path, "%s_%d.bmp", g_burstBase, ++g_burstIndex);
            SaveShot(path);
            g_burstLeft--;
        }

        if (g_shotPath[0] != '\0' && g_shotDelay > 0)
        {
            g_shotDelay--;
        }
        else if (g_shotPath[0] != '\0')
        {
            SaveShot(g_shotPath);
            g_shotPath[0] = '\0';
        }

        return g_originalSwapBuffers(dc);
    }

    // Despues de que CInput lee el mouse de Windows: cursor y botones de la automatizacion.
    void __fastcall InputUpdateHook(BYTE* input)
    {
        reinterpret_cast<void(__fastcall*)(BYTE*)>(InputUpdateFunction)(input);
        if (!g_mouseSet || g_keyStates == nullptr)
        {
            return;
        }

        *reinterpret_cast<int*>(input + 0x0C) = g_mouse.x;
        *reinterpret_cast<int*>(input + 0x10) = g_mouse.y;
        *reinterpret_cast<int*>(input + 0x14) = 0; // movimiento desde el cuadro anterior
        *reinterpret_cast<int*>(input + 0x18) = 0;
        *reinterpret_cast<int*>(input + 0x34) = g_mouse.x;
        *reinterpret_cast<int*>(input + 0x38) = g_mouse.y;

        // Boton izquierdo: +0x1C apretado recien, +0x1D apretado, +0x1E soltado recien, +0x1F doble click.
        const BYTE left = g_keyStates[VK_LBUTTON];
        input[0x1C] = left == KeyPressed ? 1 : 0;
        input[0x1D] = left == KeyPressed || left == KeyHeld ? 1 : 0;
        input[0x1E] = left == KeyReleased ? 1 : 0;
        input[0x1F] = 0;
    }

    void InstallInputHook()
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(InputUpdateCallSite);
        if (code[0] != 0xE8 || InputUpdateCallSite + 5 + *reinterpret_cast<const int*>(code + 1) != InputUpdateFunction)
        {
            g_log("Automatizacion: la lectura del mouse del login no coincide; sin clicks en el login.");
            return;
        }

        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(&InputUpdateHook) - (InputUpdateCallSite + 5);
        WriteCode(InputUpdateCallSite, call, sizeof(call));
    }

    bool IsEnabled()
    {
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&IsEnabled), &module);

        char path[MAX_PATH];
        GetModuleFileNameA(module, path, MAX_PATH);
        char* slash = strrchr(path, '\\');
        if (slash == nullptr)
        {
            return false;
        }

        slash[1] = '\0';
        sprintf_s(g_folder, "%sautomation\\", path);
        strcat_s(path, "camera3d.ini");
        return GetPrivateProfileIntA("Test", "Automation", 0, path) != 0;
    }

    bool g_enabled = false;
}

void AutomationTick(BYTE* keyStates)
{
    if (!g_enabled)
    {
        return;
    }

    *WindowActive = 1;
    g_keyStates = keyStates;
    if (g_fakeFocusTicks > 0)
    {
        g_fakeFocusTicks--;
    }

    // El juego lee el teclado real de toda la PC (no solo el de su ventana). Si no esta al frente, lo que se
    // escribe en otras ventanas no es para el: se borran las teclas (los botones del mouse, por debajo de 8, no).
    // Las de la automatizacion se ponen despues.
    if (GetForegroundWindow() != *MainWindow)
    {
        memset(keyStates + 8, KeyUp, 256 - 8);
    }

    // Soltar lo que se apreto en el paso anterior.
    if (g_releaseKey >= 0 && g_heldKey != g_releaseKey)
    {
        keyStates[g_releaseKey] = KeyReleased;
        SetButtonFlags(g_releaseKey, false);
        g_releaseKey = -1;
    }
    else if (g_heldKey >= 0)
    {
        if (g_releaseKey == g_heldKey)
        {
            g_heldKey = -1; // en este paso se ve soltar
            keyStates[g_releaseKey] = KeyReleased;
            SetButtonFlags(g_releaseKey, false);
            g_releaseKey = -1;
            if (g_heldShift)
            {
                keyStates[VK_SHIFT] = KeyReleased;
                g_heldShift = false;
            }

            return;
        }

        keyStates[g_heldKey] = KeyHeld;
    }

    if (g_shotPath[0] != '\0' || g_burstLeft > 0)
    {
        return; // esperar a que se guarden las capturas
    }

    if (g_waitTicks > 0)
    {
        g_waitTicks--;
        return;
    }

    if (g_gameCommandPending)
    {
        std::string result;
        if (ContinueGameCommand(result))
        {
            g_results += result;
            g_gameCommandPending = false;
        }

        return;
    }

    if (g_commands.empty())
    {
        LoadCommands();
        if (g_commands.empty())
        {
            return;
        }
    }

    if (g_next < g_commands.size())
    {
        if (!g_results.empty())
        {
            g_results += "\n";
        }

        Execute(g_commands[g_next++], keyStates);
        return;
    }

    Finish();
}

bool InstallAutomation(LogFunction log)
{
    g_log = log;
    if (!IsEnabled())
    {
        return false;
    }

    CreateDirectoryA(g_folder, nullptr);
    DeleteFileA(Path("commands.txt").c_str());
    DeleteFileA(Path("done.txt").c_str());

    const HMODULE gdi = GetModuleHandleA("gdi32.dll");
    const FARPROC swap = gdi != nullptr ? GetProcAddress(gdi, "SwapBuffers") : nullptr;
    DWORD* const slot = reinterpret_cast<DWORD*>(SwapBuffersImport);
    if (swap != nullptr && *slot == reinterpret_cast<DWORD>(swap))
    {
        g_originalSwapBuffers = reinterpret_cast<SwapBuffersType>(swap);
        const DWORD hook = reinterpret_cast<DWORD>(&SwapBuffersHook);
        WriteCode(SwapBuffersImport, &hook, sizeof(hook));
    }
    else
    {
        g_log("Automatizacion: no se encontro SwapBuffers; sin capturas.");
    }

    InstallInputHook();
    InstallFocusHooks();
    g_enabled = true;
    g_log("Automatizacion activa en %s", g_folder);
    return true;
}
