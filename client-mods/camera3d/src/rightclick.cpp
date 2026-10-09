// Click derecho "inteligente" para el cliente MU 1.04d (main.exe S6E3, parche OpenMU).
//
// Con el inventario abierto, click derecho sobre un item:
//   - con una ventana de NPC abierta (mezclas, baul, trade, tienda personal): lo pasa a esa ventana,
//   - con la tienda de un NPC abierta: lo vende (igual que soltarlo sobre la tienda),
//   - sin ventanas de NPC: si se puede equipar, lo equipa.
// Click derecho sobre un item equipado o sobre un item en una ventana de NPC: lo devuelve al inventario.
// Los consumibles siguen funcionando como siempre (el juego los usa con click derecho).
//
// Como funciona: no se arman paquetes propios. Se simula, durante unos cuadros, que el jugador agarra
// el item con click izquierdo y lo suelta en el lugar de destino. Asi el juego hace sus propias
// validaciones y le manda al server el mismo pedido que cuando se arrastra con el mouse.
// El juego actualiza una vez por cuadro su tabla de estados de teclas (0x00791020, llamada desde
// 0x004D9D59). Esa llamada pasa por KeyUpdateHook, que despues de la actualizacion ajusta solo los
// estados de los botones izquierdo y derecho en la tabla del juego, y mueve el mouse "virtual" del juego.

