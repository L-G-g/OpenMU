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

    // Anillo de luz alrededor de un rect: alfa a en el borde del rect, 0 a "spread" de distancia.
    void Rim(const PeroniaUi::Rect& rect, float spread, float r, float g, float b, float a)
    {
        const float l = rect.left, t = rect.top, rr = rect.Right(), bb = rect.Bottom();
        const float ol = l - spread, ot = t - spread, orr = rr + spread, ob = bb + spread;
        const float inner[4][2] = { { l, t }, { rr, t }, { rr, bb }, { l, bb } };
        const float outer[4][2] = { { ol, ot }, { orr, ot }, { orr, ob }, { ol, ob } };
        glBegin(GL_QUAD_STRIP);
        for (int i = 0; i <= 4; i++)
        {
            const int k = i % 4;
            glColor4f(r, g, b, a);
            Vertex(inner[k][0], inner[k][1]);
            glColor4f(r, g, b, 0.0f);
            Vertex(outer[k][0], outer[k][1]);
        }

        glEnd();
    }
}

namespace PeroniaUi
{
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

        // Halo ancho y suave, y otro mas chico y caliente.
        Halo(cx, cy, rect.width * 0.85f, rect.height * 1.9f, 1.0f, 0.62f, 0.12f, 0.38f * s);
        Halo(cx, cy, rect.width * 0.62f, rect.height * 1.15f, 1.0f, 0.80f, 0.30f, 0.30f * s);

        // Rayos que giran despacio (aplastados en la vertical para que no tapen la barra de abajo).
        constexpr int Rays = 12;
        const float spin = time * 0.35f;
        const float length = rect.width * 0.75f;
        glBegin(GL_TRIANGLES);
        for (int i = 0; i < Rays; i++)
        {
            const float angle = spin + 2 * Pi * i / Rays;
            const float wide = 0.07f;
            const float flicker = 0.6f + 0.4f * sinf(time * 3.1f + i * 1.7f);
            glColor4f(1.0f, 0.85f, 0.4f, 0.22f * s * flicker);
            Vertex(cx, cy);
            glColor4f(1.0f, 0.7f, 0.2f, 0.0f);
            Vertex(cx + cosf(angle - wide) * length, cy + sinf(angle - wide) * length * 0.45f);
            Vertex(cx + cosf(angle + wide) * length, cy + sinf(angle + wide) * length * 0.45f);
        }

        glEnd();

        // Borde de luz pegado al marco.
        Rim(rect, 7.0f, 1.0f, 0.72f, 0.2f, 0.55f * s);
        Normal();
    }

    void MetalButton(const Rect& rect, ButtonState state)
    {
        const float line = Line();
        const float l = rect.left, t = rect.top, r = rect.Right(), b = rect.Bottom();
        const bool hover = state == ButtonState::Hover;
        const bool pressed = state == ButtonState::Pressed;
        const float light = state == ButtonState::Disabled ? 0.6f : hover ? 1.18f : 1.0f;

        // Sombra debajo y contorno negro.
        Fill(l + line, t + line, r + 2 * line, b + 2 * line, 0.0f, 0.0f, 0.0f, 0.55f);
        Fill(l, t, r, b, 0.02f, 0.02f, 0.02f, 0.97f);

        // Bisel: luz arriba e izquierda, sombra abajo y derecha (al reves si esta apretado).
        const float hi = (pressed ? 0.12f : 0.58f) * light;
        const float lo = (pressed ? 0.55f : 0.10f) * light;
        const float bevel = 2 * line;
        const float il = l + line, it = t + line, ir = r - line, ib = b - line;
        Fill(il, it, ir, it + bevel, hi, hi * 0.97f, hi * 0.9f, 1.0f);
        Fill(il, it, il + bevel, ib, hi * 0.85f, hi * 0.82f, hi * 0.77f, 1.0f);
        Fill(il, ib - bevel, ir, ib, lo, lo, lo, 1.0f);
        Fill(ir - bevel, it + bevel, ir, ib, lo, lo, lo, 1.0f);

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
        FillVertical(cl, ct, cr, cm, top, middle);
        FillVertical(cl, cm, cr, cb, middle, bottom);

        // Metal cepillado: lineas horizontales tenues.
        for (float y = ct + line; y < cb; y += 2 * line)
        {
            Fill(cl, y, cr, y + line * 0.5f, 1.0f, 1.0f, 1.0f, 0.025f);
        }

        // Linea de luz bajo el borde de arriba de la cara.
        Fill(cl, ct, cr, ct + line, 1.0f, 0.95f, 0.85f, pressed ? 0.05f : 0.16f);

        // Remaches dorados en las esquinas del bisel.
        const float stud = bevel;
        const float sr = 0.75f * light, sg = 0.58f * light, sb = 0.25f * light;
        Fill(il, it, il + stud, it + stud, sr, sg, sb, 1.0f);
        Fill(ir - stud, it, ir, it + stud, sr, sg, sb, 1.0f);
        Fill(il, ib - stud, il + stud, ib, sr * 0.7f, sg * 0.7f, sb * 0.7f, 1.0f);
        Fill(ir - stud, ib - stud, ir, ib, sr * 0.7f, sg * 0.7f, sb * 0.7f, 1.0f);
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

    void ButtonText(const Rect& rect, const char* text, DWORD color, bool bold, bool pressed)
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
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderTextObject, nullptr, bold ? *BoldFont : *NormalFont);

        // El texto del juego toma la altura de la fuente: se centra con la altura de una linea (unos 10 de 480).
        const int offset = pressed ? 1 : 0;
        const int x = static_cast<int>(rect.left) + offset;
        const int y = static_cast<int>(rect.top + (rect.height - 10.0f) / 2) + offset;
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
