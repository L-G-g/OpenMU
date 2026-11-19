// eventboard.dll - Board de los proximos eventos (tecla H), para el cliente MU 1.04d con camera3d.dll.
//
// La tecla H ([EventBoard] Key) abre y cierra el board. Al abrirlo (y cada RefreshSeconds mientras esta abierto) se
// manda el chat "/eventos" y el servidor (EventBoardViewPlugIn) contesta con un paquete que el main.exe original
// ignora (su reparto de paquetes devuelve 1 para los codigos mayores a 0xF9). Usa el codigo 0xFF de la interfaz de
// PeroniaMU:
//   C2 [largo] FF 50 [cantidad] y por evento [abierto: 0 o 1] [segundos, 4 bytes big endian] [nombre terminado en 0]
// Los segundos son hasta que abre la entrada, o, si esta abierta, hasta que cierra. Los nombres son Latin-1 y ya traen
// el nivel que le toca al personaje por sus resets (por ejemplo "Devil Square 5"). La cuenta regresiva sigue en el
// cliente desde que llego el paquete.
//
// Como funciona (igual que quests.dll):
//   - El reparto de paquetes (0x0065FD79), el dibujo despues de la interfaz (0x004D9CCC), los eventos del mouse de
//     las ventanas (0x00860998) y el "mouse sobre la interfaz" (0x00860910) se encadenan a lo que haya.
//   - La tecla llega por el paso de logica de camera3d.dll (despues de que el juego actualiza sus teclas): solo con el
//     foco en la ventana del juego (no mientras se escribe en el chat), y se le saca al juego para que no la use.
//   - Con el mouse sobre el board, el mouse es de esta DLL: el juego no camina ni elige objetos.

#include "../shared.h"
#include "../ui/peroniaui.h"

#include <GL/gl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <string>
#include <vector>

