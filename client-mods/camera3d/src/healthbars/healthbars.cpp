// healthbars.dll - Barras de vida sobre los monstruos para el cliente MU 1.04d con camera3d.dll.
//
// El cliente original no sabe cuanta vida le queda a un monstruo. El servidor (OpenMU, plugin "Show
// Object Health") manda el paquete C1 06 FA [numero del objeto, 2 bytes big endian] [vida restante en
// 1/250, de 1 a 250] cuando un monstruo recibe dano sin morir y cuando un monstruo danado entra a la
// vista. El main.exe original ignora el codigo 0xFA: su reparto de paquetes (0x006639D0) va al caso por
// defecto para los codigos mayores a 0xF9 y devuelve 1, sin anotar nada ni desconectar.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79, CALL 0x006639D0 con codigo, paquete, largo y si
//     venia cifrado) pasa por PacketHook. El paquete 0xFA se queda aca (vida por numero de objeto del
//     servidor) y no llega al juego. Con 0x13 (monstruos que entran a la vista), 0x14 (objetos que salen)
//     y 0x17 (objeto muerto) se borra la vida de esos numeros, asi un monstruo que reaparece no hereda la
//     barra; despues esos paquetes siguen al juego como siempre.
//   - En la escena principal, despues de elegir el objeto bajo el mouse (fin de la escena 3D) se llama
//     al comienzo del dibujo de la interfaz (0x004D9C86, CALL 0x00637770). Esa llamada pasa por
//     DrawHook: guarda las matrices 3D de OpenGL, llama a la original (que pone la proyeccion 2D en
//     pixeles de la ventana) y dibuja las barras. Asi quedan sobre el mundo y debajo de la interfaz.
//   - Para cada monstruo vivo (CHARACTER +0x30C vivo, +0x24 muerto, +0x320 tipo 2) con vida conocida
//     menor a la llena se proyecta un punto sobre su cabeza: la posicion (+0x404, ya interpolada por
//     camera3d durante el dibujo) mas la altura de su caja (+0x3E8, el maximo en z) y un margen. La
//     barra tiene tamano fijo en pixeles. Los puntos detras de la camara no se dibujan.

#include "../shared.h"

