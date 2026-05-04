$ErrorActionPreference = "Stop"

$dotnet = Join-Path $env:USERPROFILE "tools\dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    throw "No se encontro el SDK de .NET en $dotnet"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5000"
if (-not $env:ConnectionStrings__Default) {
    throw "Configura ConnectionStrings__Default antes de iniciar la app. Ver Docs\CONFIGURACION_LOCAL.md"
}
$env:Worker__Enabled = "false"
$env:Email__Enabled = "false"
$env:WhatsApp__Enabled = "false"

& $dotnet run --no-build --urls http://localhost:5000
