// elites.dll - Monstruos elite para el cliente MU 1.04d con camera3d.dll.
//
// Un monstruo elite (por ejemplo el "Shadow Elite" de OpenMU) tiene el numero de su original, asi que el
// cliente lo dibuja y lo nombra igual. El servidor (plugin "Elite Monster Effect") le pone un efecto
// visual y lo manda con C1 07 MagicEffectStatus [activo] [numero del objeto, 2 bytes big endian]
// [efecto] al aparecer y cuando entra a la vista. Esta DLL se queda con los numeros de objeto que tienen
// ese efecto ([Elites] Effect de camera3d.ini) y los distingue.
//
// Como funciona:
//   - La llamada al reparto de paquetes (0x0065FD79) ya puede estar redirigida por healthbars.dll: se lee
//     a donde apunta y se llama a eso. Con 0x07 se marca o desmarca el objeto; con 0x13 (entra a la vista),
//     0x14 (sale) y 0x17 (muere) se desmarca. Todos los paquetes siguen al juego.
//   - Al marcarlo, al nombre del monstruo (CHARACTER +0x38, el que el juego muestra al pasar el mouse) se
//     le agrega " Elite": "Shadow" pasa a ser "Shadow Elite". El juego vuelve a escribir el nombre cuando
//     el monstruo entra de nuevo a la vista, y ahi llega otra vez el efecto.
//   - El cuerpo de cada objeto se dibuja en 0x005DE260 (OBJECT, ..., ..., pasada). camera3d.dll reemplaza
//     a quien lo llama (RenderObject 0x005EBBB0), asi que se salta al principio de la funcion: los primeros
//     10 bytes (PUSH EBP, MOV EBP,ESP, PUSH -1, PUSH 0x00CD425C) se ejecutan en un trampolin que sigue en
//     0x005DE26A. RenderHook llama al dibujo original y despues, si el objeto es el cuerpo
//     de un monstruo elite (OBJECT = CHARACTER +0x308), se dibuja el cuerpo dos veces mas con RenderBody
//     (0x005494C0) en el color de [Elites] Color: con color parejo y brillo (0x41, intensidad [Elites] Tint)
//     y con textura, cromo y brillo (0x46, como los reflejos de varios monstruos del juego). El brillo se
//     suma a lo que hay detras (se ve transparente) y el cromo se mueve con el tiempo.

#include "../shared.h"