#include <GL/gl.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchFunction = 0x006639D0;   // int __cdecl Dispatch(int codigo, BYTE* paquete, int largo, int cifrado)
    constexpr DWORD DispatchCallSite = 0x0065FD79;   // CALL DispatchFunction al recibir cada paquete
    constexpr DWORD BeginBitmapFunction = 0x00637770; // void __cdecl BeginBitmap(): proyeccion 2D en pixeles
    constexpr DWORD BeginBitmapCallSite = 0x004D9C86; // CALL BeginBitmapFunction en la escena principal (0x004D95E0)
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // personajes: [+8], 400 de 0x598 bytes

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr DWORD CharDead = 0x24, CharKey = 0x7E, CharLive = 0x30C, CharKind = 0x320;
    constexpr DWORD CharHeight = 0x3E8;   // caja del objeto: maximo en z (la altura sobre la posicion)
    constexpr DWORD CharPosition = 0x404; // float x, y, z
    constexpr int KindMonster = 2;

    // --- Paquetes ---
    constexpr int CodeAddNpcs = 0x13, CodeOutOfScope = 0x14, CodeKilled = 0x17, CodeHealth = 0xFA;
    constexpr int FullHealth = 250;
    constexpr WORD IdMask = 0x7FFF; // 0x8000 marca a los recien aparecidos

    // --- Dibujo (pixeles) ---
    constexpr float BarWidth = 50.0f;
    constexpr float BarHeight = 5.0f;
    constexpr float BarBorder = 1.0f;
    constexpr float HeadMargin = 30.0f; // unidades del mundo sobre la caja del monstruo

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using BeginBitmapType = void(__cdecl*)();

    LogFunction g_log = nullptr;
    BYTE g_health[IdMask + 1] = {}; // 0: desconocida
    bool g_projectionWarned = false;

    WORD ReadId(const BYTE* data)
    {
        return static_cast<WORD>(((data[0] << 8) | data[1]) & IdMask);
    }

    // Borra la vida de los objetos de un paquete (despues de descifrarlo, el paquete es C1 o C2).
    void ForgetObjects(int code, const BYTE* packet, int size)
    {
        const int header = packet[0] == 0xC2 || packet[0] == 0xC4 ? 4 : 3;
        switch (code)
        {
        case CodeAddNpcs:
        {
            // Cantidad y por cada uno: numero, tipo, x, y, destino x, y, giro, cantidad de efectos y efectos.
            if (size <= header)
            {
                return;
            }

            const int count = packet[header];
            int offset = header + 1;
            for (int i = 0; i < count && offset + 10 <= size; i++)
            {
                g_health[ReadId(packet + offset)] = 0;
                offset += 10 + packet[offset + 9];
            }

            break;
        }

        case CodeOutOfScope:
        {
            const int count = size > header ? packet[header] : 0;
            for (int i = 0; i < count && header + 1 + i * 2 + 2 <= size; i++)
            {
                g_health[ReadId(packet + header + 1 + i * 2)] = 0;
            }

            break;
        }

        case CodeKilled:
            if (header + 2 <= size)
            {
                g_health[ReadId(packet + header)] = 0;
            }

            break;
        }
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr)
        {
            if (code == CodeHealth)
            {
                if (packet[0] == 0xC1 && size >= 6 && packet[5] >= 1 && packet[5] <= FullHealth)
                {
                    g_health[ReadId(packet + 3)] = packet[5];
                }

                return 1; // el juego no lo conoce
            }

            if (code == CodeAddNpcs || code == CodeOutOfScope || code == CodeKilled)
            {
                ForgetObjects(code, packet, size);
            }
        }

        return reinterpret_cast<DispatchType>(DispatchFunction)(code, packet, size, encrypted);
    }

    // Matrices 3D de la escena, guardadas antes de pasar a la interfaz.
    float g_modelView[16];
    float g_projection[16];
    GLint g_viewport[4];

    // Punto del mundo a pixeles de la ventana (origen abajo a la izquierda, como la proyeccion 2D del juego).
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
            return false; // detras de la camara (o pegado a ella)
        }

        x = g_viewport[0] + (clip[0] / clip[3] + 1.0f) * 0.5f * g_viewport[2];
        y = g_viewport[1] + (clip[1] / clip[3] + 1.0f) * 0.5f * g_viewport[3];
        return x >= g_viewport[0] && x <= g_viewport[0] + g_viewport[2]
            && y >= g_viewport[1] && y <= g_viewport[1] + g_viewport[3];
    }

    void Rectangle(float left, float bottom, float right, float top)
    {
        glVertex2f(left, bottom);
        glVertex2f(right, bottom);
        glVertex2f(right, top);
        glVertex2f(left, top);
    }

    void DrawBar(float x, float y, int health)
    {
        const float left = static_cast<float>(static_cast<int>(x - BarWidth / 2));
        const float bottom = static_cast<float>(static_cast<int>(y));
        const float right = left + BarWidth;
        const float top = bottom + BarHeight;
        const float filled = left + BarWidth * health / FullHealth;

        glColor4f(0.0f, 0.0f, 0.0f, 0.85f); // borde
        Rectangle(left - BarBorder, bottom - BarBorder, right + BarBorder, top + BarBorder);
        glColor4f(0.15f, 0.05f, 0.05f, 0.85f); // fondo
        Rectangle(left, bottom, right, top);
        glColor4f(0.85f, 0.1f, 0.1f, 1.0f); // vida
        Rectangle(left, bottom, filled, top);
    }

    void DrawBars()
    {
        const DWORD manager = *CharacterManager;
        const BYTE* characters = manager != 0 ? *reinterpret_cast<BYTE* const*>(manager + 8) : nullptr;
        if (characters == nullptr)
        {
            return;
        }

        bool drawing = false;
        for (int i = 0; i < CharacterCount; i++)
        {
            const BYTE* character = characters + i * CharacterSize;
            if (!character[CharLive] || character[CharDead] || character[CharKind] != KindMonster)
            {
                continue;
            }

            const int health = g_health[*reinterpret_cast<const WORD*>(character + CharKey) & IdMask];
            if (health == 0 || health >= FullHealth)
            {
                continue;
            }

            const float* position = reinterpret_cast<const float*>(character + CharPosition);
            const float head[3] = { position[0], position[1],
                position[2] + *reinterpret_cast<const float*>(character + CharHeight) + HeadMargin };
            float x;
            float y;
            if (!Project(head, x, y))
            {
                continue;
            }

            if (!drawing)
            {
                // Se guarda el estado de OpenGL entero y se lo devuelve despues: el juego recuerda que tiene
                // activado (texturas, mezcla, profundidad) y no tiene que enterarse de este dibujo.
                glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
                glDisable(GL_TEXTURE_2D);
                glDisable(GL_DEPTH_TEST);
                glDisable(GL_ALPHA_TEST);
                glDisable(GL_CULL_FACE);
                glDisable(GL_FOG);
                glEnable(GL_BLEND);
                glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
                glBegin(GL_QUADS);
                drawing = true;
            }

            DrawBar(x, y, health);
        }

        if (drawing)
        {
            glEnd();
            glPopAttrib();
        }
    }

    void __cdecl DrawHook()
    {
        glGetFloatv(GL_MODELVIEW_MATRIX, g_modelView);
        glGetFloatv(GL_PROJECTION_MATRIX, g_projection);
        glGetIntegerv(GL_VIEWPORT, g_viewport);
        reinterpret_cast<BeginBitmapType>(BeginBitmapFunction)();

        // Con una proyeccion en perspectiva, la fila de w es (0, 0, -1, 0).
        if (g_projection[11] != -1.0f || g_viewport[2] <= 0 || g_viewport[3] <= 0)
        {
            if (!g_projectionWarned)
            {
                g_projectionWarned = true;
                g_log("Barras de vida: la escena no tiene la proyeccion 3D esperada; no se dibujan.");
            }

            return;
        }

        __try
        {
            DrawBars();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    bool CallMatches(DWORD callSite, DWORD function)
    {
        BYTE expected[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(expected + 1) = function - (callSite + 5);
        return memcmp(reinterpret_cast<const void*>(callSite), expected, sizeof(expected)) == 0;
    }

    bool RedirectCall(DWORD callSite, const void* target)
    {
        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(target) - (callSite + 5);
        return WriteCode(callSite, call, sizeof(call));
    }
}

extern "C" __declspec(dllexport) bool StartHealthBars(const HealthBarsHost* host)
{
    if (host == nullptr || host->version != HealthBarsHostVersion)
    {
        return false;
    }

    g_log = host->log;
    if (!CallMatches(DispatchCallSite, DispatchFunction) || !CallMatches(BeginBitmapCallSite, BeginBitmapFunction))
    {
        g_log("Barras de vida: las llamadas del juego no coinciden; desactivadas.");
        return false;
    }

    if (!RedirectCall(BeginBitmapCallSite, &DrawHook) || !RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Barras de vida: no se pudieron instalar.");
        return false;
    }

    g_log("Barras de vida listas.");
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
