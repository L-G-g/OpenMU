// Comandos de juego para la automatizacion: manejar al personaje por intencion (caminar a una baldosa,
// atacar a un monstruo, usar una habilidad) en lugar de clicks en coordenadas de pantalla.
//
// Como funciona: no se arman paquetes propios ni se calcula donde cae algo en la pantalla. En cada paso
// de logica, antes de que el juego procese al personaje (0x005B5FC0, la logica que atiende los clicks
// sobre el mundo), se deja el estado como si el jugador estuviera haciendo el click:
//   - el objeto elegido bajo el mouse (0x00E61730 monstruo o jugador, 0x00E6172C NPC, 0x00E61728 item,
//     0x00E61734 objeto del mapa) es el que pide el comando; el juego lo recalcula al dibujar
//     (0x005B81E0), asi que se vuelve a poner en cada paso mientras dure el comando;
//   - los botones del mouse (0x08793384 izquierdo, 0x08793381 derecho: apretado recien, soltado,
//     sostenido) quedan apretados;
//   - para caminar o usar una habilidad sobre una baldosa, la busqueda del terreno bajo el mouse
//     (llamadas a 0x005DA250 en 0x005B741A y 0x005AAFDF) devuelve esa baldosa.
// Asi el juego hace lo mismo que con un click real: busca el camino, se acerca, ataca o lanza la
// habilidad y manda sus propios pedidos al servidor. Para saber que paso, se cuentan los paquetes que
// arma el juego (0x00404B90 empieza cada paquete): 0xD4 caminar, 0x11 ataque, 0x19/0x1E/0xDB/0x1D
// habilidades.
//
// Datos de los personajes (CHARACTER, 0x598 bytes, 400 en [[0x00E7C224]+8]; el propio en [0x07BC4F04]):
//   +0x0E en zona segura, +0x13 clase, +0x24 muerto, +0x32 habilidad elegida (indice en la lista),
//   +0x38 nombre, +0x78 caminando, +0x79 tipo de movimiento, +0x7E numero en el servidor, +0x84 numero de
//   monstruo o NPC (como en el servidor, 3 = Spider),
//   +0xAC/+0xB0 baldosa x/y, +0x308 objeto: +0x30C vivo, +0x31A accion, +0x320 tipo (1 jugador,
//   2 monstruo, 4 NPC), +0x338 modelo, +0x404 posicion.
// Atributos del personaje propio ([0x08128AC8]): +0x0B clase, +0x0E nivel, +0x22/+0x26 vida y maxima,
// +0x24/+0x28 mana, +0x2A/+0x2C SD, +0x40/+0x42 AG, +0x78 lista de habilidades (WORD).

