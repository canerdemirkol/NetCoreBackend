<#
.SYNOPSIS
    Publishes every Core.* class library to NuGet.org (except Core.Test).

.DESCRIPTION
    1. Builds the solution in Release configuration.
    2. Packs every packable project into ./nupkgs.
    3. Filters out Core.Test* (it is not packable anyway, but this is a defensive filter).
    4. Pushes each .nupkg to NuGet.org one by one.
    Uses --skip-duplicate → an already-published version is skipped, not treated as an error.

    NOTE: packing targets the whole solution (dotnet pack NetCoreBackend.sln), so any
    project added to the solution (e.g. Core.Mediation) is packed and pushed automatically —
    there is no per-package list to maintain here.

.PARAMETER ApiKey
    NuGet.org API key. If omitted, $env:NUGET_API_KEY is read.

.PARAMETER Source
    Push target feed. Default: https://api.nuget.org/v3/index.json
    For local testing you can pass a folder such as C:\local-nuget-feed.

.PARAMETER DryRun
    Packs only, does NOT push. Use it to inspect the packages before sending anything to NuGet.

.EXAMPLE
    $env:NUGET_API_KEY = "oy2x..."
    ./publish-all.ps1

.EXAMPLE
    # Publish to a local feed (for testing)
    ./publish-all.ps1 -Source "C:\local-nuget-feed"

.EXAMPLE
    # Pack only, no push — inspect the packages
    ./publish-all.ps1 -DryRun
#>

param(
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./nupkgs",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# 0. Sanity check
if (-not $DryRun -and -not $Source.StartsWith("C:") -and -not $Source.StartsWith("/")) {
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "API key required. Either pass -ApiKey or set `$env:NUGET_API_KEY."
    }
}

# 1. Clean output
if (Test-Path $OutputDir) {
    Write-Host "Cleaning old packages: $OutputDir" -ForegroundColor DarkGray
    Remove-Item $OutputDir -Recurse -Force
}

# 2. Build (whole solution, Release configuration)
Write-Host "`n=== BUILD ($Configuration) ===" -ForegroundColor Cyan
dotnet build NetCoreBackend.sln -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 3. Pack (all packable projects → ./nupkgs)
#    Core.Test has <IsPackable>false</IsPackable> in its csproj, so dotnet pack skips it
#    automatically; the defensive filter below is a second guard.
Write-Host "`n=== PACK ===" -ForegroundColor Cyan
dotnet pack NetCoreBackend.sln -c $Configuration -o $OutputDir --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

# 4. List packages + filter out Core.Test (defensive)
$packages = Get-ChildItem $OutputDir -Filter "*.nupkg" |
    Where-Object { $_.Name -notmatch "Core\.Test\." } |
    Sort-Object Name

# Symbol packages (.snupkg) are handled automatically by dotnet during push;
# when a .nupkg and its .snupkg sit side by side, the push command sends both.
$snupkgCount = (Get-ChildItem $OutputDir -Filter "*.snupkg" | Where-Object { $_.Name -notmatch "Core\.Test\." }).Count

Write-Host "`nPacked $($packages.Count) libraries + $snupkgCount symbol packages:" -ForegroundColor Green
$packages | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor DarkGray }

if ($DryRun) {
    Write-Host "`n[DRY RUN] Push skipped. Packages are under $OutputDir." -ForegroundColor Yellow
    exit 0
}

# 5. Push — one by one, sequentially (you could add ForEach-Object -Parallel,
#    but pushing all packages takes ~1-2 min; serial is easier to debug)
Write-Host "`n=== PUSH → $Source ===" -ForegroundColor Cyan
$failed = @()
foreach ($pkg in $packages) {
    Write-Host "  → $($pkg.Name)" -ForegroundColor Cyan
    if ($ApiKey) {
        dotnet nuget push $pkg.FullName `
            --api-key $ApiKey `
            --source $Source `
            --skip-duplicate
    } else {
        # Local feed: no API key needed
        dotnet nuget push $pkg.FullName `
            --source $Source `
            --skip-duplicate
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Push FAILED: $($pkg.Name)"
        $failed += $pkg.Name
    }
}

# 6. Summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
$pushed = $packages.Count - $failed.Count
Write-Host "Pushed successfully: $pushed / $($packages.Count)" -ForegroundColor Green
if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed.Count)" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  ✗ $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nIt can take 5-15 minutes to appear on the NuGet feed (indexing)." -ForegroundColor DarkGray
