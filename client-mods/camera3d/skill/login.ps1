# Abre el cliente de prueba y entra al juego con una cuenta de prueba del server de prueba.
# Coordenadas para 1024x768. Las cuentas de prueba de OpenMU usan el nombre como contrasena.
param([string]$Exe = "D:\camera3d\client\main_mod.exe", [string]$Account = "test400", [int]$Slot = 0)
. D:\camera3d\skill\mu.ps1

Get-Process main_mod -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
& D:\camera3d\skill\launch-test.ps1 -Exe $Exe | Out-Null

# Esperar la ventana y la conexion al server de prueba.
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    try { [void](Get-GameWindow); break } catch {}
}
Start-Sleep 6
Assert-TestConnection
Focus-Game
Click-Game 332 300; Start-Sleep 2          # servidor
Click-Game 510 300; Start-Sleep 3          # sub-servidor 1
Assert-TestConnection
Click-Game 533 464; Send-GameText $Account
Click-Game 533 490; Send-GameText $Account
Send-GameKey 0x0D; Start-Sleep 5           # Enter
$slotX = 200 + 160 * $Slot                 # personajes de izquierda a derecha
Click-Game $slotX 530; Start-Sleep 1
Click-Game 918 697; Start-Sleep 8          # Connect
Assert-TestConnection
Save-GameShot D:\camera3d\shots\login.png