#include "muauto.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <algorithm>
#include <vector>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD PacketStartFunction = 0x00404B90;   // void __thiscall Packet::Start(BYTE type, BYTE code)
    constexpr DWORD PickTerrainFunction = 0x005DA250;   // char PickTerrainTile(x, y, tileX, tileY, scale, size, select)
    constexpr DWORD PickCallWalk = 0x005B741A;          // en la logica del personaje: click en el terreno
    constexpr DWORD PickCallSkillTarget = 0x005AAFDF;   // en el destino de una habilidad sin objeto elegido
    constexpr DWORD EnterGameFunction = 0x004D6A60;     // void EnterGame(char check): el boton Connect de la seleccion
    constexpr DWORD PartyRequestFunction = 0x0078F100;  // __thiscall (short numero del jugador): C1 40, pedido de party
    int* const Scene = reinterpret_cast<int*>(0x00E609E8);              // 2 login, 4 seleccion de personaje, 5 juego
    int* const SelectedHero = reinterpret_cast<int*>(0x00E60B4C);       // personaje elegido en la seleccion

    DWORD* const Hero = reinterpret_cast<DWORD*>(0x07BC4F04);
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224);
    DWORD* const CharacterAttribute = reinterpret_cast<DWORD*>(0x08128AC8);
    DWORD* const SkillTable = reinterpret_cast<DWORD*>(0x08128ABC);      // 0x58 bytes por habilidad, nombre al principio
    int* const CurrentMap = reinterpret_cast<int*>(0x00E61E18);
    int* const SelectedItem = reinterpret_cast<int*>(0x00E61728);
    int* const SelectedNpc = reinterpret_cast<int*>(0x00E6172C);
    int* const SelectedCharacter = reinterpret_cast<int*>(0x00E61730);  // monstruo o jugador
    int* const SelectedOperate = reinterpret_cast<int*>(0x00E61734);
    int* const TargetLock = reinterpret_cast<int*>(0x00E61738);         // 1 ataque, 2 habilidad, -1 ninguno
    int* const ActionTarget = reinterpret_cast<int*>(0x00E61790);       // objeto hacia el que va el personaje
    int* const ClickTicks = reinterpret_cast<int*>(0x081C03B4);         // pasos desde el ultimo click atendido
    int* const ClickDelay = reinterpret_cast<int*>(0x00E616D0);         // pasos minimos entre clicks
    int* const MouseX = reinterpret_cast<int*>(0x0879340C);             // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    BYTE* const LeftButtonFlags = reinterpret_cast<BYTE*>(0x08793384);  // apretado recien, soltado, sostenido
    BYTE* const RightButtonFlags = reinterpret_cast<BYTE*>(0x08793381);
    float* const CollisionPosition = reinterpret_cast<float*>(0x08793248); // punto del terreno bajo el mouse
    float* const PickedTileX = reinterpret_cast<float*>(0x082C6A9C);
    float* const PickedTileY = reinterpret_cast<float*>(0x082C6A98);
    const float* const TerrainHeight = reinterpret_cast<const float*>(0x08406AA0); // 256x256

    constexpr int CharacterCount = 400;
    constexpr DWORD CharacterSize = 0x598;
    constexpr int MaxSkills = 150;

    // Campos del personaje (CHARACTER) y de los atributos.
    constexpr DWORD CharSafeZone = 0x0E, CharClass = 0x13, CharDead = 0x24, CharCurrentSkill = 0x32;
    constexpr DWORD CharName = 0x38, CharMoving = 0x78, CharKey = 0x7E, CharMonsterType = 0x84;
    constexpr DWORD CharTileX = 0xAC, CharTileY = 0xB0;
    constexpr DWORD CharLive = 0x30C, CharAction = 0x31A, CharKind = 0x320, CharModel = 0x338;
    constexpr DWORD AttrClass = 0x0B, AttrLevel = 0x0E, AttrLife = 0x22, AttrMana = 0x24, AttrLifeMax = 0x26;
    constexpr DWORD AttrManaMax = 0x28, AttrShield = 0x2A, AttrShieldMax = 0x2C, AttrAg = 0x40, AttrAgMax = 0x42;
    constexpr DWORD AttrSkills = 0x78;

    // Paquetes que arma el juego.
    constexpr int PacketWalk = 0xD4, PacketHit = 0x11;
    constexpr int SkillPackets[] = { 0x19, 0x1E, 0xDB, 0x1D };

    // Punto de la pantalla (640x480) donde queda el mouse mientras dura un comando: fuera de la barra de
    // abajo y de las ventanas de los costados, para que el juego tome el click como sobre el mundo.
    constexpr int SafeMouseX = 320, SafeMouseY = 180;
    constexpr int TicksPerSecond = 25;
    constexpr int SettleTicks = 4;

    LogFunction g_log = nullptr;
    bool g_installed = false;
    bool g_pickInstalled = false;

    // --- Lectura segura de la memoria del juego ---

    bool SafeCopy(void* target, DWORD address, size_t length)
    {
        __try
        {
            memcpy(target, reinterpret_cast<const void*>(address), length);
            return true;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return false;
        }
    }

    template <typename T>
    T Read(DWORD address)
    {
        T value{};
        SafeCopy(&value, address, sizeof(T));
        return value;
    }

    DWORD Character(int index)
    {
        if (index < 0 || index >= CharacterCount)
        {
            return 0;
        }

        const DWORD manager = *CharacterManager;
        const DWORD array = manager != 0 ? Read<DWORD>(manager + 8) : 0;
        return array != 0 ? array + index * CharacterSize : 0;
    }

    int IndexOf(DWORD character)
    {
        const DWORD first = Character(0);
        if (first == 0 || character < first || (character - first) % CharacterSize != 0)
        {
            return -1;
        }

        const int index = static_cast<int>((character - first) / CharacterSize);
        return index < CharacterCount ? index : -1;
    }

    // Texto sin espacios (para "clave=valor"): los espacios pasan a '_', lo no imprimible se corta.
    std::string Word(DWORD address, size_t maxLength)
    {
        char buffer[64] = {};
        if (maxLength >= sizeof(buffer) || !SafeCopy(buffer, address, maxLength))
        {
            return "?";
        }

        std::string text;
        for (size_t i = 0; i < maxLength && buffer[i] != '\0'; i++)
        {
            const unsigned char c = static_cast<unsigned char>(buffer[i]);
            if (c < 32 || c > 126)
            {
                break;
            }

            text += c == ' ' ? '_' : static_cast<char>(c);
        }

        return text.empty() ? "?" : text;
    }

    const char* KindName(int kind)
    {
        switch (kind)
        {
        case 1: return "player";
        case 2: return "monster";
        case 4: return "npc";
        default: return "other";
        }
    }

    const char* ClassName(int attributeClass)
    {
        static const char* const names[] = { "DW", "DK", "ELF", "MG", "DL", "SUM", "RF", "?" };
        return names[attributeClass & 7];
    }

    int Distance(int x1, int y1, int x2, int y2)
    {
        return (std::max)(abs(x1 - x2), abs(y1 - y2));
    }

    std::string SkillName(int skill)
    {
        const DWORD table = *SkillTable;
        return table != 0 && skill > 0 ? Word(table + skill * 0x58, 32) : "?";
    }

    int SkillAt(int index)
    {
        const DWORD attribute = *CharacterAttribute;
        return attribute != 0 && index >= 0 && index < MaxSkills ? Read<WORD>(attribute + AttrSkills + index * 2) : 0;
    }

    int FindSkillIndex(int skill)
    {
        for (int i = 0; i < MaxSkills; i++)
        {
            if (SkillAt(i) == skill)
            {
                return i;
            }
        }

        return -1;
    }

    // --- Paquetes que arma el juego ---

    using PacketStartType = void(__fastcall*)(void* packet, void* unused, int type, int code);
    PacketStartType g_packetStartOriginal = nullptr;
    volatile LONG g_packetCounts[256] = {};
    int g_lastSkillPacket = 0;

    void __fastcall PacketStartHook(void* packet, void* unused, int type, int code)
    {
        InterlockedIncrement(&g_packetCounts[code & 0xFF]);
        for (const int skillPacket : SkillPackets)
        {
            if ((code & 0xFF) == skillPacket)
            {
                g_lastSkillPacket = skillPacket;
            }
        }

        g_packetStartOriginal(packet, unused, type, code);
    }

    int PacketCount(int code)
    {
        return g_packetCounts[code];
    }

    int SkillPacketCount()
    {
        int total = 0;
        for (const int code : SkillPackets)
        {
            total += PacketCount(code);
        }

        return total;
    }

    // --- Baldosa "bajo el mouse" ---

    using PickTerrainType = char(__cdecl*)(float x, float y, int tileX, int tileY, float scale, int size, char select);
    bool g_pickActive = false;
    int g_pickX = 0;
    int g_pickY = 0;

    char __cdecl PickTerrainHook(float x, float y, int tileX, int tileY, float scale, int size, char select)
    {
        if (!g_pickActive)
        {
            return reinterpret_cast<PickTerrainType>(PickTerrainFunction)(x, y, tileX, tileY, scale, size, select);
        }

        // Lo mismo que deja la busqueda cuando acierta: el punto (centro de la baldosa) y la baldosa.
        CollisionPosition[0] = g_pickX * 100.0f + 50.0f;
        CollisionPosition[1] = g_pickY * 100.0f + 50.0f;
        CollisionPosition[2] = TerrainHeight[g_pickY * 256 + g_pickX];
        *PickedTileX = static_cast<float>(g_pickX);
        *PickedTileY = static_cast<float>(g_pickY);
        return 1;
    }

    // --- Mouse y seleccion durante un comando ---

    void SetFlags(BYTE* flags, bool pressed, bool held)
    {
        flags[0] = pressed ? 1 : 0;
        flags[1] = 0;
        flags[2] = held ? 1 : 0;
    }

    void ReleaseFlags(BYTE* flags)
    {
        flags[0] = 0;
        flags[1] = 1; // como al soltar el boton
        flags[2] = 0;
    }

    // Como si bajo el mouse estuviera el objeto (o nada, con -1).
    void Select(int character)
    {
        *SelectedItem = -1;
        *SelectedNpc = -1;
        *SelectedOperate = -1;
        *SelectedCharacter = character;
    }

    void Pick(int x, int y)
    {
        g_pickActive = g_pickInstalled;
        g_pickX = x;
        g_pickY = y;
    }

    // --- Comando en curso ---

    enum class Kind
    {
        None,
        Walk,
        Attack,
        Skill,
    };

    struct Command
    {
        Kind kind = Kind::None;
        int ticks = 0;
        int timeout = 0;
        int savedMouseX = 0;
        int savedMouseY = 0;
        int packetsAtStart = 0;
        int settleTicks = 0;

        // walk
        int x = 0, y = 0;     // destino
        int wx = 0, wy = 0;   // destino del click actual (mas cerca si no hay camino)
        bool wait = false;
        bool moving = false;  // ya se mando el pedido y se espera que llegue
        int failTicks = 0;
        int stillTicks = 0;
        int pressX = 0, pressY = 0;
        int moves = 0;

        // attack / skill
        int target = -1;
        WORD targetKey = 0;
        bool untilDead = false;
        int skill = 0;
        int skillIndex = 0;
        bool tile = false;
    };

    Command g_command;

    std::string Format(const char* format, ...)
    {
        char buffer[512];
        va_list args;
        va_start(args, format);
        vsprintf_s(buffer, format, args);
        va_end(args);
        return buffer;
    }

    std::string HeroPosition()
    {
        const DWORD hero = *Hero;
        return hero == 0 ? "x=-1 y=-1" : Format("x=%d y=%d", Read<int>(hero + CharTileX), Read<int>(hero + CharTileY));
    }

    // Termina el comando: suelta los botones y deja el mouse y la seleccion en manos del juego.
    std::string Finish(const std::string& result)
    {
        g_pickActive = false;
        if (g_command.kind == Kind::Attack || g_command.kind == Kind::Walk)
        {
            ReleaseFlags(LeftButtonFlags);
        }

        if (g_command.kind == Kind::Skill)
        {
            ReleaseFlags(RightButtonFlags);
        }

        if (g_command.kind == Kind::Attack || g_command.kind == Kind::Skill)
        {
            Select(-1);
            *TargetLock = -1; // que el ataque automatico del juego no siga con el objetivo
        }

        *MouseX = g_command.savedMouseX;
        *MouseY = g_command.savedMouseY;
        g_command.kind = Kind::None;
        return result;
    }

    // Estado del objeto del comando: "killed" muerto, "gone" ya no esta (o es otro), "" sigue.
    const char* TargetEnd(int index, WORD key)
    {
        const DWORD character = Character(index);
        if (character == 0 || Read<BYTE>(character + CharLive) == 0 || Read<WORD>(character + CharKey) != key)
        {
            return "gone";
        }

        return Read<BYTE>(character + CharDead) != 0 ? "killed" : "";
    }

    bool ContinueWalk(std::string& result)
    {
        Command& c = g_command;
        const DWORD hero = *Hero;
        if (hero == 0)
        {
            result = Finish("error: walk: sin personaje");
            return true;
        }

        const int hx = Read<int>(hero + CharTileX);
        const int hy = Read<int>(hero + CharTileY);
        const bool walking = Read<BYTE>(hero + CharMoving) != 0;
        if (hx == c.x && hy == c.y && !walking)
        {
            result = Finish(Format("walk: result=arrived x=%d y=%d moves=%d ticks=%d", hx, hy, c.moves, c.ticks));
            return true;
        }

        if (c.ticks > c.timeout)
        {
            result = Finish(Format("walk: result=timeout x=%d y=%d target=%d,%d moves=%d ticks=%d", hx, hy, c.x, c.y,
                c.moves, c.ticks));
            return true;
        }

        if (c.moving)
        {
            c.stillTicks = walking ? 0 : c.stillTicks + 1;
            if (c.stillTicks < 3)
            {
                return false;
            }

            if (hx == c.pressX && hy == c.pressY)
            {
                result = Finish(Format("walk: result=stuck x=%d y=%d target=%d,%d moves=%d ticks=%d", hx, hy, c.x, c.y,
                    c.moves, c.ticks));
                return true;
            }

            // Llego hasta donde alcanzaba el camino: otro click hacia el destino, como haria el jugador.
            c.moving = false;
            c.wx = c.x;
            c.wy = c.y;
            c.failTicks = 0;
        }

        // Click en el terreno: la baldosa bajo el mouse es la del destino.
        Select(-1);
        Pick(c.wx, c.wy);
        SetFlags(LeftButtonFlags, true, false);
        *MouseX = SafeMouseX;
        *MouseY = SafeMouseY;

        if (PacketCount(PacketWalk) != c.packetsAtStart)
        {
            // El juego mando el pedido de caminar en el paso anterior.
            c.packetsAtStart = PacketCount(PacketWalk);
            c.moves++;
            ReleaseFlags(LeftButtonFlags);
            g_pickActive = false;
            if (!c.wait)
            {
                result = Finish(Format("walk: result=sent x=%d y=%d target=%d,%d ticks=%d", hx, hy, c.wx, c.wy, c.ticks));
                return true;
            }

            c.moving = true;
            c.stillTicks = 0;
            c.pressX = hx;
            c.pressY = hy;
            return false;
        }

        // Si el juego ya acepta clicks y no manda nada, no hay camino: probar mas cerca.
        if (*ClickTicks >= *ClickDelay && ++c.failTicks > 8)
        {
            c.failTicks = 0;
            c.wx = hx + (c.wx - hx) / 2;
            c.wy = hy + (c.wy - hy) / 2;
            if (c.wx == hx && c.wy == hy)
            {
                result = Finish(Format("walk: result=nopath x=%d y=%d target=%d,%d moves=%d ticks=%d", hx, hy, c.x, c.y,
                    c.moves, c.ticks));
                return true;
            }
        }

        return false;
    }

    bool ContinueAttack(std::string& result)
    {
        Command& c = g_command;
        const DWORD hero = *Hero;
        const int hits = PacketCount(PacketHit) - c.packetsAtStart;
        const DWORD character = Character(c.target);
        const std::string name = character != 0 ? Word(character + CharName, 24) : "?";
        if (hero == 0 || Read<BYTE>(hero + CharDead) != 0)
        {
            result = Finish(Format("attack: result=herodead target=%d name=%s hits=%d ticks=%d", c.target, name.c_str(),
                hits, c.ticks));
            return true;
        }

        const char* end = TargetEnd(c.target, c.targetKey);
        if (*end != '\0' || (!c.untilDead && hits > 0) || c.ticks > c.timeout)
        {
            const char* outcome = *end != '\0' ? end : c.ticks > c.timeout ? "timeout" : "hit";
            result = Finish(Format("attack: result=%s target=%d name=%s hits=%d ticks=%d %s", outcome, c.target,
                name.c_str(), hits, c.ticks, HeroPosition().c_str()));
            return true;
        }

        // Click sostenido sobre el monstruo: el juego se acerca y ataca mientras el boton siga apretado.
        Select(c.target);
        Pick(Read<int>(character + CharTileX), Read<int>(character + CharTileY));
        SetFlags(LeftButtonFlags, c.ticks == 0, true);
        *MouseX = SafeMouseX;
        *MouseY = SafeMouseY;
        c.ticks++;
        return false;
    }

    bool ContinueSkill(std::string& result)
    {
        Command& c = g_command;
        const DWORD hero = *Hero;
        if (hero == 0)
        {
            result = Finish("error: skill: sin personaje");
            return true;
        }

        if (SkillPacketCount() != c.packetsAtStart)
        {
            result = Finish(Format("skill: result=sent skill=%d name=%s packet=0x%02X target=%d ticks=%d %s", c.skill,
                SkillName(c.skill).c_str(), g_lastSkillPacket, c.target, c.ticks, HeroPosition().c_str()));
            return true;
        }

        if (c.ticks > c.timeout)
        {
            result = Finish(Format("skill: result=timeout skill=%d name=%s target=%d ticks=%d %s", c.skill,
                SkillName(c.skill).c_str(), c.target, c.ticks, HeroPosition().c_str()));
            return true;
        }

        c.ticks++;
        *MouseX = SafeMouseX;
        *MouseY = SafeMouseY;
        SetFlags(LeftButtonFlags, false, false);
        if (Read<BYTE>(hero + CharMoving) != 0)
        {
            ReleaseFlags(RightButtonFlags); // el juego no lanza habilidades mientras camina
            return false;
        }

        // Si el objetivo esta lejos el juego no lanza la habilidad: cada tanto, un click en su baldosa
        // para acercarse (como haria el jugador) y despues se vuelve a intentar.
        const DWORD target = Character(c.target);
        if (target != 0 && c.ticks % 12 == 0
            && Distance(Read<int>(target + CharTileX), Read<int>(target + CharTileY), Read<int>(hero + CharTileX),
                   Read<int>(hero + CharTileY)) > 1)
        {
            ReleaseFlags(RightButtonFlags);
            Select(-1);
            Pick(Read<int>(target + CharTileX), Read<int>(target + CharTileY));
            SetFlags(LeftButtonFlags, true, false);
            return false;
        }

        // Click derecho sostenido con la habilidad elegida y el objetivo bajo el mouse.
        *reinterpret_cast<BYTE*>(hero + CharCurrentSkill) = static_cast<BYTE>(c.skillIndex);
        Select(c.target);
        if (c.tile)
        {
            Pick(c.x, c.y);
        }

        SetFlags(RightButtonFlags, true, true);
        return false;
    }

    // --- Comandos de consulta ---

    std::string State()
    {
        const DWORD hero = *Hero;
        const DWORD attribute = *CharacterAttribute;
        if (hero == 0 || attribute == 0)
        {
            return Format("state: hero=0 map=%d", *CurrentMap);
        }

        const int skill = SkillAt(Read<BYTE>(hero + CharCurrentSkill));
        const BYTE attributeClass = Read<BYTE>(attribute + AttrClass);
        return Format("state: name=%s map=%d x=%d y=%d hp=%d/%d mp=%d/%d sd=%d/%d ag=%d/%d level=%d class=%s "
                      "classraw=%d skill=%d skillname=%s selected=%d target=%d moving=%d attacking=%d dead=%d "
                      "safezone=%d action=%d",
            Word(hero + CharName, 11).c_str(), *CurrentMap, Read<int>(hero + CharTileX), Read<int>(hero + CharTileY),
            Read<WORD>(attribute + AttrLife), Read<WORD>(attribute + AttrLifeMax), Read<WORD>(attribute + AttrMana),
            Read<WORD>(attribute + AttrManaMax), Read<WORD>(attribute + AttrShield), Read<WORD>(attribute + AttrShieldMax),
            Read<WORD>(attribute + AttrAg), Read<WORD>(attribute + AttrAgMax), Read<WORD>(attribute + AttrLevel),
            ClassName(attributeClass), attributeClass, skill, SkillName(skill).c_str(), *SelectedCharacter,
            *ActionTarget, Read<BYTE>(hero + CharMoving), *TargetLock == 1 && Character(*SelectedCharacter) != 0 ? 1 : 0,
            Read<BYTE>(hero + CharDead), Read<BYTE>(hero + CharSafeZone), Read<short>(hero + CharAction));
    }

    std::string ObjectLine(const char* prefix, int index, int heroX, int heroY)
    {
        const DWORD character = Character(index);
        const int x = Read<int>(character + CharTileX);
        const int y = Read<int>(character + CharTileY);
        const int kind = Read<BYTE>(character + CharKind);
        return Format("%s: index=%d kind=%s kindraw=%d type=%d model=%d key=%d name=%s x=%d y=%d dist=%d live=%d dead=%d "
                      "hp=na",
            prefix, index, KindName(kind), kind, Read<short>(character + CharMonsterType), Read<int>(character + CharModel),
            Read<WORD>(character + CharKey),
            Word(character + CharName, 24).c_str(), x, y, Distance(x, y, heroX, heroY), Read<BYTE>(character + CharLive),
            Read<BYTE>(character + CharDead));
    }

    std::string Objects(int radius)
    {
        const DWORD hero = *Hero;
        if (hero == 0)
        {
            return "objects: count=0 hero=0";
        }

        const int hx = Read<int>(hero + CharTileX);
        const int hy = Read<int>(hero + CharTileY);
        std::vector<std::pair<int, int>> found; // distancia, indice
        for (int i = 0; i < CharacterCount; i++)
        {
            const DWORD character = Character(i);
            if (character == 0 || character == hero || Read<BYTE>(character + CharLive) == 0)
            {
                continue;
            }

            const int distance = Distance(Read<int>(character + CharTileX), Read<int>(character + CharTileY), hx, hy);
            if (distance <= radius)
            {
                found.emplace_back(distance, i);
            }
        }

        std::sort(found.begin(), found.end());
        std::string result = Format("objects: count=%u radius=%d x=%d y=%d", static_cast<unsigned>(found.size()), radius, hx, hy);
        for (const auto& object : found)
        {
            result += "\n" + ObjectLine("object", object.second, hx, hy);
        }

        return result;
    }

    std::string Peek(const char* arguments)
    {
        unsigned long address = 0;
        int length = 16;
        if (sscanf_s(arguments, "%lx %d", &address, &length) < 1 || length <= 0 || length > 256)
        {
            return "error: peek DIRECCION [BYTES<=256]";
        }

        BYTE bytes[256];
        if (!SafeCopy(bytes, address, length))
        {
            return Format("error: peek: no se puede leer 0x%08lX", address);
        }

        std::string result = Format("peek: address=0x%08lX bytes=", address);
        for (int i = 0; i < length; i++)
        {
            result += Format("%02X", bytes[i]);
        }

        return result;
    }

    bool ValidTarget(int index, std::string& error, const char* command)
    {
        const DWORD character = Character(index);
        if (character == 0 || character == *Hero || Read<BYTE>(character + CharLive) == 0)
        {
            error = Format("error: %s: no hay objeto %d", command, index);
            return false;
        }

        if (Read<BYTE>(character + CharDead) != 0)
        {
            error = Format("error: %s: el objeto %d esta muerto", command, index);
            return false;
        }

        return true;
    }

    void Begin(Kind kind, int timeoutTicks)
    {
        g_command = Command{};
        g_command.kind = kind;
        g_command.timeout = timeoutTicks;
        g_command.savedMouseX = *MouseX;
        g_command.savedMouseY = *MouseY;
        g_command.settleTicks = SettleTicks;
        *MouseX = SafeMouseX;
        *MouseY = SafeMouseY;
    }

    void PatchCall(DWORD site, DWORD expectedTarget, const void* hook, bool& ok)
    {
        const BYTE* code = reinterpret_cast<const BYTE*>(site);
        if (code[0] != 0xE8 || site + 5 + *reinterpret_cast<const int*>(code + 1) != expectedTarget)
        {
            g_log("Comandos de juego: la llamada en 0x%08X no coincide.", site);
            ok = false;
            return;
        }

        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(hook) - (site + 5);
        ok = WriteCode(site, call, sizeof(call)) && ok;
    }

    // Engancha el inicio de los paquetes: copia el prologo a un trampolin y salta a PacketStartHook.
    bool HookPacketStart()
    {
        static const BYTE prologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x08 }; // push ebp; mov ebp,esp; sub esp,8
        if (memcmp(reinterpret_cast<const void*>(PacketStartFunction), prologue, sizeof(prologue)) != 0)
        {
            g_log("Comandos de juego: el inicio de los paquetes no coincide.");
            return false;
        }

        BYTE* trampoline = static_cast<BYTE*>(VirtualAlloc(nullptr, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (trampoline == nullptr)
        {
            return false;
        }

        memcpy(trampoline, prologue, sizeof(prologue));
        trampoline[sizeof(prologue)] = 0xE9;
        *reinterpret_cast<DWORD*>(trampoline + sizeof(prologue) + 1) =
            (PacketStartFunction + sizeof(prologue)) - reinterpret_cast<DWORD>(trampoline + sizeof(prologue) + 5);
        g_packetStartOriginal = reinterpret_cast<PacketStartType>(trampoline);

        BYTE jump[6] = { 0xE9, 0, 0, 0, 0, 0x90 };
        *reinterpret_cast<DWORD*>(jump + 1) = reinterpret_cast<DWORD>(&PacketStartHook) - (PacketStartFunction + 5);
        return WriteCode(PacketStartFunction, jump, sizeof(jump));
    }
}

