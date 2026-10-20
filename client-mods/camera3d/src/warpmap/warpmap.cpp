// warpmap.dll - Mapa interactivo en lugar de la lista del comando M para el cliente MU 1.04d con camera3d.dll.
//
// La ventana del comando M (CNewUIMoveCommandWindow, vtable 0x00D46C1C) muestra una lista con los warps de
// Movereq.bmd. Con warpmap.dll, mientras la ventana esta abierta se ve en cambio una imagen del mundo
// (warpmap.ini, [Map] Image) con una zona por warp ([Zones], en pixeles de la imagen). Los warps sin zona
// aparecen como botones al costado de la imagen.
//
// Como funciona:
//   - Se reemplazan dos funciones de la vtable de la ventana: el dibujo ([0], 0x008327F0) y los eventos del
//     mouse ([2], 0x00831F00). La apertura y el cierre (tecla M, Esc) siguen siendo los del juego.
//   - Los warps salen de la lista de la ventana (std::list en +0x1C: cabeza en +0x30, cantidad en +0x34;
//     cada nodo: siguiente en +0, dato en +8). Cada dato (MOVEINFODATA) tiene el indice (+0x00), el nombre
//     (+0x04), el nivel minimo (+0x44), el peligro (+0x4C, la columna que antes era el costo en zen) y si
//     el personaje puede ir (+0x54). Ese ultimo lo calcula el juego (0x00831710: nivel, zen, estado del
//     personaje, mapas especiales), solo para las filas visibles de la lista (+0x90 a +0x94); antes de
//     usarlo se lo calcula para todas.
//   - Un click en una zona hace lo mismo que el click en la fila de la lista (0x00831E2D): si hace falta
//     guarda las opciones (0x00832F50, 0x00630D20), pide la clave de la ventana (0x008314B0), manda el
//     pedido de warp con el indice (0x00787D00, C1 8E 02) y cierra la ventana (0x0085F850 con 2). El
//     servidor recibe exactamente el mismo paquete que con la lista.
//   - Mientras la ventana esta abierta, los eventos del mouse no pasan a otras ventanas ni al mundo (la
//     funcion devuelve 0, como cuando el mouse esta sobre la lista). Un click afuera de la imagen y de los
//     botones cierra la ventana.
//   - La imagen se lee con WIC, escalada al tamano en que se dibuja, y se sube una vez como textura de
//     OpenGL (otra vez si cambia el tamano de la ventana). Si no se puede leer, la ventana es la original.
//   - Los textos se dibujan con el texto del juego (0x00E8203C: fuente, colores y RenderText 0x00420150).

#include "../shared.h"

#include <GL/gl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wincodec.h>

