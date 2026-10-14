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

// ---------------------------------------------------------------------------------------------------
// Objetos sueltos (fuera de los arreglos de arriba): el cuervo del Dark Lord, las mascotas nuevas, etc.
// Viven en memoria dinamica, asi que se interpolan al dibujarlos: se reemplaza RenderObject
// (0x005EBBB0), por donde pasan todos, y el estado se guarda por direccion del OBJECT.

namespace
{
    constexpr DWORD RenderObjectFunction = 0x005EBBB0;
    constexpr DWORD RenderObjectCheck = 0x005DDAF0; // bool (OBJECT*, bool, int, int)
    constexpr DWORD RenderObjectDraw = 0x005DE260;
    const BYTE RenderObjectPrologue[] = { 0x55, 0x8B, 0xEC, 0x8B, 0x45, 0x14 }; // MOV EAX,[EBP+0x14]

    struct LooseObject
    {
        BYTE* object;
        int step; // ultimo paso de logica en que se anoto
        State previous;
        State current;
    };

    constexpr int LooseCount = 8192; // tabla con direccion abierta (tambien pasan los objetos del mapa)
    LooseObject g_loose[LooseCount];
    int g_step = 0; // contador de pasos de logica vistos al dibujar
    bool g_stepped = false;
    float g_t = 0.0f;
    bool g_inGameRender = false; // dentro del dibujo de la escena del juego

    // Para el log: objetos sueltos que se movieron entre pasos (interpolados) y costo del registro.
    LARGE_INTEGER g_looseFrequency{};
    LONGLONG g_looseTicks = 0;
    int g_looseMoving = 0;
    int g_looseFrames = 0;
    DWORD g_looseReport = 0;
    LogFunction g_looseLog = nullptr;

    // OBJECT al principio; el tipo de modelo como identidad.
    const ObjectArray LooseArray = { "suelto", 0, 1, 0, 0, 0x30, 4 };

    bool InKnownArrays(BYTE* object)
    {
        if (g_characters != nullptr && object >= g_characters && object < g_characters + 400 * 0x598)
        {
            return true;
        }

        for (const ObjectArray& array : Arrays)
        {
            BYTE* base = reinterpret_cast<BYTE*>(array.base);
            if (array.base != 0 && object >= base && object < base + array.count * array.stride)
            {
                return true;
            }
        }

        return false;
    }

    LooseObject* FindLoose(BYTE* object)
    {
        int slot = static_cast<int>((reinterpret_cast<DWORD>(object) >> 4) % LooseCount);
        for (int probe = 0; probe < LooseCount; probe++, slot = (slot + 1) % LooseCount)
        {
            LooseObject& entry = g_loose[slot];
            if (entry.object == object)
            {
                return &entry;
            }

            // libre, o sin dibujarse hace mucho (se reutiliza)
            if (entry.object == nullptr || g_step - entry.step > 50)
            {
                entry.object = object;
                entry.step = -1;
                return &entry;
            }
        }

        return nullptr;
    }

    int __cdecl RenderObjectHook(BYTE* object, int flag, int a, int b)
    {
        using Function = int(__cdecl*)(BYTE*, int, int, int);
        LooseObject* entry = nullptr;
        State written{};
        LARGE_INTEGER start{};
        QueryPerformanceCounter(&start);
        if (g_inGameRender && object != nullptr && !InKnownArrays(object))
        {
            __try
            {
                entry = FindLoose(object);
                if (entry != nullptr)
                {
                    State now;
                    Read(object, LooseArray, now);
                    if (g_stepped && entry->step != g_step)
                    {
                        entry->previous = entry->step == g_step - 1 ? entry->current : now;
                        entry->current = now;
                        entry->step = g_step;
                    }
                    else if (memcmp(&now, &entry->current, sizeof(State)) != 0)
                    {
                        // lo cambio otro (el servidor, o se dejo de dibujar un rato): sin interpolar
                        entry->previous = now;
                        entry->current = now;
                        entry->step = g_step;
                    }

                    Apply(object, entry->previous, entry->current, g_t);
                    Read(object, LooseArray, written);
                    if (memcmp(entry->previous.position, entry->current.position, sizeof(entry->current.position)) != 0)
                    {
                        g_looseMoving++;
                    }
                }
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                entry = nullptr;
            }
        }

        LARGE_INTEGER middle{};
        QueryPerformanceCounter(&middle);
        g_looseTicks += middle.QuadPart - start.QuadPart;

        int result = reinterpret_cast<Function>(RenderObjectCheck)(object, flag, a, b);
        if (static_cast<BYTE>(result) != 0)
        {
            result = reinterpret_cast<Function>(RenderObjectDraw)(object, flag, a, b);
        }

        LARGE_INTEGER afterDraw{};
        QueryPerformanceCounter(&afterDraw);
        if (entry != nullptr)
        {
            // Se vuelven a poner los valores reales, salvo lo que el propio dibujo haya cambiado.
            float* position = reinterpret_cast<float*>(object + PositionOffset);
            float* angle = reinterpret_cast<float*>(object + AngleOffset);
            for (int k = 0; k < 3; k++)
            {
                if (position[k] == written.position[k])
                {
                    position[k] = entry->current.position[k];
                }

                if (angle[k] == written.angle[k])
                {
                    angle[k] = entry->current.angle[k];
                }
            }

            if (FloatAt(object, AnimationFrameOffset) == written.frame)
            {
                FloatAt(object, AnimationFrameOffset) = entry->current.frame;
            }

            LARGE_INTEGER end{};
            QueryPerformanceCounter(&end);
            g_looseTicks += end.QuadPart - afterDraw.QuadPart;
        }

        return result;
    }
}

