// tokenshop.dll - Solapas por clase en los mercaderes de tokens, para el cliente MU 1.04d con camera3d.dll.
//
// Los mercaderes de tokens de las dungeons (Zienna en Devias y los de Tarkan y Kanturu) venden el mejor set, escudo y
// armas de su dungeon para cada clase, a cambio de los tokens que da completar la dungeon. La tienda del juego solo
// conoce una lista de items con precio en zen, asi que el servidor muestra la lista de una clase por vez y esta DLL
// agrega lo que falta, con el estilo de PeroniaMU (src\ui\peroniaui.h):
//   - Siete solapas a la izquierda de la ventana del mercader, una por clase, cada una con su icono: Espada (DK),
//     Baculo (DW), Arco (ELF), Libro (SUM), Espada y baculo cruzados (MG), Cetro (DL) y Punio (RF). La solapa abierta
//     queda resaltada. El click manda el chat "/tab N" y el servidor contesta con la lista de esa clase.
//   - Debajo de los items, cuantos tokens tiene el personaje; con el mouse sobre un item, su precio en tokens.
//
// El servidor (OpenMU, plugins "Dungeon token merchants" y "Show Token Shop (tokenshop.dll)") manda, despues de la
// lista de items y despues de cada compra:
//   C2 [largo, 2 bytes big endian] FF 40 [solapa] [tokens, 4 bytes big endian] [nombre del token, 20 bytes terminado en 0]
//   [cantidad] y por item [casillero] [ancho] [alto] [precio, 2 bytes big endian].
// El main.exe original lo ignora (su reparto de paquetes devuelve 1 para los codigos mayores a 0xF9).
//
// Como funciona (las mismas llamadas que resetbutton.dll, encadenadas):
//   - Paquetes (0x0065FD79): 0xFF 0x40 se queda aca y activa las solapas mientras la tienda este abierta.
//   - Dibujo (0x004D9CCC, despues de la interfaz): dibuja las solapas y el recuadro de tokens, y llama a lo que habia.
//   - Mouse (0x00860998 y el salto en 0x00860910): con el mouse sobre una solapa ninguna ventana ni el mundo lo
//     reciben; el click (al soltar, 0x00790F00) manda /tab.
//   - El chat se manda con la funcion del juego (0x005BDE40) cuando su espera (0x08793728) lo permite.
//   - La ventana de la tienda (CNewUINPCShop) se busca por su vtable entre las del administrador de ventanas, como
//     hace el click derecho de camera3d.dll; de su grilla salen la posicion y el tamano de los casilleros.

#include "../shared.h"
#include "../ui/peroniaui.h"
#include "icons.h"

