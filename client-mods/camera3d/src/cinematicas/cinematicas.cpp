// cinematicas.dll - Escenas de dialogo con los espectros del refugio, para el cliente MU 1.04d con camera3d.dll.
//
// En el refugio de los espectros (OpenMU, plugin "Spectres") ya no hay duelo por cercania: el jugador hace click sobre
// un espectro rival y empieza una escena. La pantalla se va a negro, la camara pasa a primera persona mirando al
// espectro, vuelve la imagen y el espectro cuenta su parte de la historia en una caja de dialogo, con los botones
// "Duelo" (si el servidor dice que se puede) y "Volver mas tarde". Al elegir, la pantalla se va a negro otra vez y la
// camara vuelve a la normal.
//
// Paquetes del servidor (ShowSpectresViewPlugIn), que el main.exe original ignora:
//   - C1 06 FD 05 [objeto, 2 bytes big endian]: ese objeto es un rival del refugio del jugador. Se repite cada pocos
//     segundos mientras esta a la vista. Un click sobre el manda /espectro hablar [objeto].
//   - C2 [largo] FD 04 [objeto, 2 bytes big endian] [se puede pelear, 0 o 1] [clase] y tres textos (Latin-1, cada uno
//     termina en 0): el nombre del rival, lo que dice y por que no se puede pelear ahora (vacio si se puede). Empieza
//     la escena. "Duelo" manda /espectro duelo [objeto].
// Los demas paquetes FD siguen a espectros.dll.
//
// El modo historia (OpenMU, plugin "Story mode") usa la misma escena con cualquier NPC:
//   - C2 [largo] FF 30 [objeto, 2 bytes big endian] [paginas] [botones] y los textos (Latin-1, cada uno termina en 0):
//     el nombre del NPC, una nota (puede estar vacia), las paginas y, por cada boton, [id] y su texto. Las paginas se
//     pasan con "Continuar" (o un click en la caja); los botones salen en la ultima. Un boton con id mayor a 0 manda
//     /historia [id] [objeto]; el id 0 solo cierra la escena. Los demas paquetes FF siguen a quests.dll y las otras.
//
// Como funciona:
//   - El reparto de paquetes (0x0065FD79), el dibujo despues de la interfaz (0x004D9CCC), los eventos del mouse de las
//     ventanas (0x00860998) y el "mouse sobre la interfaz" (0x00860910) se encadenan a lo que ya haya (espectros.dll,
//     loot.dll), igual que hacen ellas.
//   - La camara: camera3d.dll llama a esta DLL despues de calcular su camara en cada cuadro (host->setCameraOverride);
//     durante la escena se pone la posicion y los angulos de la primera persona.
//   - El click: camera3d.dll avisa en cada paso de logica, despues de que el juego actualiza su tabla de teclas. Si se
//     aprieta el boton izquierdo con el mouse sobre un rival marcado (el personaje elegido bajo el mouse, 0x00E61730),
//     se manda /espectro hablar y el personaje no ve el click (no camina ni ataca).
//   - Durante la escena el personaje propio no se dibuja (byte Visible en 0) y el mouse es de la caja de dialogo.

#include "../shared.h"