#include <windows.h>
#include "mods.h"

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD KeyUpdateFunction = 0x00791020; // void __thiscall KeyState::Update()
    constexpr DWORD KeyUpdateCallSite = 0x004D9D59; // CALL KeyUpdateFunction
    // Partes de la actualizacion de la interfaz (0x00860970), sobre el sistema de ventanas interno.
    constexpr DWORD UiUpdateMouseEvents = 0x00815880;
    constexpr DWORD UiUpdate = 0x00815B90;
    // Estados por tecla: 0 suelta, 2 recien apretada, 3 apretada, 1 recien soltada.
    constexpr BYTE KeyUp = 0, KeyReleased = 1, KeyPressed = 2, KeyHeld = 3;
    int* const MouseX = reinterpret_cast<int*>(0x0879340C);            // coordenadas 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    DWORD* const PickedItem = reinterpret_cast<DWORD*>(0x09816F7C);     // item agarrado con el mouse
    // Click derecho segun los mensajes de la ventana (apretado recien, soltado, sostenido). La logica del
    // personaje los usa para lanzar habilidades y, si hay un item agarrado, lo devuelve a su lugar.
    BYTE* const RightButtonFlags = reinterpret_cast<BYTE*>(0x08793381);
    BYTE* const UiManager = reinterpret_cast<BYTE*>(0x09867090);        // administrador de ventanas
    DWORD* const UiManagerInitFlag = reinterpret_cast<DWORD*>(0x098671D4);
    DWORD* const CharacterMachine = reinterpret_cast<DWORD*>(0x08128AC4); // +0x1240: 12 items equipados
    DWORD* const ItemAttributes = reinterpret_cast<DWORD*>(0x08128AC0);   // tabla de item.bmd (0x54 c/u)

    constexpr DWORD EquipmentOffset = 0x1240;
    constexpr DWORD ItemSize = 0x6B;
    constexpr DWORD ItemAttributeSize = 0x54;
    constexpr int CellSize = 20;

    // vtables de las ventanas (para reconocerlas entre las del administrador)
    constexpr DWORD VtMyInventory = 0x00D46F6C;
    constexpr DWORD VtMix = 0x00D46AB8;
    constexpr DWORD VtStorage = 0x00D489A0;
    constexpr DWORD VtTrade = 0x00D48BB4;
    constexpr DWORD VtNpcShop = 0x00D47750;
    constexpr DWORD VtMyShop = 0x00D47314;

    // Campos de las ventanas y de la grilla (CNewUIInventoryCtrl)
    constexpr DWORD WindowVisible = 0x08;     // byte
    constexpr DWORD InvGrids = 0x18;          // CNewUIMyInventory: grillas (la principal primero)
    constexpr DWORD InvHoveredEquip = 0x11C;  // int: casillero de equipo bajo el mouse, -1 si ninguno
    constexpr DWORD InvEquipRects = 0x2C;     // 12 x {x, y, ancho, alto, ?}
    constexpr DWORD GridX = 0x28, GridY = 0x2C, GridColumns = 0x38, GridRows = 0x3C, GridCells = 0x40;
    constexpr DWORD GridHoveredCell = 0x48, GridHoveredItem = 0x54;
    constexpr DWORD ItemCellX = 0x43, ItemCellY = 0x44;

    LogFunction g_log = nullptr;

    // --- Secuencia simulada: una lista de pasos, uno por cuadro ---
    struct Step
    {
        int x;
        int y;
        bool leftDown;
    };

    Step g_steps[16];
    int g_stepCount = 0;
    int g_stepIndex = -1;          // -1: no hay secuencia en curso
    int g_savedMouseX = 0;
    int g_savedMouseY = 0;
    bool g_rightConsumed = false;  // el click derecho actual lo usamos nosotros
    BYTE g_simulatedLeftState = 0;

    bool IsRunning()
    {
        return g_stepIndex >= 0;
    }

    template <typename T>
    T Read(DWORD address)
    {
        return *reinterpret_cast<T*>(address);
    }

    struct Windows
    {
        DWORD inventory = 0;
        DWORD mix = 0;
        DWORD storage = 0;
        DWORD trade = 0;
        DWORD npcShop = 0;
        DWORD myShop = 0;
    };

    bool IsVisible(DWORD window)
    {
        return window != 0 && Read<BYTE>(window + WindowVisible) != 0;
    }

    // Busca las ventanas entre los punteros que guarda el administrador, por su vtable.
    Windows FindWindows()
    {
        Windows result;
        if ((*UiManagerInitFlag & 1) == 0)
        {
            return result;
        }

        __try
        {
            for (DWORD offset = 4; offset < 0x400; offset += 4)
            {
                const DWORD window = Read<DWORD>(reinterpret_cast<DWORD>(UiManager) + offset);
                if (window < 0x10000)
                {
                    continue;
                }

                switch (Read<DWORD>(window))
                {
                case VtMyInventory: result.inventory = window; break;
                case VtMix: result.mix = window; break;
                case VtStorage: result.storage = window; break;
                case VtTrade: result.trade = window; break;
                case VtNpcShop: result.npcShop = window; break;
                case VtMyShop: result.myShop = window; break;
                default: break;
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return Windows{};
        }

        return result;
    }

    struct ItemSizeInfo
    {
        int width;
        int height;
    };

    ItemSizeInfo GetItemSize(short type)
    {
        const DWORD attribute = *ItemAttributes + static_cast<DWORD>(type) * ItemAttributeSize;
        return { Read<BYTE>(attribute + 0x26), Read<BYTE>(attribute + 0x27) };
    }

    // Centro de un lugar libre de la grilla donde entra un item de ese tamano.
    bool FindFreeSpot(DWORD grid, ItemSizeInfo size, POINT& center)
    {
        if (grid == 0)
        {
            return false;
        }

        const int columns = Read<int>(grid + GridColumns);
        const int rows = Read<int>(grid + GridRows);
        const DWORD cells = Read<DWORD>(grid + GridCells);
        if (cells == 0 || size.width <= 0 || size.height <= 0)
        {
            return false;
        }

        for (int y = 0; y + size.height <= rows; y++)
        {
            for (int x = 0; x + size.width <= columns; x++)
            {
                bool free = true;
                for (int dy = 0; dy < size.height && free; dy++)
                {
                    for (int dx = 0; dx < size.width && free; dx++)
                    {
                        free = Read<DWORD>(cells + ((y + dy) * columns + (x + dx)) * 4) == 0;
                    }
                }

                if (free)
                {
                    center.x = Read<int>(grid + GridX) + x * CellSize + size.width * CellSize / 2;
                    center.y = Read<int>(grid + GridY) + y * CellSize + size.height * CellSize / 2;
                    return true;
                }
            }
        }

        return false;
    }

    // Item bajo el mouse en una grilla, con el centro de su dibujo.
    bool GetHoveredItem(DWORD grid, short& type, POINT& center)
    {
        if (grid == 0 || Read<int>(grid + GridHoveredCell) == -1)
        {
            return false;
        }

        const DWORD item = Read<DWORD>(grid + GridHoveredItem);
        if (item == 0)
        {
            return false;
        }

        type = Read<short>(item);
        const ItemSizeInfo size = GetItemSize(type);
        center.x = Read<int>(grid + GridX) + Read<BYTE>(item + ItemCellX) * CellSize + size.width * CellSize / 2;
        center.y = Read<int>(grid + GridY) + Read<BYTE>(item + ItemCellY) * CellSize + size.height * CellSize / 2;
        return true;
    }

    short EquippedType(int slot)
    {
        return Read<short>(*CharacterMachine + EquipmentOffset + slot * ItemSize);
    }

    POINT EquipSlotCenter(DWORD inventory, int slot)
    {
        const DWORD rect = inventory + InvEquipRects + slot * 0x14;
        return { Read<int>(rect) + Read<int>(rect + 8) / 2, Read<int>(rect + 4) + Read<int>(rect + 12) / 2 };
    }

    // Casillero de equipo para un tipo de item (grupo * 512 + indice); -1 si no se equipa.
    int EquipSlotFor(short type, bool& alternativeSlot)
    {
        alternativeSlot = false;
        const int group = type / 512;
        const int index = type % 512;
        if (group >= 0 && group <= 5)
        {
            alternativeSlot = true; // si la mano izquierda esta ocupada, la derecha
            return 0;
        }

        if (group >= 6 && group <= 11)
        {
            return group - 5; // escudo 1, casco 2, armadura 3, pantalones 4, guantes 5, botas 6
        }

        if (group == 12)
        {
            const bool wing = (index >= 0 && index <= 6) || (index >= 36 && index <= 43) || index == 49 || index == 50
                || (index >= 130 && index <= 135);
            return wing ? 7 : -1;
        }

        if (group == 13)
        {
            if (index == 5)
            {
                return 1; // Dark Raven (en la mano derecha)
            }

            if ((index >= 0 && index <= 4) || index == 37 || (index >= 64 && index <= 67) || index == 80 || index == 106 || index == 123)
            {
                return 8; // mascotas y monturas
            }

            if (index == 12 || index == 13 || (index >= 25 && index <= 28))
            {
                return 9; // colgantes
            }

            if (index == 8 || index == 9 || index == 10 || (index >= 20 && index <= 24) || (index >= 38 && index <= 42)
                || index == 68 || index == 76 || index == 122)
            {
                alternativeSlot = true;
                return 10; // anillos (10 y 11)
            }
        }

        return -1;
    }

    void StartSequence(POINT from, POINT to)
    {
        g_savedMouseX = *MouseX;
        g_savedMouseY = *MouseY;
        g_stepCount = 0;
        auto add = [](POINT p, bool down) { g_steps[g_stepCount++] = { p.x, p.y, down }; };

        // Agarrar: apuntar dos cuadros (boton suelto, para que la ventana marque el item o el
        // casillero bajo el mouse; con uno solo, los casilleros de equipo a veces no llegan), apretar,
        // soltar.
        add(from, false);
        add(from, false);
        add(from, true);
        add(from, false);
        // Soltar en el destino: igual.
        add(to, false);
        add(to, false);
        add(to, true);
        add(to, false);
        g_stepIndex = 0;
    }

    // Decide que hacer con el click derecho. true si se inicio una secuencia (y el click se consume).
    bool TryStartAction()
    {
        if (*PickedItem != 0)
        {
            return false; // ya hay un item agarrado
        }

        const Windows windows = FindWindows();
        if (!IsVisible(windows.inventory))
        {
            return false;
        }

        const DWORD inventoryGrid = Read<DWORD>(windows.inventory + InvGrids);

        // Ventana de NPC abierta (la primera que este visible).
        DWORD targetGrid = 0;
        bool sell = false;
        if (IsVisible(windows.mix)) targetGrid = Read<DWORD>(windows.mix + 0x10);
        else if (IsVisible(windows.storage)) targetGrid = Read<DWORD>(windows.storage + 0x21C);
        else if (IsVisible(windows.trade))
        {
            // De las dos grillas del trade, la propia es la de abajo.
            const DWORD a = Read<DWORD>(windows.trade + 0x178);
            const DWORD b = Read<DWORD>(windows.trade + 0x17C);
            targetGrid = (a != 0 && b != 0) ? (Read<int>(a + GridY) > Read<int>(b + GridY) ? a : b) : (a != 0 ? a : b);
        }
        else if (IsVisible(windows.myShop)) targetGrid = Read<DWORD>(windows.myShop + 0x10);
        else if (IsVisible(windows.npcShop))
        {
            targetGrid = Read<DWORD>(windows.npcShop + 0x10);
            sell = true;
        }

        short type = -1;
        POINT from{};
        POINT to{};

        // 1) Item del inventario
        if (GetHoveredItem(inventoryGrid, type, from))
        {
            if (targetGrid != 0)
            {
                if (sell)
                {
                    to.x = Read<int>(targetGrid + GridX) + Read<int>(targetGrid + 0x30) / 2;
                    to.y = Read<int>(targetGrid + GridY) + Read<int>(targetGrid + 0x34) / 2;
                }
                else if (!FindFreeSpot(targetGrid, GetItemSize(type), to))
                {
                    g_log("Click derecho: no hay lugar en la ventana abierta.");
                    return false;
                }
            }
            else
            {
                bool alternative = false;
                int slot = EquipSlotFor(type, alternative);
                if (slot < 0)
                {
                    return false; // no se equipa: que el juego lo use (pociones, etc.)
                }

                if (EquippedType(slot) != -1 && alternative && EquippedType(slot + 1) == -1)
                {
                    slot++;
                }

                if (EquippedType(slot) != -1)
                {
                    g_log("Click derecho: el casillero %d ya esta ocupado.", slot);
                    return false;
                }

                to = EquipSlotCenter(windows.inventory, slot);
            }

            g_log("Click derecho: item %d (%d,%d) -> (%d,%d)", type, from.x, from.y, to.x, to.y);
            StartSequence(from, to);
            return true;
        }

        // 2) Item equipado -> inventario
        const int equipSlot = Read<int>(windows.inventory + InvHoveredEquip);
        if (equipSlot >= 0 && equipSlot < 12 && (type = EquippedType(equipSlot)) != -1)
        {
            if (!FindFreeSpot(inventoryGrid, GetItemSize(type), to))
            {
                g_log("Click derecho: no hay lugar en el inventario.");
                return false;
            }

            from = EquipSlotCenter(windows.inventory, equipSlot);
            g_log("Click derecho: desequipar %d (casillero %d)", type, equipSlot);
            StartSequence(from, to);
            return true;
        }

        // 3) Item de la ventana del NPC -> inventario
        if (!sell && GetHoveredItem(targetGrid, type, from))
        {
            if (!FindFreeSpot(inventoryGrid, GetItemSize(type), to))
            {
                g_log("Click derecho: no hay lugar en el inventario.");
                return false;
            }

            g_log("Click derecho: item %d de la ventana al inventario", type);
            StartSequence(from, to);
            return true;
        }

        return false;
    }

    // Siguiente estado de una tecla segun si esta apretada, igual que lo calcula el juego.
    BYTE NextKeyState(BYTE state, bool down)
    {
        if (down)
        {
            return (state == KeyUp || state == KeyReleased) ? KeyPressed : KeyHeld;
        }

        return (state == KeyPressed || state == KeyHeld) ? KeyReleased : KeyUp;
    }

    bool StartActionSafely()
    {
        __try
        {
            return TryStartAction();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_log("Click derecho: error leyendo la memoria del juego; se ignora.");
            return false;
        }
    }

    // Ejecuta toda la secuencia en el mismo tick de logica: para cada paso se ajusta el mouse y el boton
    // izquierdo y se procesa la interfaz (solo eventos de mouse y actualizacion; el teclado no, para no
    // procesar dos veces una tecla apretada en este tick). Despues se deja todo como estaba.
    void RunSequenceNow(BYTE* keyStates)
    {
        const DWORD uiSystem = Read<DWORD>(reinterpret_cast<DWORD>(UiManager));
        if (uiSystem == 0)
        {
            g_stepIndex = -1;
            return;
        }

        using UiFunction = void(__fastcall*)(DWORD);
        const BYTE realLeft = keyStates[VK_LBUTTON];
        BYTE left = KeyUp;
        for (int i = 0; i < g_stepCount; i++)
        {
            *MouseX = g_steps[i].x;
            *MouseY = g_steps[i].y;
            left = NextKeyState(left, g_steps[i].leftDown);
            keyStates[VK_LBUTTON] = left;
            keyStates[VK_RBUTTON] = KeyUp;
            reinterpret_cast<UiFunction>(UiUpdateMouseEvents)(uiSystem);
            reinterpret_cast<UiFunction>(UiUpdate)(uiSystem);
        }

        *MouseX = g_savedMouseX;
        *MouseY = g_savedMouseY;
        keyStates[VK_LBUTTON] = realLeft;
        keyStates[VK_RBUTTON] = KeyUp;
        g_stepIndex = -1;
    }

    // Reemplaza la llamada del juego a su actualizacion de teclas, una vez por tick de logica.
    void __fastcall KeyUpdateHook(BYTE* keyStates)
    {
        using KeyUpdateType = void(__fastcall*)(BYTE*);
        reinterpret_cast<KeyUpdateType>(KeyUpdateFunction)(keyStates);

        const BYTE right = keyStates[VK_RBUTTON];
        if (g_rightConsumed)
        {
            // Lo mismo que hace el inventario con un click derecho propio: que el personaje no lo vea.
            // Si no, al vender devolvia el item agarrado (esperando la respuesta) al inventario.
            memset(RightButtonFlags, 0, 3);
        }

        if (right == KeyUp || right == KeyReleased)
        {
            if (g_rightConsumed)
            {
                keyStates[VK_RBUTTON] = KeyUp; // que el juego no vea soltar un click que no vio apretar
            }

            g_rightConsumed = false;
            return;
        }

        if (g_rightConsumed)
        {
            keyStates[VK_RBUTTON] = KeyUp; // sigue apretado el click que ya usamos
            return;
        }

        if (right == KeyPressed && StartActionSafely())
        {
            g_rightConsumed = true;
            __try
            {
                RunSequenceNow(keyStates);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                g_log("Click derecho: error al procesar la interfaz; se ignora.");
                *MouseX = g_savedMouseX;
                *MouseY = g_savedMouseY;
                g_stepIndex = -1;
            }

            keyStates[VK_RBUTTON] = KeyUp;
            memset(RightButtonFlags, 0, 3);
        }
    }
}

void RightClickBeforeRender()
{
    // El mouse "virtual" solo se mueve mientras el juego procesa la interfaz; para dibujar se
    // vuelve a la posicion real, asi el cursor no viaja por la pantalla.
    if (IsRunning())
    {
        *MouseX = g_savedMouseX;
        *MouseY = g_savedMouseY;
    }
}

bool RightClickFilterMessage(UINT message)
{
    // Mientras se simula el arrastre, el mouse real no tiene que mover el puntero del juego.
    return IsRunning() && (message == WM_MOUSEMOVE || message == WM_LBUTTONDOWN || message == WM_LBUTTONUP);
}

bool InstallRightClick(LogFunction log)
{
    g_log = log;

    // CALL rel32 a la actualizacion de teclas; si no coincide, el main.exe es otro.
    BYTE expected[5] = { 0xE8 };
    *reinterpret_cast<DWORD*>(expected + 1) = KeyUpdateFunction - (KeyUpdateCallSite + 5);
    if (memcmp(reinterpret_cast<const void*>(KeyUpdateCallSite), expected, sizeof(expected)) != 0)
    {
        g_log("Click derecho: la llamada de teclas no coincide; desactivado.");
        return false;
    }

    BYTE call[5] = { 0xE8 };
    *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(&KeyUpdateHook) - (KeyUpdateCallSite + 5);
    if (!WriteCode(KeyUpdateCallSite, call, sizeof(call)))
    {
        g_log("Click derecho: no se pudo instalar.");
        return false;
    }

    g_log("Click derecho listo.");
    return true;
}
