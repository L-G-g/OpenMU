// peroniaui - Estilo de las ventanas y botones nuevos de PeroniaMU. Ver peroniaui.h.
//
// Direcciones del main.exe 1.04d que usa (las mismas que loot.dll):
//   - Mouse en 640x480: 0x0879340C (x) y 0x08793410 (y).
//   - Tamano de la ventana en pixeles: 0x00E61E58 y 0x00E61E5C.
//   - Texto del juego: CUIRenderText en 0x00E8203C (+4 el que dibuja: +0x10 color, +0x14 fondo), SetFont
//     0x00420120, RenderText 0x00420150 (x, y, texto, ancho, alto, orden: 3 centro). Fuentes: normal 0x00E8C588,
//     negrita 0x00E8C58C. Mezcla con alfa del juego: 0x00635FD0 (activar) y 0x00635F50 (desactivar).

#include "peroniaui.h"

#include <GL/gl.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include <wincodec.h>

#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "windowscodecs.lib")
#pragma comment(lib, "gdi32.lib")

namespace
{
    BYTE* const RenderTextObject = reinterpret_cast<BYTE*>(0x00E8203C);
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const GameMouseX = reinterpret_cast<int*>(0x0879340C);
    int* const GameMouseY = reinterpret_cast<int*>(0x08793410);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    constexpr DWORD SetFontFunction = 0x00420120;
    constexpr DWORD RenderTextFunction = 0x00420150;
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0;
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50;
    constexpr int SortCenter = 3;

    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    constexpr float Pi = 3.14159265f;
    constexpr float BottomBarTop = 429.0f;

    float Seconds()
    {
        return (GetTickCount() % 3600000u) / 1000.0f;
    }

    // 640x480 -> pixeles de la proyeccion del juego (origen abajo).
    float Px(float x)
    {
        return x * *WindowWidth / 640.0f;
    }

    float Py(float y)
    {
        return *WindowHeight - y * *WindowHeight / 480.0f;
    }

    // Un pixel de la pantalla en unidades de 640x480 (para lineas finas nitidas).
    float OnePixel()
    {
        return *WindowWidth > 0 ? 640.0f / *WindowWidth : 1.0f;
    }

    // Grosor de las lineas del marco: 1 pixel de 640x480, nunca menos de un pixel de la pantalla.
    float Line()
    {
        const float pixel = OnePixel();
        return pixel > 1.0f ? pixel : floorf(1.0f / pixel + 0.5f) * pixel;
    }

    void Vertex(float x, float y)
    {
        glVertex2f(Px(x), Py(y));
    }

    void Fill(float left, float top, float right, float bottom, float r, float g, float b, float a)
    {
        glColor4f(r, g, b, a);
        glBegin(GL_QUADS);
        Vertex(left, top);
        Vertex(right, top);
        Vertex(right, bottom);
        Vertex(left, bottom);
        glEnd();
    }

    // Degradado vertical: color de arriba y de abajo.
    void FillVertical(float left, float top, float right, float bottom, const float* upper, const float* lower)
    {
        glBegin(GL_QUADS);
        glColor4fv(upper);
        Vertex(left, top);
        Vertex(right, top);
        glColor4fv(lower);
        Vertex(right, bottom);
        Vertex(left, bottom);
        glEnd();
    }

    void Additive()
    {
        glBlendFunc(GL_SRC_ALPHA, GL_ONE);
    }

    void Normal()
    {
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    }