namespace
{
    using PeroniaUi::Rect;

    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;     // CALL al reparto al recibir cada paquete
    constexpr DWORD DrawCallSite = 0x004D9CCC;         // CALL 0x004D5EC0 despues de dibujar la interfaz
    constexpr DWORD MouseCallSite = 0x00860998;        // CALL 0x00815880: eventos del mouse de las ventanas
    constexpr DWORD MouseOnUiFunction = 0x00860910;    // char __thiscall (sistema de ventanas): mouse sobre la interfaz
    const BYTE MouseOnUiPrologue[] = { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC };
    constexpr DWORD SendChatFunction = 0x005BDE40;     // void __cdecl (const char* texto): C1 00
    constexpr DWORD IsReleaseFunction = 0x00790F00;    // bool __cdecl(int tecla): recien soltada
    constexpr DWORD SetFontFunction = 0x00420120;      // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;   // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl()
    constexpr DWORD WindowManagerMouse = 0x38;         // administrador de ventanas: la ventana con el mouse
    constexpr int ChatReady = 0x32;                    // el chat sale si la espera es como mucho esto
    constexpr int SortLeft = 1, SortCenter = 3;
    constexpr BYTE KeyUp = 0, KeyPressed = 2, KeyHeld = 3;

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);

    // --- Paquete ---
    constexpr int CodeInterface = 0xFF;
    constexpr BYTE SubCodeBoard = 0x50;
    constexpr int HeaderLength = 6;
    constexpr int MaximumEntries = 20;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using KeyStateType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    struct Entry
    {
        bool open;
        DWORD seconds; // al llegar el paquete
        std::string name;
    };

    // --- Estado ---
    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr;

    std::vector<Entry> g_entries;
    DWORD g_entriesTime = 0;      // GetTickCount al llegar el paquete
    bool g_received = false;      // llego al menos una lista desde que se abrio
    bool g_open = false;
    bool g_requestPending = false;
    DWORD g_requestTime = 0;
    bool g_mouseOver = false;
    int g_key = 'H';              // [EventBoard] Key
    BYTE g_suppressedKey = 0;
    DWORD g_refreshMilliseconds = 60000; // [EventBoard] RefreshSeconds
    float g_top = 90.0f;          // [EventBoard] Top

    // --- Medidas (640x480) ---
    constexpr float BoardWidth = 250.0f;
    constexpr float LineHeight = 14.0f;
    constexpr float Padding = 12.0f;
    constexpr float TitleHeight = 26.0f;
    constexpr float ButtonsHeight = 32.0f;
    constexpr float ButtonWidth = 80.0f;
    constexpr float ButtonHeight = 20.0f;
    constexpr int VisibleRows = 10;

    // --- Utilidades ---
    float Px(float x)
    {
        return x * *WindowWidth / 640.0f;
    }

    float Py(float y)
    {
        const int height = *reinterpret_cast<int*>(0x00E61E5C);
        return height - y * height / 480.0f;
    }

    void Vertex(float x, float y)
    {
        glVertex2f(Px(x), Py(y));
    }

    void FillRect(const Rect& rect, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_QUADS);
        Vertex(rect.left, rect.top);
        Vertex(rect.Right(), rect.top);
        Vertex(rect.Right(), rect.Bottom());
        Vertex(rect.left, rect.Bottom());
        glEnd();
    }

    void OutlineRect(const Rect& rect, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_LINE_LOOP);
        Vertex(rect.left, rect.top);
        Vertex(rect.Right(), rect.top);
        Vertex(rect.Right(), rect.Bottom());
        Vertex(rect.left, rect.Bottom());
        glEnd();
    }

    // Ancho de un texto con la fuente del juego, en 640x480.
    float TextWidth(const char* text, bool bold)
    {
        static HDC dc = CreateCompatibleDC(nullptr);
        const HFONT font = bold ? *BoldFont : *NormalFont;
        if (dc == nullptr || font == nullptr || *WindowWidth <= 0)
        {
            return strlen(text) * 5.0f;
        }

        const HGDIOBJ old = SelectObject(dc, font);
        SIZE size = {};
        GetTextExtentPoint32A(dc, text, static_cast<int>(strlen(text)), &size);
        SelectObject(dc, old);
        return size.cx * 640.0f / *WindowWidth;
    }

    void Text(const char* text, float left, float top, float width, int sort, DWORD color, bool bold)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        *reinterpret_cast<DWORD*>(renderer + 0x10) = color;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = 0; // sin fondo
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, bold ? *BoldFont : *NormalFont);
        reinterpret_cast<RenderTextType>(RenderTextFunction)(RenderText, nullptr, static_cast<int>(left),
            static_cast<int>(top), text, static_cast<int>(width), 0, sort, nullptr);
    }

    // Segundos que faltan de un evento ahora (0 si ya paso).
    DWORD RemainingSeconds(const Entry& entry)
    {
        const DWORD elapsed = (GetTickCount() - g_entriesTime) / 1000;
        return entry.seconds > elapsed ? entry.seconds - elapsed : 0;
    }

    void FormatTime(DWORD seconds, char* text, size_t size)
    {
        const DWORD hours = seconds / 3600;
        const DWORD minutes = seconds / 60 % 60;
        if (hours > 0)
        {
            sprintf_s(text, size, "%lu:%02lu:%02lu", hours, minutes, seconds % 60);
        }
        else
        {
            sprintf_s(text, size, "%02lu:%02lu", minutes, seconds % 60);
        }
    }

    // --- Pedido al servidor ---
    void RequestBoard()
    {
        g_requestPending = true;
        g_requestTime = GetTickCount();
    }

    void SendPendingRequest()
    {
        if (g_requestPending && *ChatWait <= ChatReady)
        {
            g_requestPending = false;
            reinterpret_cast<SendChatType>(SendChatFunction)("/eventos");
        }
    }

    // --- Board ---
    int RowCount()
    {
        const int count = static_cast<int>(g_entries.size());
        return count == 0 ? 1 : (count < VisibleRows ? count : VisibleRows);
    }

    Rect BoardRect()
    {
        return { (640.0f - BoardWidth) / 2, g_top, BoardWidth, TitleHeight + RowCount() * LineHeight + ButtonsHeight + Padding / 2 };
    }

    Rect CloseButtonRect()
    {
        const Rect board = BoardRect();
        return { board.left + (board.width - ButtonWidth) / 2, board.Bottom() - ButtonsHeight + 6, ButtonWidth, ButtonHeight };
    }

    void DrawBoard()
    {
        const Rect board = BoardRect();
        const Rect close = CloseButtonRect();
        PeroniaUi::BeginShapes();
        FillRect(board, 0.0f, 0.0f, 0.0f, 0.88f);
        OutlineRect(board, 0.8f, 0.65f, 0.35f, 0.9f);
        const Rect inner = { board.left + 2, board.top + 2, board.width - 4, board.height - 4 };
        OutlineRect(inner, 0.45f, 0.35f, 0.18f, 0.7f);
        const Rect separator = { board.left + Padding, board.top + TitleHeight - 5, board.width - Padding * 2, 1 };
        FillRect(separator, 0.8f, 0.65f, 0.35f, 0.6f);
        for (int i = 0; i < static_cast<int>(g_entries.size()) && i < VisibleRows; i++)
        {
            if (g_entries[i].open)
            {
                const Rect row = { board.left + 4, board.top + TitleHeight + i * LineHeight - 1, board.width - 8, LineHeight };
                FillRect(row, 0.15f, 0.45f, 0.15f, 0.45f);
            }
        }

        PeroniaUi::MetalButton(close, PeroniaUi::StateOf(close, true));
        PeroniaUi::EndShapes();

        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr)
        {
            return;
        }

        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        const DWORD savedText = *reinterpret_cast<DWORD*>(renderer + 0x10);
        const DWORD savedBackground = *reinterpret_cast<DWORD*>(renderer + 0x14);
        Text("Proximos eventos", board.left, board.top + 8, board.width, SortCenter, PeroniaUi::GoldText, true);
        const float right = board.Right() - Padding;
        if (g_entries.empty())
        {
            Text(g_received ? "No hay eventos programados." : "Cargando...", board.left, board.top + TitleHeight,
                board.width, SortCenter, PeroniaUi::Rgb(150, 140, 130), false);
        }

        for (int i = 0; i < static_cast<int>(g_entries.size()) && i < VisibleRows; i++)
        {
            const Entry& entry = g_entries[i];
            const float top = board.top + TitleHeight + i * LineHeight;
            const DWORD remaining = RemainingSeconds(entry);
            char time[32];
            char clock[16];
            FormatTime(remaining, clock, sizeof(clock));
            if (entry.open)
            {
                sprintf_s(time, "ABIERTO %s", clock);
            }
            else
            {
                strcpy_s(time, remaining == 0 ? "abriendo" : clock);
            }

            const DWORD color = entry.open ? PeroniaUi::Rgb(120, 230, 120) : (i == 0 ? PeroniaUi::GoldText : PeroniaUi::Rgb(230, 230, 230));
            Text(entry.name.c_str(), board.left + Padding, top, board.width - Padding * 2, SortLeft, color, entry.open || i == 0);
            const float width = TextWidth(time, entry.open);
            Text(time, right - width, top, width + 2, SortLeft, color, entry.open);
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();

        const auto closeState = PeroniaUi::StateOf(close, true);
        PeroniaUi::ButtonText(close, "Cerrar",
            closeState == PeroniaUi::ButtonState::Normal ? PeroniaUi::GoldText : PeroniaUi::GoldTextHover, false,
            closeState == PeroniaUi::ButtonState::Pressed);
    }

    void UpdateBoard()
    {
        if (!g_open)
        {
            return;
        }

        // Se vuelve a pedir cada tanto, y cuando un evento abrio o cerro (su cuenta llego a 0).
        const DWORD now = GetTickCount();
        bool expired = false;
        for (const Entry& entry : g_entries)
        {
            expired |= RemainingSeconds(entry) == 0;
        }

        const DWORD sinceRequest = now - g_requestTime;
        if (!g_requestPending && (sinceRequest >= g_refreshMilliseconds || (expired && sinceRequest >= 3000)))
        {
            RequestBoard();
        }
    }

    // --- Paquete ---
    void OnBoard(const BYTE* packet, int size)
    {
        const int count = packet[5];
        std::vector<Entry> entries;
        int offset = HeaderLength;
        for (int i = 0; i < count && i < MaximumEntries; i++)
        {
            if (offset + 5 >= size)
            {
                return; // paquete cortado
            }

            Entry entry;
            entry.open = packet[offset] != 0;
            entry.seconds = (static_cast<DWORD>(packet[offset + 1]) << 24) | (packet[offset + 2] << 16)
                | (packet[offset + 3] << 8) | packet[offset + 4];
            offset += 5;
            const BYTE* end = static_cast<const BYTE*>(memchr(packet + offset, 0, size - offset));
            if (end == nullptr)
            {
                return;
            }

            entry.name.assign(reinterpret_cast<const char*>(packet + offset), end - (packet + offset));
            offset = static_cast<int>(end - packet) + 1;
            entries.push_back(entry);
        }

        g_entries = entries;
        g_entriesTime = GetTickCount();
        g_received = true;
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (code == CodeInterface && packet != nullptr && packet[0] == 0xC2 && size >= HeaderLength
            && packet[4] == SubCodeBoard)
        {
            OnBoard(packet, size);
            return 1; // como el juego con los codigos que no conoce
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // --- Tecla ---
    bool IsDown(const BYTE* keyStates, int key)
    {
        return keyStates[key] == KeyPressed || keyStates[key] == KeyHeld;
    }

    void __cdecl KeyTick(BYTE* keyStates)
    {
        if (g_suppressedKey != 0)
        {
            if (IsDown(keyStates, g_suppressedKey))
            {
                keyStates[g_suppressedKey] = KeyUp;
                return;
            }

            g_suppressedKey = 0;
        }

        if (g_key == 0 || GetFocus() != *MainWindow)
        {
            return; // escribiendo en el chat (o el juego no tiene el foco)
        }

        if (keyStates[g_key] == KeyPressed && !IsDown(keyStates, VK_SHIFT) && !IsDown(keyStates, VK_CONTROL))
        {
            g_suppressedKey = static_cast<BYTE>(g_key);
            keyStates[g_key] = KeyUp;
            g_open = !g_open;
            if (g_open)
            {
                g_received = false;
                RequestBoard();
            }
        }
    }

    // --- Mouse ---
    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingRequest();
        UpdateBoard();
        const bool windowHasMouse = *reinterpret_cast<const DWORD*>(manager + WindowManagerMouse) != 0;
        g_mouseOver = g_open && !windowHasMouse && PeroniaUi::MouseInside(BoardRect());
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        // El mouse es de esta DLL: ninguna ventana lo tiene y el juego no camina.
        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<KeyStateType>(IsReleaseFunction)(VK_LBUTTON) && PeroniaUi::MouseInside(CloseButtonRect()))
        {
            g_open = false;
            g_mouseOver = false;
        }

        return 1;
    }

    char __fastcall MouseOnUiHook(BYTE** system, void* unused)
    {
        if (g_mouseOver)
        {
            return 1;
        }

        if (g_nextMouseOnUi != nullptr)
        {
            return g_nextMouseOnUi(system, unused);
        }

        const BYTE* manager = *system;
        return manager != nullptr && *reinterpret_cast<const DWORD*>(manager + WindowManagerMouse) != 0 ? 1 : 0;
    }

    // --- Dibujo despues de la interfaz ---
    void DrawInterface()
    {
        __try
        {
            if (g_open)
            {
                DrawBoard();
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void __cdecl DrawHook()
    {
        DrawInterface();
        g_nextDraw();
    }

    // --- Instalacion ---
    DWORD CallTarget(DWORD callSite)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(callSite);
        return code[0] == 0xE8 ? callSite + 5 + *reinterpret_cast<const DWORD*>(code + 1) : 0;
    }

    DWORD JumpTarget(DWORD address)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(address);
        return code[0] == 0xE9 ? address + 5 + *reinterpret_cast<const DWORD*>(code + 1) : 0;
    }

    bool RedirectCall(DWORD callSite, const void* target)
    {
        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(target) - (callSite + 5);
        return WriteCode(callSite, call, sizeof(call));
    }

    void ReadKey(const char* iniPath)
    {
        char text[16];
        GetPrivateProfileStringA("EventBoard", "Key", "H", text, sizeof(text), iniPath);
        if (text[0] == '0' && text[1] == 'x')
        {
            const long code = strtol(text, nullptr, 16);
            g_key = code > 0 && code < 256 ? static_cast<int>(code) : 0;
        }
        else if (text[0] != '\0' && text[1] == '\0')
        {
            g_key = text[0] >= 'a' && text[0] <= 'z' ? text[0] - 'a' + 'A' : text[0];
        }
    }
}

