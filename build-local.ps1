$ErrorActionPreference = "Stop"
$env:PATH += ";$env:USERPROFILE\.cargo\bin"

Set-Location $PSScriptRoot

Write-Host "Stopping running Backup Sync Tool instance..."
$existing = Get-Process -Name "backupsynctool" -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Stop-Process -Force
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 250
        $existing = Get-Process -Name "backupsynctool" -ErrorAction SilentlyContinue
    } while ($existing -and (Get-Date) -lt $deadline)

    if ($existing) {
        Write-Error "Could not stop backupsynctool.exe before copying the new build."
        exit 1
    }
}

Write-Host "Building release exe..."
cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-Error "cargo build failed"
    exit 1
}

Copy-Item "target\release\backupsynctool.exe" "backupsynctool.exe" -Force
Write-Host "Copied target\release\backupsynctool.exe to repo root."

Write-Host "Launching backupsynctool.exe from repo root..."
Start-Process "backupsynctool.exe"
Start-Sleep -Milliseconds 500

$running = Get-Process -Name "backupsynctool" -ErrorAction SilentlyContinue
if (-not $running) {
    Write-Error "Build succeeded, but backupsynctool.exe is not running."
    exit 1
}

Write-Host "Done. Build succeeded with 0 errors and Backup Sync Tool is running."
