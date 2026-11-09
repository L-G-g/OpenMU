// quests.dll - Signos de quest sobre los NPC, ventana de la quest y seguimiento, para el cliente MU 1.04d con
// camera3d.dll.
//
// En OpenMU (plugin "Skill quests") algunos NPC ensenan las skills de cada clase con quests. El servidor
// (SkillQuestViewPlugIn) manda paquetes que el main.exe original ignora (su reparto de paquetes devuelve 1 para los
// codigos mayores a 0xF9). Usan el codigo 0xFF de la interfaz de PeroniaMU (0x00 es el boton de reset):
//   - C2 [largo] FF 10 [cantidad] y por NPC [objeto, 2 bytes big endian] [signo]: los signos sobre los NPC cercanos
//     (1 quest disponible, 2 en curso, 3 lista para entregar). Se repite cada pocos segundos; una lista vacia los
//     borra. Si no llega nada en MarkTimeout, se borran solos.
//   - C2 [largo] FF 11 [cantidad] y por linea [color] [texto terminado en 0]: el seguimiento de las quests activas
//     (vacio lo oculta).
//   - C2 [largo] FF 12 [objeto] [accion] [titulo terminado en 0] [cantidad] y las lineas: la ventana de la quest del
//     NPC; la accion es el boton (0 ninguno, 1 aceptar, 2 entregar).
// Colores: 0 blanco, 1 dorado, 2 verde, 3 gris. Los textos son Latin-1 (como la pagina de codigos del juego).
// Se contesta con el comando de chat /quest <objeto>, /quest aceptar <objeto> y /quest entregar <objeto>.
//
// Los signos: "!" dorado (quest disponible) y "?" dorado (lista para entregar) con el resplandor de peroniaui, y "?"
// gris (en curso), "!" gris (la quest siguiente, sin los requisitos cumplidos). Flotan sobre la cabeza del NPC, separados de el: un click en el signo abre la ventana de la quest
// (un click en el NPC sigue abriendo su ventana de siempre).
//
// Como funciona:
//   - El reparto de paquetes (0x0065FD79), el dibujo despues de la interfaz (0x004D9CCC), los eventos del mouse de
//     las ventanas (0x00860998) y el "mouse sobre la interfaz" (0x00860910) se encadenan como en espectros.dll.
//   - Los signos se dibujan en el comienzo del dibujo de la interfaz (0x004D9C86, CALL 0x00637770; healthbars.dll
//     tambien lo encadena): se guardan las matrices 3D, se llama a lo que haya y se proyecta un punto sobre la cabeza
//     del NPC (como las barras de vida). Asi quedan sobre el mundo y debajo de las ventanas del juego.
//   - Con el mouse sobre un signo o sobre la ventana, el mouse es de esta DLL: el juego no camina ni elige objetos.

