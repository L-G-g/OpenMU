// Herramientas de prueba (muauto.dll): piezas compartidas entre sus partes.
#pragma once

#include "../shared.h"

#include <string>

// Lo que ofrece camera3d.dll (log y ajustes de prueba del dibujo).
const TestHost& Host();

// Carpeta de camera3d.dll y muauto.dll, con la barra final.
const char* ClientFolder();

// Modo de prueba: si camera3d.ini lo pide, redirige toda conexion externa al servidor de prueba.
bool InstallTestMode(LogFunction log);

// Diagnostico (pruebas): anota que cambia el dibujo. Funciona si camera3d instalo los cuadros extra.
bool InstallRenderDiagnostics(LogFunction log);

// Automatizacion (modo de prueba): comandos desde automation\commands.txt. AutomationTick se llama en
// cada paso de logica, despues de que el juego actualiza sus teclas.
bool InstallAutomation(LogFunction log);
void AutomationTick(BYTE* keyStates);

// Comandos de alto nivel sobre el juego (estado, objetos, caminar, atacar, habilidades). Se instalan con
// la automatizacion.
bool InstallGameCommands(LogFunction log);

// Ejecuta un comando de juego. Devuelve false si el nombre no es de un comando de juego. Si el comando
// sigue en los proximos pasos de logica, deja pending en true y el resultado se completa con
// ContinueGameCommand.
bool StartGameCommand(const char* name, const char* arguments, std::string& result, bool& pending);

// Continua el comando de juego en curso en este paso de logica; true cuando termino (con su resultado).
bool ContinueGameCommand(std::string& result);