    // Marco de "width" de ancho dentro del rect, con degradado del borde de afuera (outer) hacia adentro (60%),
    // igual en los cuatro lados (trapecios que se juntan en las esquinas).
    void Bevel(float left, float top, float right, float bottom, float width, float outer)
    {
        const float o[4] = { outer, outer * 0.98f, outer * 0.94f, 1.0f };
        const float i[4] = { outer * 0.6f, outer * 0.59f, outer * 0.57f, 1.0f };
        const float il = left + width, it = top + width, ir = right - width, ib = bottom - width;
        const float outerPoints[5][2] = { { left, top }, { right, top }, { right, bottom }, { left, bottom }, { left, top } };
        const float innerPoints[5][2] = { { il, it }, { ir, it }, { ir, ib }, { il, ib }, { il, it } };
        glBegin(GL_QUAD_STRIP);
        for (int k = 0; k < 5; k++)
        {
            glColor4fv(o);
            Vertex(outerPoints[k][0], outerPoints[k][1]);
            glColor4fv(i);
            Vertex(innerPoints[k][0], innerPoints[k][1]);
        }

        glEnd();
    }

    // Halo eliptico: centro con alfa, borde transparente.
    void Halo(float cx, float cy, float rx, float ry, float r, float g, float b, float a)
    {
        constexpr int Segments = 48;
        glBegin(GL_TRIANGLE_FAN);
        glColor4f(r, g, b, a);
        Vertex(cx, cy);
        glColor4f(r, g, b, 0.0f);
        for (int i = 0; i <= Segments; i++)
        {
            const float angle = 2 * Pi * i / Segments;
            Vertex(cx + cosf(angle) * rx, cy + sinf(angle) * ry);
        }

        glEnd();
    }