#include "../shared.h"
#include "../ui/peroniaui.h"

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
    using PeroniaUi::Rect;

    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;     // CALL al reparto al recibir cada paquete
    constexpr DWORD BeginBitmapCallSite = 0x004D9C86;  // CALL 0x00637770: comienzo de la interfaz (proyeccion 2D)
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

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    int* const ChatWait = reinterpret_cast<int*>(0x08793728);
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // personajes: [+8], 400 de 0x598 bytes

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr DWORD CharKey = 0x7E, CharLive = 0x30C;
    constexpr DWORD CharHeight = 0x3E8;   // caja del objeto: maximo en z
    constexpr DWORD CharPosition = 0x404; // float x, y, z
    constexpr WORD IdMask = 0x7FFF;

    // --- Paquetes ---
    constexpr int CodeInterface = 0xFF;
    constexpr BYTE SubCodeMarks = 0x10, SubCodeTracker = 0x11, SubCodeDialog = 0x12;
    constexpr int HeaderLength = 5;
    constexpr int MaximumMarks = 60;
    constexpr DWORD MarkTimeout = 6500; // el servidor repite los signos cada 3 segundos

    enum Mark : BYTE
    {
        MarkNone = 0,
        MarkAvailable = 1,
        MarkInProgress = 2,
        MarkReady = 3,
        MarkLocked = 4, // quest siguiente sin requisitos cumplidos: exclamacion gris
    };

    enum Action : BYTE
    {
        ActionNone = 0,
        ActionAccept = 1,
        ActionDeliver = 2,
    };

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using DrawType = void(__cdecl*)();
    using MouseType = int(__fastcall*)(BYTE* manager, void* unused);
    using MouseOnUiType = char(__fastcall*)(BYTE** system, void* unused);
    using SendChatType = void(__cdecl*)(const char* text);
    using KeyStateType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    struct Line
    {
        BYTE color;
        std::string text;
    };

    struct MarkEntry
    {
        WORD id;
        BYTE mark;
    };

    struct MarkHit
    {
        WORD id;
        Rect rect;
    };

    // --- Estado ---
    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    DrawType g_nextBeginBitmap = nullptr;
    DrawType g_nextDraw = nullptr;
    MouseType g_nextMouse = nullptr;
    MouseOnUiType g_nextMouseOnUi = nullptr;
    std::deque<std::string> g_pendingChat;

    MarkEntry g_marks[MaximumMarks];
    int g_markCount = 0;
    DWORD g_marksTime = 0;
    MarkHit g_hits[MaximumMarks]; // donde se dibujo cada signo en el ultimo cuadro (640x480)
    int g_hitCount = 0;
    int g_hoverMark = -1;         // signo bajo el mouse (indice de g_hits)

    std::vector<Line> g_tracker;

    bool g_dialogOpen = false;
    WORD g_dialogNpc = 0;
    BYTE g_dialogAction = ActionNone;
    std::string g_dialogTitle;
    std::vector<Line> g_dialogLines; // ya cortadas al ancho de la ventana

    bool g_mouseOver = false;     // el mouse es de esta DLL (ventana o signo)
    float g_trackerLeft = 470.0f; // [Quests] TrackerLeft, TrackerTop
    float g_trackerTop = 70.0f;
    float g_markSize = 1.0f;      // [Quests] MarkSize
    float g_headMargin = 70.0f;   // [Quests] HeadMargin (unidades del mundo sobre la caja del NPC)

    // Matrices 3D de la escena, guardadas antes de pasar a la interfaz.
    float g_modelView[16];
    float g_projection[16];
    GLint g_viewport[4];
    bool g_projectionWarned = false;

    // --- Medidas (640x480) ---
    constexpr float DialogWidth = 300.0f;
    constexpr float DialogTop = 80.0f;
    constexpr float LineHeight = 12.0f;
    constexpr float DialogPadding = 12.0f;
    constexpr float TitleHeight = 24.0f;
    constexpr float ButtonsHeight = 34.0f;
    constexpr float ButtonWidth = 90.0f;
    constexpr float ButtonHeight = 22.0f;
    constexpr float TrackerWidth = 165.0f;
    constexpr float MarkWidth = 14.0f;  // alto del signo: MarkHeight; por MarkSize
    constexpr float MarkHeight = 26.0f;

    // --- Utilidades ---
    float Seconds()
    {
        return (GetTickCount() % 3600000u) / 1000.0f;
    }

    float Px(float x)
    {
        return x * *WindowWidth / 640.0f;
    }

    float Py(float y)
    {
        return *WindowHeight - y * *WindowHeight / 480.0f;
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

    DWORD TextColor(BYTE color)
    {
        switch (color)
        {
        case 1: return PeroniaUi::Rgb(255, 204, 26);  // dorado
        case 2: return PeroniaUi::Rgb(120, 230, 120); // verde
        case 3: return PeroniaUi::Rgb(150, 140, 130); // gris
        default: return PeroniaUi::Rgb(230, 230, 230); // blanco
        }
    }

    // Ancho de un texto con la fuente del juego, en 640x480.
    float TextWidth(const std::string& text, bool bold)
    {
        static HDC dc = CreateCompatibleDC(nullptr);
        const HFONT font = bold ? *BoldFont : *NormalFont;
        if (dc == nullptr || font == nullptr || *WindowWidth <= 0)
        {
            return text.size() * 5.0f;
        }

        const HGDIOBJ old = SelectObject(dc, font);
        SIZE size = {};
        GetTextExtentPoint32A(dc, text.c_str(), static_cast<int>(text.size()), &size);
        SelectObject(dc, old);
        return size.cx * 640.0f / *WindowWidth;
    }

    // Corta una linea en varias que entren en el ancho (por palabras; la sangria se repite).
    void Wrap(const Line& line, float width, std::vector<Line>& output)
    {
        const std::string& text = line.text;
        const size_t indent = text.find_first_not_of(' ');
        if (indent == std::string::npos || TextWidth(text, false) <= width)
        {
            output.push_back(line);
            return;
        }

        const std::string prefix(indent, ' ');
        std::string current = prefix;
        size_t position = indent;
        while (position < text.size())
        {
            size_t end = text.find(' ', position);
            if (end == std::string::npos)
            {
                end = text.size();
            }

            const std::string word = text.substr(position, end - position);
            const std::string candidate = current.size() > prefix.size() ? current + " " + word : current + word;
            if (current.size() > prefix.size() && TextWidth(candidate, false) > width)
            {
                output.push_back({ line.color, current });
                current = prefix + word;
            }
            else
            {
                current = candidate;
            }

            position = end + 1;
        }

        output.push_back({ line.color, current });
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

    // --- Chat ---
    void QueueChat(const char* text)
    {
        g_pendingChat.push_back(text);
        g_log("Quests: %s", text);
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

    // --- Ventana de la quest ---
    float DialogHeight()
    {
        return TitleHeight + g_dialogLines.size() * LineHeight + ButtonsHeight + DialogPadding / 2;
    }

    Rect DialogRect()
    {
        return { (640.0f - DialogWidth) / 2, DialogTop, DialogWidth, DialogHeight() };
    }

    Rect ActionButtonRect()
    {
        const Rect dialog = DialogRect();
        return { dialog.left + dialog.width / 2 - ButtonWidth - 6, dialog.Bottom() - ButtonsHeight + 4, ButtonWidth,
            ButtonHeight };
    }

    Rect CloseButtonRect()
    {
        const Rect dialog = DialogRect();
        if (g_dialogAction == ActionNone)
        {
            return { dialog.left + (dialog.width - ButtonWidth) / 2, dialog.Bottom() - ButtonsHeight + 4, ButtonWidth,
                ButtonHeight };
        }

        return { dialog.left + dialog.width / 2 + 6, dialog.Bottom() - ButtonsHeight + 4, ButtonWidth, ButtonHeight };
    }

    const char* ActionText()
    {
        return g_dialogAction == ActionAccept ? "Aceptar" : "Entregar";
    }

    void DrawDialog()
    {
        const Rect dialog = DialogRect();
        const Rect action = ActionButtonRect();
        const Rect close = CloseButtonRect();
        const bool hasAction = g_dialogAction != ActionNone;

        PeroniaUi::BeginShapes();
        FillRect(dialog, 0.0f, 0.0f, 0.0f, 0.88f);
        OutlineRect(dialog, 0.8f, 0.65f, 0.35f, 0.9f);
        const Rect inner = { dialog.left + 2, dialog.top + 2, dialog.width - 4, dialog.height - 4 };
        OutlineRect(inner, 0.45f, 0.35f, 0.18f, 0.7f);
        const Rect separator = { dialog.left + DialogPadding, dialog.top + TitleHeight - 4, dialog.width - DialogPadding * 2, 1 };
        FillRect(separator, 0.8f, 0.65f, 0.35f, 0.6f);
        if (hasAction)
        {
            PeroniaUi::Glow(action, PeroniaUi::MouseInside(action) ? 1.0f : 0.6f);
            PeroniaUi::MetalButton(action, PeroniaUi::StateOf(action, true));
            PeroniaUi::Shine(action);
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
        Text(g_dialogTitle.c_str(), dialog.left, dialog.top + 7, dialog.width, SortCenter, PeroniaUi::GoldText, true);
        float top = dialog.top + TitleHeight;
        for (const Line& line : g_dialogLines)
        {
            if (line.text.find_first_not_of(' ') == std::string::npos)
            {
                top += LineHeight; // el texto del juego dibuja basura con un texto vacio
                continue;
            }

            Text(line.text.c_str(), dialog.left + DialogPadding, top, dialog.width - DialogPadding * 2, SortLeft,
                TextColor(line.color), line.color == 1 && line.text.find_first_not_of(' ') == 0);
            top += LineHeight;
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();

        const auto state = [](const Rect& rect) { return PeroniaUi::StateOf(rect, true); };
        if (hasAction)
        {
            const auto actionState = state(action);
            PeroniaUi::ButtonText(action, ActionText(),
                actionState == PeroniaUi::ButtonState::Normal ? PeroniaUi::GoldText : PeroniaUi::GoldTextHover, true,
                actionState == PeroniaUi::ButtonState::Pressed);
        }

        const auto closeState = state(close);
        PeroniaUi::ButtonText(close, "Cerrar",
            closeState == PeroniaUi::ButtonState::Normal ? PeroniaUi::GoldText : PeroniaUi::GoldTextHover, false,
            closeState == PeroniaUi::ButtonState::Pressed);
    }

    // --- Seguimiento ---
    void DrawTracker()
    {
        if (g_tracker.empty())
        {
            return;
        }

        const Rect box = { g_trackerLeft, g_trackerTop, TrackerWidth, 8 + g_tracker.size() * LineHeight };
        PeroniaUi::BeginShapes();
        FillRect(box, 0.0f, 0.0f, 0.0f, 0.45f);
        const Rect edge = { box.left, box.top, 2, box.height };
        FillRect(edge, 0.85f, 0.65f, 0.2f, 0.8f);
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
        float top = box.top + 4;
        for (const Line& line : g_tracker)
        {
            if (line.text.find_first_not_of(' ') == std::string::npos)
            {
                top += LineHeight;
                continue;
            }

            const bool title = line.color == 1 && line.text.find_first_not_of(' ') == 0;
            Text(line.text.c_str(), box.left + 7, top, box.width - 10, SortLeft, TextColor(line.color), title);
            top += LineHeight;
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }

    // --- Signos ---
    bool Project(const float* point, float& x, float& y)
    {
        float eye[4];
        float clip[4];
        for (int row = 0; row < 4; row++)
        {
            eye[row] = g_modelView[row] * point[0] + g_modelView[4 + row] * point[1] + g_modelView[8 + row] * point[2]
                + g_modelView[12 + row];
        }

        for (int row = 0; row < 4; row++)
        {
            clip[row] = g_projection[row] * eye[0] + g_projection[4 + row] * eye[1] + g_projection[8 + row] * eye[2]
                + g_projection[12 + row] * eye[3];
        }

        if (clip[3] <= 1.0f)
        {
            return false;
        }

        // Pixeles de la ventana (origen abajo) a 640x480 (origen arriba).
        const float px = g_viewport[0] + (clip[0] / clip[3] + 1.0f) * 0.5f * g_viewport[2];
        const float py = g_viewport[1] + (clip[1] / clip[3] + 1.0f) * 0.5f * g_viewport[3];
        x = px * 640.0f / *WindowWidth;
        y = (*WindowHeight - py) * 480.0f / *WindowHeight;
        return x >= 0 && x <= 640 && y >= 0 && y <= 480;
    }

    // Disco relleno (centro y radios en 640x480; el radio vertical se corrige para que se vea redondo).
    void Disc(float cx, float cy, float radius)
    {
        const float ry = radius * 640.0f / 480.0f * *WindowHeight / *WindowWidth;
        glBegin(GL_TRIANGLE_FAN);
        Vertex(cx, cy);
        for (int i = 0; i <= 20; i++)
        {
            const float angle = i * 6.2831853f / 20;
            Vertex(cx + radius * cosf(angle), cy + ry * sinf(angle));
        }

        glEnd();
    }

    // Arco grueso: angulos en radianes (0 a la derecha, positivo hacia arriba).
    void Arc(float cx, float cy, float radius, float thickness, float from, float to, float expand)
    {
        const float aspect = 640.0f / 480.0f * *WindowHeight / *WindowWidth;
        const float inner = radius - thickness / 2 - expand;
        const float outer = radius + thickness / 2 + expand;
        glBegin(GL_QUAD_STRIP);
        for (int i = 0; i <= 24; i++)
        {
            const float angle = from + (to - from) * i / 24;
            const float c = cosf(angle);
            const float s = sinf(angle);
            Vertex(cx + inner * c, cy - inner * s * aspect);
            Vertex(cx + outer * c, cy - outer * s * aspect);
        }

        glEnd();
    }

    void Quad(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        glBegin(GL_QUADS);
        Vertex(x0, y0);
        Vertex(x1, y1);
        Vertex(x2, y2);
        Vertex(x3, y3);
        glEnd();
    }

    // "!" con el borde superior en top, centrado en cx; s es la escala.
    void Exclamation(float cx, float top, float s, float expand)
    {
        const float e = expand;
        Quad(cx - 3.6f * s - e, top - e, cx + 3.6f * s + e, top - e, cx + 2.0f * s + e, top + 16.0f * s + e,
            cx - 2.0f * s - e, top + 16.0f * s + e);
        Disc(cx, top + 21.5f * s, 2.8f * s + e);
    }

    // "?" con el borde superior en top, centrado en cx.
    void Question(float cx, float top, float s, float expand)
    {
        const float radius = 5.5f * s;
        const float thickness = 3.6f * s;
        const float cy = top + radius + thickness / 2;
        const float aspect = 640.0f / 480.0f * *WindowHeight / *WindowWidth;
        Arc(cx, cy, radius, thickness, 3.0f, -1.5708f, expand);
        const float stemTop = cy + radius * aspect - thickness / 2;
        const float stemBottom = top + 16.0f * s;
        const float half = thickness / 2 * 0.9f + expand;
        Quad(cx - half, stemTop - expand, cx + half, stemTop - expand, cx + half, stemBottom + expand, cx - half,
            stemBottom + expand);
        Disc(cx, top + 21.5f * s, 2.6f * s + expand);
    }

    void Glyph(BYTE mark, float cx, float top, float s, float expand)
    {
        if (mark == MarkAvailable || mark == MarkLocked)
        {
            Exclamation(cx, top, s, expand);
        }
        else
        {
            Question(cx, top, s, expand);
        }
    }

    void DrawMark(BYTE mark, float cx, float top, float s, bool hover)
    {
        const bool gold = mark == MarkAvailable || mark == MarkReady;
        if (gold)
        {
            const Rect glowRect = { cx - 9 * s, top + 2 * s, 18 * s, 20 * s };
            PeroniaUi::Glow(glowRect, hover ? 1.0f : 0.45f);
            glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        }

        // Borde oscuro, cuerpo y un brillo arriba.
        glColor4f(0.14f, 0.08f, 0.0f, 0.95f);
        Glyph(mark, cx, top, s, 1.6f);
        if (gold)
        {
            hover ? glColor4f(1.0f, 0.95f, 0.6f, 1.0f) : glColor4f(1.0f, 0.78f, 0.15f, 1.0f);
        }
        else
        {
            hover ? glColor4f(0.85f, 0.85f, 0.85f, 1.0f) : glColor4f(0.62f, 0.62f, 0.62f, 1.0f);
        }

        Glyph(mark, cx, top, s, 0.0f);
        glColor4f(1.0f, 1.0f, 0.9f, gold ? 0.45f : 0.25f);
        if (mark == MarkAvailable || mark == MarkLocked)
        {
            Quad(cx - 2.4f * s, top + 1.0f * s, cx - 0.6f * s, top + 1.0f * s, cx - 0.8f * s, top + 9.0f * s,
                cx - 1.9f * s, top + 9.0f * s);
        }
    }

    const BYTE* FindCharacter(BYTE* characters, WORD id)
    {
        for (int i = 0; i < CharacterCount; i++)
        {
            const BYTE* character = characters + i * CharacterSize;
            if (character[CharLive] && (*reinterpret_cast<const WORD*>(character + CharKey) & IdMask) == id)
            {
                return character;
            }
        }

        return nullptr;
    }

    void DrawMarks()
    {
        g_hitCount = 0;
        if (g_markCount == 0 || GetTickCount() - g_marksTime > MarkTimeout)
        {
            g_markCount = 0;
            return;
        }

        const DWORD manager = *CharacterManager;
        BYTE* characters = manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
        if (characters == nullptr)
        {
            return;
        }

        const float bob = sinf(Seconds() * 3.0f) * 2.5f;
        const float s = g_markSize;
        bool drawing = false;
        for (int i = 0; i < g_markCount; i++)
        {
            const MarkEntry& entry = g_marks[i];
            const BYTE* character = FindCharacter(characters, entry.id);
            if (character == nullptr || entry.mark == MarkNone)
            {
                continue;
            }

            const float* position = reinterpret_cast<const float*>(character + CharPosition);
            const float head[3] = { position[0], position[1],
                position[2] + *reinterpret_cast<const float*>(character + CharHeight) + g_headMargin };
            float x;
            float y;
            if (!Project(head, x, y))
            {
                continue;
            }

            const float top = y - MarkHeight * s + bob;
            const Rect hit = { x - MarkWidth * s / 2 - 4, top - 4, MarkWidth * s + 8, MarkHeight * s + 8 };
            const bool hover = g_hoverMark >= 0 && g_hoverMark < MaximumMarks && g_hits[g_hoverMark].id == entry.id;
            if (!drawing)
            {
                PeroniaUi::BeginShapes();
                drawing = true;
            }

            DrawMark(entry.mark, x, top, s, hover);
            g_hits[g_hitCount++] = { entry.id, hit };
        }

        if (drawing)
        {
            PeroniaUi::EndShapes();
        }
    }

    // Sin objetos con destructor: puede usar __try.
    void DrawMarksSafely()
    {
        __try
        {
            DrawMarks();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_hitCount = 0;
        }
    }

    void __cdecl BeginBitmapHook()
    {
        glGetFloatv(GL_MODELVIEW_MATRIX, g_modelView);
        glGetFloatv(GL_PROJECTION_MATRIX, g_projection);
        glGetIntegerv(GL_VIEWPORT, g_viewport);
        g_nextBeginBitmap();

        if (g_projection[11] != -1.0f || g_viewport[2] <= 0 || g_viewport[3] <= 0)
        {
            if (!g_projectionWarned)
            {
                g_projectionWarned = true;
                g_log("Quests: la escena no tiene la proyeccion 3D esperada; no se dibujan los signos.");
            }

            g_hitCount = 0;
            return;
        }

        DrawMarksSafely();
    }

    // --- Paquetes ---
    // Lee [cantidad] y las lineas [color] [texto 0]; devuelve false si el paquete esta cortado.
    bool ReadLines(const BYTE* packet, int size, int offset, std::vector<Line>& lines)
    {
        lines.clear();
        if (offset >= size)
        {
            return false;
        }

        const int count = packet[offset++];
        for (int i = 0; i < count; i++)
        {
            if (offset >= size)
            {
                return false;
            }

            const BYTE color = packet[offset++];
            const char* text = reinterpret_cast<const char*>(packet + offset);
            const int length = static_cast<int>(strnlen(text, size - offset));
            if (offset + length >= size)
            {
                return false;
            }

            lines.push_back({ color, std::string(text, length) });
            offset += length + 1;
        }

        return true;
    }

    void OnMarks(const BYTE* packet, int size)
    {
        const int count = size > HeaderLength ? packet[HeaderLength] : 0;
        g_markCount = 0;
        for (int i = 0; i < count && i < MaximumMarks && HeaderLength + 1 + i * 3 + 3 <= size; i++)
        {
            const BYTE* entry = packet + HeaderLength + 1 + i * 3;
            g_marks[g_markCount++] = { static_cast<WORD>(((entry[0] << 8) | entry[1]) & IdMask), entry[2] };
        }

        g_marksTime = GetTickCount();
    }

    void OnDialog(const BYTE* packet, int size)
    {
        if (size < HeaderLength + 4)
        {
            return;
        }

        const WORD npc = static_cast<WORD>(((packet[HeaderLength] << 8) | packet[HeaderLength + 1]) & IdMask);
        const BYTE action = packet[HeaderLength + 2];
        int offset = HeaderLength + 3;
        const char* title = reinterpret_cast<const char*>(packet + offset);
        const int titleLength = static_cast<int>(strnlen(title, size - offset));
        if (offset + titleLength >= size)
        {
            return;
        }

        offset += titleLength + 1;
        std::vector<Line> lines;
        if (!ReadLines(packet, size, offset, lines))
        {
            return;
        }

        g_dialogNpc = npc;
        g_dialogAction = action;
        g_dialogTitle.assign(title, titleLength);
        g_dialogLines.clear();
        for (const Line& line : lines)
        {
            Wrap(line, DialogWidth - DialogPadding * 2, g_dialogLines);
        }

        g_dialogOpen = true;
    }

    void OnInterfacePacket(const BYTE* packet, int size)
    {
        switch (packet[4])
        {
        case SubCodeMarks:
            OnMarks(packet, size);
            break;
        case SubCodeTracker:
        {
            std::vector<Line> lines;
            if (ReadLines(packet, size, HeaderLength, lines))
            {
                g_tracker.clear();
                for (const Line& line : lines)
                {
                    Wrap(line, TrackerWidth - 10, g_tracker);
                }
            }

            break;
        }
        case SubCodeDialog:
            OnDialog(packet, size);
            break;
        }
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (code == CodeInterface && packet != nullptr && packet[0] == 0xC2 && size > HeaderLength
            && packet[4] >= SubCodeMarks && packet[4] <= SubCodeDialog)
        {
            OnInterfacePacket(packet, size);
            return 1; // como el juego con los codigos que no conoce
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // --- Mouse ---
    bool MouseOverDialog()
    {
        return g_dialogOpen && PeroniaUi::MouseInside(DialogRect());
    }

    int HitMark()
    {
        for (int i = g_hitCount - 1; i >= 0; i--)
        {
            if (PeroniaUi::MouseInside(g_hits[i].rect))
            {
                return i;
            }
        }

        return -1;
    }

    void ClickDialog()
    {
        char text[48];
        if (g_dialogAction != ActionNone && PeroniaUi::MouseInside(ActionButtonRect()))
        {
            sprintf_s(text, "/quest %s %u", g_dialogAction == ActionAccept ? "aceptar" : "entregar", g_dialogNpc);
            QueueChat(text);
            g_dialogOpen = false;
        }
        else if (PeroniaUi::MouseInside(CloseButtonRect()))
        {
            g_dialogOpen = false;
        }
    }

    int __fastcall MouseHook(BYTE* manager, void* unused)
    {
        SendPendingChat();
        const bool overDialog = MouseOverDialog();
        const bool windowHasMouse = *reinterpret_cast<const DWORD*>(manager + WindowManagerMouse) != 0;
        g_hoverMark = overDialog || windowHasMouse ? -1 : HitMark();
        g_mouseOver = overDialog || g_hoverMark >= 0;
        if (!g_mouseOver)
        {
            return g_nextMouse(manager, unused);
        }

        // El mouse es de esta DLL: ninguna ventana lo tiene y el juego no camina.
        *reinterpret_cast<DWORD*>(manager + WindowManagerMouse) = 0;
        if (reinterpret_cast<KeyStateType>(IsReleaseFunction)(VK_LBUTTON))
        {
            if (overDialog)
            {
                ClickDialog();
            }
            else
            {
                char text[32];
                sprintf_s(text, "/quest %u", g_hits[g_hoverMark].id);
                QueueChat(text);
            }
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
            DrawTracker();
            if (g_dialogOpen)
            {
                DrawDialog();
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

    float ReadFloat(const char* iniPath, const char* key, float fallback)
    {
        char text[32];
        GetPrivateProfileStringA("Quests", key, "", text, sizeof(text), iniPath);
        return text[0] != '\0' ? static_cast<float>(atof(text)) : fallback;
    }
}

extern "C" __declspec(dllexport) bool StartQuests(const QuestsHost* host)
{
    if (host == nullptr || host->version != QuestsHostVersion)
    {
        return false;
    }

    g_log = host->log;
    g_trackerLeft = ReadFloat(host->iniPath, "TrackerLeft", g_trackerLeft);
    g_trackerTop = ReadFloat(host->iniPath, "TrackerTop", g_trackerTop);
    g_markSize = ReadFloat(host->iniPath, "MarkSize", g_markSize);
    g_headMargin = ReadFloat(host->iniPath, "HeadMargin", g_headMargin);
    if (g_markSize < 0.5f || g_markSize > 3.0f)
    {
        g_markSize = 1.0f;
    }

    // Todas las llamadas pueden estar redirigidas por otras DLL: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    const DWORD beginBitmap = CallTarget(BeginBitmapCallSite);
    const DWORD draw = CallTarget(DrawCallSite);
    const DWORD mouse = CallTarget(MouseCallSite);
    const DWORD mouseOnUi = JumpTarget(MouseOnUiFunction);
    if (dispatch == 0 || beginBitmap == 0 || draw == 0 || mouse == 0
        || (mouseOnUi == 0
            && memcmp(reinterpret_cast<const void*>(MouseOnUiFunction), MouseOnUiPrologue, sizeof(MouseOnUiPrologue)) != 0))
    {
        g_log("Quests: el juego no coincide; desactivado.");
        return false;
    }

    PeroniaUi::Initialize(host->iniPath, host->log);
    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    g_nextBeginBitmap = reinterpret_cast<DrawType>(beginBitmap);
    g_nextDraw = reinterpret_cast<DrawType>(draw);
    g_nextMouse = reinterpret_cast<MouseType>(mouse);
    g_nextMouseOnUi = reinterpret_cast<MouseOnUiType>(mouseOnUi);
    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&MouseOnUiHook) - (MouseOnUiFunction + 5);
    if (!WriteCode(MouseOnUiFunction, jump, sizeof(jump)) || !RedirectCall(MouseCallSite, &MouseHook)
        || !RedirectCall(DrawCallSite, &DrawHook) || !RedirectCall(BeginBitmapCallSite, &BeginBitmapHook)
        || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Quests: no se pudo instalar.");
        return false;
    }

    g_log("Quests listas (signos, ventana y seguimiento).");
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
