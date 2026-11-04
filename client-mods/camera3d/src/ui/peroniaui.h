// peroniaui - Estilo de las ventanas y botones nuevos de PeroniaMU (cliente MU 1.04d con camera3d.dll).
//
// Las DLL de las funciones nuevas (resetbutton.dll, y despues loot.dll, espectros.dll, etc.) dibujan sus
// botones con estas piezas para que se vean como la interfaz del juego: marco de metal oscuro con bisel,
// como los botones de las ventanas de entrada a Devil Square o Blood Castle, letras doradas con borde oscuro y,
// para lo importante, un resplandor dorado detras.
//
// Se compila dentro de cada DLL (src\ui\peroniaui.cpp en la linea de cl). Todo se dibuja con OpenGL en la
// proyeccion 2D que deja el juego despues de dibujar su interfaz (pixeles, origen abajo a la izquierda); las
// medidas se dan en coordenadas de 640x480, como las del mouse del juego.
#pragma once

#include <windows.h>

namespace PeroniaUi
{
    struct Rect
    {
        float left;
        float top;
        float width;
        float height;

        float Right() const { return left + width; }
        float Bottom() const { return top + height; }
    };

    enum class ButtonState
    {
        Normal,
        Hover,
        Pressed,
        Disabled,
    };

    // Colores de texto (formato del texto del juego: rojo en el byte bajo, alfa en el alto).
    constexpr DWORD Rgb(BYTE red, BYTE green, BYTE blue)
    {
        return red | (green << 8) | (blue << 16) | 0xFF000000u;
    }

    constexpr DWORD GoldText = Rgb(255, 210, 60);
    constexpr DWORD GoldTextHover = Rgb(255, 238, 150);
    constexpr DWORD GoldTextDisabled = Rgb(120, 105, 70);
    constexpr DWORD TextOutline = Rgb(35, 20, 0);

    // Mouse del juego en 640x480.
    float MouseX();
    float MouseY();
    bool MouseInside(const Rect& rect);

    // Boton izquierdo apretado ahora (solo para el dibujo del boton apretado).
    bool LeftButtonDown();

    // Estado de un boton segun el mouse.
    ButtonState StateOf(const Rect& rect, bool enabled);

    // Formas: entre BeginShapes y EndShapes (guardan y reponen el estado de OpenGL).
    void BeginShapes();
    void EndShapes();

    // Resplandor dorado detras de un boton: halo suave que late, rayos que giran despacio y un borde de luz.
    // strength: 0 a 1 (por ejemplo, mas fuerte con el mouse encima).
    void Glow(const Rect& rect, float strength);

    // Marco de metal oscuro con bisel, como los botones del juego.
    void MetalButton(const Rect& rect, ButtonState state);

    // Reflejo de luz que cruza la cara del boton cada tanto (encima del marco, debajo del texto).
    void Shine(const Rect& rect);

    // Texto con la fuente del juego, centrado en el rect, con borde oscuro (despues de EndShapes).
    void ButtonText(const Rect& rect, const char* text, DWORD color, bool bold, bool pressed);
}