void InterpolationSetFrame(bool rendering, bool stepped, float t)
{
    if (!rendering && g_inGameRender)
    {
        g_looseFrames++;
        if (g_looseLog != nullptr && GetTickCount() - g_looseReport >= 5000 && g_looseFrames > 0)
        {
            g_looseLog("Objetos sueltos: %.1f interpolados por cuadro, registro %.3f ms por cuadro.",
                static_cast<double>(g_looseMoving) / g_looseFrames,
                static_cast<double>(g_looseTicks) * 1000.0 / g_looseFrequency.QuadPart / g_looseFrames);
            g_looseReport = GetTickCount();
            g_looseMoving = 0;
            g_looseFrames = 0;
            g_looseTicks = 0;
        }
    }

    g_inGameRender = rendering;
    g_stepped = stepped;
    g_t = t;
    if (rendering && stepped)
    {
        g_step++;
    }
}

bool InstallLooseObjectInterpolation(LogFunction log)
{
    g_looseLog = log;
    QueryPerformanceFrequency(&g_looseFrequency);
    if (memcmp(reinterpret_cast<const void*>(RenderObjectFunction), RenderObjectPrologue, sizeof(RenderObjectPrologue)) != 0)
    {
        log("Movimiento fluido: RenderObject no coincide; los objetos sueltos no se interpolan.");
        return false;
    }

    BYTE jump[5] = { 0xE9 };
    const DWORD to = reinterpret_cast<DWORD>(&RenderObjectHook) - (RenderObjectFunction + 5);
    memcpy(jump + 1, &to, 4);
    return WriteCode(RenderObjectFunction, jump, sizeof(jump));
}

// ---------------------------------------------------------------------------------------------------
// Estelas (uniones): cintas de luz de armas y skills. Cada una guarda hasta 50 puntos (de 4 esquinas
// cada uno) que la logica actualiza en cada paso: los corre, los recalcula, agrega o saca alguno. Para
// que la cinta se deslice en vez de avanzar a saltos, cada punto se dibuja entre su posicion anterior y
// la actual (si los puntos se corren, el punto i nuevo es el i-1 viejo y la cinta entera fluye). Si la
// cantidad de puntos cambio en mas de uno, o la estela salto lejos, se deja como esta.

namespace
{
    BYTE* const Joints = reinterpret_cast<BYTE*>(0x087C5708);
    constexpr int JointCount = 500;
    constexpr int JointSize = 0xA18;
    constexpr int JointLive = 0x00;      // byte
    constexpr int JointType = 0x04;      // int
    constexpr int JointHead = 0x18;      // float[3]
    constexpr int JointTails = 0x70;     // [50][4] float[3]
    constexpr int JointTailSize = 0x30;
    constexpr int JointMaxTails = 50;
    constexpr int JointTailCount = 0x9D0; // int

    struct JointState
    {
        bool live;
        int type;
        int count;
        float head[3];
        BYTE tails[JointMaxTails * JointTailSize];
    };

    JointState* g_jointPrevious = nullptr;
    JointState* g_jointCurrent = nullptr;
    bool g_jointShifted[JointCount];
    bool g_jointApplied = false;
    bool g_smoothTrails = true;
    int g_trailSteps = 0, g_trailLive = 0, g_trailShifted = 0; // para el log
    DWORD g_trailReport = 0;

    void ReadJoint(BYTE* joint, JointState& state)
    {
        state.live = joint[JointLive] != 0;
        state.type = *reinterpret_cast<int*>(joint + JointType);
        state.count = *reinterpret_cast<int*>(joint + JointTailCount);
        memcpy(state.head, joint + JointHead, sizeof(state.head));
        const int count = state.count < 0 ? 0 : state.count > JointMaxTails ? JointMaxTails : state.count;
        memcpy(state.tails, joint + JointTails, count * JointTailSize);
    }

