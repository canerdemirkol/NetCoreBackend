# NuGet Publishing Guide

Bu repository içindeki **28 class library**'yi (Core.Test hariç) NuGet.org'a yayımlama rehberi.

> Kısa yol: API key al → `$env:NUGET_API_KEY = "..."` → `./publish-all.ps1` → bitti.

---

## 1. Önkoşullar

### 1.1 NuGet.org hesabı + API key

```
1. https://www.nuget.org → "Sign in with Microsoft"
2. Profil → "API Keys" → "Create"
     - Key Name:     net-core-backend-publish
     - Glob Pattern: NetCoreBackend.NArchitecture.*    ← scope'u kısıtla (full-access değil)
     - Scopes:       ✓ Push  ✓ Push new packages and package versions
     - Expires:      365 days
3. "Create" → key'i bir kez kopyala (bir daha gösterilmez)
```

> **Asla** API key'i `git`'e commit etme. `.env`, plain config dosyası, repo'ya yazılı bir not — hiçbiri olmaz. Production CI/CD için GitHub Secrets / Azure Key Vault gibi secret store kullan.

### 1.2 .NET SDK

`net10.0` build hedefi olduğu için **.NET 10 SDK** gerekir. `dotnet --version` ile doğrula.

### 1.3 PowerShell

`publish-all.ps1` Windows PowerShell 5.1 ile de PowerShell 7+ ile de çalışır. Standart Windows kurulumunda zaten 5.1 mevcut — ekstra bir şey yapmana gerek yok.

---

## 2. Hızlı kullanım

### 2.1 NuGet.org'a yayımla

```powershell
cd "C:\Users\caner.demirkol\Desktop\New folder\NetCoreBackend"

$env:NUGET_API_KEY = "oy2x..."   # ← Adım 1.1'de kopyaladığın key

./publish-all.ps1
```

Beklenen çıktı:

```
=== BUILD (Release) ===
Build succeeded.

=== PACK ===
Paketlenen 28 library + 28 symbol package:
  NetCoreBackend.NArchitecture.Core.Application.1.0.0.nupkg
  NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.1.0.0.nupkg
  ... (28 satır)

=== PUSH → https://api.nuget.org/v3/index.json ===
  → NetCoreBackend.NArchitecture.Core.Application.1.0.0.nupkg
  → ... (28 push)

=== ÖZET ===
Başarılı push: 28 / 28
NuGet feed'de görünmesi 5-15 dakika sürebilir (indexing).
```

### 2.2 Sadece pack et, push etme (paketleri incelemek için)

```powershell
./publish-all.ps1 -DryRun
```

Paketler `./nupkgs/` altında oluşur, hiçbir yere gönderilmez. NuGet'e gönderilen şeyi `.nupkg`'yi WinRAR/7-Zip'le açıp `.nuspec`'i inceleyerek doğrulayabilirsin.

### 2.3 Lokal feed'e yayımla (NuGet.org'a gitmeden test et)

NuGet.org **irreversible**: push edilen bir version silinemez, sadece unlist edilebilir. Önce kendi diskinde test etmek istersen:

```powershell
# 1. Lokal feed klasörü
mkdir C:\local-nuget-feed -Force

# 2. publish-all.ps1'i lokal feed'e yönlendir
./publish-all.ps1 -Source "C:\local-nuget-feed"

# 3. Test consumer projesi oluştur ve paketi tüket
mkdir C:\test-consumer; cd C:\test-consumer
dotnet new webapi
dotnet nuget add source "C:\local-nuget-feed" --name local-test
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox
dotnet build    # restore + compile çalışıyor mu?
```

Lokal'de takılan bir şey yoksa `./publish-all.ps1` (API key set'liyken) ile NuGet.org'a geç.

---

## 3. publish-all.ps1 parametreleri

| Parametre | Default | Açıklama |
|---|---|---|
| `-ApiKey` | `$env:NUGET_API_KEY` | Verilmezse environment variable okunur. Lokal feed'de gerekmez. |
| `-Source` | `https://api.nuget.org/v3/index.json` | Hedef feed URL'i. Lokal klasör de verebilirsin. |
| `-Configuration` | `Release` | Build configuration. |
| `-OutputDir` | `./nupkgs` | Pack output klasörü. |
| `-DryRun` | `false` | Push'u atlar, sadece pack yapar. |

---

## 4. Yapılan kurulum (referans)

Bu repo `publish-all.ps1`'in çalışması için aşağıdaki dosyalarla hazırlandı:

### 4.1 `Directory.Build.props`

Tüm csproj'lara otomatik uygulanan ortak metadata:

