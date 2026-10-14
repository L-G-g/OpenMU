# Arma una grilla con capturas seguidas (RUTA_1.bmp ... RUTA_N.bmp), recortando una zona de cada una.
param([string]$Base, [int]$Count, [int]$X, [int]$Y, [int]$W, [int]$H, [int]$Columns = 4, [string]$Out)
Add-Type -AssemblyName System.Drawing
$rows = [Math]::Ceiling($Count / $Columns)
$all = New-Object System.Drawing.Bitmap ($W * $Columns), ($H * $rows)
$g = [System.Drawing.Graphics]::FromImage($all)
for ($k = 1; $k -le $Count; $k++) {
    $image = [System.Drawing.Image]::FromFile("$($Base)_$k.bmp")
    $column = ($k - 1) % $Columns
    $row = [Math]::Floor(($k - 1) / $Columns)
    $target = New-Object System.Drawing.Rectangle ($column * $W), ($row * $H), $W, $H
    $source = New-Object System.Drawing.Rectangle $X, $Y, $W, $H
    $g.DrawImage($image, $target, $source, [System.Drawing.GraphicsUnit]::Pixel)
    $image.Dispose()
}
$all.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$Out