bool InstallGameCommands(LogFunction log)
{
    g_log = log;
    if (!HookPacketStart())
    {
        g_log("Comandos de juego: desactivados.");
        return false;
    }

    bool ok = true;
    PatchCall(PickCallWalk, PickTerrainFunction, &PickTerrainHook, ok);
    PatchCall(PickCallSkillTarget, PickTerrainFunction, &PickTerrainHook, ok);
    g_pickInstalled = ok;
    g_installed = true;
    g_log(ok ? "Comandos de juego listos." : "Comandos de juego listos, sin caminar ni habilidades sobre baldosas.");
    return true;
}

bool StartGameCommand(const char* name, const char* arguments, std::string& result, bool& pending)
{
    pending = false;
    int a = 0, b = 0, c = 0;
    char word[32] = {};
    const int count = sscanf_s(arguments, "%d %d %d", &a, &b, &c);
    if (_stricmp(name, "state") == 0)
    {
        result = State();
    }
    else if (_stricmp(name, "objects") == 0)
    {
        result = Objects(count >= 1 ? a : 15);
    }
    else if (_stricmp(name, "target") == 0)
    {
        if (count < 1 || Character(a) == 0)
        {
            result = "error: target N";
            return true;
        }

        const DWORD hero = *Hero;
        result = ObjectLine("target", a, hero != 0 ? Read<int>(hero + CharTileX) : 0, hero != 0 ? Read<int>(hero + CharTileY) : 0);
    }
    else if (_stricmp(name, "peek") == 0)
    {
        result = Peek(arguments);
    }
    else if (_stricmp(name, "enter") == 0)
    {
        // Seleccion de personaje: elige el lugar N y hace lo mismo que el boton Connect.
        const DWORD character = count >= 1 ? Character(a) : 0;
        if (*Scene != 4 || character == 0 || Read<BYTE>(character + CharLive) == 0)
        {
            result = Format("error: enter N: no hay personaje %d en la seleccion (escena %d)", a, *Scene);
            return true;
        }

        *SelectedHero = a;
        reinterpret_cast<void(__cdecl*)(char)>(EnterGameFunction)(1);
        result = Format("enter: slot=%d name=%s", a, Word(character + CharName, 11).c_str());
    }
    else if (!g_installed)
    {
        result = Format("error: %s: comandos de juego desactivados (ver camera3d.log)", name);
    }
    else if (_stricmp(name, "walk") == 0)
    {
        if (count < 2 || a < 0 || a > 255 || b < 0 || b > 255 || !g_pickInstalled)
        {
            result = "error: walk X Y [wait]";
            return true;
        }

        Begin(Kind::Walk, 60 * TicksPerSecond);
        g_command.x = g_command.wx = a;
        g_command.y = g_command.wy = b;
        g_command.wait = strstr(arguments, "wait") != nullptr;
        g_command.packetsAtStart = PacketCount(PacketWalk);
        pending = true;
    }
    else if (_stricmp(name, "attack") == 0)
    {
        if (count < 1)
        {
            result = "error: attack N [untildead [SEGUNDOS]]";
            return true;
        }

        if (!ValidTarget(a, result, "attack"))
        {
            return true;
        }

        if (*Hero != 0 && Read<BYTE>(*Hero + CharSafeZone) != 0)
        {
            result = "error: attack: el personaje esta en zona segura";
            return true;
        }

        const char* until = strstr(arguments, "untildead");
        int seconds = 10;
        if (until != nullptr && sscanf_s(until, "%31s %d", word, static_cast<unsigned>(sizeof(word)), &seconds) < 2)
        {
            seconds = 60;
        }

        Begin(Kind::Attack, seconds * TicksPerSecond);
        g_command.target = a;
        g_command.targetKey = Read<WORD>(Character(a) + CharKey);
        g_command.untilDead = until != nullptr;
        g_command.packetsAtStart = PacketCount(PacketHit);
        pending = true;
    }
    else if (_stricmp(name, "skill") == 0)
    {
        const int index = count >= 1 ? FindSkillIndex(a) : -1;
        if (count < 1 || a <= 0)
        {
            result = "error: skill S [N | X Y]";
            return true;
        }

        if (index < 0)
        {
            result = Format("error: skill: el personaje no tiene la habilidad %d", a);
            return true;
        }

        if (count == 2 && !ValidTarget(b, result, "skill"))
        {
            return true;
        }

        Begin(Kind::Skill, 8 * TicksPerSecond);
        g_command.skill = a;
        g_command.skillIndex = index;
        g_command.target = count == 2 ? b : -1;
        g_command.tile = count >= 3 && g_pickInstalled;
        g_command.x = b;
        g_command.y = c;
        g_command.packetsAtStart = SkillPacketCount();
        pending = true;
    }
    else if (_stricmp(name, "party") == 0)
    {
        // Invita al jugador N a la party, como el boton Party de la ventana de comandos (C1 40).
        const DWORD character = count >= 1 ? Character(a) : 0;
        if (character == 0 || character == *Hero || Read<BYTE>(character + CharLive) == 0
            || Read<BYTE>(character + CharKind) != 1)
        {
            result = "error: party N: N tiene que ser un jugador";
            return true;
        }

        const WORD key = Read<WORD>(character + CharKey);
        reinterpret_cast<void(__fastcall*)(void*, void*, short)>(PartyRequestFunction)(nullptr, nullptr,
            static_cast<short>(key));
        result = Format("party: key=%d name=%s", key, Word(character + CharName, 11).c_str());
    }
    else
    {
        return false;
    }

    return true;
}

bool ContinueGameCommand(std::string& result)
{
    // Los primeros pasos solo dejan el mouse sobre el mundo: si estaba sobre una ventana o la barra de
    // abajo, la interfaz tarda unos pasos en soltarlo y hasta entonces el juego ignora los clicks.
    if (g_command.kind != Kind::None && g_command.settleTicks > 0)
    {
        g_command.settleTicks--;
        *MouseX = SafeMouseX;
        *MouseY = SafeMouseY;
        return false;
    }

    switch (g_command.kind)
    {
    case Kind::Walk:
        g_command.ticks++;
        return ContinueWalk(result);
    case Kind::Attack:
        return ContinueAttack(result);
    case Kind::Skill:
        return ContinueSkill(result);
    default:
        return true;
    }
}