    // Anillo de luz alrededor de un rect, con las esquinas redondeadas: alfa "a" pegado al rect, que se apaga en dos
    // tramos (rapido y despues lento) hasta 0 a "spread" de distancia. Asi no se ve un rectangulo de luz.
    void Rim(const PeroniaUi::Rect& rect, float spread, float r, float g, float b, float a)
    {
        constexpr int CornerSegments = 10;
        const float cornerX[4] = { rect.Right(), rect.left, rect.left, rect.Right() };
        const float cornerY[4] = { rect.top, rect.top, rect.Bottom(), rect.Bottom() };
        const float middle = 0.35f; // alfa relativo a mitad de camino
        float points[4 * (CornerSegments + 1) + 1][3];
        int count = 0;
        for (int corner = 0; corner < 4; corner++)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                // Esquinas: arriba derecha (de 0 a 90 grados, con y para arriba), arriba izquierda, abajo izquierda, abajo derecha.
                const float angle = (corner + static_cast<float>(i) / CornerSegments) * Pi / 2;
                points[count][0] = cornerX[corner];
                points[count][1] = cornerY[corner];
                points[count][2] = angle;
                count++;
            }
        }

        points[count][0] = points[0][0];
        points[count][1] = points[0][1];
        points[count][2] = points[0][2];
        count++;
        for (int ring = 0; ring < 2; ring++)
        {
            const float from = ring == 0 ? 0.0f : spread * 0.4f;
            const float to = ring == 0 ? spread * 0.4f : spread;
            const float alphaFrom = ring == 0 ? a : a * middle;
            const float alphaTo = ring == 0 ? a * middle : 0.0f;
            glBegin(GL_QUAD_STRIP);
            for (int k = 0; k < count; k++)
            {
                const float dx = cosf(points[k][2]);
                const float dy = -sinf(points[k][2]); // en 640x480 y crece para abajo
                glColor4f(r, g, b, alphaFrom);
                Vertex(points[k][0] + dx * from, points[k][1] + dy * from);
                glColor4f(r, g, b, alphaTo);
                Vertex(points[k][0] + dx * to, points[k][1] + dy * to);
            }

            glEnd();
        }
    }

    // --- Textura de piedra de la interfaz original ---
    // Los .OZJ del cliente son JPEG con una cabecera propia adelante: se busca el comienzo del JPEG (FF D8 FF) y se
    // decodifica con WIC (Windows). La textura se crea la primera vez que se dibuja (hace falta el contexto de OpenGL).
    char g_stonePath[MAX_PATH] = "Data\\Interface\\newui_msgbox_back.OZJ";
    GLuint g_stone = 0;
    int g_stoneWidth = 0;
    int g_stoneHeight = 0;
    bool g_stoneTried = false;
    LogFunction g_uiLog = nullptr;

    bool DecodeJpeg(const BYTE* data, size_t size, std::vector<BYTE>& pixels, UINT& width, UINT& height)
    {
        const HRESULT com = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        IWICImagingFactory* factory = nullptr;
        IWICStream* stream = nullptr;
        IWICBitmapDecoder* decoder = nullptr;
        IWICBitmapFrameDecode* frame = nullptr;
        IWICFormatConverter* converter = nullptr;
        bool ok = SUCCEEDED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory)))
            && SUCCEEDED(factory->CreateStream(&stream))
            && SUCCEEDED(stream->InitializeFromMemory(const_cast<BYTE*>(data), static_cast<DWORD>(size)))
            && SUCCEEDED(factory->CreateDecoderFromStream(stream, nullptr, WICDecodeMetadataCacheOnLoad, &decoder))
            && SUCCEEDED(decoder->GetFrame(0, &frame))
            && SUCCEEDED(factory->CreateFormatConverter(&converter))
            && SUCCEEDED(converter->Initialize(frame, GUID_WICPixelFormat32bppRGBA, WICBitmapDitherTypeNone, nullptr, 0.0,
                WICBitmapPaletteTypeCustom))
            && SUCCEEDED(converter->GetSize(&width, &height));
        if (ok)
        {
            pixels.resize(static_cast<size_t>(width) * height * 4);
            ok = SUCCEEDED(converter->CopyPixels(nullptr, width * 4, static_cast<UINT>(pixels.size()), pixels.data()));
        }

        if (converter) converter->Release();
        if (frame) frame->Release();
        if (decoder) decoder->Release();
        if (stream) stream->Release();
        if (factory) factory->Release();
        if (SUCCEEDED(com))
        {
            CoUninitialize();
        }

        return ok;
    }

    void LoadStone()
    {
        g_stoneTried = true;
        FILE* file = nullptr;
        if (fopen_s(&file, g_stonePath, "rb") != 0 || file == nullptr)
        {
            if (g_uiLog) g_uiLog("Estilo: no se encontro la textura %s; sin piedra.", g_stonePath);
            return;
        }

        std::vector<BYTE> data;
        BYTE buffer[4096];
        size_t read;
        while ((read = fread(buffer, 1, sizeof(buffer), file)) > 0)
        {
            data.insert(data.end(), buffer, buffer + read);
        }

        fclose(file);
        // La cabecera de 24 bytes del .OZJ tambien empieza con FF D8 FF: si el JPEG esta despues, se usa ese.
        constexpr size_t OzjHeader = 24;
        size_t start = data.size() > OzjHeader + 3 && data[OzjHeader] == 0xFF && data[OzjHeader + 1] == 0xD8
                && data[OzjHeader + 2] == 0xFF
            ? OzjHeader
            : 0;
        while (start + 3 <= data.size() && !(data[start] == 0xFF && data[start + 1] == 0xD8 && data[start + 2] == 0xFF))
        {
            start++;
        }

        std::vector<BYTE> pixels;
        UINT width = 0, height = 0;
        if (start + 3 > data.size() || !DecodeJpeg(data.data() + start, data.size() - start, pixels, width, height))
        {
            if (g_uiLog) g_uiLog("Estilo: no se pudo leer la textura %s.", g_stonePath);
            return;
        }

        GLint previous = 0;
        glGetIntegerv(GL_TEXTURE_BINDING_2D, &previous);
        glGenTextures(1, &g_stone);
        glBindTexture(GL_TEXTURE_2D, g_stone);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
        glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(previous));
        g_stoneWidth = static_cast<int>(width);
        g_stoneHeight = static_cast<int>(height);
        if (g_uiLog) g_uiLog("Estilo: textura de piedra %s (%ux%u).", g_stonePath, width, height);
    }

    // Piedra en la cara del boton, del tamano real de la textura en pixeles de la pantalla (repetida si hace falta).
    // El juego recuerda que textura dejo puesta: se vuelve a poner la que habia.
    bool StoneFace(float left, float top, float right, float bottom, float shade)
    {
        if (!g_stoneTried)
        {
            LoadStone();
        }

        if (g_stone == 0)
        {
            return false;
        }

        GLint previous = 0;
        glGetIntegerv(GL_TEXTURE_BINDING_2D, &previous);
        glEnable(GL_TEXTURE_2D);
        glBindTexture(GL_TEXTURE_2D, g_stone);
        glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
        const float u = (Px(right) - Px(left)) / g_stoneWidth;
        const float v = (Py(top) - Py(bottom)) / g_stoneHeight;
        glColor4f(shade, shade, shade, 1.0f);
        glBegin(GL_QUADS);
        glTexCoord2f(0, 0);
        Vertex(left, top);
        glTexCoord2f(u, 0);
        Vertex(right, top);
        glTexCoord2f(u, v);
        Vertex(right, bottom);
        glTexCoord2f(0, v);
        Vertex(left, bottom);
        glEnd();
        glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(previous));
        glDisable(GL_TEXTURE_2D);
        return true;
    }

    // --- Marco original de los botones (Devil Square) ---
    // newui_btn_empty_big.OZT: un TGA con 4 bytes de cabecera propia adelante, con los estados del boton uno debajo del
    // otro (normal, mouse encima, apretado). Se dibuja en tres partes: las puntas a su ancho proporcional y el medio
    // estirado, asi el bisel no se deforma.
    char g_framePath[MAX_PATH] = "Data\\Interface\\newui_btn_empty_big.OZT";
    int g_frameStates = 3;
    GLuint g_frame = 0;
    int g_frameWidth = 0;
    int g_frameHeight = 0;
    bool g_frameTried = false;

    bool ReadWholeFile(const char* path, std::vector<BYTE>& data)
    {
        FILE* file = nullptr;
        if (fopen_s(&file, path, "rb") != 0 || file == nullptr)
        {
            return false;
        }

        BYTE buffer[4096];
        size_t read;
        while ((read = fread(buffer, 1, sizeof(buffer), file)) > 0)
        {
            data.insert(data.end(), buffer, buffer + read);
        }

        fclose(file);
        return true;
    }

    // TGA sin compresion o con RLE (tipos 2 y 10), de 24 o 32 bits, a RGBA con la fila de arriba primero.
    bool DecodeTga(const BYTE* data, size_t size, std::vector<BYTE>& pixels, int& width, int& height)
    {
        if (size < 18 || (data[2] != 2 && data[2] != 10) || (data[16] != 24 && data[16] != 32) || data[1] != 0)
        {
            return false;
        }

        width = data[12] | (data[13] << 8);
        height = data[14] | (data[15] << 8);
        const int bytes = data[16] / 8;
        const bool topFirst = (data[17] & 0x20) != 0;
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
        {
            return false;
        }

        const size_t count = static_cast<size_t>(width) * height;
        std::vector<BYTE> raw(count * 4);
        size_t at = 18 + data[0];
        size_t done = 0;
        auto take = [&](size_t index) {
            BYTE* out = &raw[index * 4];
            out[0] = data[at + 2];
            out[1] = data[at + 1];
            out[2] = data[at];
            out[3] = bytes == 4 ? data[at + 3] : 255;
        };
        while (done < count)
        {
            if (data[2] == 2)
            {
                if (at + bytes > size) return false;
                take(done++);
                at += bytes;
                continue;
            }

            if (at >= size) return false;
            const BYTE packet = data[at++];
            const size_t run = (packet & 0x7F) + 1u;
            if (done + run > count) return false;
            if (packet & 0x80)
            {
                if (at + bytes > size) return false;
                for (size_t k = 0; k < run; k++) take(done++);
                at += bytes;
            }
            else
            {
                for (size_t k = 0; k < run; k++)
                {
                    if (at + bytes > size) return false;
                    take(done++);
                    at += bytes;
                }
            }
        }

        pixels.resize(count * 4);
        const size_t row = static_cast<size_t>(width) * 4;
        for (int y = 0; y < height; y++)
        {
            const int from = topFirst ? y : height - 1 - y;
            memcpy(&pixels[y * row], &raw[from * row], row);
        }

        return true;
    }

    void LoadFrame()
    {
        g_frameTried = true;
        std::vector<BYTE> data;
        std::vector<BYTE> pixels;
        if (!ReadWholeFile(g_framePath, data))
        {
            if (g_uiLog) g_uiLog("Estilo: no se encontro el marco %s; marco dibujado.", g_framePath);
            return;
        }

        // Cabecera propia de 4 bytes del .OZT; si no esta, el archivo es un TGA comun.
        const bool loaded = (data.size() > 4 && DecodeTga(data.data() + 4, data.size() - 4, pixels, g_frameWidth, g_frameHeight))
            || DecodeTga(data.data(), data.size(), pixels, g_frameWidth, g_frameHeight);
        if (!loaded)
        {
            if (g_uiLog) g_uiLog("Estilo: no se pudo leer el marco %s.", g_framePath);
            return;
        }

        GLint previous = 0;
        glGetIntegerv(GL_TEXTURE_BINDING_2D, &previous);
        glGenTextures(1, &g_frame);
        glBindTexture(GL_TEXTURE_2D, g_frame);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, g_frameWidth, g_frameHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
        glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(previous));
        if (g_uiLog) g_uiLog("Estilo: marco %s (%dx%d, %d estados).", g_framePath, g_frameWidth, g_frameHeight, g_frameStates);
    }

    void TexturedQuad(float left, float top, float right, float bottom, float u0, float v0, float u1, float v1)
    {
        glTexCoord2f(u0, v0);
        Vertex(left, top);
        glTexCoord2f(u1, v0);
        Vertex(right, top);
        glTexCoord2f(u1, v1);
        Vertex(right, bottom);
        glTexCoord2f(u0, v1);
        Vertex(left, bottom);
    }

    // Boton con el marco original; false si no hay marco (se dibuja el propio).
    bool FrameButton(const PeroniaUi::Rect& rect, int state, float shade)
    {
        if (!g_frameTried)
        {
            LoadFrame();
        }

        if (g_frame == 0 || g_frameStates <= 0)
        {
            return false;
        }

        const int stateHeight = g_frameHeight / g_frameStates;
        const int row = state < g_frameStates ? state : 0;
        const float v0 = static_cast<float>(row * stateHeight) / g_frameHeight;
        const float v1 = static_cast<float>((row + 1) * stateHeight) / g_frameHeight;

        // Puntas: 0,6 del alto del estado en la textura, al ancho que les toca segun el alto en pantalla.
        const float capTexture = stateHeight * 0.6f;
        const float uCap = capTexture / g_frameWidth;
        float cap = rect.height * capTexture / stateHeight * (*WindowHeight / 480.0f) / (*WindowWidth / 640.0f);
        if (cap * 2 > rect.width)
        {
            cap = rect.width / 2;
        }

        GLint previous = 0;
        glGetIntegerv(GL_TEXTURE_BINDING_2D, &previous);
        glEnable(GL_TEXTURE_2D);
        glBindTexture(GL_TEXTURE_2D, g_frame);
        glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
        glColor4f(shade, shade, shade, 1.0f);
        const float l = rect.left, t = rect.top, r = rect.Right(), b = rect.Bottom();
        glBegin(GL_QUADS);
        TexturedQuad(l, t, l + cap, b, 0.0f, v0, uCap, v1);
        TexturedQuad(l + cap, t, r - cap, b, uCap, v0, 1.0f - uCap, v1);
        TexturedQuad(r - cap, t, r, b, 1.0f - uCap, v0, 1.0f, v1);
        glEnd();
        glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(previous));
        glDisable(GL_TEXTURE_2D);
        return true;
    }
}