#include <GL/gl.h>
#include <math.h>
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
    constexpr DWORD DrawCallSite = 0x004D9CCC;         // CALL despues de dibujar la interfaz
    constexpr DWORD MouseCallSite = 0x00860998;        // CALL: eventos del mouse de las ventanas
    constexpr DWORD MouseOnUiFunction = 0x00860910;    // char __thiscall (sistema de ventanas): mouse sobre la interfaz
    const BYTE MouseOnUiPrologue[] = { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC };
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    constexpr BYTE KeyUp = 0, KeyPressed = 2, KeyHeld = 3; // estados de la tabla de teclas
    constexpr DWORD SendChatFunction = 0x005BDE40;     // void __cdecl (const char* texto): C1 00
    constexpr DWORD IsReleaseFunction = 0x00790F00;    // bool __cdecl(int tecla): recien soltada
    constexpr DWORD SetFontFunction = 0x00420120;      // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;   // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl()
    constexpr DWORD WindowManagerMouse = 0x38;         // administrador de ventanas: la ventana con el mouse
    constexpr int ChatReady = 0x32;                    // el chat sale si la espera es como mucho esto
    constexpr int SortLeft = 1, SortCenter = 3;

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const MouseX = reinterpret_cast<int*>(0x0879340C); // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    BYTE* const LeftButtonFlags = reinterpret_cast<BYTE*>(0x08793384); // apretado recien, soltado, sostenido
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);
    int* const CurrentMap = reinterpret_cast<int*>(0x00E61E18);
    int* const SelectedCharacter = reinterpret_cast<int*>(0x00E61730); // personaje bajo el mouse (indice), -1 ninguno
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // personajes: [+8], 400 de 0x598 bytes
    DWORD* const HeroPointer = reinterpret_cast<DWORD*>(0x07BC4F04);

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr DWORD CharKey = 0x7E, CharObject = 0x308, CharLive = 0x30C;
    constexpr DWORD CharVisible = 0x314; // el juego solo dibuja a los personajes con este byte en 1
    constexpr DWORD ObjLive = 0x04, ObjAlpha = 0x98, ObjPosition = 0xFC, ObjAngle = 0x108;

    // Monturas y mascotas: se dibujan aparte del personaje y no miran su byte Visible, pero si el alfa de su objeto.
    BYTE* const PetList = reinterpret_cast<BYTE*>(0x08791550);
    constexpr int PetCount = 10;
    constexpr DWORD PetSize = 0x288, PetOwner = 0x1D8; // PetOwner: el OBJECT del dueno
    constexpr WORD IdMask = 0x7FFF;

    // --- Paquetes ---
    constexpr int CodeSpectres = 0xFD;
    constexpr BYTE SubCodeDialogue = 0x04, SubCodeRival = 0x05;
    constexpr DWORD RivalMarkDuration = 10000; // la marca se repite cada 4 segundos
    constexpr int CodeInterface = 0xFF;        // interfaz de PeroniaMU
    constexpr BYTE SubCodeStoryScene = 0x30;
    constexpr size_t MaxButtons = 3;

    // --- Escena ---
    constexpr DWORD FadeDuration = 400;    // ms de cada fundido a negro
    // El encuadre es siempre el mismo: el espectro de pie, con casi todo el cuerpo entre las bandas y la caja. El personaje
    // propio no se dibuja, asi que la distancia no depende de donde esta.
    constexpr float EyeHeight = 135.0f;    // altura de la camara sobre los pies del espectro
    constexpr float FrameHeight = 97.0f;   // altura del espectro a la que apunta la camara (con aire para el vuelo)
    constexpr float ViewDistance = 690.0f; // distancia de la camara al espectro
    constexpr float MountedFrameHeight = 113.0f, MountedViewDistance = 820.0f; // con montura es mas alto
    constexpr float MountedTurn = 45.0f;   // y mira a un costado, asi la cabeza de la montura no lo tapa
    constexpr float CharsPerSecond = 45.0f; // el texto aparece de a poco
    constexpr DWORD MaxSceneDuration = 10 * 60 * 1000;

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using KeyStateType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    enum class Phase
    {
        Idle,      // sin escena
        Waiting,   // se mando /espectro hablar, esperando el dialogo
        FadeIn,    // pantalla a negro, camara normal
        Showing,   // camara en el espectro, la imagen vuelve y se ve la caja
        FadeOut,   // eligio: pantalla a negro, camara en el espectro
        Returning, // camara normal, la imagen vuelve
    };

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr;
    std::deque<std::string> g_pendingChat;
    DWORD g_rivals[IdMask + 1] = {}; // GetTickCount de la ultima marca de rival de cada objeto
    int g_suppressedKey = 0;

    Phase g_phase = Phase::Idle;
    DWORD g_phaseStart = 0;
    DWORD g_sceneStart = 0;
    int g_sceneMap = -1;
    WORD g_rival = 0;
    struct SceneButton
    {
        std::string label;
        std::string command; // lo que se manda por chat al elegirlo; vacio solo cierra la escena
    };

    std::vector<SceneButton> g_buttons;  // los de la ultima pagina, de izquierda a derecha
    std::vector<std::string> g_pages;
    size_t g_page = 0;
    std::string g_chosenCommand;
    std::string g_title;
    std::string g_reason;
    std::deque<std::string> g_lines; // el texto, cortado al ancho de la caja
    size_t g_textLength = 0;
    float g_revealed = 0.0f;          // caracteres visibles del texto
    DWORD g_lastDraw = 0;
    bool g_loggedCamera = false;
    bool g_rivalMounted = false;
    bool g_turnedRival = false;
    float g_rivalAngle = 0.0f;        // el giro de un espectro con montura, cuando empezo la escena

    BYTE* g_hiddenHero = nullptr;
    BYTE g_heroVisible = 1;
    float g_petAlpha[PetCount] = {};
    bool g_petHidden[PetCount] = {};

    // --- Caja (coordenadas de 640x480) ---
    constexpr float BarHeight = 46.0f;
    constexpr float BoxLeft = 60.0f, BoxWidth = 520.0f, BoxTop = 316.0f, BoxHeight = 150.0f;
    constexpr float LineHeight = 13.0f;
    constexpr size_t LineChars = 92;
    constexpr float ButtonWidth = 110.0f, ButtonHeight = 18.0f;

    struct Rect
    {
        float left;
        float top;
        float width;
        float height;
    };

    constexpr int ButtonNone = -1;
    constexpr int ButtonClose = -2; // Escape: cierra sin elegir

    bool LastPage()
    {
        return g_page + 1 >= g_pages.size();
    }

    // Los botones a la vista: en la ultima pagina los de la escena, antes solo "Continuar".
    int VisibleButtons()
    {
        return LastPage() ? static_cast<int>(g_buttons.size()) : 1;
    }

    // Alineados a la derecha, en orden.
    Rect ButtonRect(int button)
    {
        const float top = BoxTop + BoxHeight - ButtonHeight - 8.0f;
        const float right = BoxLeft + BoxWidth - 10.0f;
        const int fromRight = VisibleButtons() - 1 - button;
        return Rect{ right - ButtonWidth - fromRight * (ButtonWidth + 8.0f), top, ButtonWidth, ButtonHeight };
    }

    bool Inside(const Rect& rect, float x, float y)
    {
        return x >= rect.left && x < rect.left + rect.width && y >= rect.top && y < rect.top + rect.height;
    }

    bool SceneVisible()
    {
        return g_phase == Phase::Showing || g_phase == Phase::FadeOut;
    }

    // La camara esta en el espectro.
    bool CameraOnRival()
    {
        return g_phase == Phase::Showing || g_phase == Phase::FadeOut;
    }

    bool ButtonsEnabled()
    {
        return g_phase == Phase::Showing && GetTickCount() - g_phaseStart >= FadeDuration;
    }

    int HitButton()
    {
        if (!ButtonsEnabled())
        {
            return ButtonNone;
        }

        const float x = static_cast<float>(*MouseX);
        const float y = static_cast<float>(*MouseY);
        for (int button = 0; button < VisibleButtons(); button++)
        {
            if (Inside(ButtonRect(button), x, y))
            {
                return button;
            }
        }

        return ButtonNone;
    }

    bool MouseInBox()
    {
        return Inside({ BoxLeft, BoxTop, BoxWidth, BoxHeight }, static_cast<float>(*MouseX), static_cast<float>(*MouseY));
    }

    // --- Chat ---
    void QueueChat(const char* text)
    {
        g_pendingChat.push_back(text);
        g_log("Cinematicas: %s", text);
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

    // --- Personajes ---
    BYTE* Characters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    BYTE* FindCharacter(WORD id)
    {
        BYTE* characters = Characters();
        if (characters == nullptr)
        {
            return nullptr;
        }

        for (int i = 0; i < CharacterCount; i++)
        {
            BYTE* character = characters + i * CharacterSize;
            if (character[CharLive] && (*reinterpret_cast<const WORD*>(character + CharKey) & IdMask) == id)
            {
                return character;
            }
        }

        return nullptr;
    }

    bool IsRival(WORD id)
    {
        const DWORD mark = g_rivals[id & IdMask];
        return mark != 0 && GetTickCount() - mark <= RivalMarkDuration;
    }

    // --- Escena ---
    void SetPhase(Phase phase)
    {
        g_phase = phase;
        g_phaseStart = GetTickCount();
    }

    void Wrap(const char* text)
    {
        g_lines.clear();
        g_textLength = 0;
        std::string line;
        std::string word;
        auto flushWord = [&]()
        {
            if (word.empty())
            {
                return;
            }

            if (!line.empty() && line.size() + 1 + word.size() > LineChars)
            {
                g_lines.push_back(line);
                line.clear();
            }

            line += line.empty() ? word : " " + word;
            word.clear();
        };

        for (const char* c = text; *c != 0; c++)
        {
            if (*c == ' ' || *c == '\n')
            {
                flushWord();
            }
            else
            {
                word += *c;
            }
        }

        flushWord();
        if (!line.empty())
        {
            g_lines.push_back(line);
        }

        for (const std::string& l : g_lines)
        {
            g_textLength += l.size();
        }
    }

    void EndSceneNow()
    {
        if (g_phase != Phase::Idle)
        {
            g_log("Cinematicas: fin de la escena.");
        }

        SetPhase(Phase::Idle);
        g_loggedCamera = false;
    }

    void ShowPage(size_t page)
    {
        g_page = page;
        Wrap(g_page < g_pages.size() ? g_pages[g_page].c_str() : "");
        g_revealed = 0.0f;
    }

    void Choose(int button)
    {
        if (button == ButtonNone)
        {
            return;
        }

        if (button >= 0 && !LastPage())
        {
            ShowPage(g_page + 1); // "Continuar"
            return;
        }

        g_chosenCommand = button >= 0 && button < static_cast<int>(g_buttons.size()) ? g_buttons[button].command : "";
        SetPhase(Phase::FadeOut);
    }

    void StartScene(WORD id)
    {
        g_rival = id;
        g_chosenCommand.clear();
        ShowPage(0);
        g_sceneMap = *CurrentMap;
        g_sceneStart = GetTickCount();
        if (CameraOnRival())
        {
            SetPhase(Phase::Showing); // otro dialogo durante la escena: sin fundido
        }
        else
        {
            SetPhase(Phase::FadeIn);
        }
    }

    void OnDialogue(const BYTE* packet, int size)
    {
        if (size < 13)
        {
            return;
        }

        const WORD id = static_cast<WORD>(((packet[5] << 8) | packet[6]) & IdMask);
        const bool canDuel = packet[7] != 0;
        g_rivalMounted = (packet[9] & 1) != 0;
        g_turnedRival = false;
        const char* strings[3] = { "", "", "" };
        int offset = 10;
        for (int i = 0; i < 3 && offset < size; i++)
        {
            strings[i] = reinterpret_cast<const char*>(packet + offset);
            const size_t length = strnlen(strings[i], static_cast<size_t>(size - offset));
            if (offset + static_cast<int>(length) >= size)
            {
                return; // sin el 0 del final
            }

            offset += static_cast<int>(length) + 1;
        }

        g_title = strings[0];
        g_reason = strings[2];
        g_pages.assign(1, strings[1]);
        g_buttons.clear();
        if (canDuel)
        {
            char command[48];
            sprintf_s(command, "/espectro duelo %d", id);
            g_buttons.push_back({ "Duelo", command });
        }

        g_buttons.push_back({ "Volver m\xE1s tarde", "" });
        StartScene(id);
        g_log("Cinematicas: dialogo de %s (objeto %d, duelo %d).", g_title.c_str(), id, canDuel ? 1 : 0);
    }

    // Lee un texto terminado en 0; devuelve false si el paquete se corta antes.
    bool ReadText(const BYTE* packet, int size, int& offset, std::string& text)
    {
        if (offset >= size)
        {
            return false;
        }

        const char* start = reinterpret_cast<const char*>(packet + offset);
        const size_t length = strnlen(start, static_cast<size_t>(size - offset));
        if (offset + static_cast<int>(length) >= size)
        {
            return false; // sin el 0 del final
        }

        text.assign(start, length);
        offset += static_cast<int>(length) + 1;
        return true;
    }

    // C2 [largo] FF 30 [objeto] [paginas] [botones] titulo, nota, paginas y botones ([id] texto).
    void OnStoryScene(const BYTE* packet, int size)
    {
        if (size < 11)
        {
            return;
        }

        const WORD id = static_cast<WORD>(((packet[5] << 8) | packet[6]) & IdMask);
        const int pageCount = packet[7];
        const int buttonCount = packet[8];
        int offset = 9;
        std::string title, note, text;
        std::vector<std::string> pages;
        std::vector<SceneButton> buttons;
        if (pageCount < 1 || !ReadText(packet, size, offset, title) || !ReadText(packet, size, offset, note))
        {
            return;
        }

        for (int i = 0; i < pageCount; i++)
        {
            if (!ReadText(packet, size, offset, text))
            {
                return;
            }

            pages.push_back(text);
        }

        for (int i = 0; i < buttonCount; i++)
        {
            if (offset >= size)
            {
                return;
            }

            const int buttonId = packet[offset++];
            if (!ReadText(packet, size, offset, text))
            {
                return;
            }

            if (buttons.size() < MaxButtons)
            {
                char command[48] = "";
                if (buttonId > 0)
                {
                    sprintf_s(command, "/historia %d %d", buttonId, id);
                }

                buttons.push_back({ text, command });
            }
        }

        if (buttons.empty())
        {
            buttons.push_back({ "Cerrar", "" });
        }

        g_title = title;
        g_reason = note;
        g_pages = pages;
        g_buttons = buttons;
        g_rivalMounted = false;
        g_turnedRival = false;
        StartScene(id);
        g_log("Cinematicas: escena de %s (objeto %d, %d paginas, %d botones).", g_title.c_str(), id, pageCount,
            static_cast<int>(g_buttons.size()));
    }

    void OnSpectresPacket(const BYTE* packet, int size)
    {
        if (packet[0] == 0xC1 && size >= 6 && packet[3] == SubCodeRival)
        {
            g_rivals[((packet[4] << 8) | packet[5]) & IdMask] = GetTickCount();
        }
        else if (packet[0] == 0xC2 && size >= 5 && packet[4] == SubCodeDialogue)
        {
            OnDialogue(packet, size);
        }
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (code == CodeSpectres && packet != nullptr && size >= 5)
        {
            const BYTE subCode = packet[0] == 0xC1 ? packet[3] : packet[4];
            if (subCode == SubCodeDialogue || subCode == SubCodeRival)
            {
                OnSpectresPacket(packet, size);
                return 1;
            }
        }
        else if (code == CodeInterface && packet != nullptr && size >= 5 && packet[0] == 0xC2
            && packet[4] == SubCodeStoryScene)
        {
            OnStoryScene(packet, size);
            return 1;
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // Avanza las fases por tiempo y termina la escena si el jugador cambio de mapa o el espectro ya no esta.
    void UpdatePhase()
    {
        const DWORD now = GetTickCount();
        const DWORD elapsed = now - g_phaseStart;
        if (g_phase != Phase::Idle && g_phase != Phase::Waiting
            && (*CurrentMap != g_sceneMap || now - g_sceneStart > MaxSceneDuration || *HeroPointer == 0))
        {
            EndSceneNow();
            return;
        }

        switch (g_phase)
        {
        case Phase::Waiting:
            if (elapsed > 3000)
            {
                EndSceneNow(); // el servidor no contesto
            }

            break;
        case Phase::FadeIn:
            if (elapsed >= FadeDuration)
            {
                SetPhase(FindCharacter(g_rival) != nullptr ? Phase::Showing : Phase::Returning);
            }

            break;
        case Phase::Showing:
            if (FindCharacter(g_rival) == nullptr)
            {
                SetPhase(Phase::Returning);
            }

            break;
        case Phase::FadeOut:
            if (elapsed >= FadeDuration)
            {
                if (!g_chosenCommand.empty())
                {
                    QueueChat(g_chosenCommand.c_str());
                    g_chosenCommand.clear();
                }

                SetPhase(Phase::Returning);
            }

            break;
        case Phase::Returning:
            if (elapsed >= FadeDuration)
            {
                EndSceneNow();
            }

            break;
        default:
            break;
        }
    }

    // Que tan negra esta la pantalla (0 a 1).
    float Darkness()
    {
        const float t = static_cast<float>(GetTickCount() - g_phaseStart) / FadeDuration;
        const float clamped = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
        switch (g_phase)
        {
        case Phase::FadeIn:
        case Phase::FadeOut:
            return clamped;
        case Phase::Showing:
        case Phase::Returning:
            return 1.0f - clamped;
        default:
            return 0.0f;
        }
    }

    // --- Camara (llamada por camera3d.dll en cada cuadro, despues de su camara) ---
    void RestoreHero()
    {
        if (g_hiddenHero != nullptr)
        {
            g_hiddenHero[CharVisible] = g_heroVisible;
            g_hiddenHero = nullptr;
            for (int i = 0; i < PetCount; i++)
            {
                if (g_petHidden[i])
                {
                    *reinterpret_cast<float*>(PetList + i * PetSize + ObjAlpha) = g_petAlpha[i];
                    g_petHidden[i] = false;
                }
            }
        }
    }

    void HideHero()
    {
        const DWORD hero = *HeroPointer;
        if (hero == 0)
        {
            return;
        }

        BYTE* character = reinterpret_cast<BYTE*>(hero);
        if (g_hiddenHero != character)
        {
            RestoreHero();
            g_hiddenHero = character;
            g_heroVisible = character[CharVisible]; // si no se dibujo la interfaz desde el cuadro anterior, ya estaba guardado
            for (int i = 0; i < PetCount; i++)
            {
                BYTE* pet = PetList + i * PetSize;
                if (pet[ObjLive] && *reinterpret_cast<const DWORD*>(pet + PetOwner) == hero + CharObject)
                {
                    g_petHidden[i] = true;
                    g_petAlpha[i] = *reinterpret_cast<const float*>(pet + ObjAlpha);
                }
            }
        }

        character[CharVisible] = 0;
        for (int i = 0; i < PetCount; i++)
        {
            if (g_petHidden[i])
            {
                *reinterpret_cast<float*>(PetList + i * PetSize + ObjAlpha) = 0.0f;
            }
        }
    }

    // Forma de los angulos de la camara del juego (deducida de su matriz de vista): inclinacion 0 mira hacia abajo y -90
    // mira al horizonte; con la inclinacion -90 el giro Y mira hacia (sin Y, cos Y).
    bool SetCamera(float* position, float* angle)
    {
        const DWORD hero = *HeroPointer;
        BYTE* rival = FindCharacter(g_rival);
        if (hero == 0 || rival == nullptr)
        {
            return false;
        }

        const float* heroPosition = reinterpret_cast<const float*>(hero + CharObject + ObjPosition);
        const float* rivalPosition = reinterpret_cast<const float*>(rival + CharObject + ObjPosition);
        if (!g_loggedCamera)
        {
            // Para comprobar la forma de los angulos: la camara normal mira hacia el personaje.
            g_loggedCamera = true;
            g_log("Cinematicas: camara normal pos (%.0f, %.0f, %.0f) angulos (%.1f, %.1f, %.1f), heroe (%.0f, %.0f, %.0f).",
                position[0], position[1], position[2], angle[0], angle[1], angle[2], heroPosition[0], heroPosition[1],
                heroPosition[2]);
        }

        float dx = heroPosition[0] - rivalPosition[0];
        float dy = heroPosition[1] - rivalPosition[1];
        float distance = sqrtf(dx * dx + dy * dy);
        if (distance < 1.0f)
        {
            dx = 0.0f;
            dy = -1.0f;
            distance = 1.0f;
        }

        dx /= distance;
        dy /= distance;

        const float viewDistance = g_rivalMounted ? MountedViewDistance : ViewDistance;
        const float frameHeight = g_rivalMounted ? MountedFrameHeight : FrameHeight;
        if (g_rivalMounted)
        {
            // En cada cuadro: el casteo del espectro lo vuelve a girar hacia el personaje.
            float* rivalAngle = reinterpret_cast<float*>(rival + CharObject + ObjAngle);
            if (!g_turnedRival)
            {
                g_turnedRival = true;
                g_rivalAngle = rivalAngle[2];
            }

            rivalAngle[2] = g_rivalAngle + MountedTurn;
        }

        const float eye[3] = {
            rivalPosition[0] + dx * viewDistance,
            rivalPosition[1] + dy * viewDistance,
            rivalPosition[2] + EyeHeight,
        };

        // Mira hacia el espectro: la direccion es -d.
        const float yaw = atan2f(-dx, -dy) * 180.0f / 3.14159265f;
        const float pitch = -90.0f + atan2f(EyeHeight - frameHeight, viewDistance) * 180.0f / 3.14159265f;
        position[0] = eye[0];
        position[1] = eye[1];
        position[2] = eye[2];
        angle[0] = pitch;
        angle[1] = 0.0f;
        angle[2] = yaw;
        return true;
    }

    // Sin objetos con destructor: puede usar __try.
    bool __cdecl OverrideCamera(float* position, float* angle)
    {
        __try
        {
            UpdatePhase();
            if (!CameraOnRival())
            {
                return false;
            }

            if (!SetCamera(position, angle))
            {
                return false;
            }

            HideHero();
            return true;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            EndSceneNow();
            return false;
        }
    }

    // --- Mouse y teclado ---
    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingChat();
        if (g_phase == Phase::Idle || g_phase == Phase::Waiting)
        {
            return g_nextMouse(manager, unused);
        }

        // Durante la escena el mouse es de la caja: ninguna ventana lo tiene.
        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<KeyStateType>(IsReleaseFunction)(VK_LBUTTON))
        {
            const int button = HitButton();
            if (button != ButtonNone)
            {
                Choose(button);
            }
            else if (g_phase == Phase::Showing && g_revealed < g_textLength)
            {
                g_revealed = static_cast<float>(g_textLength); // un click muestra todo el texto
            }
            else if (ButtonsEnabled() && !LastPage() && MouseInBox())
            {
                ShowPage(g_page + 1); // un click en la caja pasa de pagina
            }
        }

        return 1;
    }

    char __fastcall MouseOnUiHook(BYTE** system, void* unused)
    {
        if (g_phase != Phase::Idle && g_phase != Phase::Waiting)
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

    // El personaje bajo el mouse, si es un rival marcado.
    int HoveredRival()
    {
        BYTE* characters = Characters();
        const int index = *SelectedCharacter;
        if (characters == nullptr || index < 0 || index >= CharacterCount)
        {
            return -1;
        }

        BYTE* character = characters + index * CharacterSize;
        const WORD id = *reinterpret_cast<const WORD*>(character + CharKey) & IdMask;
        return character[CharLive] && IsRival(id) ? id : -1;
    }

    void __cdecl KeyTick(BYTE* keyStates)
    {
        __try
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

            if (g_phase != Phase::Idle && g_phase != Phase::Waiting)
            {
                // El personaje no ve los clicks de la escena.
                memset(LeftButtonFlags, 0, 3);
                if (keyStates[VK_ESCAPE] == KeyPressed && GetFocus() == *MainWindow)
                {
                    g_suppressedKey = VK_ESCAPE;
                    keyStates[VK_ESCAPE] = KeyUp;
                    if (g_phase == Phase::Showing)
                    {
                        Choose(ButtonClose);
                    }
                }

                return;
            }

            // El click real cambia la tabla de teclas; el de muauto.dll (cliente de prueba), solo los botones del juego.
            if ((keyStates[VK_LBUTTON] != KeyPressed && LeftButtonFlags[0] == 0) || g_phase == Phase::Waiting)
            {
                return;
            }

            const int rival = HoveredRival();
            if (rival < 0)
            {
                return;
            }

            char text[48];
            sprintf_s(text, "/espectro hablar %d", rival);
            QueueChat(text);
            SetPhase(Phase::Waiting);
            memset(LeftButtonFlags, 0, 3); // que el personaje no camine ni ataque
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            EndSceneNow();
        }
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

    void BeginShapes()
    {
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glLineWidth(1.0f);
    }

    void DrawShapes(int hover)
    {
        BeginShapes();
        if (SceneVisible())
        {
            // Bandas de cine arriba y abajo, y la caja.
            Fill({ 0, 0, 640, BarHeight }, 0.0f, 0.0f, 0.0f, 1.0f);
            Fill({ 0, 480 - BarHeight, 640, BarHeight }, 0.0f, 0.0f, 0.0f, 1.0f);
            const Rect box = { BoxLeft, BoxTop, BoxWidth, BoxHeight };
            Fill(box, 0.02f, 0.0f, 0.05f, 0.88f);
            Outline(box, 0.55f, 0.35f, 0.8f, 0.9f);
            if (ButtonsEnabled())
            {
                for (int button = 0; button < VisibleButtons(); button++)
                {
                    const Rect rect = ButtonRect(button);
                    const bool hovered = button == hover;
                    Fill(rect, hovered ? 0.4f : 0.12f, hovered ? 0.25f : 0.08f, hovered ? 0.55f : 0.18f, 0.95f);
                    Outline(rect, 0.55f, 0.35f, 0.8f, 0.9f);
                }
            }
        }

        const float darkness = Darkness();
        if (darkness > 0.0f)
        {
            Fill({ 0, 0, 640, 480 }, 0.0f, 0.0f, 0.0f, darkness);
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

    void DrawTexts(int hover)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr || g_phase != Phase::Showing)
        {
            return; // durante los fundidos solo se ve la caja
        }

        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        const DWORD savedText = *reinterpret_cast<DWORD*>(renderer + 0x10);
        const DWORD savedBackground = *reinterpret_cast<DWORD*>(renderer + 0x14);
        Text(g_title.c_str(), BoxLeft + 12, BoxTop + 8, BoxWidth - 24, SortLeft, Color(200, 160, 255), true);

        // El texto aparece de a poco.
        float top = BoxTop + 28;
        size_t remaining = static_cast<size_t>(g_revealed);
        char line[LineChars + 1];
        for (const std::string& full : g_lines)
        {
            if (remaining == 0)
            {
                break;
            }

            const size_t count = full.size() < remaining ? full.size() : remaining;
            memcpy(line, full.c_str(), count);
            line[count] = 0;
            remaining -= count;
            Text(line, BoxLeft + 12, top, BoxWidth - 24, SortLeft, Color(225, 220, 235), false);
            top += LineHeight;
        }

        if (g_pages.size() > 1)
        {
            char pageNumber[24];
            sprintf_s(pageNumber, "%u / %u", static_cast<unsigned>(g_page + 1), static_cast<unsigned>(g_pages.size()));
            Text(pageNumber, BoxLeft + BoxWidth - 72, BoxTop + 8, 60, SortCenter, Color(150, 130, 180), false);
        }

        if (ButtonsEnabled())
        {
            if (LastPage() && !g_reason.empty() && g_revealed >= g_textLength)
            {
                Text(g_reason.c_str(), BoxLeft + 12, BoxTop + BoxHeight - ButtonHeight - 26, BoxWidth - 24, SortLeft,
                    Color(255, 150, 110), false);
            }

            for (int button = 0; button < VisibleButtons(); button++)
            {
                const Rect rect = ButtonRect(button);
                Text(LastPage() ? g_buttons[button].label.c_str() : "Continuar", rect.left, rect.top + 3, rect.width,
                    SortCenter, button == hover ? Color(255, 240, 255) : Color(220, 200, 240), false);
            }
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }

    // Sin objetos con destructor: puede usar __try.
    void DrawScene()
    {
        __try
        {
            const DWORD now = GetTickCount();
            if (g_phase == Phase::Showing && g_lastDraw != 0)
            {
                g_revealed += (now - g_lastDraw) * CharsPerSecond / 1000.0f;
                if (g_revealed > g_textLength)
                {
                    g_revealed = static_cast<float>(g_textLength);
                }
            }

            g_lastDraw = now;
            const int hover = HitButton();
            DrawShapes(hover);
            DrawTexts(hover);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    void RestoreHeroSafely()
    {
        __try
        {
            RestoreHero();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_hiddenHero = nullptr;
            memset(g_petHidden, 0, sizeof(g_petHidden));
        }
    }

    void __cdecl DrawHook()
    {
        RestoreHeroSafely();
        if (g_phase != Phase::Idle && g_phase != Phase::Waiting)
        {
            DrawScene();
        }
        else
        {
            g_lastDraw = 0;
        }

        g_nextDraw(); // despues: el cursor del juego queda arriba de la escena
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
}

extern "C" __declspec(dllexport) bool StartCinematicas(const CinematicasHost* host)
{
    if (host == nullptr || host->version != CinematicasHostVersion)
    {
        return false;
    }

    g_log = host->log;
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("Cinematicas: el juego no coincide; desactivado.");
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
        g_log("Cinematicas: no se pudo instalar.");
        return false;
    }

    host->setCameraOverride(&OverrideCamera);
    host->setKeyTick(&KeyTick);
    g_log("Cinematicas listas (click en un espectro del refugio).");
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