```xml
<Project>
  <PropertyGroup>
    <Authors>Caner Demirkol, Contributors</Authors>
    <Copyright>© $([System.DateTime]::Now.Year) Caner Demirkol</Copyright>
    <Product>NetCoreBackend NArchitecture</Product>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <Deterministic>true</Deterministic>
  </PropertyGroup>

  <ItemGroup Condition="Exists('$(MSBuildProjectDirectory)\README.md')">
    <None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

Her library kendi klasöründeki `README.md`'sini paketin köküne kopyalar → NuGet.org sayfasında README görünür.

### 4.2 `Core.Test/Core.Test.csproj`

```xml
<IsPackable>false</IsPackable>
<GeneratePackageOnBuild>false</GeneratePackageOnBuild>
```

Core.Test framework'ün kendi xUnit regression suite'i + tüketici test helper'larını içerir; paketlenmez. (Helper'ları NuGet'ten dağıtmak istenirse `Core.TestKit` adıyla ayrı bir proje önerilir.)

### 4.3 `.gitignore`

```
nupkgs/
```

Pack output git'e girmesin.

---

## 5. Yayımlanan paketler (28 adet)

### Kademe 1 — sıfır internal dependency (paralel push'lanabilir)

```
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction
NetCoreBackend.NArchitecture.Core.Localization.Abstraction
NetCoreBackend.NArchitecture.Core.Mailing
NetCoreBackend.NArchitecture.Core.Translation.Abstraction
NetCoreBackend.NArchitecture.Core.Persistence
```

### Kademe 2 — Kademe 1'e bağımlı

```
NetCoreBackend.NArchitecture.Core.MultiTenancy
NetCoreBackend.NArchitecture.Core.Security
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.SeriLog
NetCoreBackend.NArchitecture.Core.Mailing.MailKit
NetCoreBackend.NArchitecture.Core.ElasticSearch
NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate
NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml
NetCoreBackend.NArchitecture.Core.Localization.Translation
NetCoreBackend.NArchitecture.Core.Outbox
```

### Kademe 3 — DI extension'ları ve adapter'lar (Kademe 1+2'ye bağımlı)

```
NetCoreBackend.NArchitecture.Core.Application
NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection
NetCoreBackend.NArchitecture.Core.Persistence.WebApi
NetCoreBackend.NArchitecture.Core.Security.DependencyInjection
NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger
NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection
NetCoreBackend.NArchitecture.Core.Localization.WebApi
NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File
NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection
```

`publish-all.ps1` kademe sırasına dikkat etmez (sıralı serial push); ilk yayım sonrasında restore çakışması olmaz çünkü her push tamamlanınca NuGet feed bir sonraki paketin dependency'sini hemen sunabilir.

---

## 6. Versiyon yönetimi

### 6.1 Mevcut versiyon

Tüm csproj'larda `<Version>1.0.0</Version>`. Aynı version'u tekrar push etmeye çalışmak `--skip-duplicate` flag'i sayesinde sessiz no-op olur (hata değil).

### 6.2 Yeni yayım için bump

Tek tek 28 csproj güncellemek yerine `Directory.Build.props`'ta merkezi version tut:

```xml
<PropertyGroup>
  <Version>1.1.0</Version>      <!-- tek noktada -->
  ...
