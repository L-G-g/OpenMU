// stacks.dll - Stacks de pociones de hasta 255 para el cliente MU 1.04d con camera3d.dll.
//
// El servidor (actualizacion "Potion Stacks Of 255" de OpenMU) deja apilar las pociones hasta 255: la
// cantidad es la durabilidad del item (un byte). Al comprar o levantar del piso ya se juntan, pero el
// cliente decide por su cuenta si deja soltar el item que esta en el cursor sobre otro: para las pociones
// pide que cada uno tenga menos de 3 (10, 30 o 50 en algunos items de eventos) y si no, ni siquiera manda
// el pedido de mover. Esta DLL sube ese tope a 255 para las pociones del servidor.
//
// Como funciona:
//   - 0x007DD490 es el chequeo de apilar: bool __thiscall (inventario, ITEM* en el cursor, ITEM* de abajo),
//     RET 8. Lo usan el color de la casilla bajo el cursor (0x007DB0E0, verde o rojo) y el inventario al
//     soltar (0x008380F0, que con verdadero manda el pedido de mover con 0x005BD850). Dice que si cuando
//     los dos son el mismo item apilable y los dos tienen menos que el tope; el servidor junta lo que entra
//     y deja el resto aparte.
//   - Se salta al principio de la funcion: los primeros 6 bytes (PUSH EBP, MOV EBP,ESP, SUB ESP,0x1C) se
//     ejecutan en un trampolin que sigue en 0x007DD496. StackHook dice que si para dos pociones iguales del
//     grupo 14 (numeros 0-8, 35-40, 46-50 y 85-87) del mismo nivel con menos de 255 cada una; para el
//     resto llama al chequeo original.
//   - ITEM: +0x00 tipo (grupo * 512 + numero, 2 bytes), +0x02 opciones (nivel en los bits 3-6),
//     +0x16 durabilidad (la cantidad del stack).

#include "../shared.h"

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD CanStackFunction = 0x007DD490; // bool __thiscall (this, ITEM* cursor, ITEM* abajo), RET 8
    const BYTE CanStackPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x1C };

    // ITEM: tipo, opciones (nivel en los bits 3-6) y durabilidad (la cantidad del stack).
    constexpr DWORD ItemType = 0x00, ItemLevel = 0x02, ItemDurability = 0x16;
    constexpr int MaxStack = 255;

    // Pociones del grupo 14 que el servidor apila hasta 255 (las de PotionStacks.cs).
    struct Range { short first; short last; };
    constexpr short PotionGroup = 14 * 512;
    const Range StackedPotions[] = {
        { PotionGroup + 0, PotionGroup + 8 },   // manzana, vida, mana, pociones de asedio, antidoto
        { PotionGroup + 35, PotionGroup + 40 }, // escudo y complejas
        { PotionGroup + 46, PotionGroup + 50 }, // Halloween
        { PotionGroup + 85, PotionGroup + 87 }, // Cherry Blossom
    };

    LogFunction g_log = nullptr;

    // Nivel del item (Potion of Bless y Potion of Soul son el mismo tipo con distinto nivel).
    int Level(const BYTE* item)
    {
        return *reinterpret_cast<const int*>(item + ItemLevel) >> 3 & 0xF;
    }

    bool IsStackedPotion(short type)
    {
        for (const Range& range : StackedPotions)
        {
            if (type >= range.first && type <= range.last)
            {
                return true;
            }
        }

        return false;
    }

    // Los primeros 6 bytes de 0x007DD490 y un salto al resto.
    __declspec(naked) bool __fastcall CanStackOriginal(void*, void*, const BYTE*, const BYTE*)
    {
        __asm
        {
            push ebp
            mov ebp, esp
            sub esp, 0x1C
            push 0x007DD496 // CanStackFunction + 6
            ret
        }
    }

    // Se llama por un puntero volatil: el compilador no puede suponer que la funcion desnuda deja los
    // registros como estaban.
    using CanStackType = bool(__fastcall*)(void*, void*, const BYTE*, const BYTE*);
    CanStackType volatile g_canStackOriginal = &CanStackOriginal;

    // __fastcall con un segundo registro sin usar: recibe ECX (this) y los dos argumentos de la pila como el
    // __thiscall original, y los saca de la pila al volver (RET 8).
    bool __fastcall StackHook(void* inventory, void* unused, const BYTE* cursor, const BYTE* below)
    {
        if (cursor != nullptr && below != nullptr)
        {
            const short type = *reinterpret_cast<const short*>(cursor + ItemType);
            if (type == *reinterpret_cast<const short*>(below + ItemType) && IsStackedPotion(type))
            {
                return Level(cursor) == Level(below) && cursor[ItemDurability] < MaxStack
                    && below[ItemDurability] < MaxStack;
            }
        }

        return g_canStackOriginal(inventory, unused, cursor, below);
    }
}

extern "C" __declspec(dllexport) bool StartStacks(const StacksHost* host)
{
    if (host == nullptr || host->version != StacksHostVersion)
    {
        return false;
    }

    g_log = host->log;
    BYTE jump[5] = { 0xE9 };
    *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&StackHook) - (CanStackFunction + 5);
    if (memcmp(reinterpret_cast<const void*>(CanStackFunction), CanStackPrologue, sizeof(CanStackPrologue)) != 0)
    {
        g_log("Stacks: el chequeo de apilar no coincide; desactivados.");
        return false;
    }

    if (!WriteCode(CanStackFunction, jump, sizeof(jump)))
    {
        g_log("Stacks: no se pudieron instalar.");
        return false;
    }

    g_log("Stacks listos (pociones hasta %d al arrastrar).", MaxStack);
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
