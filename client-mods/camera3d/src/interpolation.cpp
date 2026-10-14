// Movimiento fluido de personajes, monstruos y objetos que se mueven (etapa 3 de los FPS).
//
// La logica mueve a todos 25 veces por segundo; a 60 FPS eso se veia como un paso cada 2 o 3 cuadros.
// Al empezar cada cuadro con paso de logica se guarda el estado anterior y el actual de cada uno
// (posicion, angulo y cuadro de animacion). En cada cuadro, antes de dibujar, se pone un punto
// intermedio segun el tiempo que paso desde el ultimo paso (con 40 ms de retraso), y despues de dibujar
// se vuelven a poner los valores reales, asi la logica nunca ve los intermedios.
//
// No se interpola (se usa el valor real) si el lugar lo ocupa otro, si se teletransporto o si cambio de
// accion (la animacion empieza de nuevo).
//
// Ademas de personajes y monstruos se interpolan otros arreglos de objetos que mueve la logica, con la
// misma estructura (OBJECT): efectos y proyectiles, mascotas que vuelan (angel, imp, etc.) y pajaros.

#include "mods.h"

#include <math.h>
#include <string.h>

namespace
{
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // [manager + 8] = personajes

    // Arreglo de objetos a interpolar. keyOffset: dato que identifica al ocupante (relativo al
    // elemento); si cambia, es otro y no se interpola.
    struct ObjectArray
    {
        const char* name;
        DWORD base;       // 0: personajes (la direccion se lee del administrador)
        int count;
        int stride;
        int objectOffset; // OBJECT dentro del elemento
        int keyOffset;
        int keySize;      // 2 (short) o 4 (int)
    };

    const ObjectArray Arrays[] = {
        { "personajes", 0, 400, 0x598, 0x308, 0x7E, 2 },  // numero en el servidor
        { "efectos", 0x08967FE8, 200, 0x288, 0, 0x30, 4 }, // tipo de modelo
        { "efectos con duracion", 0x097622F0, 100, 0x288, 0, 0x30, 4 },
        { "mascotas", 0x08791550, 10, 0x288, 0, 0x30, 4 },
        { "pajaros", 0x0878FC00, 10, 0x288, 0, 0x30, 4 },
    };
    constexpr int MaxEntries = 400 + 200 + 100 + 10 + 10;

    // Campos del OBJECT.
    constexpr int LiveOffset = 0x04;           // byte
    constexpr int ActionOffset = 0x12;         // accion actual (ushort)
    constexpr int AnimationFrameOffset = 0x8C; // float
    constexpr int PositionOffset = 0xFC;       // float[3]
    constexpr int AngleOffset = 0x108;         // float[3], grados

    constexpr float TeleportDistance = 300.0f; // mas que esto en un paso: no se interpola
    constexpr float MaxAnimationStep = 3.0f;   // mas cuadros de animacion en un paso: no se interpola

    struct State
    {
        bool live;
        int key;
        unsigned short action;
        float frame;
        float position[3];
        float angle[3];
    };

    State g_previous[MaxEntries];
    State g_current[MaxEntries];
    BYTE* g_objects[MaxEntries]; // OBJECT de cada entrada (para volver a poner los valores reales)
    bool g_valid = false;
    bool g_applied = false;
    BYTE* g_characters = nullptr;

    float& FloatAt(BYTE* object, int offset)
    {
        return *reinterpret_cast<float*>(object + offset);
    }

    void Read(BYTE* element, const ObjectArray& array, State& state)
    {
        BYTE* object = element + array.objectOffset;
        memset(&state, 0, sizeof(state)); // tambien el relleno: los estados se comparan con memcmp
        state.live = object[LiveOffset] != 0;
        state.key = array.keySize == 2 ? *reinterpret_cast<short*>(element + array.keyOffset)
                                       : *reinterpret_cast<int*>(element + array.keyOffset);
        state.action = *reinterpret_cast<unsigned short*>(object + ActionOffset);
        state.frame = FloatAt(object, AnimationFrameOffset);
        memcpy(state.position, object + PositionOffset, sizeof(state.position));
        memcpy(state.angle, object + AngleOffset, sizeof(state.angle));
    }