</PropertyGroup>
```

Her csproj'daki `<Version>1.0.0</Version>` satırını sil → Directory.Build.props'taki değer otomatik geçerli olur. (Şu an csproj'lar her birinde 1.0.0 var; ilk version bump'tan sonra bu konsolidasyon önerilir.)

### 6.3 SemVer kuralı

```
1.0.0          ← ilk stabil yayım
1.0.1          ← bug fix (R5 doc gibi)
1.1.0          ← yeni feature (Outbox eklendi gibi)
2.0.0          ← breaking change (IElasticSearch API'sini değiştirdik gibi)
1.1.0-beta.1   ← test için pre-release (NuGet.org "Show prerelease" ile görünür)
```

---

## 7. Troubleshooting

### "publish-all.ps1 cannot be loaded because running scripts is disabled on this system"

Windows'ta PowerShell ExecutionPolicy default olarak imzasız script'leri reddeder. **Sadece o terminal session'ı için** bypass et — sistem-wide değişiklik yapma:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
./publish-all.ps1 -DryRun
```

`-Scope Process` flag'i kritik: bypass terminal kapanınca biter, kalıcı değil. `-Scope CurrentUser` veya `-Scope LocalMachine` ile yapma — sonradan unutursun, sistem güvenlik açığı olur.

### "Response status code does not indicate success: 403"

Yanlış API key veya scope. NuGet.org → API Keys → key'in glob pattern'ı `NetCoreBackend.NArchitecture.*`'a uyuyor mu kontrol et.

### "A package with id 'X' and version '1.0.0' already exists"

Aynı version daha önce push edilmiş. `--skip-duplicate` flag'i bunu zaten silently atlar. Version'u bump'la.

### "Unable to load the service index"

NuGet feed'e ağ erişimi yok. `https://api.nuget.org/v3/index.json` browser'da açılıyor mu? Proxy/firewall kontrolü.

### "Failed to publish symbol package"

`.snupkg` push'u 503 verirse problem değil — sembol paketleri ayrı feed'e gider (symbols.nuget.org) ve genelde gecikmeli işlenir. `dotnet nuget push` otomatik retry yapar.

### Pack çıktısında yanlış README

Her csproj kendi klasöründeki `README.md`'yi paketler. Yanlış README görüyorsan `Directory.Build.props`'taki `Exists('$(MSBuildProjectDirectory)\README.md')` koşulu doğru çalışmıyor olabilir — proje klasörünün gerçek path'ini kontrol et.

### Build clean, pack'te NU5128

"PackageReadmeFile is set but file is missing" — proje klasöründe `README.md` yok ama Directory.Build.props onu pack'e ekleyemiyor. Eksik library için README.md oluştur veya `<PackageReadmeFile></PackageReadmeFile>` ile o projede property'yi boşalt.

### "The package version is invalid"

`<Version>` formatı SemVer'e uymuyor. `1.0.0`, `1.0.0-beta.1` OK; `v1.0` ya da `1.0` yanlış.

---

## 8. Yayım sonrası

### 8.1 İndexing

NuGet feed'de paketin görünmesi 5-15 dakika sürer. Bu süre içinde:

- `https://www.nuget.org/packages/NetCoreBackend.NArchitecture.Core.Outbox` → "Package not found"
- `dotnet add package` → restore fail

Sabırla bekle. Bir defalık bir gecikme; sonraki version'larda daha hızlı index'lenir.

### 8.2 NuGet sayfasını kontrol et

Her paketin sayfasında olması gerekenler:
- ✅ MIT license badge
- ✅ README görünümü (klasördeki README.md'nin render'ı)
- ✅ Description
- ✅ Tags
- ✅ "Dependencies" listesi doğru
- ✅ "Versions" tab'inde sadece 1.0.0
- ❌ "Source repository" linki (henüz public repo yok, boş bırakıldı)
- ❌ "Project website" linki (aynı sebep)

### 8.3 Paketi unlist etmek (silmek değil)

Yanlış push'u geri almak istersen:

```
1. https://www.nuget.org/packages/<package-id>/<version> → "Manage Package"
2. "Listing" → "List package in search results" kutusunu KAPAT → Save
```

Unlist edilen paket arama sonuçlarında görünmez ama version'ı koruyorsun — yeniden push edemiyorsun. Aynı version'u tekrar yayımlamak ya unlisted bırakılması ya da yeni version push edilmesi gerekir.

---

## 9. Gelecek

İlerde yapılması anlamlı olanlar (sıralama gevşek, ihtiyaca göre):

- [ ] **GitHub repo'su açıp** `RepositoryUrl` ve `PackageProjectUrl`'i Directory.Build.props'a doldur — NuGet sayfasında "Source repository" / "Project website" linkleri görünür.
- [ ] **SourceLink** ekle — tüketici `.snupkg`'leri kullandığında IDE'den paketin orijinal source'una step-in yapabilir. Tek paket: `Microsoft.SourceLink.GitHub`, otomatik çalışır.
- [ ] **CI/CD pipeline** — GitHub Actions / Azure DevOps ile `main`'e merge'de otomatik publish. Manual `publish-all.ps1`'in yerine geçer.
- [ ] **PackageIcon** — Directory.Build.props'a icon ekle (PNG, 128x128). NuGet sayfasında brand görünür.
- [ ] **Merkezi Version** — csproj'lardan `<Version>1.0.0</Version>` satırlarını sil, Directory.Build.props'ta tek değer bırak.
- [ ] **PackageId kısalt** — `NetCoreBackend.NArchitecture.Core.X` çok uzun. Repo public olduktan sonra `NArchitecture.Core.X` veya başka kısa namespace mümkün.

---

## 10. İlgili dosyalar

- [`Directory.Build.props`](./Directory.Build.props) — tüm csproj'lara uygulanan ortak metadata
- [`publish-all.ps1`](./publish-all.ps1) — otomasyon script'i
- [`Directory.Packages.props`](./Directory.Packages.props) — CPM (Central Package Management) — paket versiyon merkezi
- [`README.md`](./README.md) — projenin ana README'si
- [`SETUP.md`](./SETUP.md) — tüketici uygulamanın bu paketleri nasıl kullanacağı
