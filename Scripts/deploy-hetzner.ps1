param(
    [string]$HostAlias = "seguros-alonzo",
    [string]$RemotePath = "/opt/seguros-alonzo",
    [string]$ServiceName = "seguros-alonzo"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $root "publish"
$stagingDir = Join-Path $root ".deploy-staging"
$archivePath = Join-Path $root "seguros-alonzo-deploy.tar.gz"
$remoteArchive = "/tmp/seguros-alonzo-deploy.tar.gz"

function Remove-SafeDirectory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if (-not $resolved.StartsWith($root.Path, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the repo: $resolved"
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
}

Write-Host "Building React app..."
npm --prefix (Join-Path $root "ClientApp") run build

Write-Host "Publishing .NET app..."
Remove-SafeDirectory $publishDir
dotnet publish (Join-Path $root "ReclamosWhatsApp.csproj") -c Release -o $publishDir

Write-Host "Preparing deploy archive..."
Remove-SafeDirectory $stagingDir
New-Item -ItemType Directory -Path $stagingDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $stagingDir -Recurse -Force

$localAppSettings = Join-Path $stagingDir "appsettings.json"
if (Test-Path -LiteralPath $localAppSettings) {
    Remove-Item -LiteralPath $localAppSettings -Force
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

tar -czf $archivePath -C $stagingDir .
Remove-SafeDirectory $stagingDir

Write-Host "Uploading archive to $HostAlias..."
scp $archivePath "${HostAlias}:$remoteArchive"

Write-Host "Deploying on server..."
$remoteScript = @"
set -euo pipefail
mkdir -p "$RemotePath"

if [ -f "$RemotePath/appsettings.json" ]; then
  cp "$RemotePath/appsettings.json" /tmp/appsettings.seguros-alonzo.keep
fi

find "$RemotePath" -mindepth 1 ! -name appsettings.json -exec rm -rf {} +
tar -xzf "$remoteArchive" -C "$RemotePath"

if [ -f /tmp/appsettings.seguros-alonzo.keep ]; then
  mv /tmp/appsettings.seguros-alonzo.keep "$RemotePath/appsettings.json"
fi

mkdir -p "$RemotePath/storage" "$RemotePath/Uploads"
chown -R www-data:www-data "$RemotePath"
systemctl restart "$ServiceName"
sleep 3
systemctl is-active "$ServiceName"
"@

$remoteScript | ssh $HostAlias "tr -d '\015' | bash -s"

Write-Host "Done. Site should be live at https://corredurialonzo.com/"