    float LerpAngle(float from, float to, float t)
    {
        // por el camino mas corto (de 350 a 10 son 20 grados)
        float delta = fmodf(to - from, 360.0f);
        if (delta > 180.0f)
        {
            delta -= 360.0f;
        }
        else if (delta < -180.0f)
        {
            delta += 360.0f;
        }

        return from + delta * t;
    }

    BYTE* GetCharacters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    // Pone el punto intermedio entre el estado anterior y el actual.
    void Apply(BYTE* object, const State& previous, const State& current, float t)
    {
        if (!current.live || !previous.live || previous.key != current.key)
        {
            return;
        }

        const float dx = current.position[0] - previous.position[0];
        const float dy = current.position[1] - previous.position[1];
        if (dx * dx + dy * dy > TeleportDistance * TeleportDistance)
        {
            return;
        }

        float* position = reinterpret_cast<float*>(object + PositionOffset);
        float* angle = reinterpret_cast<float*>(object + AngleOffset);
        for (int k = 0; k < 3; k++)
        {
            position[k] = previous.position[k] + (current.position[k] - previous.position[k]) * t;
            angle[k] = LerpAngle(previous.angle[k], current.angle[k], t);
        }

        if (previous.action == current.action && current.frame >= previous.frame &&
            current.frame - previous.frame <= MaxAnimationStep)
        {
            FloatAt(object, AnimationFrameOffset) = previous.frame + (current.frame - previous.frame) * t;
        }
    }
}

// Al empezar a dibujar la escena del juego. stepped: hubo paso de logica desde el cuadro anterior.
// t: fraccion (0 a 1) del tiempo desde el ultimo paso, sobre 40 ms.
void InterpolationBeforeRender(bool stepped, float t)
{
    __try
    {
        BYTE* const characters = GetCharacters();
        if (characters == nullptr)
        {
            g_valid = false;
            return;
        }

        const bool fresh = !g_valid || characters != g_characters;
        int entry = 0;
        for (const ObjectArray& array : Arrays)
        {
            BYTE* const base = array.base == 0 ? characters : reinterpret_cast<BYTE*>(array.base);
            for (int i = 0; i < array.count; i++, entry++)
            {
                BYTE* const element = base + i * array.stride;
                BYTE* const object = element + array.objectOffset;
                g_objects[entry] = object;
                State& previous = g_previous[entry];
                State& current = g_current[entry];

                if (stepped || fresh)
                {
                    previous = current;
                    Read(element, array, current);
                    if (fresh)
                    {
                        previous = current;
                    }
                }
                else
                {
                    // Entre cuadros el juego procesa lo que manda el servidor (cambio de mapa,
                    // posiciones, acciones) fuera de los pasos de logica. Si algo cambio desde la ultima
                    // copia, ese es el valor real nuevo: se adopta sin interpolar. Si no, despues de
                    // dibujar se habria vuelto a poner el valor viejo y el juego perdia, por ejemplo, la
                    // posicion nueva despues de cambiar de mapa.
                    State now;
                    Read(element, array, now);
                    if (memcmp(&now, &current, sizeof(State)) != 0)
                    {
                        current = now;
                        previous = now;
                        continue;
                    }
                }

                Apply(object, previous, current, t);
            }
        }

        g_characters = characters;
        g_valid = true;
        g_applied = true;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        g_valid = false;
        g_applied = false;
    }
}

// Despues de dibujar: vuelven los valores reales.
void InterpolationAfterRender()
{
    if (!g_applied)
    {
        return;
    }

    g_applied = false;
    __try
    {
        for (int entry = 0; entry < MaxEntries; entry++)
        {
            const State& state = g_current[entry];
            BYTE* object = g_objects[entry];
            if (!state.live || object[LiveOffset] == 0)
            {
                continue;
            }

            memcpy(object + PositionOffset, state.position, sizeof(state.position));
            memcpy(object + AngleOffset, state.angle, sizeof(state.angle));
            if (*reinterpret_cast<unsigned short*>(object + ActionOffset) == state.action)
            {
                FloatAt(object, AnimationFrameOffset) = state.frame;
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        g_valid = false;
    }
}
