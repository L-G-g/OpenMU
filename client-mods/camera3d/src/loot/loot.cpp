// loot.dll - Ventana de need, greed y pass de las dungeons para el cliente MU 1.04d con camera3d.dll.
//
// En una dungeon de OpenMU (plugin "Dungeon") los drops se reparten con una tirada: cada jugador de la party
// elige need, greed o pass. El servidor (ShowLootRollViewPlugIn) manda paquetes que el
// main.exe original ignora (su reparto de paquetes devuelve 1 para los codigos mayores a 0xF9):
//   - C1 30 FC 00 [tirada, 2 bytes big endian] [segundos] [puede need: 0 o 1] [item, texto de 40 bytes]:
//     empezo una tirada para este jugador.
//   - C1 07 FC 01 [tirada, 2 bytes big endian] [gano: 0 o 1]: el jugador eligio o la tirada termino. Si
//     gano, el servidor le agrega el item al inventario enseguida (C1 22).
// Esta DLL muestra las tiradas abiertas en una ventana chica abajo en el centro de la pantalla (arriba de
// los avisos dorados, del chat y de la barra de abajo; [Loot] Bottom de camera3d.ini), apiladas hacia
// arriba, con el item, el tiempo que queda y los botones Need (apagado si el item no es para la clase),
// Greed y Pass.
// El boton manda el chat "/need N", "/greed N" o "/pass N" (N: la tirada), que el servidor ya entiende.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79) ya puede estar redirigida por healthbars.dll,
//     elites.dll y resets.dll: se lee a donde apunta y se llama a eso. Los paquetes 0xFC se quedan aca.
//   - Dibujo: en la escena principal, despues de la interfaz (0x008609C0) se llama a 0x004D5EC0 entre
//     BeginBitmap y EndBitmap (0x004D9CCC). Esa llamada pasa por DrawHook, que dibuja la ventana y
//     despues llama a la original (lo que dibuja ella, por ejemplo el item agarrado, queda encima).
//   - Mouse: la actualizacion de la interfaz (0x00860970) le pasa los eventos del mouse a las ventanas
//     (0x00815880, llamada en 0x00860998). Esa llamada pasa por MouseHook: con el mouse sobre la ventana
//     no llama a la original (las otras ventanas no lo reciben) y procesa el click (al soltar el boton,
//     0x00790F00). Para que el mundo tampoco lo reciba (caminar, atacar, elegir objetos), se reemplaza
//     0x00860910 (el "mouse sobre la interfaz" que consultan el juego y el dibujo): devuelve 1 con el
//     mouse sobre la ventana y si no hace lo mismo que la original (la ventana con el mouse, +0x38 del
//     administrador de ventanas).
//   - El chat se manda con la funcion del juego (0x005BDE40, la del Enter de la ventana de chat, C1 00).
//     Esa funcion descarta el mensaje si se mando otro hace poco (0x08793728 mayor a 0x32; baja con cada
//     paso de logica): los pedidos esperan en una cola y salen cuando se puede.
//   - Los textos se dibujan con el texto del juego (0x00E8203C: fuente, colores y RenderText 0x00420150).
//   - Item ganado: el juego, al recibir un item en el inventario (0x22, 0x0064B440), muestra "<item>
//     Obtained" con el nombre del item del piso que el jugador quiso levantar por ultima vez (el indice
//     0x081C0420 en la tabla de items del piso 0x081F6C00, 0x350 bytes cada uno: tipo +0x02, nivel en
//     +0x04). Un item ganado no se levanto, asi que el nombre era otro (o el Kris, el item 0,0). Para los
//     0x22 que siguen a las tiradas ganadas (uno por tirada, hasta 10 s despues de la ultima; dos tiradas
//     pueden terminar juntas) se pone el indice en 0 y en ese lugar de la tabla el tipo
//     (0x0058AA80 con los datos del item del paquete) y el nivel del item recibido; despues de llamar al
//     reparto se deja todo como estaba. Asi el mensaje dice el nombre correcto.

#include "../shared.h"

