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
mkdir -p /root/backups

if [ -f "$RemotePath/appsettings.json" ]; then
  cp "$RemotePath/appsettings.json" /tmp/appsettings.seguros-alonzo.keep
fi

if command -v mysqldump >/dev/null 2>&1; then
  backup_stamp=`$(date -u +%Y%m%d%H%M%S)
  mysqldump --single-transaction --quick reclamos_auto | gzip > "/root/backups/reclamos_auto-predeploy-`$backup_stamp.sql.gz"
fi

# Keep runtime data generated on the server. These folders contain uploaded
# documents, company branding, and ASP.NET data-protection keys.
# -maxdepth 1 ensures we only delete top-level items; storage/ and Uploads/ subtrees are preserved intact.
find "$RemotePath" -mindepth 1 -maxdepth 1 \
  ! -name appsettings.json \
  ! -name storage \
  ! -name Uploads \
  ! -name .aspnet \
  -exec rm -rf {} +
tar -xzf "$remoteArchive" -C "$RemotePath"

if [ -f /tmp/appsettings.seguros-alonzo.keep ]; then
  mv /tmp/appsettings.seguros-alonzo.keep "$RemotePath/appsettings.json"
fi

mkdir -p "$RemotePath/storage" "$RemotePath/Uploads" "$RemotePath/.aspnet"
chown -R www-data:www-data "$RemotePath"
find "$RemotePath" -type d -exec chmod 755 {} +
find "$RemotePath" -type f -exec chmod 644 {} +
chmod 755 "$RemotePath/ReclamosWhatsApp.exe" 2>/dev/null || true
systemctl restart "$ServiceName"
sleep 3
systemctl is-active "$ServiceName"
"@

$remoteScript | ssh $HostAlias "tr -d '\015' | bash -s"

Write-Host "Done. Site should be live at https://corredurialonzo.com/"