#include <string>
#include <vector>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD MoveWindowVtable = 0x00D46C1C;
    constexpr DWORD MoveRenderFunction = 0x008327F0;      // bool __thiscall Render()
    constexpr DWORD MoveMouseFunction = 0x00831F00;       // bool __thiscall UpdateMouseEvent(): 0 si uso el mouse
    constexpr DWORD UpdateCanMoveFunction = 0x00831710;   // void __thiscall: +0x54 de las filas +0x90 a +0x94
    constexpr DWORD NeedsSaveFunction = 0x00832F50;       // int __thiscall (mapa actual, indice del warp)
    constexpr DWORD SaveOptionsFunction = 0x00630D20;     // void __cdecl(): C1 F3 30 (teclas rapidas y opciones)
    constexpr DWORD MoveKeyFunction = 0x008314B0;         // DWORD __thiscall: clave del pedido de warp
    constexpr DWORD SendMoveFunction = 0x00787D00;        // void __cdecl(DWORD clave, WORD indice): C1 8E 02
    constexpr DWORD UiSystemFunction = 0x00860FC0;        // CNewUISystem* __cdecl()
    constexpr DWORD HideWindowFunction = 0x0085F850;      // void __thiscall CNewUISystem::Hide(int ventana)
    constexpr DWORD IsReleaseFunction = 0x00790F00;       // bool __cdecl(int tecla): recien soltada
    constexpr DWORD SetFontFunction = 0x00420120;         // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;      // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool): mezcla con alfa (estado del juego)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl(): sin mezcla (estado del juego)
    constexpr int MoveWindowId = 2;
    constexpr int SortCenter = 3; // RenderText: centrado en la caja
    constexpr int MainBarHeight = 51; // barra de abajo de la interfaz, en 640x480

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const MouseX = reinterpret_cast<int*>(0x0879340C); // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    int* const MouseWheel = reinterpret_cast<int*>(0x0879337C);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    int* const CurrentMap = reinterpret_cast<int*>(0x00E61E18);
    BYTE** const HeroAttributes = reinterpret_cast<BYTE**>(0x08128AC8); // +0x0B clase, +0x0E nivel

    // Ventana del comando M y sus filas (MOVEINFODATA).
    constexpr DWORD WindowListHead = 0x30, WindowListSize = 0x34, WindowFirstRow = 0x90, WindowEndRow = 0x94;
    constexpr DWORD EntryIndex = 0x00, EntryName = 0x04, EntryLevel = 0x44, EntryDanger = 0x4C, EntryCanMove = 0x54;
    constexpr int EntryNameLength = 32;

    using RenderType = bool(__fastcall*)(BYTE* window, void* unused);
    using MouseType = bool(__fastcall*)(BYTE* window, void* unused);
    using UpdateCanMoveType = void(__fastcall*)(BYTE* window, void* unused);
    using NeedsSaveType = int(__fastcall*)(BYTE* window, void* unused, int map, int index);
    using MoveKeyType = DWORD(__fastcall*)(BYTE* window, void* unused);
    using SendMoveType = void(__cdecl*)(DWORD key, int index);
    using UiSystemType = BYTE*(__cdecl*)();
    using HideWindowType = void(__fastcall*)(BYTE* system, void* unused, int window);
    using IsReleaseType = bool(__cdecl*)(int key);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    constexpr GLenum BgraFormat = 0x80E1; // GL_BGRA_EXT

    // --- Configuracion (warpmap.ini) ---
    struct Zone
    {
        int index;
        std::vector<POINT> points; // en pixeles de la imagen original
    };

    struct NamedWarp
    {
        int index;
        std::string name;
    };

    LogFunction g_log = nullptr;
    char g_folder[MAX_PATH] = {};
    char g_imagePath[MAX_PATH] = {};
    std::vector<Zone> g_zones;
    std::vector<NamedWarp> g_names;
    std::string g_levelText = "Level";
    std::string g_dangerText = "Danger";
    std::string g_lockedText = "Locked";

    // --- Estado ---
    bool g_failed = false;     // la imagen no se pudo leer: ventana original
    GLuint g_texture = 0;
    int g_imageWidth = 0;      // tamano original de la imagen
    int g_imageHeight = 0;
    int g_textureWidth = 0;    // tamano escalado (lo que se subio) y de la textura (potencia de 2)
    int g_textureHeight = 0;
    int g_textureSizeX = 0;
    int g_textureSizeY = 0;
    DWORD g_lastMouseTick = 0;

    // Una fila de la ventana, con lo que se dibuja.
    struct Warp
    {
        BYTE* entry;
        int index;
        int level;
        int danger;
        bool canMove;
        const char* name;
    };

    struct Layout
    {
        float imageX, imageY, imageWidth, imageHeight; // pixeles de la ventana, origen arriba a la izquierda
        float columnX, columnY, buttonWidth, buttonHeight, buttonStep;
    };

    // Texto del ini (UTF-8 o ANSI) a la pagina de codigos del juego.
    std::string ToGameText(const char* text)
    {
        wchar_t wide[512];
        const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text, -1, wide, 512);
        if (length <= 0)
        {
            return text; // no es UTF-8: ya esta en ANSI
        }

        char converted[512];
        return WideCharToMultiByte(CP_ACP, 0, wide, -1, converted, sizeof(converted), nullptr, nullptr) > 0
            ? converted
            : text;
    }

    std::string ReadText(const char* ini, const char* section, const char* key, const char* fallback)
    {
        char value[256];
        GetPrivateProfileStringA(section, key, fallback, value, sizeof(value), ini);
        return ToGameText(value);
    }

    // Lineas "clave=valor" de una seccion, sin comentarios.
    std::vector<std::pair<std::string, std::string>> ReadSection(const char* ini, const char* section)
    {
        std::vector<char> buffer(32768);
        const DWORD length = GetPrivateProfileSectionA(section, buffer.data(), static_cast<DWORD>(buffer.size()), ini);
        std::vector<std::pair<std::string, std::string>> lines;
        for (const char* line = buffer.data(); line < buffer.data() + length && *line != '\0'; line += strlen(line) + 1)
        {
            const char* equals = strchr(line, '=');
            if (*line == ';' || equals == nullptr)
            {
                continue;
            }

            lines.emplace_back(std::string(line, equals), std::string(equals + 1));
        }

        return lines;
    }

    void LoadConfiguration()
    {
        char ini[MAX_PATH];
        sprintf_s(ini, "%swarpmap.ini", g_folder);
        if (GetFileAttributesA(ini) == INVALID_FILE_ATTRIBUTES)
        {
            g_log("Mapa del comando M: no esta %s; se usa mapa.png sin zonas.", ini);
        }

        char image[MAX_PATH];
        GetPrivateProfileStringA("Map", "Image", "mapa.png", image, sizeof(image), ini);
        sprintf_s(g_imagePath, "%s%s", g_folder, image);

        for (const auto& line : ReadSection(ini, "Zones"))
        {
            Zone zone{ atoi(line.first.c_str()) };
            std::vector<int> numbers;
            for (const char* p = line.second.c_str(); *p != '\0';)
            {
                char* end;
                const long number = strtol(p, &end, 10);
                if (end == p)
                {
                    p++;
                    continue;
                }

                numbers.push_back(static_cast<int>(number));
                p = end;
            }

            if (numbers.size() == 4)
            {
                zone.points = { { numbers[0], numbers[1] }, { numbers[2], numbers[1] }, { numbers[2], numbers[3] },
                    { numbers[0], numbers[3] } };
            }
            else if (numbers.size() >= 6 && numbers.size() % 2 == 0)
            {
                for (size_t i = 0; i < numbers.size(); i += 2)
                {
                    zone.points.push_back({ numbers[i], numbers[i + 1] });
                }
            }
            else
            {
                g_log("Mapa del comando M: la zona %s no tiene un rectangulo ni un poligono; se ignora.",
                    line.first.c_str());
                continue;
            }

            g_zones.push_back(zone);
        }

        for (const auto& line : ReadSection(ini, "Names"))
        {
            g_names.push_back({ atoi(line.first.c_str()), ToGameText(line.second.c_str()) });
        }

        g_levelText = ReadText(ini, "Text", "Level", "Level");
        g_dangerText = ReadText(ini, "Text", "Danger", "Danger");
        g_lockedText = ReadText(ini, "Text", "Locked", "Locked");
        g_log("Mapa del comando M: %s, %d zonas.", g_imagePath, static_cast<int>(g_zones.size()));
    }

    // --- Imagen ---
    int PowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result *= 2;
        }

        return result;
    }

    // Lee la imagen escalada a width x height (BGRA, filas de arriba hacia abajo).
    bool DecodeImage(int width, int height, std::vector<BYTE>& pixels)
    {
        const HRESULT com = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        bool decoded = false;
        IWICImagingFactory* factory = nullptr;
        IWICBitmapDecoder* decoder = nullptr;
        IWICBitmapFrameDecode* frame = nullptr;
        IWICBitmapScaler* scaler = nullptr;
        IWICFormatConverter* converter = nullptr;
        wchar_t path[MAX_PATH];
        MultiByteToWideChar(CP_ACP, 0, g_imagePath, -1, path, MAX_PATH);
        if (SUCCEEDED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory)))
            && SUCCEEDED(factory->CreateDecoderFromFilename(path, nullptr, GENERIC_READ, WICDecodeMetadataCacheOnDemand,
                &decoder))
            && SUCCEEDED(decoder->GetFrame(0, &frame)))
        {
            UINT originalWidth = 0;
            UINT originalHeight = 0;
            frame->GetSize(&originalWidth, &originalHeight);
            g_imageWidth = static_cast<int>(originalWidth);
            g_imageHeight = static_cast<int>(originalHeight);
            if (width <= 0 || height <= 0)
            {
                decoded = true; // solo el tamano
            }
            else if (SUCCEEDED(factory->CreateBitmapScaler(&scaler))
                && SUCCEEDED(scaler->Initialize(frame, width, height, WICBitmapInterpolationModeFant))
                && SUCCEEDED(factory->CreateFormatConverter(&converter))
                && SUCCEEDED(converter->Initialize(scaler, GUID_WICPixelFormat32bppBGRA, WICBitmapDitherTypeNone,
                    nullptr, 0.0, WICBitmapPaletteTypeCustom)))
            {
                pixels.resize(static_cast<size_t>(width) * height * 4);
                decoded = SUCCEEDED(converter->CopyPixels(nullptr, width * 4, static_cast<UINT>(pixels.size()),
                    pixels.data()));
            }
        }

        if (converter != nullptr) converter->Release();
        if (scaler != nullptr) scaler->Release();
        if (frame != nullptr) frame->Release();
        if (decoder != nullptr) decoder->Release();
        if (factory != nullptr) factory->Release();
        if (SUCCEEDED(com))
        {
            CoUninitialize();
        }

        return decoded;
    }

    // El fondo casi blanco que toca el borde de la imagen (afuera del pergamino) queda transparente.
    void ClearBackground(std::vector<BYTE>& pixels, int width, int height)
    {
        constexpr BYTE Light = 230;
        auto background = [&](int i) {
            const BYTE* pixel = &pixels[static_cast<size_t>(i) * 4];
            return pixel[3] != 0 && pixel[0] >= Light && pixel[1] >= Light && pixel[2] >= Light;
        };

        std::vector<int> pending;
        for (int x = 0; x < width; x++)
        {
            pending.push_back(x);
            pending.push_back((height - 1) * width + x);
        }

        for (int y = 0; y < height; y++)
        {
            pending.push_back(y * width);
            pending.push_back(y * width + width - 1);
        }

        while (!pending.empty())
        {
            const int i = pending.back();
            pending.pop_back();
            if (!background(i))
            {
                continue;
            }

            pixels[static_cast<size_t>(i) * 4 + 3] = 0;
            const int x = i % width;
            const int y = i / width;
            if (x > 0) pending.push_back(i - 1);
            if (x < width - 1) pending.push_back(i + 1);
            if (y > 0) pending.push_back(i - width);
            if (y < height - 1) pending.push_back(i + width);
        }
    }

    bool ImageSize()
    {
        if (g_imageWidth > 0 || g_failed)
        {
            return !g_failed;
        }

        std::vector<BYTE> none;
        if (!DecodeImage(0, 0, none) || g_imageWidth <= 0 || g_imageHeight <= 0)
        {
            g_failed = true;
            g_log("Mapa del comando M: no se pudo leer %s; se usa la lista original.", g_imagePath);
        }

        return !g_failed;
    }

    // Sube la imagen escalada al tamano en que se dibuja (otra vez solo si cambio ese tamano).
    bool PrepareTexture(int width, int height)
    {
        if (g_texture != 0 && width == g_textureWidth && height == g_textureHeight)
        {
            return true;
        }

        GLint maxSize = 0;
        glGetIntegerv(GL_MAX_TEXTURE_SIZE, &maxSize);
        const int sizeX = PowerOfTwo(width);
        const int sizeY = PowerOfTwo(height);
        std::vector<BYTE> pixels;
        if (width <= 0 || height <= 0 || sizeX > maxSize || sizeY > maxSize || !DecodeImage(width, height, pixels))
        {
            g_failed = true;
            g_log("Mapa del comando M: no se pudo preparar la imagen (%dx%d); se usa la lista original.", width, height);
            return false;
        }

        ClearBackground(pixels, width, height);

        if (g_texture == 0)
        {
            glGenTextures(1, &g_texture);
        }

        glPushAttrib(GL_TEXTURE_BIT);
        glPushClientAttrib(GL_CLIENT_PIXEL_STORE_BIT);
        glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
        glPixelStorei(GL_UNPACK_ROW_LENGTH, 0);
        glBindTexture(GL_TEXTURE_2D, g_texture);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, sizeX, sizeY, 0, BgraFormat, GL_UNSIGNED_BYTE, nullptr);
        glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, width, height, BgraFormat, GL_UNSIGNED_BYTE, pixels.data());
        glPopClientAttrib();
        glPopAttrib();
        g_textureWidth = width;
        g_textureHeight = height;
        g_textureSizeX = sizeX;
        g_textureSizeY = sizeY;
        return true;
    }

    // --- Datos de la ventana ---
    const char* WarpName(BYTE* entry, int index)
    {
        for (const NamedWarp& named : g_names)
        {
            if (named.index == index)
            {
                return named.name.c_str();
            }
        }

        static char names[64][EntryNameLength + 1];
        static int next = 0;
        char* name = names[next++ % 64];
        memcpy(name, entry + EntryName, EntryNameLength);
        name[EntryNameLength] = '\0';
        return name;
    }

    // Nivel minimo como lo muestra la lista: 2/3 para Magic Gladiator, Dark Lord y Rage Fighter (salvo 400).
    int RequiredLevel(int level)
    {
        const BYTE* attributes = *HeroAttributes;
        const int heroClass = attributes != nullptr ? attributes[0x0B] & 7 : 0;
        if ((heroClass == 3 || heroClass == 4 || heroClass == 6) && level != 400)
        {
            return static_cast<int>(level * 2 / 3.0);
        }

        return level;
    }

    int HeroLevel()
    {
        const BYTE* attributes = *HeroAttributes;
        return attributes != nullptr ? *reinterpret_cast<const WORD*>(attributes + 0x0E) : 0;
    }

    std::vector<Warp> ReadWarps(BYTE* window)
    {
        std::vector<Warp> warps;
        BYTE* const head = *reinterpret_cast<BYTE**>(window + WindowListHead);
        const int size = *reinterpret_cast<int*>(window + WindowListSize);
        if (head == nullptr || size <= 0 || size > 1000)
        {
            return warps;
        }

        // El juego calcula "puede ir" solo para las filas visibles; se lo pide para todas.
        int& first = *reinterpret_cast<int*>(window + WindowFirstRow);
        int& end = *reinterpret_cast<int*>(window + WindowEndRow);
        const int savedFirst = first;
        const int savedEnd = end;
        first = 0;
        end = size;
        reinterpret_cast<UpdateCanMoveType>(UpdateCanMoveFunction)(window, nullptr);
        first = savedFirst;
        end = savedEnd;

        BYTE* node = *reinterpret_cast<BYTE**>(head);
        for (int i = 0; node != head && node != nullptr && i < size; i++)
        {
            BYTE* entry = *reinterpret_cast<BYTE**>(node + 8);
            if (entry != nullptr)
            {
                const int index = *reinterpret_cast<int*>(entry + EntryIndex);
                warps.push_back({ entry, index, RequiredLevel(*reinterpret_cast<int*>(entry + EntryLevel)),
                    *reinterpret_cast<int*>(entry + EntryDanger), entry[EntryCanMove] == 1, WarpName(entry, index) });
            }

            node = *reinterpret_cast<BYTE**>(node);
        }

        return warps;
    }

    const Zone* FindZone(int index)
    {
        for (const Zone& zone : g_zones)
        {
            if (zone.index == index)
            {
                return &zone;
            }
        }

        return nullptr;
    }

    int ButtonCount(const std::vector<Warp>& warps)
    {
        int count = 0;
        for (const Warp& warp : warps)
        {
            count += FindZone(warp.index) == nullptr ? 1 : 0;
        }

        return count;
    }

    // --- Ubicacion en la pantalla ---
    Layout ComputeLayout(int buttons)
    {
        const float width = static_cast<float>(*WindowWidth);
        const float height = static_cast<float>(*WindowHeight);
        const float margin = height * 0.02f;
        const float bar = height * MainBarHeight / 480; // la barra de abajo del juego se dibuja encima
        const float gap = width * 0.015f;
        Layout layout{};
        layout.buttonWidth = buttons > 0 ? width * 0.16f : 0.0f;
        layout.buttonHeight = height * 0.04f;
        layout.buttonStep = layout.buttonHeight * 1.25f;
        const float aspect = static_cast<float>(g_imageWidth) / g_imageHeight;
        layout.imageHeight = height - bar - 2 * margin;
        layout.imageWidth = layout.imageHeight * aspect;
        const float room = width - 2 * margin - (buttons > 0 ? gap + layout.buttonWidth : 0.0f);
        if (layout.imageWidth > room)
        {
            layout.imageWidth = room;
            layout.imageHeight = room / aspect;
        }

        layout.imageWidth = static_cast<float>(static_cast<int>(layout.imageWidth));
        layout.imageHeight = static_cast<float>(static_cast<int>(layout.imageHeight));
        const float total = layout.imageWidth + (buttons > 0 ? gap + layout.buttonWidth : 0.0f);
        layout.imageX = static_cast<float>(static_cast<int>((width - total) / 2));
        layout.imageY = static_cast<float>(static_cast<int>((height - bar - layout.imageHeight) / 2));
        layout.columnX = layout.imageX + layout.imageWidth + gap;
        layout.columnY = layout.imageY + (layout.imageHeight - buttons * layout.buttonStep) / 2;
        return layout;
    }

    POINT MousePixels()
    {
        return { *MouseX * *WindowWidth / 640, *MouseY * *WindowHeight / 480 };
    }

    // Punto de la imagen original a pixeles de la ventana.
    void ImageToScreen(const Layout& layout, const POINT& point, float& x, float& y)
    {
        x = layout.imageX + point.x * layout.imageWidth / g_imageWidth;
        y = layout.imageY + point.y * layout.imageHeight / g_imageHeight;
    }

    bool InsideZone(const Layout& layout, const Zone& zone, const POINT& mouse)
    {
        const float x = (mouse.x - layout.imageX) * g_imageWidth / layout.imageWidth;
        const float y = (mouse.y - layout.imageY) * g_imageHeight / layout.imageHeight;
        bool inside = false;
        const size_t count = zone.points.size();
        for (size_t i = 0, j = count - 1; i < count; j = i++)
        {
            const POINT& a = zone.points[i];
            const POINT& b = zone.points[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / static_cast<float>(b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    bool InsideRect(float left, float top, float width, float height, const POINT& mouse)
    {
        return mouse.x >= left && mouse.x < left + width && mouse.y >= top && mouse.y < top + height;
    }

    // Warp bajo el mouse (indice en warps) o -1; outside: el mouse no esta sobre la imagen ni los botones.
    int HitTest(const Layout& layout, const std::vector<Warp>& warps, const POINT& mouse, bool& outside)
    {
        int button = 0;
        outside = !InsideRect(layout.imageX, layout.imageY, layout.imageWidth, layout.imageHeight, mouse);
        for (size_t i = 0; i < warps.size(); i++)
        {
            const Zone* zone = FindZone(warps[i].index);
            if (zone != nullptr)
            {
                if (InsideZone(layout, *zone, mouse))
                {
                    return static_cast<int>(i);
                }

                continue;
            }

            if (InsideRect(layout.columnX, layout.columnY + button * layout.buttonStep, layout.buttonWidth,
                    layout.buttonHeight, mouse))
            {
                outside = false;
                return static_cast<int>(i);
            }

            button++;
        }

        return -1;
    }

    // --- Pedido de warp: lo mismo que el click en una fila de la lista (0x00831E2D) ---
    void SendWarp(BYTE* window, int index)
    {
        if (reinterpret_cast<NeedsSaveType>(NeedsSaveFunction)(window, nullptr, *CurrentMap, index) != 0)
        {
            reinterpret_cast<void(__cdecl*)()>(SaveOptionsFunction)();
        }

        const DWORD key = reinterpret_cast<MoveKeyType>(MoveKeyFunction)(window, nullptr);
        reinterpret_cast<SendMoveType>(SendMoveFunction)(key, index);
        g_log("Mapa del comando M: warp %d.", index);
    }

    void HideWindow()
    {
        BYTE* system = reinterpret_cast<UiSystemType>(UiSystemFunction)();
        reinterpret_cast<HideWindowType>(HideWindowFunction)(system, nullptr, MoveWindowId);
    }

    bool __fastcall MouseHook(BYTE* window, void* unused)
    {
        if (g_failed || !ImageSize())
        {
            return reinterpret_cast<MouseType>(MoveMouseFunction)(window, unused);
        }

        // Recien abierta: el click que la abrio (si fue con el mouse) no la cierra.
        const DWORD now = GetTickCount();
        const bool justOpened = now - g_lastMouseTick > 250;
        g_lastMouseTick = now;
        *MouseWheel = 0;
        if (justOpened || !reinterpret_cast<IsReleaseType>(IsReleaseFunction)(VK_LBUTTON))
        {
            return false;
        }

        const std::vector<Warp> warps = ReadWarps(window);
        const Layout layout = ComputeLayout(ButtonCount(warps));
        bool outside = false;
        const int hit = HitTest(layout, warps, MousePixels(), outside);
        if (hit >= 0 && warps[hit].canMove)
        {
            SendWarp(window, warps[hit].index);
            HideWindow();
        }
        else if (hit < 0 && outside)
        {
            HideWindow();
        }

        return false; // el mouse es de la ventana: no llega al mundo ni a otras ventanas
    }

    // --- Dibujo ---
    void Quad(float left, float top, float right, float bottom)
    {
        glVertex2f(left, top);
        glVertex2f(right, top);
        glVertex2f(right, bottom);
        glVertex2f(left, bottom);
    }

    void Outline(float left, float top, float right, float bottom)
    {
        glBegin(GL_LINE_LOOP);
        Quad(left + 0.5f, top + 0.5f, right - 0.5f, bottom - 0.5f);
        glEnd();
    }

    void FillZone(const Layout& layout, const Zone& zone, GLenum mode)
    {
        glBegin(mode);
        for (const POINT& point : zone.points)
        {
            float x;
            float y;
            ImageToScreen(layout, point, x, y);
            glVertex2f(x, y);
        }

        glEnd();
    }

    void DrawShapes(const Layout& layout, const std::vector<Warp>& warps, int hovered)
    {
        const float width = static_cast<float>(*WindowWidth);
        const float height = static_cast<float>(*WindowHeight);
        glMatrixMode(GL_PROJECTION);
        glPushMatrix();
        glLoadIdentity();
        glOrtho(0, width, height, 0, -1, 1);
        glMatrixMode(GL_MODELVIEW);
        glPushMatrix();
        glLoadIdentity();
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_TEXTURE_BIT
            | GL_LINE_BIT | GL_POLYGON_BIT);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glDisable(GL_FOG);
        glDisable(GL_LIGHTING);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
        glLineWidth(2.0f);

        // Fondo oscuro sobre el juego.
        glDisable(GL_TEXTURE_2D);
        glColor4f(0.0f, 0.0f, 0.0f, 0.6f);
        glBegin(GL_QUADS);
        Quad(0, 0, width, height);
        glEnd();

        // La imagen.
        glEnable(GL_TEXTURE_2D);
        glBindTexture(GL_TEXTURE_2D, g_texture);
        glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
        const float u = static_cast<float>(g_textureWidth) / g_textureSizeX;
        const float v = static_cast<float>(g_textureHeight) / g_textureSizeY;
        const float right = layout.imageX + layout.imageWidth;
        const float bottom = layout.imageY + layout.imageHeight;
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        glBegin(GL_QUADS);
        glTexCoord2f(0, 0);
        glVertex2f(layout.imageX, layout.imageY);
        glTexCoord2f(u, 0);
        glVertex2f(right, layout.imageY);
        glTexCoord2f(u, v);
        glVertex2f(right, bottom);
        glTexCoord2f(0, v);
        glVertex2f(layout.imageX, bottom);
        glEnd();
        glDisable(GL_TEXTURE_2D);

        // Zonas: oscuras si el personaje no puede ir, resaltadas con el mouse encima.
        int button = 0;
        for (size_t i = 0; i < warps.size(); i++)
        {
            const Warp& warp = warps[i];
            const bool hover = static_cast<int>(i) == hovered;
            const Zone* zone = FindZone(warp.index);
            if (zone != nullptr)
            {
                if (!warp.canMove)
                {
                    glColor4f(0.0f, 0.0f, 0.0f, hover ? 0.35f : 0.45f);
                    FillZone(layout, *zone, GL_POLYGON);
                }
                else if (hover)
                {
                    glColor4f(1.0f, 0.85f, 0.4f, 0.25f);
                    FillZone(layout, *zone, GL_POLYGON);
                }

                if (hover)
                {
                    glColor4f(1.0f, 0.85f, 0.4f, 0.9f);
                    FillZone(layout, *zone, GL_LINE_LOOP);
                }

                continue;
            }

            const float top = layout.columnY + button * layout.buttonStep;
            const float buttonRight = layout.columnX + layout.buttonWidth;
            const float buttonBottom = top + layout.buttonHeight;
            button++;
            if (hover && warp.canMove)
            {
                glColor4f(0.45f, 0.32f, 0.12f, 0.9f);
            }
            else
            {
                glColor4f(0.08f, 0.06f, 0.04f, 0.85f);
            }

            glBegin(GL_QUADS);
            Quad(layout.columnX, top, buttonRight, buttonBottom);
            glEnd();
            if (hover)
            {
                glColor4f(1.0f, 0.85f, 0.4f, 0.9f);
            }
            else
            {
                glColor4f(0.6f, 0.5f, 0.3f, warp.canMove ? 0.8f : 0.4f);
            }

            Outline(layout.columnX, top, buttonRight, buttonBottom);
        }

        glPopAttrib();
        glMatrixMode(GL_PROJECTION);
        glPopMatrix();
        glMatrixMode(GL_MODELVIEW);
        glPopMatrix();
    }

    // Recuadro del cartel (pixeles, origen arriba a la izquierda) con la proyeccion 2D del juego (origen abajo).
    void DrawBox(float left, float top, float right, float bottom)
    {
        const float height = static_cast<float>(*WindowHeight);
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glColor4f(0.0f, 0.0f, 0.0f, 0.85f);
        glBegin(GL_QUADS);
        Quad(left, height - top, right, height - bottom);
        glEnd();
        glLineWidth(1.0f);
        glColor4f(0.8f, 0.65f, 0.35f, 0.9f);
        Outline(left, height - bottom, right, height - top);
        glPopAttrib();
    }

    DWORD Color(BYTE red, BYTE green, BYTE blue)
    {
        return red | (green << 8) | (blue << 16) | 0xFF000000;
    }

    // Texto centrado en una caja, en coordenadas de 640x480.
    void Text(const char* text, float left, float top, float width, DWORD color, bool bold)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr)
        {
            return;
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = color;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = 0; // sin fondo
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, bold ? *BoldFont : *NormalFont);
        reinterpret_cast<RenderTextType>(RenderTextFunction)(RenderText, nullptr, static_cast<int>(left),
            static_cast<int>(top), text, static_cast<int>(width), 0, SortCenter, nullptr);
    }

    void DrawTexts(const Layout& layout, const std::vector<Warp>& warps, int hovered)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr)
        {
            return;
        }

        // Como la lista original: mezcla con alfa del juego para los textos, y al final sin mezcla.
        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        const DWORD savedText = *reinterpret_cast<DWORD*>(renderer + 0x10);
        const DWORD savedBackground = *reinterpret_cast<DWORD*>(renderer + 0x14);
        const float toX = 640.0f / *WindowWidth;
        const float toY = 480.0f / *WindowHeight;

        // Nombres de los botones.
        int button = 0;
        for (const Warp& warp : warps)
        {
            if (FindZone(warp.index) != nullptr)
            {
                continue;
            }

            const float top = layout.columnY + button * layout.buttonStep;
            button++;
            Text(warp.name, layout.columnX * toX, (top + layout.buttonHeight / 2) * toY - 5.0f,
                layout.buttonWidth * toX, warp.canMove ? Color(255, 230, 180) : Color(130, 120, 110), false);
        }

        // Cartel del warp bajo el mouse: nombre, nivel minimo y peligro.
        if (hovered >= 0)
        {
            const Warp& warp = warps[hovered];
            char level[96];
            char danger[96];
            sprintf_s(level, "%s: %d", g_levelText.c_str(), warp.level);
            sprintf_s(danger, "%s: %d", g_dangerText.c_str(), warp.danger);
            const bool locked = !warp.canMove;
            const int lines = locked ? 4 : 3;
            const float boxWidth = 120.0f; // 640x480
            const float lineHeight = 12.0f;
            const POINT mouse = MousePixels();
            float left = mouse.x * toX + 12.0f;
            float top = mouse.y * toY + 14.0f;
            const float boxHeight = lines * lineHeight + 8.0f;
            if (left + boxWidth > 636.0f)
            {
                left = mouse.x * toX - 12.0f - boxWidth;
            }

            if (top + boxHeight > 476.0f)
            {
                top = 476.0f - boxHeight;
            }

            DrawBox(left / toX, top / toY, (left + boxWidth) / toX, (top + boxHeight) / toY);
            float y = top + 4.0f;
            Text(warp.name, left, y, boxWidth, Color(255, 204, 26), true);
            y += lineHeight;
            Text(level, left, y, boxWidth, HeroLevel() < warp.level ? Color(255, 51, 26) : Color(230, 230, 230), false);
            y += lineHeight;
            Text(danger, left, y, boxWidth, Color(230, 230, 230), false);
            if (locked)
            {
                y += lineHeight;
                Text(g_lockedText.c_str(), left, y, boxWidth, Color(255, 51, 26), false);
            }
        }

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }

    // Sin objetos con destructor: puede usar __try.
    void DrawAll(const Layout& layout, const std::vector<Warp>& warps, int hovered)
    {
        __try
        {
            DrawShapes(layout, warps, hovered);
            DrawTexts(layout, warps, hovered);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    bool __fastcall RenderHook(BYTE* window, void* unused)
    {
        if (g_failed || !ImageSize())
        {
            return reinterpret_cast<RenderType>(MoveRenderFunction)(window, unused);
        }

        const std::vector<Warp> warps = ReadWarps(window);
        const Layout layout = ComputeLayout(ButtonCount(warps));
        if (!PrepareTexture(static_cast<int>(layout.imageWidth), static_cast<int>(layout.imageHeight)))
        {
            return reinterpret_cast<RenderType>(MoveRenderFunction)(window, unused);
        }

        bool outside = false;
        DrawAll(layout, warps, HitTest(layout, warps, MousePixels(), outside));
        return true;
    }

    bool SlotMatches(int slot, DWORD function)
    {
        return reinterpret_cast<const DWORD*>(MoveWindowVtable)[slot] == function;
    }

    bool SetSlot(int slot, const void* function)
    {
        const DWORD value = reinterpret_cast<DWORD>(function);
        return WriteCode(MoveWindowVtable + slot * 4, &value, sizeof(value));
    }
}

extern "C" __declspec(dllexport) bool StartWarpMap(const WarpMapHost* host)
{
    if (host == nullptr || host->version != WarpMapHostVersion)
    {
        return false;
    }

    g_log = host->log;
    HMODULE module = nullptr;
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&StartWarpMap), &module)
        || GetModuleFileNameA(module, g_folder, MAX_PATH) == 0)
    {
        return false;
    }

    char* slash = strrchr(g_folder, '\\');
    if (slash != nullptr)
    {
        slash[1] = '\0';
    }

    if (!SlotMatches(0, MoveRenderFunction) || !SlotMatches(2, MoveMouseFunction))
    {
        g_log("Mapa del comando M: la ventana del juego no coincide; desactivado.");
        return false;
    }

    LoadConfiguration();
    if (!SetSlot(0, &RenderHook) || !SetSlot(2, &MouseHook))
    {
        g_log("Mapa del comando M: no se pudo instalar.");
        return false;
    }

    g_log("Mapa del comando M listo.");
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