    bool SameJoint(BYTE* joint, const JointState& state)
    {
        if ((joint[JointLive] != 0) != state.live || *reinterpret_cast<int*>(joint + JointType) != state.type ||
            *reinterpret_cast<int*>(joint + JointTailCount) != state.count ||
            memcmp(joint + JointHead, state.head, sizeof(state.head)) != 0)
        {
            return false;
        }

        const int count = state.count < 0 ? 0 : state.count > JointMaxTails ? JointMaxTails : state.count;
        return memcmp(joint + JointTails, state.tails, count * JointTailSize) == 0;
    }
}

void InterpolationSetSmoothTrails(bool smooth)
{
    g_smoothTrails = smooth;
}

void TrailsBeforeRender(bool stepped, float t)
{
    if (!g_smoothTrails)
    {
        return;
    }

    if (g_jointPrevious == nullptr)
    {
        g_jointPrevious = static_cast<JointState*>(VirtualAlloc(nullptr, sizeof(JointState) * JointCount, MEM_COMMIT, PAGE_READWRITE));
        g_jointCurrent = static_cast<JointState*>(VirtualAlloc(nullptr, sizeof(JointState) * JointCount, MEM_COMMIT, PAGE_READWRITE));
        if (g_jointPrevious == nullptr || g_jointCurrent == nullptr)
        {
            g_smoothTrails = false;
            return;
        }
    }

    __try
    {
        for (int i = 0; i < JointCount; i++)
        {
            BYTE* joint = Joints + i * JointSize;
            JointState& previous = g_jointPrevious[i];
            JointState& current = g_jointCurrent[i];
            if (joint[JointLive] == 0 && !current.live)
            {
                continue;
            }

            if (stepped)
            {
                memcpy(&previous, &current, sizeof(JointState));
                ReadJoint(joint, current);

                // Misma estela (viva, mismo tipo), la cantidad de puntos cambio a lo sumo en uno y la
                // cabeza no salto lejos.
                const float dx = current.head[0] - previous.head[0];
                const float dy = current.head[1] - previous.head[1];
                const int change = current.count - previous.count;
                g_jointShifted[i] = previous.live && current.live && previous.type == current.type &&
                    current.count >= 1 && previous.count >= 1 && current.count <= JointMaxTails &&
                    change >= -1 && change <= 1 && dx * dx + dy * dy <= TeleportDistance * TeleportDistance;
                g_trailLive += current.live ? 1 : 0;
                g_trailShifted += g_jointShifted[i] ? 1 : 0;
            }
            else if (!SameJoint(joint, current))
            {
                ReadJoint(joint, current); // lo cambio otro: sin interpolar hasta el proximo paso
                g_jointShifted[i] = false;
            }

            if (!g_jointShifted[i])
            {
                continue;
            }

            // Cada punto entre su lugar anterior (el punto i viejo) y el actual (el punto i nuevo).
            // El punto 0 nuevo no tenia lugar anterior: sale de la cabeza anterior.
            float* tails = reinterpret_cast<float*>(joint + JointTails);
            const float* now = reinterpret_cast<const float*>(current.tails);
            const float* before = reinterpret_cast<const float*>(previous.tails);
            const int floatsPerTail = JointTailSize / 4;
            for (int k = 0; k < current.count; k++)
            {
                for (int f = 0; f < floatsPerTail; f++)
                {
                    const float from = k < previous.count ? before[k * floatsPerTail + f] : now[k * floatsPerTail + f];
                    const float to = now[k * floatsPerTail + f];
                    tails[k * floatsPerTail + f] = from + (to - from) * t;
                }
            }

            float* head = reinterpret_cast<float*>(joint + JointHead);
            for (int k = 0; k < 3; k++)
            {
                head[k] = previous.head[k] + (current.head[k] - previous.head[k]) * t;
            }
        }

        g_jointApplied = true;
        if (stepped)
        {
            g_trailSteps++;
        }

        if (g_looseLog != nullptr && GetTickCount() - g_trailReport >= 5000 && g_trailSteps > 0)
        {
            g_looseLog("Estelas: %.1f vivas y %.1f interpoladas por paso de logica.",
                static_cast<double>(g_trailLive) / g_trailSteps, static_cast<double>(g_trailShifted) / g_trailSteps);
            g_trailReport = GetTickCount();
            g_trailSteps = g_trailLive = g_trailShifted = 0;
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        g_jointApplied = false;
    }
}

void TrailsAfterRender()
{
    if (!g_jointApplied)
    {
        return;
    }

    g_jointApplied = false;
    __try
    {
        for (int i = 0; i < JointCount; i++)
        {
            if (!g_jointShifted[i])
            {
                continue;
            }

            BYTE* joint = Joints + i * JointSize;
            const JointState& current = g_jointCurrent[i];
            if (joint[JointLive] == 0)
            {
                continue;
            }

            memcpy(joint + JointHead, current.head, sizeof(current.head));
            memcpy(joint + JointTails, current.tails, current.count * JointTailSize);
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
    }
}