#include <GL/gl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <deque>
#include <string>
#include <vector>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;     // CALL al reparto al recibir cada paquete
    constexpr DWORD DrawCallSite = 0x004D9CCC;         // CALL 0x004D5EC0 despues de dibujar la interfaz
    constexpr DWORD DrawFunction = 0x004D5EC0;
    constexpr DWORD MouseCallSite = 0x00860998;        // CALL 0x00815880: eventos del mouse de las ventanas
    constexpr DWORD MouseFunction = 0x00815880;        // int __thiscall (administrador de ventanas)
    constexpr DWORD MouseOnUiFunction = 0x00860910;    // char __thiscall (sistema de ventanas): mouse sobre la interfaz
    const BYTE MouseOnUiPrologue[] = { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC };
    constexpr DWORD SendChatFunction = 0x005BDE40;     // void __cdecl (const char* texto): C1 00
    constexpr DWORD IsReleaseFunction = 0x00790F00;    // bool __cdecl(int tecla): recien soltada
    constexpr DWORD SetFontFunction = 0x00420120;      // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;   // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool): mezcla con alfa (estado del juego)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl(): sin mezcla (estado del juego)
    constexpr DWORD WindowManagerMouse = 0x38;         // administrador de ventanas: la ventana con el mouse
    constexpr int ChatReady = 0x32;                    // el chat sale si la espera es como mucho esto
    constexpr int SortLeft = 1, SortRight = 2, SortCenter = 3; // RenderText: lugar del texto en la caja

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const MouseX = reinterpret_cast<int*>(0x0879340C); // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);
    constexpr DWORD ItemTypeFunction = 0x0058AA80;    // int __cdecl (const BYTE* datos del item): tipo
    int* const PickedItem = reinterpret_cast<int*>(0x081C0420); // item del piso que se quiso levantar
    BYTE* const GroundItems = reinterpret_cast<BYTE*>(0x081F6C00); // items del piso, 0x350 bytes cada uno
    constexpr DWORD GroundItemType = 0x02, GroundItemLevel = 0x04; // short tipo; int con el nivel en los bits 3 a 6

    // --- Paquetes ---
    constexpr int CodeLoot = 0xFC, CodeItemAdded = 0x22;
    constexpr BYTE SubCodeStarted = 0x00, SubCodeClosed = 0x01;
    constexpr int StartedSize = 48;
    constexpr int ClosedWonSize = 7;
    constexpr int ItemTextSize = 40;
    constexpr int ItemAddedSize = 4 + 11;               // C1 22 [lugar] [item: 11 bytes en OpenMU para S6]
    constexpr BYTE ItemAddedFailed = 0xFF, ItemAddedMoney = 0xFE;
    constexpr DWORD WonItemWait = 10000;                // el item ganado llega enseguida

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using ItemTypeType = int(__cdecl*)(const BYTE* item);
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using IsReleaseType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    // --- Ventana (en coordenadas de 640x480) ---
    constexpr int MaximumRows = 4;
    constexpr float PanelWidth = 230.0f;
    constexpr float DefaultBottom = 300.0f; // arriba de los avisos dorados, del chat y de la barra de abajo
    constexpr float RowHeight = 40.0f;
    constexpr float RowGap = 3.0f;
    constexpr float ButtonTop = 25.0f;   // dentro de la fila
    constexpr float ButtonHeight = 12.0f;
    constexpr float ButtonWidth = 66.0f;
    constexpr float ButtonGap = 7.0f;
    constexpr DWORD ExpiredGrace = 2000; // la fila se va sola un rato despues de terminar el tiempo

    enum Choice
    {
        ChoiceNeed = 0,
        ChoiceGreed = 1,
        ChoicePass = 2,
        ChoiceCount = 3,
    };

    const char* const ChoiceText[ChoiceCount] = { "Need", "Greed", "Pass" };
    const char* const ChoiceCommand[ChoiceCount] = { "/need", "/greed", "/pass" };

    struct Roll
    {
        WORD id;
        bool canNeed;
        DWORD start;    // GetTickCount al llegar
        DWORD duration; // milisegundos
        char item[ItemTextSize + 1];
    };

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    std::vector<Roll> g_rolls;
    std::deque<std::string> g_pendingChat;
    bool g_mouseOver = false;
    int g_wonItemsPending = 0; // tiradas ganadas: los proximos 0x22 son esos items (pueden terminar dos juntas)
    DWORD g_wonTick = 0;
    float g_bottom = DefaultBottom; // [Loot] Bottom: borde de abajo de la fila mas vieja, en 640x480

    float PanelLeft()
    {
        return (640.0f - PanelWidth) / 2;
    }

    int VisibleRows()
    {
        return static_cast<int>(g_rolls.size() < MaximumRows ? g_rolls.size() : MaximumRows);
    }

    // Las filas se apilan hacia arriba desde g_bottom: la 0 (la mas vieja) queda abajo.
    float RowTop(int row)
    {
        return g_bottom - RowHeight - row * (RowHeight + RowGap);
    }

    float ButtonLeft(int choice)
    {
        const float buttons = ChoiceCount * ButtonWidth + (ChoiceCount - 1) * ButtonGap;
        return PanelLeft() + (PanelWidth - buttons) / 2 + choice * (ButtonWidth + ButtonGap);
    }

    bool Inside(float left, float top, float width, float height, float x, float y)
    {
        return x >= left && x < left + width && y >= top && y < top + height;
    }

    bool MouseOverPanel()
    {
        const int rows = VisibleRows();
        return rows > 0 && Inside(PanelLeft(), RowTop(rows - 1), PanelWidth, g_bottom - RowTop(rows - 1),
            static_cast<float>(*MouseX), static_cast<float>(*MouseY));
    }

    // Boton bajo el mouse: fila y eleccion, o false.
    bool HitButton(int& row, int& choice)
    {
        const float x = static_cast<float>(*MouseX);
        const float y = static_cast<float>(*MouseY);
        for (row = 0; row < VisibleRows(); row++)
        {
            for (choice = 0; choice < ChoiceCount; choice++)
            {
                if (Inside(ButtonLeft(choice), RowTop(row) + ButtonTop, ButtonWidth, ButtonHeight, x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void RemoveRoll(WORD id)
    {
        for (size_t i = 0; i < g_rolls.size(); i++)
        {
            if (g_rolls[i].id == id)
            {
                g_rolls.erase(g_rolls.begin() + i);
                return;
            }
        }
    }

    void RemoveExpired()
    {
        const DWORD now = GetTickCount();
        for (size_t i = 0; i < g_rolls.size();)
        {
            if (now - g_rolls[i].start > g_rolls[i].duration + ExpiredGrace)
            {
                g_rolls.erase(g_rolls.begin() + i);
            }
            else
            {
                i++;
            }
        }
    }

    // --- Paquetes ---
    void OnLootPacket(const BYTE* packet, int size)
    {
        if (packet[0] != 0xC1 || size < 6)
        {
            return;
        }

        const WORD id = static_cast<WORD>((packet[4] << 8) | packet[5]);
        if (packet[3] == SubCodeClosed)
        {
            RemoveRoll(id);
            if (size >= ClosedWonSize && packet[6] != 0)
            {
                g_log("Botin: tirada %u ganada.", static_cast<unsigned>(id));
                g_wonItemsPending = GetTickCount() - g_wonTick < WonItemWait ? g_wonItemsPending + 1 : 1;
                g_wonTick = GetTickCount();
            }

            return;
        }

        if (packet[3] != SubCodeStarted || size < StartedSize)
        {
            return;
        }

        RemoveRoll(id);
        Roll roll{};
        roll.id = id;
        roll.duration = packet[6] * 1000u;
        roll.canNeed = packet[7] != 0;
        roll.start = GetTickCount();
        memcpy(roll.item, packet + 8, ItemTextSize);
        roll.item[ItemTextSize] = '\0';
        g_rolls.push_back(roll);
    }

    // El item ganado llega como cualquier item agregado al inventario: el mensaje "<item> Obtained" del juego
    // toma el nombre del item del piso que se quiso levantar. Mientras el juego procesa el paquete, ese item
    // es el recibido (lugar 0 de la tabla); despues todo vuelve a estar como antes.
    int DispatchWonItem(int code, BYTE* packet, int size, int encrypted)
    {
        BYTE* const slot = GroundItems;
        const int savedPicked = *PickedItem;
        const short savedType = *reinterpret_cast<short*>(slot + GroundItemType);
        const int savedLevel = *reinterpret_cast<int*>(slot + GroundItemLevel);
        *PickedItem = 0;
        const int type = reinterpret_cast<ItemTypeType>(ItemTypeFunction)(packet + 4);
        g_log("Botin: item ganado (tipo %d, nivel %d) en el lugar %d.", type, (packet[5] >> 3) & 0xF, packet[3]);
        *reinterpret_cast<short*>(slot + GroundItemType) = static_cast<short>(type);
        *reinterpret_cast<int*>(slot + GroundItemLevel) = packet[5]; // nivel en los bits 3 a 6, como en el piso
        const int result = g_nextDispatch(code, packet, size, encrypted);
        *PickedItem = savedPicked;
        *reinterpret_cast<short*>(slot + GroundItemType) = savedType;
        *reinterpret_cast<int*>(slot + GroundItemLevel) = savedLevel;
        return result;
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (code == CodeLoot && packet != nullptr)
        {
            OnLootPacket(packet, size);
            return 1; // como el juego con los codigos que no conoce
        }

        if (code == CodeItemAdded && g_wonItemsPending > 0 && packet != nullptr)
        {
            g_wonItemsPending--;
            if (GetTickCount() - g_wonTick < WonItemWait && size >= ItemAddedSize
                && packet[3] != ItemAddedFailed && packet[3] != ItemAddedMoney)
            {
                return DispatchWonItem(code, packet, size, encrypted);
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // --- Mouse y chat ---
    void Choose(int row, int choice)
    {
        const Roll& roll = g_rolls[row];
        if (choice == ChoiceNeed && !roll.canNeed)
        {
            return;
        }

        char text[32];
        sprintf_s(text, "%s %u", ChoiceCommand[choice], static_cast<unsigned>(roll.id));
        g_pendingChat.push_back(text);
        g_log("Botin: %s (%s).", text, roll.item);
        g_rolls.erase(g_rolls.begin() + row);
    }

    void SendPendingChat()
    {
        if (!g_pendingChat.empty() && *ChatWait <= ChatReady)
        {
            const std::string text = g_pendingChat.front();
            g_pendingChat.pop_front();
            reinterpret_cast<SendChatType>(SendChatFunction)(text.c_str());
        }
    }

    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        RemoveExpired();
        SendPendingChat();
        g_mouseOver = MouseOverPanel();
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        // El mouse es de esta ventana: ninguna otra lo tiene.
        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        int row;
        int choice;
        if (reinterpret_cast<IsReleaseType>(IsReleaseFunction)(VK_LBUTTON) && HitButton(row, choice))
        {
            Choose(row, choice);
            g_mouseOver = MouseOverPanel();
        }

        return 1;
    }

    // Reemplaza a 0x00860910 (sistema de ventanas: +0 el administrador).
    char __fastcall MouseOnUiHook(BYTE** system, void*)
    {
        if (g_mouseOver)
        {
            return 1;
        }

        const BYTE* manager = *system;
        return manager != nullptr && *reinterpret_cast<const DWORD*>(manager + WindowManagerMouse) != 0 ? 1 : 0;
    }

    // --- Dibujo ---
    float ToPixelsX(float x)
    {
        return x * *WindowWidth / 640.0f;
    }

    // Proyeccion 2D del juego: pixeles con el origen abajo.
    float ToPixelsY(float y)
    {
        return *WindowHeight - y * *WindowHeight / 480.0f;
    }

    void Quad(float left, float top, float right, float bottom)
    {
        glVertex2f(ToPixelsX(left), ToPixelsY(top));
        glVertex2f(ToPixelsX(right), ToPixelsY(top));
        glVertex2f(ToPixelsX(right), ToPixelsY(bottom));
        glVertex2f(ToPixelsX(left), ToPixelsY(bottom));
    }

    void Fill(float left, float top, float right, float bottom, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_QUADS);
        Quad(left, top, right, bottom);
        glEnd();
    }

    void Outline(float left, float top, float right, float bottom, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_LINE_LOOP);
        Quad(left, top, right, bottom);
        glEnd();
    }

    void DrawShapes(int hoverRow, int hoverChoice)
    {
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glLineWidth(1.0f);
        const DWORD now = GetTickCount();
        const float left = PanelLeft();
        const float right = left + PanelWidth;
        for (int row = 0; row < VisibleRows(); row++)
        {
            const Roll& roll = g_rolls[row];
            const float top = RowTop(row);
            Fill(left, top, right, top + RowHeight, 0.0f, 0.0f, 0.0f, 0.8f);
            Outline(left, top, right, top + RowHeight, 0.8f, 0.65f, 0.35f, 0.9f);

            // Tiempo que queda.
            const DWORD elapsed = now - roll.start;
            const float remaining = roll.duration == 0 || elapsed >= roll.duration
                ? 0.0f
                : 1.0f - static_cast<float>(elapsed) / roll.duration;
            Fill(left + 6, top + 18, right - 6, top + 21, 0.2f, 0.18f, 0.15f, 0.9f);
            Fill(left + 6, top + 18, left + 6 + (PanelWidth - 12) * remaining, top + 21,
                remaining > 0.25f ? 0.9f : 0.95f, remaining > 0.25f ? 0.7f : 0.25f, 0.2f, 0.95f);

            for (int choice = 0; choice < ChoiceCount; choice++)
            {
                const bool enabled = choice != ChoiceNeed || roll.canNeed;
                const bool hover = enabled && row == hoverRow && choice == hoverChoice;
                const float x = ButtonLeft(choice);
                const float y = top + ButtonTop;
                if (hover)
                {
                    Fill(x, y, x + ButtonWidth, y + ButtonHeight, 0.45f, 0.32f, 0.12f, 0.95f);
                }
                else
                {
                    Fill(x, y, x + ButtonWidth, y + ButtonHeight, 0.12f, 0.09f, 0.05f, enabled ? 0.95f : 0.6f);
                }

                Outline(x, y, x + ButtonWidth, y + ButtonHeight, 0.6f, 0.5f, 0.3f, enabled ? 0.9f : 0.35f);
            }
        }

        glPopAttrib();
    }

    DWORD Color(BYTE red, BYTE green, BYTE blue)
    {
        return red | (green << 8) | (blue << 16) | 0xFF000000;
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

    void DrawTexts(int hoverRow, int hoverChoice)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr)
        {
            return;
        }

        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        const DWORD savedText = *reinterpret_cast<DWORD*>(renderer + 0x10);
        const DWORD savedBackground = *reinterpret_cast<DWORD*>(renderer + 0x14);
        const DWORD now = GetTickCount();
        const float left = PanelLeft();
        for (int row = 0; row < VisibleRows(); row++)
        {
            const Roll& roll = g_rolls[row];
            const float top = RowTop(row);
            const DWORD elapsed = now - roll.start;
            const DWORD seconds = elapsed >= roll.duration ? 0 : (roll.duration - elapsed + 999) / 1000;
            char time[16];
            sprintf_s(time, "%us", static_cast<unsigned>(seconds));
            Text(roll.item, left + 6, top + 4, PanelWidth - 46, SortLeft, Color(255, 204, 26), true);
            Text(time, left + PanelWidth - 40, top + 4, 34, SortRight, Color(230, 230, 230), false);
            for (int choice = 0; choice < ChoiceCount; choice++)
            {
                const bool enabled = choice != ChoiceNeed || roll.canNeed;
                const DWORD color = !enabled ? Color(110, 100, 90)
                    : row == hoverRow && choice == hoverChoice ? Color(255, 240, 200)
                    : Color(230, 210, 170);
                Text(ChoiceText[choice], ButtonLeft(choice), top + ButtonTop + 1, ButtonWidth, SortCenter, color, false);
            }
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }

    // Sin objetos con destructor: puede usar __try.
    void DrawPanel()
    {
        __try
        {
            int row = -1;
            int choice = -1;
            if (!HitButton(row, choice))
            {
                row = -1;
                choice = -1;
            }

            DrawShapes(row, choice);
            DrawTexts(row, choice);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void __cdecl DrawHook()
    {
        if (!g_rolls.empty())
        {
            DrawPanel();
        }

        g_nextDraw();
    }

    // Destino de un CALL rel32, o 0 si no es un CALL.
    DWORD CallTarget(DWORD callSite)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(callSite);
        return code[0] == 0xE8 ? callSite + 5 + *reinterpret_cast<const DWORD*>(code + 1) : 0;
    }

    bool RedirectCall(DWORD callSite, const void* target)
    {
        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(target) - (callSite + 5);
        return WriteCode(callSite, call, sizeof(call));
    }
}

extern "C" __declspec(dllexport) bool StartLoot(const LootHost* host)
{
    if (host == nullptr || host->version != LootHostVersion)
    {
        return false;
    }

    g_log = host->log;
    char bottom[32];
    GetPrivateProfileStringA("Loot", "Bottom", "", bottom, sizeof(bottom), host->iniPath);
    if (bottom[0] != '\0')
    {
        g_bottom = static_cast<float>(atof(bottom));
    }

    // El reparto de paquetes puede estar redirigido por otras DLL: se encadena a lo que haya. El dibujo y el
    // mouse no los toca ninguna otra: tienen que ser las llamadas originales.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0 || CallTarget(DrawCallSite) != DrawFunction || CallTarget(MouseCallSite) != MouseFunction
        || memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0)
    {
        g_log("Botin: el juego no coincide; desactivado.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    g_nextDraw = reinterpret_cast<DrawType>(DrawFunction);
    g_nextMouse = reinterpret_cast<MouseType>(MouseFunction);
    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&MouseOnUiHook) - (MouseOnUiFunction + 5);
    if (!WriteCode(MouseOnUiFunction, jump, sizeof(jump)) || !RedirectCall(MouseCallSite, &MouseHook)
        || !RedirectCall(DrawCallSite, &DrawHook) || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Botin: no se pudo instalar.");
        return false;
    }

    g_log("Ventana de botin lista.");
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
