# Exporta de la base del servidor lo que usa el simulador: tasa de exp, config del plugin de reset
# y del Monster Attribute Scaler. Solo lee; no cambia nada.
#
# Uso (en la PC del servidor, con Docker andando):
#   powershell -ExecutionPolicy Bypass -File tools\simulador-progreso\exportar-config.ps1
# Deja server.json al lado de este script. Después:
#   python tools\simulador-progreso\simulador.py --server tools\simulador-progreso\server.json

param(
    [string]$Contenedor = "database",
    [string]$Salida = (Join-Path $PSScriptRoot "server.json")
)

$consulta = @'
SELECT json_build_object(
  'exp_config', (SELECT "ExperienceRate" FROM config."GameConfiguration" LIMIT 1),
  'nivel_maximo', (SELECT "MaximumLevel" FROM config."GameConfiguration" LIMIT 1),
  'servidores', (SELECT json_agg(json_build_object('servidor', "ServerID", 'exp', "ExperienceRate")) FROM config."GameServerDefinition"),
  'plugins', (SELECT json_agg(json_build_object('tipo', "TypeId", 'activo', "IsActive", 'config', "CustomConfiguration"))
              FROM config."PlugInConfiguration"
              WHERE "TypeId" IN ('6a9d585d-79d7-4674-b6ea-7e87392fa501', '59e5b45f-0a3d-4baf-8b6c-9e1d2f3a4b5c'))
);
'@

$json = $consulta | docker exec -i $Contenedor psql -U postgres -d openmu -t -A
if ($LASTEXITCODE -ne 0) { throw "No se pudo leer la base (contenedor '$Contenedor')." }
[System.IO.File]::WriteAllText($Salida, ($json -join "`n"), (New-Object System.Text.UTF8Encoding $false))
Write-Host "Listo: $Salida"