namespace PeroniaUi
{
    void Initialize(const char* iniPath, LogFunction log)
    {
        g_uiLog = log;
        char path[MAX_PATH];
        GetPrivateProfileStringA("PeroniaUi", "StoneTexture", "", path, sizeof(path), iniPath);
        if (path[0] != '\0')
        {
            strcpy_s(g_stonePath, path);
        }

        GetPrivateProfileStringA("PeroniaUi", "ButtonTexture", g_framePath, path, sizeof(path), iniPath);
        strcpy_s(g_framePath, path); // vacio: sin marco original, se dibuja el propio
        g_frameStates = GetPrivateProfileIntA("PeroniaUi", "ButtonStates", g_frameStates, iniPath);
    }
    float MouseX()
    {
        return static_cast<float>(*GameMouseX);
    }

    float MouseY()
    {
        return static_cast<float>(*GameMouseY);
    }

    bool MouseInside(const Rect& rect)
    {
        const float x = MouseX();
        const float y = MouseY();
        return x >= rect.left && x < rect.Right() && y >= rect.top && y < rect.Bottom();
    }

    bool LeftButtonDown()
    {
        return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
    }

    ButtonState StateOf(const Rect& rect, bool enabled)
    {
        if (!enabled)
        {
            return ButtonState::Disabled;
        }

        if (!MouseInside(rect))
        {
            return ButtonState::Normal;
        }

        return LeftButtonDown() ? ButtonState::Pressed : ButtonState::Hover;
    }

