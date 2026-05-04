$ErrorActionPreference = "Stop"

$mariaDir = Join-Path $env:USERPROFILE "tools\mariadb-11.4.7-winx64"
$dataDir = Join-Path $env:USERPROFILE "tools\mariadb-data-reclamos"
$server = Join-Path $mariaDir "bin\mariadbd.exe"
$config = Join-Path $dataDir "my.ini"

if (-not (Test-Path $server)) {
    throw "No se encontro MariaDB en $mariaDir"
}

if (-not (Test-Path $config)) {
    throw "No se encontro la configuracion de datos en $config"
}

$running = Get-NetTCPConnection -LocalPort 3307 -State Listen -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "MariaDB ya esta escuchando en el puerto 3307."
    return
}

Start-Process -FilePath $server -ArgumentList @("--defaults-file=$config", "--console") -WindowStyle Hidden
Start-Sleep -Seconds 3

Write-Host "MariaDB iniciado en 127.0.0.1:3307."
