$ErrorActionPreference = "Stop"

$nodeDir = Join-Path $env:USERPROFILE "tools\node-v24.15.0-win-x64"
$npm = Join-Path $nodeDir "npm.cmd"

if (-not (Test-Path $npm)) {
    throw "No se encontro npm portable en $npm"
}

$env:Path = "$nodeDir;$env:Path"
Push-Location (Join-Path $PSScriptRoot "..\ClientApp")
try {
    & $npm run dev -- --host localhost
}
finally {
    Pop-Location
}