    void BeginShapes()
    {
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT | GL_LIGHTING_BIT | GL_SCISSOR_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glShadeModel(GL_SMOOTH);
        Normal();
    }

    void EndShapes()
    {
        glPopAttrib();
    }

    void Glow(const Rect& rect, float strength)
    {
        const float time = Seconds();
        const float pulse = 0.75f + 0.25f * sinf(time * 2 * Pi / 1.6f); // late cada 1,6 s
        const float s = strength * pulse;
        const float cx = rect.left + rect.width / 2;
        const float cy = rect.top + rect.height / 2;
        Additive();

        // El resplandor no pasa a la barra de abajo del juego (empieza en 429 de 480).
        glEnable(GL_SCISSOR_TEST);
        glScissor(0, static_cast<GLint>(Py(BottomBarTop)), *WindowWidth, *WindowHeight);

        // Halo ancho y suave, y otro mas chico y caliente.
        Halo(cx, cy, rect.width * 1.05f, rect.height * 2.4f, 1.0f, 0.60f, 0.10f, 0.55f * s);
        Halo(cx, cy, rect.width * 0.7f, rect.height * 1.4f, 1.0f, 0.82f, 0.35f, 0.45f * s);

        // Rayos que giran despacio (aplastados en la vertical para que no tapen la barra de abajo).
        constexpr int Rays = 12;
        const float spin = time * 0.35f;
        const float length = rect.width * 0.95f;
        glBegin(GL_TRIANGLES);
        for (int i = 0; i < Rays; i++)
        {
            const float angle = spin + 2 * Pi * i / Rays;
            const float wide = 0.06f;
            const float flicker = 0.6f + 0.4f * sinf(time * 3.1f + i * 1.7f);
            glColor4f(1.0f, 0.88f, 0.45f, 0.45f * s * flicker);
            Vertex(cx, cy);
            glColor4f(1.0f, 0.7f, 0.2f, 0.0f);
            Vertex(cx + cosf(angle - wide) * length, cy + sinf(angle - wide) * length * 0.45f);
            Vertex(cx + cosf(angle + wide) * length, cy + sinf(angle + wide) * length * 0.45f);
        }

        glEnd();

        // Borde de luz pegado al marco.
        Rim(rect, 12.0f, 1.0f, 0.75f, 0.25f, 0.32f * s);
        glDisable(GL_SCISSOR_TEST);
        Normal();
    }

