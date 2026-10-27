// espectros.dll - Ventana de espectros y espectros transparentes para el cliente MU 1.04d con camera3d.dll.
//
// En OpenMU (plugin "Spectres") cada personaje puede invocar espectros de guerrero, mago y elfa: bots que lo
// siguen, lo buffean y cazan con el. El servidor (ShowSpectresViewPlugIn) manda paquetes que el main.exe original
// ignora (su reparto de paquetes devuelve 1 para los codigos mayores a 0xF9):
//   - C1 05 FD 01 [objeto, 2 bytes big endian]: ese objeto es un espectro. Se repite cada 2 segundos mientras
//     esta a la vista.
//   - C2 [largo] FD 00 y una entrada por espectro (guerrero, mago, elfa): el estado de los espectros del jugador
//     (invocado, nombre, nivel, resets, puntos, estadisticas, porcentajes y los 12 items equipados). Llega al
//     invocar, retirar, equipar, quitar, cambiar porcentajes, resetear y subir de nivel, y al pedirlo.
// La ventana contesta con el comando de chat /espectro, que el servidor ya entiende.
//
// La ventana (Shift+S) muestra una pestana por espectro: invocar o retirar, resetear, los items equipados (un click
// lo quita y lo devuelve al inventario), las estadisticas y los porcentajes con los que el espectro reparte sus
// puntos (+ y -, y Aplicar cuando suman 100). Con la ventana abierta, un click derecho sobre un item equipable del
// inventario se lo equipa al espectro de la pestana elegida.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79), el dibujo despues de la interfaz (0x004D9CCC) y la llamada a
//     los eventos del mouse de las ventanas (0x00860998) ya pueden estar redirigidos por otras DLL (por ejemplo
//     loot.dll): se lee a donde apuntan y se llama a eso. El "mouse sobre la interfaz" (0x00860910) tambien puede
//     estar reemplazado por un salto (loot.dll): se salta a eso, o si no se hace lo mismo que el original.
//   - Teclado: camera3d.dll avisa en cada paso de logica, despues de que el juego actualiza su tabla de teclas
//     (GetAsyncKeyState de las 256). Shift+S abre o cierra la ventana y Esc la cierra; la tecla se borra de la
//     tabla mientras siga apretada, asi ninguna ventana la ve (S sola, con el inventario abierto, abre la tienda
//     personal). Solo si el foco es la ventana principal del juego (0x00E8C578): escribiendo en el chat, el foco
//     es la caja del chat.
//   - Items: se dibujan en 3D con la funcion del juego (0x005CF310, la del item agarrado con el mouse), con la misma
//     preparacion que hace el juego para ese item (0x004D5C60): matrices, vista y perspectiva propias.
//   - Transparencia: al empezar cada cuadro (camera3d.dll) el alfa de los espectros a la vista (OBJECT +0x98, el
//     que el juego usa para desvanecer objetos) se pone en [Espectros] Alpha; despues de la interfaz se vuelve a
//     poner el que tenian.
//   - Click derecho: camera3d.dll pregunta antes de equipar un item del inventario (filtro).
//   - El chat y los textos, como loot.dll.

#include "../shared.h"