extern "C" __declspec(dllexport) bool StartEventBoard(const EventBoardHost* host)
{
    if (host == nullptr || host->version != EventBoardHostVersion)
    {
        return false;
    }

    g_log = host->log;
    ReadKey(host->iniPath);
    const int refresh = GetPrivateProfileIntA("EventBoard", "RefreshSeconds", 60, host->iniPath);
    g_refreshMilliseconds = (refresh < 10 ? 10 : refresh) * 1000u;
    char top[32];
    GetPrivateProfileStringA("EventBoard", "Top", "", top, sizeof(top), host->iniPath);
    if (top[0] != '\0')
    {
        g_top = static_cast<float>(atof(top));
    }

    // Todas las llamadas pueden estar redirigidas por otras DLL: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("EventBoard: el juego no coincide; desactivado.");
        return false;
    }

    PeroniaUi::Initialize(host->iniPath, host->log);
    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    g_nextDraw = reinterpret_cast<DrawType>(draw);
    g_nextMouse = reinterpret_cast<MouseType>(mouse);
    g_nextMouseOnUi = reinterpret_cast<MouseOnUiType>(mouseOnUi);
    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&MouseOnUiHook) - (MouseOnUiFunction + 5);
    if (!WriteCode(MouseOnUiFunction, jump, sizeof(jump)) || !RedirectCall(MouseCallSite, &MouseHook)
        || !RedirectCall(DrawCallSite, &DrawHook) || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("EventBoard: no se pudo instalar.");
        return false;
    }

    host->setKeyTick(&KeyTick);
    g_log("EventBoard listo (tecla 0x%02X, se actualiza cada %lu s).", g_key, g_refreshMilliseconds / 1000);
    return true;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}