#include <stdlib.h>
#include <string.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DispatchCallSite = 0x0065FD79;         // CALL al reparto al recibir cada paquete
    constexpr DWORD RenderBodyObjectFunction = 0x005DE260; // void __cdecl (OBJECT*, char, int, int): cuerpo de un objeto
    const BYTE RenderBodyObjectPrologue[] = { 0x55, 0x8B, 0xEC, 0x6A, 0xFF, 0x68, 0x5C, 0x42, 0xCD, 0x00 };
    constexpr DWORD ModelsFunction = 0x004CDA10;           // como al empezar 0x005DE260: (tipo) -> manejador
    constexpr DWORD ModelFunction = 0x00969AD0;            // __thiscall manejador -> BMD* del tipo
    constexpr DWORD RenderBodyFunction = 0x005494C0;       // void __thiscall BMD::RenderBody(flags, alfa, ...)
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224); // personajes: [+8], 400 de 0x598 bytes
    float* const RenderScale = reinterpret_cast<float*>(0x00E61638); // escala del dibujo: mayor a 1 en el contorno
                                                                     // del monstruo bajo el mouse (0x005DDAF0)

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr DWORD CharName = 0x38, CharDead = 0x24, CharKey = 0x7E, CharObject = 0x308;
    constexpr DWORD CharLive = 0x30C, CharKind = 0x320;
    constexpr int KindMonster = 2;
    constexpr size_t NameSize = 32; // lugar del nombre en el personaje (los nombres de monstruos son mas cortos)

    // OBJECT: tipo de modelo, malla oculta, mezcla, alfa, coordenadas de textura.
    constexpr DWORD ObjType = 0x30, ObjHiddenMesh = 0x3C, ObjBlendMesh = 0x44, ObjBlendMeshLight = 0x64;
    constexpr DWORD ObjTextureU = 0x68, ObjTextureV = 0x6C, ObjAlpha = 0x98;
    constexpr DWORD ModelBodyLight = 0x48; // BMD: color del dibujo (float r, g, b)
    constexpr int RenderColorBright = 0x41;          // color parejo y brillo
    constexpr int RenderTextureChromeBright = 0x46; // textura, cromo y brillo (0x44 sin textura no dibuja nada)

    // --- Paquetes ---
    constexpr int CodeEffect = 0x07, CodeAddNpcs = 0x13, CodeOutOfScope = 0x14, CodeKilled = 0x17;
    constexpr WORD IdMask = 0x7FFF; // 0x8000 marca a los recien aparecidos

    using DispatchType = int(__cdecl*)(int, BYTE*, int, int);
    using RenderBodyType = void(__thiscall*)(BYTE*, int, float, int, float, float, float, int, int);

    constexpr const char* NameSuffix = " Elite";

    LogFunction g_log = nullptr;
    DispatchType g_nextDispatch = nullptr;
    int g_effect = 1;
    float g_color[4] = { 0.6f, 0.2f, 1.0f, 1.5f }; // violeta; el cuarto es la intensidad del brillo
    bool g_elite[IdMask + 1] = {};
    float g_tint = 0.35f;     // [Elites] Tint: intensidad del tinte parejo (0: sin tinte)

    WORD ReadId(const BYTE* data)
    {
        return static_cast<WORD>(((data[0] << 8) | data[1]) & IdMask);
    }

    BYTE* Characters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    // Agrega " Elite" al nombre del monstruo con ese numero de objeto (si no lo tiene ya).
    void RenameElite(WORD id)
    {
        BYTE* characters = Characters();
        if (characters == nullptr)
        {
            return;
        }

        const size_t suffix = strlen(NameSuffix);
        for (int i = 0; i < CharacterCount; i++)
        {
            BYTE* character = characters + i * CharacterSize;
            if (!character[CharLive] || character[CharKind] != KindMonster
                || (*reinterpret_cast<const WORD*>(character + CharKey) & IdMask) != id)
            {
                continue;
            }

            char* name = reinterpret_cast<char*>(character + CharName);
            const size_t length = strnlen(name, NameSize);
            if (length + suffix < NameSize && (length < suffix || strcmp(name + length - suffix, NameSuffix) != 0))
            {
                memcpy(name + length, NameSuffix, suffix + 1);
            }
        }
    }

    void Unmark(WORD id)
    {
        g_elite[id] = false;
    }

    // Desmarca los objetos de un paquete (despues de descifrarlo, el paquete es C1 o C2).
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
                Unmark(ReadId(packet + offset));
                offset += 10 + packet[offset + 9];
            }

            break;
        }

        case CodeOutOfScope:
        {
            const int count = size > header ? packet[header] : 0;
            for (int i = 0; i < count && header + 1 + i * 2 + 2 <= size; i++)
            {
                Unmark(ReadId(packet + header + 1 + i * 2));
            }

            break;
        }

        case CodeKilled:
            if (header + 2 <= size)
            {
                Unmark(ReadId(packet + header));
            }

            break;
        }
    }

    int __cdecl PacketHook(int code, BYTE* packet, int size, int encrypted)
    {
        if (packet != nullptr)
        {
            if (code == CodeAddNpcs || code == CodeOutOfScope || code == CodeKilled)
            {
                ForgetObjects(code, packet, size);
            }
            else if (code == CodeEffect && packet[0] == 0xC1 && size >= 7 && packet[6] == g_effect)
            {
                const WORD id = ReadId(packet + 4);
                g_elite[id] = packet[3] != 0;
                if (g_elite[id])
                {
                    __try
                    {
                        RenameElite(id);
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER)
                    {
                    }
                }
            }
        }

        return g_nextDispatch(code, packet, size, encrypted);
    }

    // true si el objeto dibujado es el cuerpo de un monstruo elite vivo.
    bool IsEliteBody(const BYTE* object)
    {
        const BYTE* characters = Characters();
        if (characters == nullptr || object < characters + CharObject
            || object >= characters + CharacterCount * CharacterSize)
        {
            return false;
        }

        if (static_cast<DWORD>(object - characters - CharObject) % CharacterSize != 0)
        {
            return false; // otro objeto dentro del personaje
        }

        const BYTE* character = object - CharObject;
        if (!character[CharLive] || character[CharDead] || character[CharKind] != KindMonster)
        {
            return false;
        }

        if (g_elite[*reinterpret_cast<const WORD*>(character + CharKey) & IdMask])
        {
            return true;
        }

        // El nombre con " Elite" lo puso esta DLL al llegar el efecto, y el juego lo vuelve a escribir cuando
        // crea otra vez al monstruo: alcanza aunque la marca se haya borrado con un paquete de la vista.
        const char* name = reinterpret_cast<const char*>(character + CharName);
        const size_t length = strnlen(name, NameSize);
        const size_t suffix = strlen(NameSuffix);
        return length > suffix && length < NameSize && strcmp(name + length - suffix, NameSuffix) == 0;
    }

    // El modelo (BMD) de un tipo, igual que al empezar 0x005DE260: PUSH tipo, CALL 0x004CDA10, MOV ECX,EAX,
    // CALL 0x00969AD0.
    __declspec(naked) BYTE* __cdecl Model(int /*type*/)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            push dword ptr [ebp + 8]
            mov eax, ModelsFunction
            call eax
            mov ecx, eax
            mov eax, ModelFunction
            call eax
            mov esp, ebp
            pop ebp
            ret
        }
    }

    // Tambien por un puntero volatil, por lo mismo que el dibujo original (ver RenderHook).
    using ModelType = BYTE*(__cdecl*)(int);
    ModelType volatile g_model = &Model;

    void DrawEliteGlow(BYTE* object)
    {
        BYTE* model = g_model(*reinterpret_cast<const int*>(object + ObjType));
        if (model == nullptr)
        {
            return;
        }

        float* light = reinterpret_cast<float*>(model + ModelBodyLight);
        const float saved[3] = { light[0], light[1], light[2] };
        const float savedScale = *RenderScale;
        *RenderScale = 1.0f;
        // Primero un tinte violeta parejo en todo el cuerpo (color y brillo: se suma, se ve transparente) y
        // despues el cromo, que se mueve con el tiempo.
        const int passes[2] = { RenderColorBright, RenderTextureChromeBright };
        const float strength[2] = { g_tint, g_color[3] };
        for (int i = 0; i < 2; i++)
        {
            light[0] = g_color[0] * strength[i];
            light[1] = g_color[1] * strength[i];
            light[2] = g_color[2] * strength[i];
            reinterpret_cast<RenderBodyType>(RenderBodyFunction)(model, passes[i],
                *reinterpret_cast<const float*>(object + ObjAlpha), *reinterpret_cast<const int*>(object + ObjBlendMesh),
                *reinterpret_cast<const float*>(object + ObjBlendMeshLight),
                *reinterpret_cast<const float*>(object + ObjTextureU), *reinterpret_cast<const float*>(object + ObjTextureV),
                *reinterpret_cast<const int*>(object + ObjHiddenMesh), -1);
        }

        light[0] = saved[0];
        light[1] = saved[1];
        light[2] = saved[2];
        *RenderScale = savedScale;
    }

    // Los primeros 10 bytes de 0x005DE260 y un salto al resto.
    __declspec(naked) void __cdecl RenderBodyObjectOriginal(BYTE*, char, int, int)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            push -1
            push 0x00CD425C
            push 0x005DE26A // RenderBodyObjectFunction + 10
            ret
        }
    }

    // Se llama por un puntero volatil: el compilador no puede suponer que la funcion desnuda (que devuelve en
    // EAX lo que devuelve el dibujo original) deja los registros como estaban.
    using RenderBodyObjectType = void(__cdecl*)(BYTE*, char, int, int);
    RenderBodyObjectType volatile g_renderBodyObjectOriginal = &RenderBodyObjectOriginal;

    void __cdecl RenderHook(BYTE* object, char select, int param3, int pass)
    {
        g_renderBodyObjectOriginal(object, select, param3, pass);
        if (object == nullptr || pass != 0)
        {
            return; // las otras pasadas (por ejemplo 10) son dibujos especiales del juego
        }

        __try
        {
            if (IsEliteBody(object))
            {
                DrawEliteGlow(object);
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    // Destino de un CALL rel32, o 0 si no es un CALL.
    DWORD CallTarget(DWORD callSite)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(callSite);
        return code[0] == 0xE8 ? callSite + 5 + *reinterpret_cast<const DWORD*>(code + 1) : 0;
    }

    bool RedirectCall(DWORD callSite, const void* target)
    {
        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(target) - (callSite + 5);
        return WriteCode(callSite, call, sizeof(call));
    }

    // [Elites] Color=R,G,B,A (de 0 a 1; A es la intensidad del brillo).
    void ReadColor(const char* iniPath)
    {
        char text[64];
        GetPrivateProfileStringA("Elites", "Color", "", text, sizeof(text), iniPath);
        float values[4];
        int count = 0;
        char* context = nullptr;
        for (char* part = strtok_s(text, ", ", &context); part != nullptr && count < 4; part = strtok_s(nullptr, ", ", &context))
        {
            values[count++] = static_cast<float>(atof(part));
        }

        if (count == 4)
        {
            memcpy(g_color, values, sizeof(values));
        }
    }
}

extern "C" __declspec(dllexport) bool StartElites(const ElitesHost* host)
{
    if (host == nullptr || host->version != ElitesHostVersion)
    {
        return false;
    }

    g_log = host->log;
    g_effect = GetPrivateProfileIntA("Elites", "Effect", 1, host->iniPath);
    ReadColor(host->iniPath);
    char tint[32];
    GetPrivateProfileStringA("Elites", "Tint", "0.35", tint, sizeof(tint), host->iniPath);
    g_tint = static_cast<float>(atof(tint));

    // healthbars.dll puede haber redirigido la llamada antes: se encadena a lo que haya.
    const DWORD dispatch = CallTarget(DispatchCallSite);
    if (dispatch == 0)
    {
        g_log("Elites: la llamada del reparto de paquetes no coincide; desactivados.");
        return false;
    }

    g_nextDispatch = reinterpret_cast<DispatchType>(dispatch);
    if (!RedirectCall(DispatchCallSite, &PacketHook))
    {
        g_log("Elites: no se pudieron instalar.");
        return false;
    }

    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&RenderHook) - (RenderBodyObjectFunction + 5);
    if (memcmp(reinterpret_cast<const void*>(RenderBodyObjectFunction), RenderBodyObjectPrologue,
            sizeof(RenderBodyObjectPrologue)) != 0
        || !WriteCode(RenderBodyObjectFunction, jump, sizeof(jump)))
    {
        g_log("Elites: el dibujo de los cuerpos no coincide; sin brillo (solo el nombre).");
    }

    g_log("Elites listos (efecto %d, color %.2f %.2f %.2f %.2f).", g_effect, g_color[0], g_color[1], g_color[2],
        g_color[3]);
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
