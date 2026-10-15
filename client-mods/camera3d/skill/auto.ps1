# Control del cliente de prueba por automatizacion interna (muauto.dll junto a camera3d.dll, modo de prueba).
# Funciona con la PC bloqueada y sin robar el foco. Ver src/muauto/automation.cpp para la lista de comandos.

$script:AutoFolder = "D:\camera3d\client\automation"

# Ejecuta comandos (uno por linea) y devuelve el resultado de cada uno.
function Invoke-Mu([string[]]$Commands, [int]$TimeoutSeconds = 60) {
    $done = Join-Path $script:AutoFolder "done.txt"
    Remove-Item $done -ErrorAction SilentlyContinue
    $tmp = Join-Path $script:AutoFolder "commands.tmp"
    Set-Content -Path $tmp -Value $Commands -Encoding ASCII
    Move-Item $tmp (Join-Path $script:AutoFolder "commands.txt") -Force
    $limit = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not (Test-Path $done)) {
        if ((Get-Date) -gt $limit) { throw "El cliente no respondio a los comandos" }
        if (-not (Get-Process -Id ([int](Get-Content D:\camera3d\testpid.txt)) -ErrorAction SilentlyContinue)) { throw "El cliente se cerro" }
        Start-Sleep -Milliseconds 100
    }
    Start-Sleep -Milliseconds 50
    $result = Get-Content $done
    Remove-Item $done -ErrorAction SilentlyContinue
    return $result
}

# Captura del juego en PNG (se lee directo de OpenGL).
function Get-MuShot([string]$Png) {
    $bmp = [IO.Path]::ChangeExtension($Png, ".bmp")
    Remove-Item $bmp -ErrorAction SilentlyContinue
    Invoke-Mu @("shot $bmp", "wait 2") | Out-Null
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($bmp)
    $image.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png)
    $image.Dispose()
    Remove-Item $bmp
    return $Png
}

# Conexiones del cliente: todas tienen que ser locales.
function Assert-MuLocal {
    $testPid = [int](Get-Content D:\camera3d\testpid.txt)
    $bad = Get-NetTCPConnection -OwningProcess $testPid -ErrorAction SilentlyContinue |
        Where-Object { $_.RemotePort -ne 0 -and -not $_.RemoteAddress.StartsWith("127.") }
    if ($bad) { Stop-Process -Id $testPid -Force; throw "Conexion no local; cliente cerrado" }
}

# --- Comandos de juego (muauto.dll, gamecommands.cpp) ---
# Cada resultado es una linea "nombre: clave=valor ..."; se devuelve como objeto con esas propiedades.

function ConvertFrom-MuLine([string]$Line) {
    $name, $rest = $Line -split ': ', 2
    $result = [ordered]@{ line = $name }
    if ($name -eq 'error') { $result.error = $rest; return [pscustomobject]$result }
    foreach ($pair in ($rest -split ' ')) {
        $key, $value = $pair -split '=', 2
        if ($null -eq $value) { continue }
        $number = 0
        $result[$key] = if ([int]::TryParse($value, [ref]$number)) { $number } else { $value }
    }
    return [pscustomobject]$result
}

function Invoke-MuGame([string]$Command, [int]$TimeoutSeconds = 60) {
    $lines = @(Invoke-Mu @($Command) -TimeoutSeconds $TimeoutSeconds)
    return $lines | Where-Object { $_ } | ForEach-Object { ConvertFrom-MuLine $_ }
}

# Mapa, baldosa, vida, mana, SD, AG, nivel, clase, habilidad elegida, objetivo.
function Get-MuState { Invoke-MuGame "state" }

# Objetos vivos cerca (monstruos, NPCs, jugadores), del mas cercano al mas lejano.
function Get-MuObjects([int]$Radius = 15, [string]$Kind) {
    $objects = Invoke-MuGame "objects $Radius" | Where-Object { $_.line -eq 'object' }
    if ($Kind) { $objects = $objects | Where-Object { $_.kind -eq $Kind } }
    return $objects
}

# Estado de un objeto por indice.
function Get-MuTarget([int]$Index) { Invoke-MuGame "target $Index" }

# Caminar a una baldosa. -Wait: hasta llegar, trabarse o 60 s.
function Invoke-MuWalk([int]$X, [int]$Y, [switch]$Wait) {
    $command = "walk $X $Y" + $(if ($Wait) { " wait" } else { "" })
    Invoke-MuGame $command -TimeoutSeconds 75
}

# Ataque normal al objeto. -UntilDead: hasta que muere, desaparece o pasan -Seconds.
function Invoke-MuAttack([int]$Index, [switch]$UntilDead, [int]$Seconds = 60) {
    $command = "attack $Index" + $(if ($UntilDead) { " untildead $Seconds" } else { "" })
    Invoke-MuGame $command -TimeoutSeconds ($Seconds + 15)
}

# Habilidad por numero: sobre un objeto (-Target), una baldosa (-X -Y) o uno mismo (sin nada).
function Invoke-MuSkill([int]$Skill, [int]$Target = -1, [int]$X = -1, [int]$Y = -1) {
    $command = "skill $Skill"
    if ($Target -ge 0) { $command += " $Target" } elseif ($X -ge 0) { $command += " $X $Y" }
    Invoke-MuGame $command -TimeoutSeconds 20
}

# El monstruo vivo mas cercano (o $null).
function Get-MuNearestMonster([int]$Radius = 15) {
    Get-MuObjects $Radius | Where-Object { $_.kind -eq 'monster' -and $_.dead -eq 0 } | Select-Object -First 1
}
