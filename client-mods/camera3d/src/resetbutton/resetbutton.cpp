// resetbutton.dll - Boton "Reset" para el cliente MU 1.04d con camera3d.dll.
//
// Cuando el personaje puede resetear (nivel, zen, limite de resets e items, lo mismo que pide /reset), aparece un
// boton "Reset" abajo en el centro de la pantalla, arriba de la barra de habilidades, con el estilo de PeroniaMU
// (src\ui\peroniaui.h): marco de metal oscuro, letras doradas y un resplandor dorado detras. El click manda el chat
// "/reset", como si el jugador lo escribiera.
//
// El servidor (OpenMU, plugins "Reset button" y "Show Reset Button (resetbutton.dll)") manda C1 05 FF 00 [0 o 1]
// cuando cambia y, mientras se puede, cada pocos segundos. El main.exe original lo ignora (su reparto de paquetes
// devuelve 1 para los codigos mayores a 0xF9).
//
// Como funciona (las mismas llamadas que loot.dll y espectros.dll, encadenadas):
//   - Paquetes (0x0065FD79): 0xFF 0x00 se queda aca. Con C3 F3 03 (informacion del personaje: entrar al juego, otro
//     personaje, despues de un reset) se esconde el boton hasta el proximo aviso.
//   - Dibujo (0x004D9CCC, despues de la interfaz): dibuja el boton y despues llama a lo que habia.
//   - Mouse (0x00860998 y el salto en 0x00860910): con el mouse sobre el boton ninguna ventana ni el mundo lo reciben;
//     el click (al soltar, 0x00790F00) manda /reset.
//   - El chat se manda con la funcion del juego (0x005BDE40) cuando su espera (0x08793728) lo permite.

#include "../shared.h"
#include "../ui/peroniaui.h"

#include <stdlib.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;
    constexpr DWORD DrawCallSite = 0x004D9CCC;
    constexpr DWORD MouseCallSite = 0x00860998;
    constexpr DWORD MouseOnUiFunction = 0x00860910;
    const BYTE MouseOnUiPrologue[] = { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC };
    constexpr DWORD SendChatFunction = 0x005BDE40;
    constexpr DWORD IsReleaseFunction = 0x00790F00;
    constexpr DWORD WindowManagerMouse = 0x38;
    constexpr int ChatReady = 0x32;
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);

    // --- Paquetes ---
    constexpr int CodeInterface = 0xFF, CodeCharacterInfo = 0xF3;
    constexpr BYTE SubCodeResetButton = 0x00, SubCodeCharacterInfo = 0x03;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using IsReleaseType = bool(__cdecl*)(int key);

    // --- Boton (640x480): la barra de abajo empieza en 429 ---
    float g_width = 120.0f;
    float g_height = 24.0f;
    float g_bottom = 420.0f;
    char g_text[32] = "RESET";
    constexpr DWORD ClickWait = 4000; // despues del click, esconderlo hasta que el servidor conteste

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr;
    bool g_available = false;
    bool g_mouseOver = false;
    bool g_sendPending = false;
    DWORD g_clickTick = 0;

    PeroniaUi::Rect ButtonRect()
    {
        return { (640.0f - g_width) / 2, g_bottom - g_height, g_width, g_height };
    }

    bool Visible()
    {
        return g_available && (g_clickTick == 0 || GetTickCount() - g_clickTick > ClickWait);
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr && packet[0] == 0xC1)
        {
            if (code == CodeInterface && size >= 5 && packet[3] == SubCodeResetButton)
            {
                const bool available = packet[4] != 0;
                if (available != g_available)
                {
                    g_log("Reset: boton %s.", available ? "visible" : "escondido");
                }

                g_available = available;
                if (!available)
                {
                    g_clickTick = 0;
                }

                return 1;
            }
        }

        if (packet != nullptr && code == CodeCharacterInfo)
        {
            const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
            if (size > header && packet[header] == SubCodeCharacterInfo)
            {
                g_available = false;
                g_clickTick = 0;
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    void SendPendingChat()
    {
        if (g_sendPending && *ChatWait <= ChatReady)
        {
            g_sendPending = false;
            reinterpret_cast<SendChatType>(SendChatFunction)("/reset");
            g_log("Reset: /reset enviado.");
        }
    }

    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingChat();
        g_mouseOver = Visible() && PeroniaUi::MouseInside(ButtonRect());
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<IsReleaseType>(IsReleaseFunction)(VK_LBUTTON))
        {
            g_sendPending = true;
            g_clickTick = GetTickCount();
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

    void DrawButton()
    {
        __try
        {
            const PeroniaUi::Rect rect = ButtonRect();
            const PeroniaUi::ButtonState state = PeroniaUi::StateOf(rect, true);
            const bool hover = state == PeroniaUi::ButtonState::Hover || state == PeroniaUi::ButtonState::Pressed;
            PeroniaUi::BeginShapes();
            PeroniaUi::Glow(rect, hover ? 1.0f : 0.8f);
            PeroniaUi::MetalButton(rect, state);
            PeroniaUi::Shine(rect);
            PeroniaUi::EndShapes();
            PeroniaUi::ButtonText(rect, g_text, hover ? PeroniaUi::GoldTextHover : PeroniaUi::GoldText, true,
                state == PeroniaUi::ButtonState::Pressed);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void __cdecl DrawHook()
    {
        if (Visible())
        {
            DrawButton();
        }

        g_nextDraw();
    }

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

    float ReadFloat(const char* iniPath, const char* key, float fallback)
    {
        char text[32];
        GetPrivateProfileStringA("ResetButton", key, "", text, sizeof(text), iniPath);
        return text[0] != '\0' ? static_cast<float>(atof(text)) : fallback;
    }
}

extern "C" __declspec(dllexport) bool StartResetButton(const ResetButtonHost* host)
{
    if (host == nullptr || host->version != ResetButtonHostVersion)
    {
        return false;
    }

    g_log = host->log;
    PeroniaUi::Initialize(host->iniPath, host->log);
    g_width = ReadFloat(host->iniPath, "Width", g_width);
    g_height = ReadFloat(host->iniPath, "Height", g_height);
    g_bottom = ReadFloat(host->iniPath, "Bottom", g_bottom);
    char text[sizeof(g_text)];
    GetPrivateProfileStringA("ResetButton", "Text", "", text, sizeof(text), host->iniPath);
    if (text[0] != '\0')
    {
        strcpy_s(g_text, text);
    }

    // El reparto de paquetes, el dibujo y el mouse pueden estar redirigidos por otras DLL: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("Reset: el juego no coincide; boton desactivado.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    g_nextDraw = reinterpret_cast<DrawType>(draw);
    g_nextMouse = reinterpret_cast<MouseType>(mouse);
    g_nextMouseOnUi = reinterpret_cast<MouseOnUiType>(mouseOnUi);
    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&MouseOnUiHook) - (MouseOnUiFunction + 5);
    if (!WriteCode(MouseOnUiFunction, jump, sizeof(jump)) || !RedirectCall(MouseCallSite, &MouseHook)
        || !RedirectCall(DrawCallSite, &DrawHook) || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Reset: no se pudo instalar el boton.");
        return false;
    }

    g_log("Boton de reset listo (abajo en %.0f, %.0fx%.0f).", g_bottom, g_width, g_height);
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