    void MetalButton(const Rect& rect, ButtonState state)
    {
        const float line = Line();
        const float l = rect.left, t = rect.top, r = rect.Right(), b = rect.Bottom();
        const bool hover = state == ButtonState::Hover;
        const bool pressed = state == ButtonState::Pressed;
        const float light = state == ButtonState::Disabled ? 0.6f : hover ? 1.18f : 1.0f;
        if (g_framePath[0] != '\0'
            && FrameButton(rect, pressed ? 2 : hover ? 1 : 0, state == ButtonState::Disabled ? 0.6f : 1.0f))
        {
            return;
        }


        // Contorno: negro, con un filo gris tenue por fuera, como los marcos de la barra de habilidades.
        Fill(l - line, t - line, r + line, b + line, 0.32f, 0.31f, 0.29f, 0.45f);
        Fill(l, t, r, b, 0.02f, 0.02f, 0.02f, 1.0f);

        // Bisel de metal oscuro: el mismo degradado en los cuatro lados, claro por fuera y mas oscuro hacia la cara
        // (mas oscuro si esta apretado). Sin colores fuertes, para que entone con los marcos del juego.
        const float hi = (pressed ? 0.22f : 0.36f) * light;
        const float bevel = 2 * line;
        const float il = l + line, it = t + line, ir = r - line, ib = b - line;
        Bevel(il, it, ir, ib, bevel, hi);

        // Linea oscura entre el bisel y la cara.
        const float fl = il + bevel, ft = it + bevel, fr = ir - bevel, fb = ib - bevel;
        Fill(fl, ft, fr, fb, 0.04f, 0.04f, 0.04f, 1.0f);

        // Cara: metal gris oscuro, mas claro arriba, con el brillo en la mitad de arriba.
        const float cl = fl + line, ct = ft + line, cr = fr - line, cb = fb - line;
        const float shade = pressed ? 0.8f : light;
        const float top[4] = { 0.31f * shade, 0.30f * shade, 0.28f * shade, 1.0f };
        const float middle[4] = { 0.15f * shade, 0.145f * shade, 0.14f * shade, 1.0f };
        const float bottom[4] = { 0.21f * shade, 0.20f * shade, 0.19f * shade, 1.0f };
        const float cm = (ct + cb) / 2;
        if (StoneFace(cl, ct, cr, cb, shade))
        {
            // Piedra de la interfaz original, con luz arriba y sombra en el medio por encima.
            const float lit[4] = { 1.0f, 0.95f, 0.85f, 0.10f };
            const float clear[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
            const float dark[4] = { 0.0f, 0.0f, 0.0f, 0.30f };
            FillVertical(cl, ct, cr, cm, lit, clear);
            FillVertical(cl, cm, cr, cb, clear, dark);
        }
        else
        {
            FillVertical(cl, ct, cr, cm, top, middle);
            FillVertical(cl, cm, cr, cb, middle, bottom);

            // Metal cepillado: lineas horizontales tenues.
            for (float y = ct + line; y < cb; y += 2 * line)
            {
                Fill(cl, y, cr, y + line * 0.5f, 1.0f, 1.0f, 1.0f, 0.025f);
            }
        }

        // Linea de luz bajo el borde de arriba de la cara.
        Fill(cl, ct, cr, ct + line, 1.0f, 0.95f, 0.85f, pressed ? 0.03f : 0.08f);
    }

    void Shine(const Rect& rect)
    {
        // Una banda de luz cruza la cara en 0,7 s cada 2,8 s.
        constexpr float Period = 2.8f, Sweep = 0.7f;
        const float phase = fmodf(Seconds(), Period);
        if (phase > Sweep)
        {
            return;
        }

        const float inset = 4 * Line();
        const float l = rect.left + inset, t = rect.top + inset, r = rect.Right() - inset, b = rect.Bottom() - inset;
        const float band = rect.height * 0.9f;
        const float slant = (b - t) * 0.6f;
        const float x = l - band - slant + (r - l + 2 * band + slant) * (phase / Sweep);
        Additive();
        glEnable(GL_SCISSOR_TEST);
        glScissor(static_cast<GLint>(Px(l)), static_cast<GLint>(Py(b)), static_cast<GLsizei>(Px(r) - Px(l)),
            static_cast<GLsizei>(Py(t) - Py(b)));
        glBegin(GL_QUADS);
        glColor4f(1.0f, 0.9f, 0.6f, 0.0f);
        Vertex(x + slant, t);
        glColor4f(1.0f, 0.9f, 0.6f, 0.35f);
        Vertex(x + slant + band / 2, t);
        Vertex(x + band / 2, b);
        glColor4f(1.0f, 0.9f, 0.6f, 0.0f);
        Vertex(x, b);
        glColor4f(1.0f, 0.9f, 0.6f, 0.35f);
        Vertex(x + slant + band / 2, t);
        glColor4f(1.0f, 0.9f, 0.6f, 0.0f);
        Vertex(x + slant + band, t);
        Vertex(x + band, b);
        glColor4f(1.0f, 0.9f, 0.6f, 0.35f);
        Vertex(x + band / 2, b);
        glEnd();
        glDisable(GL_SCISSOR_TEST);
        Normal();
    }

    // Fuente propia para textos mas grandes que los del juego: Arial negrita de "size" (en 480 de alto), recreada
    // si cambia el tamano de la ventana. El texto del juego acepta cualquier HFONT.
    HFONT LargeFont(float size)
    {
        static HFONT font = nullptr;
        static int height = 0;
        const int wanted = static_cast<int>(size * *WindowHeight / 480.0f + 0.5f);
        if (font == nullptr || wanted != height)
        {
            if (font != nullptr)
            {
                DeleteObject(font);
            }

            height = wanted;
            font = CreateFontA(-height, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS,
                CLIP_DEFAULT_PRECIS, ANTIALIASED_QUALITY, DEFAULT_PITCH | FF_SWISS, "Arial");
        }

        return font;
    }

    void ButtonText(const Rect& rect, const char* text, DWORD color, bool bold, bool pressed, float size)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderTextObject + 4);
        if (renderer == nullptr)
        {
            return;
        }

        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        DWORD* const textColor = reinterpret_cast<DWORD*>(renderer + 0x10);
        DWORD* const background = reinterpret_cast<DWORD*>(renderer + 0x14);
        const DWORD savedText = *textColor;
        const DWORD savedBackground = *background;
        *background = 0;
        const HFONT large = size > 0 ? LargeFont(size) : nullptr;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderTextObject, nullptr,
            large != nullptr ? large : bold ? *BoldFont : *NormalFont);

        // El texto del juego toma la altura de la fuente: se centra con la altura de una linea (unos 10 de 480).
        const int offset = pressed ? 1 : 0;
        const int x = static_cast<int>(rect.left) + offset;
        const float lineHeight = large != nullptr ? size : 10.0f;
        const int y = static_cast<int>(rect.top + (rect.height - lineHeight) / 2) + offset;
        const int width = static_cast<int>(rect.width);
        const auto render = reinterpret_cast<RenderTextType>(RenderTextFunction);
        *textColor = TextOutline;
        const int around[4][2] = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
        for (const auto& step : around)
        {
            render(RenderTextObject, nullptr, x + step[0], y + step[1], text, width, 0, SortCenter, nullptr);
        }

        *textColor = color;
        render(RenderTextObject, nullptr, x, y, text, width, 0, SortCenter, nullptr);

        *textColor = savedText;
        *background = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderTextObject, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }
}
