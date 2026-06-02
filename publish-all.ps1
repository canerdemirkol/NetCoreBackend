<#
.SYNOPSIS
    Bütün Core.* class library'lerini NuGet.org'a yayımlar (Core.Test hariç).

.DESCRIPTION
    1. Release config'le solution'ı build eder.
    2. Bütün paketlenebilir projeleri ./nupkgs altında pack eder.
    3. Core.Test*'i filtreler (paketlenmiyor zaten ama defansif filter).
    4. Her .nupkg'yi NuGet.org'a tek tek push eder.
    --skip-duplicate kullanır → aynı version varsa hata değil, atlanır.

.PARAMETER ApiKey
    NuGet.org API key. Verilmezse $env:NUGET_API_KEY okunur.

.PARAMETER Source
    Push target feed. Default: https://api.nuget.org/v3/index.json
    Lokal test için: C:\local-nuget-feed gibi bir klasör verebilirsin.

.PARAMETER DryRun
    Sadece pack eder, push ETMEZ. NuGet'e bir şey gönderilmeden önce paketleri
    incelemek için.

.EXAMPLE
    $env:NUGET_API_KEY = "oy2x..."
    ./publish-all.ps1

.EXAMPLE
    # Lokal feed'e yayımla (test için)
    ./publish-all.ps1 -Source "C:\local-nuget-feed"

.EXAMPLE
    # Sadece pack, push yok — paketleri kontrol et
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
        throw "API key gerekli. Ya -ApiKey parametresi ver ya da `$env:NUGET_API_KEY set et."
    }
}

# 1. Clean output
if (Test-Path $OutputDir) {
    Write-Host "Eski paketleri temizle: $OutputDir" -ForegroundColor DarkGray
    Remove-Item $OutputDir -Recurse -Force
}

# 2. Build (tüm solution release config)
Write-Host "`n=== BUILD ($Configuration) ===" -ForegroundColor Cyan
dotnet build NetCoreBackend.sln -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build başarısız" }

# 3. Pack (tüm packable projeler → ./nupkgs)
#    Core.Test'in csproj'ında <IsPackable>false</IsPackable> var → dotnet pack
#    onu otomatik atlar; ayrıca aşağıda defansif filter de var.
Write-Host "`n=== PACK ===" -ForegroundColor Cyan
dotnet pack NetCoreBackend.sln -c $Configuration -o $OutputDir --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw "Pack başarısız" }

# 4. Paketleri listele + Core.Test'i filtrele (defansif)
$packages = Get-ChildItem $OutputDir -Filter "*.nupkg" |
    Where-Object { $_.Name -notmatch "Core\.Test\." } |
    Sort-Object Name

# Symbol package'ları (.snupkg) push sırasında dotnet otomatik handle ediyor;
# aynı klasörde .nupkg ile .snupkg yan yana olduğunda push komutu ikisini birden yollar.
$snupkgCount = (Get-ChildItem $OutputDir -Filter "*.snupkg" | Where-Object { $_.Name -notmatch "Core\.Test\." }).Count

Write-Host "`nPaketlenen $($packages.Count) library + $snupkgCount symbol package:" -ForegroundColor Green
$packages | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor DarkGray }

if ($DryRun) {
    Write-Host "`n[DRY RUN] Push atlandı. Paketler $OutputDir altında." -ForegroundColor Yellow
    exit 0
}

# 5. Push — tek tek, sıralı (paralel için ForEach-Object -Parallel ekleyebilirsin,
#    ama 28 paketin push'u zaten ~1-2 dk; serial debug için daha kolay)
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
        # Lokal feed: API key gerekmez
        dotnet nuget push $pkg.FullName `
            --source $Source `
            --skip-duplicate
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Push BAŞARISIZ: $($pkg.Name)"
        $failed += $pkg.Name
    }
}

# 6. Özet
Write-Host "`n=== ÖZET ===" -ForegroundColor Cyan
$pushed = $packages.Count - $failed.Count
Write-Host "Başarılı push: $pushed / $($packages.Count)" -ForegroundColor Green
if ($failed.Count -gt 0) {
    Write-Host "Başarısız: $($failed.Count)" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  ✗ $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nNuGet feed'de görünmesi 5-15 dakika sürebilir (indexing)." -ForegroundColor DarkGray