#include <stdio.h>
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
    BYTE* const UiManager = reinterpret_cast<BYTE*>(0x09867090);
    DWORD* const UiManagerInitFlag = reinterpret_cast<DWORD*>(0x098671D4);
    constexpr DWORD VtNpcShop = 0x00D47750;
    constexpr DWORD WindowVisible = 0x08;   // byte
    constexpr DWORD ShopGrid = 0x10;        // CNewUINPCShop: su grilla de items
    constexpr DWORD GridX = 0x28, GridY = 0x2C, GridColumns = 0x38, GridRows = 0x3C;
    constexpr float CellSize = 20.0f;

    // --- Paquetes ---
    constexpr int CodeInterface = 0xFF, CodeCharacterInfo = 0xF3;
    constexpr BYTE SubCodeTokenShop = 0x40, SubCodeCharacterInfo = 0x03;
    constexpr int NameLength = 20;
    constexpr int PacketHeader = 11 + NameLength; // C2, largo, FF, 40, solapa, tokens (4), nombre, cantidad
    constexpr int PacketItem = 5;
    constexpr int MaxItems = 64;
    constexpr int TabCount = TokenShopIconCount;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using IsReleaseType = bool(__cdecl*)(int key);

    const char* const ClassNames[TabCount] = {
        "Dark Knight", "Dark Wizard", "Elf", "Summoner", "Magic Gladiator", "Dark Lord", "Rage Fighter",
    };

    struct ShopItem
    {
        BYTE slot;
        BYTE width;
        BYTE height;
        WORD price;
    };

    // --- Medidas (640x480), de [TokenShop] en camera3d.ini ---
    float g_tabWidth = 28.0f;   // 0: cuadradas en pantalla segun el alto
    float g_tabHeight = 38.0f;
    float g_tabGap = 5.0f;
    float g_tabRight = 16.0f;   // distancia del borde derecho de las solapas a la grilla (el marco de la ventana)
    float g_tabTop = 0.0f;      // desde el borde de arriba de la grilla
    float g_infoTop = 5.0f;     // del recuadro de tokens, desde el borde de abajo de la grilla
    float g_infoHeight = 14.0f;
    constexpr DWORD ActiveGrace = 1500; // la ventana puede abrirse un instante despues del paquete
    constexpr DWORD ClickWait = 400;    // entre dos clicks de solapa

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr;

    bool g_active = false;      // el mercader abierto es de tokens
    DWORD g_activeTick = 0;
    int g_tab = 0;
    DWORD g_tokens = 0;
    char g_tokenName[NameLength] = "";
    ShopItem g_items[MaxItems];
    int g_itemCount = 0;

    DWORD g_shopWindow = 0;
    bool g_mouseOver = false;
    int g_pendingTab = -1;
    DWORD g_clickTick = 0;
    unsigned int g_iconTexture = 0;
    bool g_iconTried = false;

    template <typename T>
    T Read(DWORD address)
    {
        return *reinterpret_cast<T*>(address);
    }

    // La ventana de la tienda, buscada por su vtable entre las del administrador de ventanas.
    DWORD FindShopWindow()
    {
        if ((*UiManagerInitFlag & 1) == 0)
        {
            return 0;
        }

        __try
        {
            if (g_shopWindow != 0 && Read<DWORD>(g_shopWindow) == VtNpcShop)
            {
                return g_shopWindow;
            }

            g_shopWindow = 0;
            for (DWORD offset = 4; offset < 0x400; offset += 4)
            {
                const DWORD window = Read<DWORD>(reinterpret_cast<DWORD>(UiManager) + offset);
                if (window >= 0x10000 && Read<DWORD>(window) == VtNpcShop)
                {
                    g_shopWindow = window;
                    break;
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_shopWindow = 0;
        }

        return g_shopWindow;
    }

    // Posicion de la grilla de la tienda abierta; false si no hay tienda abierta.
    bool ShopGridPosition(float& x, float& y, float& width, float& height)
    {
        const DWORD window = FindShopWindow();
        if (window == 0)
        {
            return false;
        }

        __try
        {
            if (Read<BYTE>(window + WindowVisible) == 0)
            {
                return false;
            }

            const DWORD grid = Read<DWORD>(window + ShopGrid);
            if (grid < 0x10000)
            {
                return false;
            }

            const int columns = Read<int>(grid + GridColumns);
            const int rows = Read<int>(grid + GridRows);
            x = static_cast<float>(Read<int>(grid + GridX));
            y = static_cast<float>(Read<int>(grid + GridY));
            width = (columns > 0 && columns <= 16 ? columns : 8) * CellSize;
            height = (rows > 0 && rows <= 32 ? rows : 15) * CellSize;
            return true;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return false;
        }
    }

    // Tienda de tokens abierta ahora, con la posicion de su grilla.
    bool Visible(float& x, float& y, float& width, float& height)
    {
        if (!g_active)
        {
            return false;
        }

        if (ShopGridPosition(x, y, width, height))
        {
            return true;
        }

        if (GetTickCount() - g_activeTick > ActiveGrace)
        {
            // Se cerro la tienda: la proxima puede ser un mercader comun.
            g_active = false;
            g_pendingTab = -1;
        }

        return false;
    }

    PeroniaUi::Rect TabRect(int tab, float gridX, float gridY)
    {
        const float width = g_tabWidth > 0 ? g_tabWidth : PeroniaUi::SquareWidth(g_tabHeight);
        return { gridX - g_tabRight - width, gridY + g_tabTop + tab * (g_tabHeight + g_tabGap), width, g_tabHeight };
    }

    int TabUnderMouse(float gridX, float gridY)
    {
        for (int tab = 0; tab < TabCount; ++tab)
        {
            if (PeroniaUi::MouseInside(TabRect(tab, gridX, gridY)))
            {
                return tab;
            }
        }

        return -1;
    }

    const ShopItem* ItemUnderMouse(float gridX, float gridY)
    {
        const float mouseX = PeroniaUi::MouseX();
        const float mouseY = PeroniaUi::MouseY();
        for (int i = 0; i < g_itemCount; ++i)
        {
            const ShopItem& item = g_items[i];
            const float left = gridX + (item.slot % 8) * CellSize;
            const float top = gridY + (item.slot / 8) * CellSize;
            if (mouseX >= left && mouseX < left + item.width * CellSize && mouseY >= top && mouseY < top + item.height * CellSize)
            {
                return &item;
            }
        }

        return nullptr;
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr && code == CodeInterface && packet[0] == 0xC2 && size >= PacketHeader
            && packet[4] == SubCodeTokenShop)
        {
            g_tab = packet[5] < TabCount ? packet[5] : 0;
            g_tokens = (static_cast<DWORD>(packet[6]) << 24) | (packet[7] << 16) | (packet[8] << 8) | packet[9];
            memcpy(g_tokenName, packet + 10, NameLength);
            g_tokenName[NameLength - 1] = '\0';
            int count = packet[10 + NameLength];
            if (count > MaxItems)
            {
                count = MaxItems;
            }

            if (PacketHeader + count * PacketItem > size)
            {
                count = (size - PacketHeader) / PacketItem;
            }

            for (int i = 0; i < count; ++i)
            {
                const BYTE* data = packet + PacketHeader + i * PacketItem;
                g_items[i] = { data[0], data[1], data[2], static_cast<WORD>((data[3] << 8) | data[4]) };
            }

            g_itemCount = count;
            if (!g_active)
            {
                g_log("Tokens: mercader de %s, solapa %d, %lu tokens, %d items.", g_tokenName, g_tab + 1, g_tokens, count);
            }

            g_active = true;
            g_activeTick = GetTickCount();
            return 1;
        }

        if (packet != nullptr && code == CodeCharacterInfo)
        {
            const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
            if (size > header && packet[header] == SubCodeCharacterInfo)
            {
                g_active = false;
                g_pendingTab = -1;
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    void SendPendingChat()
    {
        if (g_pendingTab >= 0 && *ChatWait <= ChatReady)
        {
            char text[16];
            sprintf_s(text, "/tab %d", g_pendingTab + 1);
            g_pendingTab = -1;
            reinterpret_cast<SendChatType>(SendChatFunction)(text);
        }
    }

    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingChat();
        float x, y, width, height;
        const int tab = Visible(x, y, width, height) ? TabUnderMouse(x, y) : -1;
        g_mouseOver = tab >= 0;
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<IsReleaseType>(IsReleaseFunction)(VK_LBUTTON) && tab != g_tab
            && GetTickCount() - g_clickTick > ClickWait)
        {
            g_pendingTab = tab;
            g_clickTick = GetTickCount();
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

    // El icono de la solapa, centrado y cuadrado en pantalla, con una sombra debajo.
    void DrawIcon(int tab, const PeroniaUi::Rect& rect, bool selected, bool hover, bool pressed)
    {
        float iconHeight = rect.height - 10.0f;
        float iconWidth = PeroniaUi::SquareWidth(iconHeight);
        if (iconWidth > rect.width - 6.0f)
        {
            iconWidth = rect.width - 6.0f;
            iconHeight *= iconWidth / PeroniaUi::SquareWidth(iconHeight);
        }

        const float push = pressed ? 1.0f : 0.0f;
        const PeroniaUi::Rect icon = { rect.left + (rect.width - iconWidth) / 2 + push,
            rect.top + (rect.height - iconHeight) / 2 + push, iconWidth, iconHeight };
        const PeroniaUi::Rect shadow = { icon.left + 1.0f, icon.top + 1.0f, icon.width, icon.height };
        const float u0 = static_cast<float>(tab) / TabCount;
        const float u1 = static_cast<float>(tab + 1) / TabCount;
        PeroniaUi::MaskIcon(g_iconTexture, shadow, u0, u1, 0.10f, 0.06f, 0.0f, 0.85f);
        if (selected)
        {
            PeroniaUi::MaskIcon(g_iconTexture, icon, u0, u1, 1.0f, 0.93f, 0.59f, 1.0f); // dorado claro
        }
        else if (hover)
        {
            PeroniaUi::MaskIcon(g_iconTexture, icon, u0, u1, 1.0f, 0.82f, 0.24f, 1.0f); // dorado
        }
        else
        {
            PeroniaUi::MaskIcon(g_iconTexture, icon, u0, u1, 0.62f, 0.52f, 0.26f, 1.0f); // dorado apagado
        }
    }

    void DrawShop(float gridX, float gridY, float gridWidth, float gridHeight)
    {
        __try
        {
            if (!g_iconTried)
            {
                g_iconTried = true;
                g_iconTexture = PeroniaUi::CreateMaskTexture(TokenShopIcons, TokenShopIconSize * TokenShopIconCount, TokenShopIconSize);
            }

            const int hoverTab = TabUnderMouse(gridX, gridY);
            const ShopItem* const hoverItem = hoverTab < 0 ? ItemUnderMouse(gridX, gridY) : nullptr;
            const PeroniaUi::Rect info = { gridX, gridY + gridHeight + g_infoTop, gridWidth, g_infoHeight };
            PeroniaUi::Rect name{};
            PeroniaUi::BeginShapes();
            for (int tab = 0; tab < TabCount; ++tab)
            {
                const PeroniaUi::Rect rect = TabRect(tab, gridX, gridY);
                const bool selected = tab == g_tab;
                const PeroniaUi::ButtonState state = PeroniaUi::StateOf(rect, true);
                const bool hover = state == PeroniaUi::ButtonState::Hover || state == PeroniaUi::ButtonState::Pressed;
                if (selected)
                {
                    PeroniaUi::Glow(rect, 0.7f);
                }

                // La solapa abierta queda como apretada.
                PeroniaUi::MetalButton(rect, selected ? PeroniaUi::ButtonState::Hover : state);
                DrawIcon(tab, rect, selected, hover, state == PeroniaUi::ButtonState::Pressed);
            }

            PeroniaUi::Panel(info, 0.85f);
            if (hoverTab >= 0)
            {
                // Nombre de la clase a la izquierda de la solapa.
                const PeroniaUi::Rect rect = TabRect(hoverTab, gridX, gridY);
                name = { rect.left - 84.0f, rect.top + (rect.height - 14.0f) / 2, 80.0f, 14.0f };
                PeroniaUi::Panel(name, 0.85f);
            }

            PeroniaUi::EndShapes();

            char text[96];
            if (hoverItem != nullptr)
            {
                sprintf_s(text, "Precio: %u %s (tenes %lu)", hoverItem->price, g_tokenName, g_tokens);
            }
            else
            {
                sprintf_s(text, "%s: %lu", g_tokenName, g_tokens);
            }

            const bool enough = hoverItem == nullptr || g_tokens >= hoverItem->price;
            PeroniaUi::ButtonText(info, text, enough ? PeroniaUi::GoldText : PeroniaUi::Rgb(255, 110, 90), false, false);
            if (hoverTab >= 0)
            {
                PeroniaUi::ButtonText(name, ClassNames[hoverTab], PeroniaUi::GoldTextHover, false, false);
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void __cdecl DrawHook()
    {
        float x, y, width, height;
        if (Visible(x, y, width, height))
        {
            DrawShop(x, y, width, height);
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
        GetPrivateProfileStringA("TokenShop", key, "", text, sizeof(text), iniPath);
        return text[0] != '\0' ? static_cast<float>(atof(text)) : fallback;
    }
}

extern "C" __declspec(dllexport) bool StartTokenShop(const TokenShopHost* host)
{
    if (host == nullptr || host->version != TokenShopHostVersion)
    {
        return false;
    }

    g_log = host->log;
    PeroniaUi::Initialize(host->iniPath, host->log);
    g_tabWidth = ReadFloat(host->iniPath, "TabWidth", g_tabWidth);
    g_tabHeight = ReadFloat(host->iniPath, "TabHeight", g_tabHeight);
    g_tabGap = ReadFloat(host->iniPath, "TabGap", g_tabGap);
    g_tabRight = ReadFloat(host->iniPath, "TabRight", g_tabRight);
    g_tabTop = ReadFloat(host->iniPath, "TabTop", g_tabTop);
    g_infoTop = ReadFloat(host->iniPath, "InfoTop", g_infoTop);

    // El reparto de paquetes, el dibujo y el mouse pueden estar redirigidos por otras DLL: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("Tokens: el juego no coincide; solapas desactivadas.");
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
        g_log("Tokens: no se pudieron instalar las solapas.");
        return false;
    }

    g_log("Solapas de los mercaderes de tokens listas (%d solapas de %.0fx%.0f).", TabCount, g_tabWidth, g_tabHeight);
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
