$ErrorActionPreference = "Stop"
$env:PATH += ";$env:USERPROFILE\.cargo\bin"

Set-Location $PSScriptRoot

# Bump patch version
$toml = Get-Content "Cargo.toml" -Raw
$m = [regex]::Match($toml, 'version\s*=\s*"(\d+)\.(\d+)\.(\d+)"')
$major = $m.Groups[1].Value
$minor = $m.Groups[2].Value
$patch = [int]$m.Groups[3].Value + 1
$newVersion = "$major.$minor.$patch"
$toml = $toml -replace '(?m)^version\s*=\s*"\d+\.\d+\.\d+"', "version = `"$newVersion`""
Set-Content "Cargo.toml" $toml -NoNewline
Write-Host "Bumped version to $newVersion"

# Kill running instance
Get-Process backupsynctool -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Build
cargo build --release
if ($LASTEXITCODE -ne 0) { Write-Error "cargo build failed"; exit 1 }
Copy-Item "target\release\backupsynctool.exe" ".\backupsynctool.exe" -Force
Write-Host "Built backupsynctool.exe"

# Commit everything, tag, push
$v = "v$newVersion"
git add -A
git commit -m "release: $v"
git tag -f $v
git push origin main --follow-tags --force-with-lease
Write-Host "Done. Pushed $v — GitHub Actions will create the release."