#include <GL/gl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <deque>
#include <string>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;     // CALL al reparto al recibir cada paquete
    constexpr DWORD DrawCallSite = 0x004D9CCC;         // CALL 0x004D5EC0 despues de dibujar la interfaz
    constexpr DWORD MouseCallSite = 0x00860998;        // CALL 0x00815880: eventos del mouse de las ventanas
    constexpr DWORD MouseOnUiFunction = 0x00860910;    // char __thiscall (sistema de ventanas): mouse sobre la interfaz
    const BYTE MouseOnUiPrologue[] = { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC };
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    constexpr BYTE KeyUp = 0, KeyPressed = 2, KeyHeld = 3; // estados de la tabla de teclas
    constexpr DWORD SendChatFunction = 0x005BDE40;     // void __cdecl (const char* texto): C1 00
    constexpr DWORD IsReleaseFunction = 0x00790F00;    // bool __cdecl(int tecla): recien soltada
    constexpr DWORD SetFontFunction = 0x00420120;      // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;   // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool): mezcla con alfa (estado del juego)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl(): sin mezcla (estado del juego)
    constexpr DWORD RenderItem3DFunction = 0x005CF310; // void __cdecl (x, y, ancho, alto, tipo, nivel, exc, ancient, agarrado)
    constexpr DWORD ViewportFunction = 0x006363D0;     // void __cdecl (x, y, ancho, alto)
    constexpr DWORD PerspectiveFunction = 0x006358A0;  // void __cdecl (fov, aspecto, cerca, lejos)
    constexpr DWORD CameraRotationFunction = 0x00635830; // void __cdecl (float* angulos)
    constexpr DWORD EnableDepthTestFunction = 0x00635DE0; // void __cdecl()
    constexpr DWORD EnableDepthMaskFunction = 0x00635E40; // void __cdecl()
    constexpr DWORD CameraMatrixFunction = 0x00636720; // void __cdecl(): vuelve a poner la camara
    float* const CameraAngles = reinterpret_cast<float*>(0x087933A0);
    float* const NearPlane = reinterpret_cast<float*>(0x00E61E38);
    float* const FarPlane = reinterpret_cast<float*>(0x00E61E3C);
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
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // personajes: [+8], 400 de 0x598 bytes

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr DWORD CharKey = 0x7E, CharObject = 0x308, CharLive = 0x30C;
    constexpr DWORD ObjAlpha = 0x98;
    constexpr WORD IdMask = 0x7FFF;

    // --- Paquetes ---
    constexpr int CodeSpectres = 0xFD;
    constexpr BYTE SubCodePanel = 0x00, SubCodeSpectre = 0x01;
    constexpr int KindCount = 3;
    constexpr int SlotCount = 12;
    constexpr int NameLength = 11;
    constexpr int ItemDataLength = 12;
    constexpr int DescriptionLength = 40;
    constexpr int SlotLength = 6 + ItemDataLength + DescriptionLength;
    constexpr int EntryHeaderLength = 41;
    constexpr int EntryLength = EntryHeaderLength + SlotCount * SlotLength;
    constexpr DWORD MarkDuration = 5000; // la marca se repite cada 2 segundos

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using KeyStateType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);
    using RenderItem3DType = void(__cdecl*)(float x, float y, float width, float height, int type, int level,
        int excellent, int ancient, bool picked);

    // --- Estado ---
    struct Item
    {
        bool present;
        int type;
        BYTE data[ItemDataLength];
        char description[DescriptionLength + 1];
    };

    struct Spectre
    {
        bool summoned;
        char name[NameLength + 1];
        int level;
        int resets;
        int points;
        int stats[4];
        int percentages[4];
        Item items[SlotCount];
    };

    const char* const KindName[KindCount] = { "Guerrero", "Mago", "Elfa" };
    const char* const KindCommand[KindCount] = { "g", "m", "e" };
    const char* const StatName[4] = { "FUE", "AGI", "VIT", "ENE" };

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr; // otra DLL ya reemplazo 0x00860910 (o nullptr)
    int g_suppressedKey = 0;                 // tecla que se borra de la tabla hasta que se suelte
    std::deque<std::string> g_pendingChat;
    bool g_open = false;
    bool g_mouseOver = false;
    bool g_received = false;
    int g_tab = 0;
    Spectre g_spectres[KindCount] = {};
    int g_edit[KindCount][4] = {};   // porcentajes que el jugador esta cambiando
    bool g_editing[KindCount] = {};  // los cambio y todavia no los aplico
    DWORD g_marked[IdMask + 1] = {}; // GetTickCount de la ultima marca de espectro de cada objeto
    float g_alpha = 0.5f;            // [Espectros] Alpha
    float g_left = 10.0f;            // [Espectros] Left, Top
    float g_top = 40.0f;

    struct SavedAlpha
    {
        BYTE* object;
        float alpha;
    };

    SavedAlpha g_savedAlpha[CharacterCount];
    int g_savedAlphaCount = 0;

    // --- Ventana (en coordenadas de 640x480, relativas a la esquina de la ventana) ---
    constexpr float PanelWidth = 200.0f;
    constexpr float PanelHeight = 340.0f;

    struct Rect
    {
        float left;
        float top;
        float width;
        float height;
    };

    // Casilleros de equipo: 0 arma, 1 escudo, 2 casco, 3 armadura, 4 pantalones, 5 guantes, 6 botas, 7 alas,
    // 8 mascota, 9 colgante, 10 y 11 anillos.
    constexpr float EquipTop = 78.0f;
    const Rect SlotRect[SlotCount] = {
        { 8, EquipTop + 46, 40, 62 },    // arma
        { 152, EquipTop + 46, 40, 62 },  // escudo
        { 80, EquipTop, 40, 40 },        // casco
        { 72, EquipTop + 46, 56, 62 },   // armadura
        { 80, EquipTop + 114, 40, 40 },  // pantalones
        { 8, EquipTop + 114, 40, 40 },   // guantes
        { 152, EquipTop + 114, 40, 40 }, // botas
        { 136, EquipTop, 56, 40 },       // alas
        { 8, EquipTop, 34, 34 },         // mascota
        { 48, EquipTop + 8, 26, 26 },    // colgante
        { 52, EquipTop + 124, 22, 22 },  // anillo
        { 126, EquipTop + 124, 22, 22 }, // anillo
    };

    constexpr float TabTop = 20.0f, TabHeight = 14.0f, TabWidth = 60.0f, TabGap = 4.0f;
    constexpr float ActionTop = 56.0f, ActionHeight = 14.0f, ActionWidth = 70.0f;
    constexpr float StatsTop = EquipTop + 162, StatRowHeight = 16.0f;
    constexpr float SmallButton = 14.0f;
    constexpr float MinusLeft = 104.0f, PlusLeft = 158.0f, PercentLeft = 120.0f, PercentWidth = 36.0f;
    constexpr float ApplyLeft = 120.0f, ApplyWidth = 72.0f;
    constexpr float HintTop = PanelHeight - 18.0f;
    constexpr int PercentStep = 5;

    enum Button
    {
        ButtonNone = -1,
        ButtonTab = 0,         // + pestana
        ButtonSummon = 10,
        ButtonReset = 11,
        ButtonSlot = 20,       // + casillero
        ButtonMinus = 40,      // + estadistica
        ButtonPlus = 50,       // + estadistica
        ButtonApply = 60,
        ButtonClose = 61,
    };

    Rect TabRect(int tab)
    {
        const float tabs = KindCount * TabWidth + (KindCount - 1) * TabGap;
        return { (PanelWidth - tabs) / 2 + tab * (TabWidth + TabGap), TabTop, TabWidth, TabHeight };
    }

    Rect ButtonRect(int button)
    {
        if (button >= ButtonTab && button < ButtonTab + KindCount)
        {
            return TabRect(button - ButtonTab);
        }

        if (button >= ButtonSlot && button < ButtonSlot + SlotCount)
        {
            return SlotRect[button - ButtonSlot];
        }

        if (button >= ButtonMinus && button < ButtonMinus + 4)
        {
            return { MinusLeft, StatsTop + (button - ButtonMinus) * StatRowHeight, SmallButton, SmallButton };
        }

        if (button >= ButtonPlus && button < ButtonPlus + 4)
        {
            return { PlusLeft, StatsTop + (button - ButtonPlus) * StatRowHeight, SmallButton, SmallButton };
        }

        switch (button)
        {
        case ButtonSummon: return { PanelWidth / 2 - ActionWidth - 4, ActionTop, ActionWidth, ActionHeight };
        case ButtonReset: return { PanelWidth / 2 + 4, ActionTop, ActionWidth, ActionHeight };
        case ButtonApply: return { ApplyLeft, StatsTop + 4 * StatRowHeight + 2, ApplyWidth, SmallButton };
        case ButtonClose: return { PanelWidth - 16, 4, 12, 12 };
        default: return { 0, 0, 0, 0 };
        }
    }

    bool Inside(const Rect& rect, float x, float y)
    {
        return x >= rect.left && x < rect.left + rect.width && y >= rect.top && y < rect.top + rect.height;
    }

    float LocalMouseX()
    {
        return static_cast<float>(*MouseX) - g_left;
    }

    float LocalMouseY()
    {
        return static_cast<float>(*MouseY) - g_top;
    }

    bool MouseOverPanel()
    {
        return g_open && Inside({ 0, 0, PanelWidth, PanelHeight }, LocalMouseX(), LocalMouseY());
    }

    int EditTotal(int kind)
    {
        return g_edit[kind][0] + g_edit[kind][1] + g_edit[kind][2] + g_edit[kind][3];
    }

    bool IsEnabled(int button)
    {
        const Spectre& spectre = g_spectres[g_tab];
        if (button >= ButtonSlot && button < ButtonSlot + SlotCount)
        {
            return spectre.summoned && spectre.items[button - ButtonSlot].present;
        }

        if (button >= ButtonMinus && button < ButtonMinus + 4)
        {
            return spectre.summoned && g_edit[g_tab][button - ButtonMinus] >= PercentStep;
        }

        if (button >= ButtonPlus && button < ButtonPlus + 4)
        {
            return spectre.summoned && g_edit[g_tab][button - ButtonPlus] + PercentStep <= 100;
        }

        switch (button)
        {
        case ButtonReset: return spectre.summoned;
        case ButtonApply: return spectre.summoned && g_editing[g_tab] && EditTotal(g_tab) == 100;
        default: return true;
        }
    }

    // Boton bajo el mouse (los que se dibujan en la pestana actual), o ButtonNone.
    int HitButton()
    {
        static const int Buttons[] = {
            ButtonTab, ButtonTab + 1, ButtonTab + 2, ButtonSummon, ButtonReset, ButtonSlot, ButtonSlot + 1,
            ButtonSlot + 2, ButtonSlot + 3, ButtonSlot + 4, ButtonSlot + 5, ButtonSlot + 6, ButtonSlot + 7,
            ButtonSlot + 8, ButtonSlot + 9, ButtonSlot + 10, ButtonSlot + 11, ButtonMinus, ButtonMinus + 1,
            ButtonMinus + 2, ButtonMinus + 3, ButtonPlus, ButtonPlus + 1, ButtonPlus + 2, ButtonPlus + 3,
            ButtonApply, ButtonClose,
        };

        if (!MouseOverPanel())
        {
            return ButtonNone;
        }

        const float x = LocalMouseX();
        const float y = LocalMouseY();
        for (int button : Buttons)
        {
            if (Inside(ButtonRect(button), x, y))
            {
                return button;
            }
        }

        return ButtonNone;
    }

    // --- Chat ---
    void QueueChat(const char* text)
    {
        g_pendingChat.push_back(text);
        g_log("Espectros: %s", text);
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

    void SetOpen(bool open)
    {
        g_open = open;
        g_mouseOver = MouseOverPanel();
        if (open)
        {
            QueueChat("/espectro panel");
        }
    }

    // --- Paquetes ---
    int ReadInt(const BYTE* data)
    {
        return data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
    }

    int ReadShort(const BYTE* data)
    {
        return data[0] | (data[1] << 8);
    }

    void ReadEntry(const BYTE* entry)
    {
        const int kind = entry[0];
        if (kind < 0 || kind >= KindCount)
        {
            return;
        }

        Spectre& spectre = g_spectres[kind];
        spectre.summoned = entry[1] != 0;
        memcpy(spectre.name, entry + 2, NameLength);
        spectre.name[NameLength] = '\0';
        spectre.level = ReadShort(entry + 13);
        spectre.resets = ReadShort(entry + 15);
        spectre.points = ReadInt(entry + 17);
        for (int i = 0; i < 4; i++)
        {
            spectre.stats[i] = ReadInt(entry + 21 + i * 4);
            spectre.percentages[i] = entry[37 + i];
        }

        for (int slot = 0; slot < SlotCount; slot++)
        {
            const BYTE* data = entry + EntryHeaderLength + slot * SlotLength;
            Item& item = spectre.items[slot];
            item.present = data[0] != 0;
            item.type = ReadShort(data + 1);
            memcpy(item.data, data + 6, ItemDataLength);
            memcpy(item.description, data + 6 + ItemDataLength, DescriptionLength);
            item.description[DescriptionLength] = '\0';
        }

        if (!g_editing[kind])
        {
            memcpy(g_edit[kind], spectre.percentages, sizeof(g_edit[kind]));
        }
    }

    void OnSpectresPacket(const BYTE* packet, int size)
    {
        if (packet[0] == 0xC1 && size >= 6 && packet[3] == SubCodeSpectre)
        {
            g_marked[((packet[4] << 8) | packet[5]) & IdMask] = GetTickCount();
            return;
        }

        if (packet[0] != 0xC2 || size < 5 || packet[4] != SubCodePanel)
        {
            return;
        }

        for (int offset = 5; offset + EntryLength <= size; offset += EntryLength)
        {
            ReadEntry(packet + offset);
        }

        g_received = true;
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (code == CodeSpectres && packet != nullptr)
        {
            OnSpectresPacket(packet, size);
            return 1; // como el juego con los codigos que no conoce
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // --- Transparencia ---
    BYTE* Characters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    void RestoreAlpha()
    {
        for (int i = 0; i < g_savedAlphaCount; i++)
        {
            *reinterpret_cast<float*>(g_savedAlpha[i].object + ObjAlpha) = g_savedAlpha[i].alpha;
        }

        g_savedAlphaCount = 0;
    }

    void ApplyAlpha()
    {
        RestoreAlpha();
        BYTE* characters = Characters();
        if (characters == nullptr)
        {
            return;
        }

        const DWORD now = GetTickCount();
        for (int i = 0; i < CharacterCount; i++)
        {
            BYTE* character = characters + i * CharacterSize;
            const WORD id = *reinterpret_cast<const WORD*>(character + CharKey) & IdMask;
            if (!character[CharLive] || g_marked[id] == 0 || now - g_marked[id] > MarkDuration)
            {
                continue;
            }

            BYTE* object = character + CharObject;
            float* alpha = reinterpret_cast<float*>(object + ObjAlpha);
            g_savedAlpha[g_savedAlphaCount++] = { object, *alpha };
            if (*alpha > g_alpha)
            {
                *alpha = g_alpha;
            }
        }
    }

    // Sin objetos con destructor: puede usar __try.
    void __cdecl BeforeRender()
    {
        __try
        {
            ApplyAlpha();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_savedAlphaCount = 0;
        }
    }

    // --- Mouse y teclado ---
    void Click(int button)
    {
        if (button == ButtonNone || !IsEnabled(button))
        {
            return;
        }

        char text[96];
        const char* kind = KindCommand[g_tab];
        if (button >= ButtonTab && button < ButtonTab + KindCount)
        {
            g_tab = button - ButtonTab;
        }
        else if (button >= ButtonSlot && button < ButtonSlot + SlotCount)
        {
            sprintf_s(text, "/espectro quitar %s %d", kind, button - ButtonSlot);
            QueueChat(text);
        }
        else if (button >= ButtonMinus && button < ButtonMinus + 4)
        {
            g_edit[g_tab][button - ButtonMinus] -= PercentStep;
            g_editing[g_tab] = true;
        }
        else if (button >= ButtonPlus && button < ButtonPlus + 4)
        {
            g_edit[g_tab][button - ButtonPlus] += PercentStep;
            g_editing[g_tab] = true;
        }
        else if (button == ButtonSummon)
        {
            sprintf_s(text, "/espectro %s", kind);
            QueueChat(text);
        }
        else if (button == ButtonReset)
        {
            sprintf_s(text, "/espectro reset %s", kind);
            QueueChat(text);
        }
        else if (button == ButtonApply)
        {
            const int* edit = g_edit[g_tab];
            sprintf_s(text, "/espectro puntos %s %d %d %d %d", kind, edit[0], edit[1], edit[2], edit[3]);
            QueueChat(text);
            g_editing[g_tab] = false;
        }
        else if (button == ButtonClose)
        {
            SetOpen(false);
        }
    }

    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingChat();
        g_mouseOver = MouseOverPanel();
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        // El mouse es de esta ventana: ninguna otra lo tiene.
        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<KeyStateType>(IsReleaseFunction)(VK_LBUTTON))
        {
            Click(HitButton());
            g_mouseOver = MouseOverPanel();
        }

        return 1;
    }

    // Reemplaza a 0x00860910 (sistema de ventanas: +0 el administrador).
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

        if (GetFocus() != *MainWindow)
        {
            return; // escribiendo en el chat (o el juego no tiene el foco)
        }

        if (keyStates['S'] == KeyPressed && IsDown(keyStates, VK_SHIFT))
        {
            g_suppressedKey = 'S';
            SetOpen(!g_open);
        }
        else if (g_open && keyStates[VK_ESCAPE] == KeyPressed)
        {
            g_suppressedKey = VK_ESCAPE;
            SetOpen(false);
        }
        else
        {
            return;
        }

        keyStates[g_suppressedKey] = KeyUp;
    }

    bool RightClickHook(int inventorySlot)
    {
        if (!g_open)
        {
            return false;
        }

        if (!g_spectres[g_tab].summoned)
        {
            return true; // con la ventana abierta el click es para el espectro: no se equipa al personaje
        }

        char text[64];
        sprintf_s(text, "/espectro equipar %s %d", KindCommand[g_tab], inventorySlot);
        QueueChat(text);
        return true;
    }

    // --- Dibujo ---
    float ToPixelsX(float x)
    {
        return (g_left + x) * *WindowWidth / 640.0f;
    }

    // Proyeccion 2D del juego: pixeles con el origen abajo.
    float ToPixelsY(float y)
    {
        return *WindowHeight - (g_top + y) * *WindowHeight / 480.0f;
    }

    void Quad(const Rect& rect)
    {
        glVertex2f(ToPixelsX(rect.left), ToPixelsY(rect.top));
        glVertex2f(ToPixelsX(rect.left + rect.width), ToPixelsY(rect.top));
        glVertex2f(ToPixelsX(rect.left + rect.width), ToPixelsY(rect.top + rect.height));
        glVertex2f(ToPixelsX(rect.left), ToPixelsY(rect.top + rect.height));
    }

    void Fill(const Rect& rect, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_QUADS);
        Quad(rect);
        glEnd();
    }

    void Outline(const Rect& rect, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_LINE_LOOP);
        Quad(rect);
        glEnd();
    }

    void DrawButton(int button, int hover, bool selected)
    {
        const Rect rect = ButtonRect(button);
        const bool enabled = IsEnabled(button);
        if (selected || (enabled && button == hover))
        {
            Fill(rect, 0.45f, 0.32f, 0.12f, 0.95f);
        }
        else
        {
            Fill(rect, 0.12f, 0.09f, 0.05f, enabled ? 0.95f : 0.6f);
        }

        Outline(rect, 0.6f, 0.5f, 0.3f, enabled ? 0.9f : 0.35f);
    }

    void DrawShapes(int hover)
    {
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glLineWidth(1.0f);
        const Rect panel = { 0, 0, PanelWidth, PanelHeight };
        Fill(panel, 0.0f, 0.0f, 0.0f, 0.85f);
        Outline(panel, 0.8f, 0.65f, 0.35f, 0.9f);
        for (int tab = 0; tab < KindCount; tab++)
        {
            DrawButton(ButtonTab + tab, hover, tab == g_tab);
        }

        DrawButton(ButtonClose, hover, false);
        DrawButton(ButtonSummon, hover, false);
        DrawButton(ButtonReset, hover, false);
        const Spectre& spectre = g_spectres[g_tab];
        for (int slot = 0; slot < SlotCount; slot++)
        {
            const Rect& rect = SlotRect[slot];
            const bool hovered = hover == ButtonSlot + slot && IsEnabled(hover);
            Fill(rect, hovered ? 0.25f : 0.08f, hovered ? 0.18f : 0.07f, hovered ? 0.08f : 0.06f, 0.95f);
            Outline(rect, 0.5f, 0.42f, 0.28f, spectre.summoned ? 0.8f : 0.35f);
        }

        if (spectre.summoned)
        {
            for (int stat = 0; stat < 4; stat++)
            {
                DrawButton(ButtonMinus + stat, hover, false);
                DrawButton(ButtonPlus + stat, hover, false);
            }

            DrawButton(ButtonApply, hover, false);
        }

        glPopAttrib();
    }

    // Los items equipados, en 3D, como el item agarrado con el mouse (0x004D5C60).
    void DrawItems()
    {
        const Spectre& spectre = g_spectres[g_tab];
        if (!spectre.summoned)
        {
            return;
        }

        GLfloat modelView[16];
        glGetFloatv(GL_MODELVIEW_MATRIX, modelView);
        glMatrixMode(GL_PROJECTION);
        glPushMatrix();
        glLoadIdentity();
        reinterpret_cast<void(__cdecl*)(int, int, int, int)>(ViewportFunction)(0, 0, *WindowWidth, *WindowHeight);
        reinterpret_cast<void(__cdecl*)(float, float, float, float)>(PerspectiveFunction)(
            1.0f, static_cast<float>(*WindowWidth) / static_cast<float>(*WindowHeight), *NearPlane, *FarPlane);
        glMatrixMode(GL_MODELVIEW);
        glPushMatrix();
        glLoadIdentity();
        reinterpret_cast<void(__cdecl*)(float*)>(CameraRotationFunction)(CameraAngles);
        reinterpret_cast<void(__cdecl*)()>(EnableDepthTestFunction)();
        reinterpret_cast<void(__cdecl*)()>(EnableDepthMaskFunction)();
        glClear(GL_DEPTH_BUFFER_BIT);
        for (int slot = 0; slot < SlotCount; slot++)
        {
            const Item& item = spectre.items[slot];
            if (!item.present)
            {
                continue;
            }

            const Rect& rect = SlotRect[slot];
            reinterpret_cast<RenderItem3DType>(RenderItem3DFunction)(g_left + rect.left, g_top + rect.top, rect.width,
                rect.height, item.type, item.data[1], item.data[3] & 0x3F, item.data[4], true);
        }

        glMatrixMode(GL_PROJECTION);
        glPopMatrix();
        glMatrixMode(GL_MODELVIEW);
        glPopMatrix();
        reinterpret_cast<void(__cdecl*)()>(CameraMatrixFunction)();
        glLoadMatrixf(modelView);
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
        reinterpret_cast<RenderTextType>(RenderTextFunction)(RenderText, nullptr, static_cast<int>(g_left + left),
            static_cast<int>(g_top + top), text, static_cast<int>(width), 0, sort, nullptr);
    }

    void ButtonText(int button, const char* text, int hover)
    {
        const Rect rect = ButtonRect(button);
        const bool enabled = IsEnabled(button);
        const DWORD color = !enabled ? Color(110, 100, 90)
            : button == hover ? Color(255, 240, 200)
            : Color(230, 210, 170);
        Text(text, rect.left, rect.top + 1, rect.width, SortCenter, color, false);
    }

    void DrawTexts(int hover)
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
        const DWORD gold = Color(255, 204, 26);
        const DWORD white = Color(230, 230, 230);
        const DWORD gray = Color(150, 140, 130);
        const DWORD green = Color(120, 230, 120);
        Text("Espectros", 0, 4, PanelWidth, SortCenter, gold, true);
        ButtonText(ButtonClose, "x", hover);
        for (int tab = 0; tab < KindCount; tab++)
        {
            const Rect rect = TabRect(tab);
            Text(KindName[tab], rect.left, rect.top + 1, rect.width, SortCenter,
                g_spectres[tab].summoned ? green : tab == g_tab ? Color(255, 240, 200) : Color(230, 210, 170), false);
        }

        const Spectre& spectre = g_spectres[g_tab];
        char text[96];
        if (!g_received)
        {
            Text("Esperando al servidor...", 0, 40, PanelWidth, SortCenter, gray, false);
        }
        else if (spectre.summoned)
        {
            sprintf_s(text, "%s  Nv %d  Resets %d", spectre.name, spectre.level, spectre.resets);
            Text(text, 0, 40, PanelWidth, SortCenter, white, true);
        }
        else
        {
            Text("No invocado", 0, 40, PanelWidth, SortCenter, gray, false);
        }

        ButtonText(ButtonSummon, spectre.summoned ? "Retirar" : "Invocar", hover);
        ButtonText(ButtonReset, "Reset", hover);
        if (spectre.summoned)
        {
            for (int stat = 0; stat < 4; stat++)
            {
                const float top = StatsTop + stat * StatRowHeight;
                sprintf_s(text, "%s %d", StatName[stat], spectre.stats[stat]);
                Text(text, 8, top + 1, 90, SortLeft, white, false);
                sprintf_s(text, "%d%%", g_edit[g_tab][stat]);
                Text(text, PercentLeft, top + 1, PercentWidth, SortCenter,
                    g_edit[g_tab][stat] != spectre.percentages[stat] ? gold : white, false);
                ButtonText(ButtonMinus + stat, "-", hover);
                ButtonText(ButtonPlus + stat, "+", hover);
            }

            const float top = StatsTop + 4 * StatRowHeight + 2;
            const int total = EditTotal(g_tab);
            sprintf_s(text, "Total %d%%", total);
            Text(text, 8, top + 1, 100, SortLeft, total == 100 ? white : Color(255, 90, 90), false);
            ButtonText(ButtonApply, "Aplicar", hover);
            if (spectre.points > 0)
            {
                sprintf_s(text, "Puntos sin usar: %d", spectre.points);
                Text(text, 8, top + 17, PanelWidth - 16, SortLeft, gray, false);
            }
        }

        // Ayuda: el item bajo el mouse o como equipar.
        const char* hint = spectre.summoned ? "Click derecho en tu inventario: equipar" : "Invoca al espectro para equiparlo";
        DWORD hintColor = gray;
        if (hover >= ButtonSlot && hover < ButtonSlot + SlotCount && spectre.items[hover - ButtonSlot].present)
        {
            sprintf_s(text, "%s (click: quitar)", spectre.items[hover - ButtonSlot].description);
            hint = text;
            hintColor = gold;
        }

        Text(hint, 6, HintTop, PanelWidth - 12, SortCenter, hintColor, false);
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
            const int hover = HitButton();
            DrawShapes(hover);
            DrawItems();
            DrawTexts(hover);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void RestoreAlphaSafely()
    {
        __try
        {
            RestoreAlpha();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_savedAlphaCount = 0;
        }
    }

    void __cdecl DrawHook()
    {
        RestoreAlphaSafely();
        if (g_open)
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

    // Destino de un JMP rel32, o 0 si no es un JMP.
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
        GetPrivateProfileStringA("Espectros", key, "", text, sizeof(text), iniPath);
        return text[0] != '\0' ? static_cast<float>(atof(text)) : fallback;
    }
}

extern "C" __declspec(dllexport) bool StartEspectros(const EspectrosHost* host)
{
    if (host == nullptr || host->version != EspectrosHostVersion)
    {
        return false;
    }

    g_log = host->log;
    g_alpha = ReadFloat(host->iniPath, "Alpha", g_alpha);
    g_left = ReadFloat(host->iniPath, "Left", g_left);
    g_top = ReadFloat(host->iniPath, "Top", g_top);

    // El reparto de paquetes, el dibujo y el mouse pueden estar redirigidos por otras DLL: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("Espectros: el juego no coincide; desactivado.");
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
        g_log("Espectros: no se pudo instalar.");
        return false;
    }

    host->setBeforeRender(&BeforeRender);
    host->setRightClickFilter(&RightClickHook);
    host->setKeyTick(&KeyTick);
    g_log("Espectros listos (Shift+S, alfa %.2f).", g_alpha);
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
