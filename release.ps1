# release.ps1 — cut a new WebDavSync release
#
# Usage:
#   .\release.ps1 -Version 0.2.1
#
# What it does:
#   1. Bumps <Version> and <AssemblyVersion> in the .csproj
#   2. Publishes a self-contained single-file exe
#   3. Computes SHA-256 of the output exe
#   4. Updates appcast.json with the new version + sha256
#      (you must fill in downloadUrl and releaseNotesUrl manually after uploading to GitHub Releases)
#   5. Prints a checklist of remaining manual steps

param(
    [Parameter(Mandatory)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$CsprojPath = Join-Path $RepoRoot "Csharp\WebDavSync.Portable.csproj"
$AppcastPath = Join-Path $RepoRoot "appcast.json"
$PublishDir  = Join-Path $RepoRoot "Csharp\publish"
$ExePath     = Join-Path $PublishDir "WebDavSync.exe"

# ── 1. Validate version format ────────────────────────────────────────────────
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in the form MAJOR.MINOR.PATCH (e.g. 0.2.1)"
}

Write-Host ""
Write-Host "==> Releasing v$Version" -ForegroundColor Cyan

# ── 2. Bump version in .csproj ────────────────────────────────────────────────
Write-Host "--> Updating .csproj..."
$csproj = Get-Content $CsprojPath -Raw
$csproj = $csproj -replace '<Version>[^<]+</Version>',         "<Version>$Version</Version>"
$csproj = $csproj -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
Set-Content $CsprojPath $csproj -NoNewline
Write-Host "    Version set to $Version"

# ── 3. Publish ────────────────────────────────────────────────────────────────
Write-Host "--> Publishing..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

dotnet publish "$CsprojPath" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$PublishDir" `
    | Tee-Object -Variable publishOutput

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
}

if (-not (Test-Path $ExePath)) {
    Write-Error "Expected output not found: $ExePath"
}

$sizeMB = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
Write-Host "    Output: $ExePath ($sizeMB MB)"

# ── 4. Compute SHA-256 ────────────────────────────────────────────────────────
Write-Host "--> Computing SHA-256..."
$sha256 = (Get-FileHash $ExePath -Algorithm SHA256).Hash.ToLower()
Write-Host "    SHA-256: $sha256"

# ── 5. Update appcast.json (version + sha256 only; URL left blank for you) ────
Write-Host "--> Updating appcast.json..."
$now = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
$appcast = [ordered]@{
    version          = $Version
    publishedAtUtc   = $now
    downloadUrl      = ""
    sha256           = $sha256
    releaseNotesUrl  = ""
}
$appcast | ConvertTo-Json | Set-Content $AppcastPath
Write-Host "    appcast.json updated (downloadUrl and releaseNotesUrl are blank — fill them in after upload)"

# ── 6. Summary ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "==> Done. Manual steps remaining:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Upload the exe to GitHub Releases as tag v$Version :"
Write-Host "       $ExePath"
Write-Host ""
Write-Host "  2. Fill in appcast.json:"
Write-Host "       downloadUrl    — direct link to the uploaded .exe"
Write-Host "       releaseNotesUrl — GitHub release page URL"
Write-Host ""
Write-Host "  3. Commit and push:"
Write-Host "       git add Csharp\WebDavSync.Portable.csproj appcast.json"
Write-Host "       git commit -m `"release v$Version`""
Write-Host "       git tag v$Version"
Write-Host "       git push && git push --tags"
Write-Host ""
Write-Host "  SHA-256 (for your records): $sha256" -ForegroundColor Green
Write-Host ""
