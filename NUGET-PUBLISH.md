# NuGet Publishing Guide

A guide to publishing the **28 class libraries** in this repository (excluding Core.Test) to NuGet.org.

> Shortcut: get an API key → `$env:NUGET_API_KEY = "..."` → `./publish-all.ps1` → done.

---

## 1. Prerequisites

### 1.1 NuGet.org account + API key

```
1. https://www.nuget.org → "Sign in with Microsoft"
2. Profile → "API Keys" → "Create"
     - Key Name:     net-core-backend-publish
     - Glob Pattern: NetCoreBackend.NArchitecture.*    ← restrict the scope (not full-access)
     - Scopes:       ✓ Push  ✓ Push new packages and package versions
     - Expires:      365 days
3. "Create" → copy the key once (it is not shown again)
```

> **Never** commit the API key to `git`. A `.env` file, a plain config file, a note written into the repo — none of these are acceptable. For production CI/CD, use a secret store such as GitHub Secrets / Azure Key Vault.

### 1.2 .NET SDK

Since the build target is `net10.0`, the **.NET 10 SDK** is required. Verify with `dotnet --version`.

### 1.3 PowerShell

`publish-all.ps1` works with both Windows PowerShell 5.1 and PowerShell 7+. A standard Windows installation already has 5.1 — there is nothing extra you need to do.

---

## 2. Quick usage

### 2.1 Publish to NuGet.org

```powershell
cd "C:\Users\caner.demirkol\Desktop\New folder\NetCoreBackend"

$env:NUGET_API_KEY = "oy2x..."   # ← the key you copied in step 1.1

./publish-all.ps1
```

Expected output:

```
=== BUILD (Release) ===
Build succeeded.

=== PACK ===
Packed 28 libraries + 28 symbol packages:
  NetCoreBackend.NArchitecture.Core.Application.1.0.0.nupkg
  NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.1.0.0.nupkg
  ... (28 lines)

=== PUSH → https://api.nuget.org/v3/index.json ===
  → NetCoreBackend.NArchitecture.Core.Application.1.0.0.nupkg
  → ... (28 push)

=== SUMMARY ===
Successful push: 28 / 28
It may take 5-15 minutes to appear in the NuGet feed (indexing).
```

### 2.2 Pack only, don't push (to inspect the packages)

```powershell
./publish-all.ps1 -DryRun
```

The packages are produced under `./nupkgs/` and are not sent anywhere. You can verify what gets sent to NuGet by opening the `.nupkg` with WinRAR/7-Zip and inspecting the `.nuspec`.

### 2.3 Publish to a local feed (test without going to NuGet.org)

NuGet.org is **irreversible**: a pushed version cannot be deleted, only unlisted. If you want to test on your own disk first:

```powershell
# 1. Local feed folder
mkdir C:\local-nuget-feed -Force

# 2. Point publish-all.ps1 at the local feed
./publish-all.ps1 -Source "C:\local-nuget-feed"

# 3. Create a test consumer project and consume the package
mkdir C:\test-consumer; cd C:\test-consumer
dotnet new webapi
dotnet nuget add source "C:\local-nuget-feed" --name local-test
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox
dotnet build    # do restore + compile work?
```

If nothing snags locally, move on to NuGet.org with `./publish-all.ps1` (with the API key set).

---

## 3. publish-all.ps1 parameters

| Parameter | Default | Description |
|---|---|---|
| `-ApiKey` | `$env:NUGET_API_KEY` | If not provided, the environment variable is read. Not needed for a local feed. |
| `-Source` | `https://api.nuget.org/v3/index.json` | Target feed URL. You can also pass a local folder. |
| `-Configuration` | `Release` | Build configuration. |
| `-OutputDir` | `./nupkgs` | Pack output folder. |
| `-DryRun` | `false` | Skips the push, only packs. |

---

## 4. Setup performed (reference)

This repo was prepared with the following files so that `publish-all.ps1` works:

### 4.1 `Directory.Build.props`

Common metadata applied automatically to all csproj files:

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

Each library copies the `README.md` from its own folder to the package root → the README is shown on the NuGet.org page.

### 4.2 `Core.Test/Core.Test.csproj`

```xml
<IsPackable>false</IsPackable>
<GeneratePackageOnBuild>false</GeneratePackageOnBuild>
```

Core.Test contains the framework's own xUnit regression suite + consumer test helpers; it is not packaged. (If you want to distribute the helpers via NuGet, a separate project named `Core.TestKit` is recommended.)

### 4.3 `.gitignore`

```
nupkgs/
```

Keep the pack output out of git.

---

## 5. Published packages (28 in total)

### Tier 1 — zero internal dependencies (can be pushed in parallel)

```
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging
NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction
NetCoreBackend.NArchitecture.Core.Localization.Abstraction
NetCoreBackend.NArchitecture.Core.Mailing
NetCoreBackend.NArchitecture.Core.Translation.Abstraction
NetCoreBackend.NArchitecture.Core.Persistence
```

### Tier 2 — depends on Tier 1

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

### Tier 3 — DI extensions and adapters (depends on Tier 1+2)

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

`publish-all.ps1` does not care about tier order (it does a sequential serial push); there is no restore conflict after the first publish, because once each push completes the NuGet feed can immediately serve the next package's dependency.

---

## 6. Version management

### 6.1 Current version

`<Version>1.0.0</Version>` in all csproj files. Trying to push the same version again is a silent no-op thanks to the `--skip-duplicate` flag (not an error).

### 6.2 Bumping for a new release

Instead of updating all 28 csproj files one by one, keep a central version in `Directory.Build.props`:

```xml
<PropertyGroup>
  <Version>1.1.0</Version>      <!-- in a single place -->
  ...
</PropertyGroup>
```

Delete the `<Version>1.0.0</Version>` line from each csproj → the value in Directory.Build.props automatically takes effect. (Right now each csproj has 1.0.0; this consolidation is recommended after the first version bump.)

### 6.3 SemVer rule

```
1.0.0          ← first stable release
1.0.1          ← bug fix (e.g. an R5 doc)
1.1.0          ← new feature (e.g. Outbox added)
2.0.0          ← breaking change (e.g. we changed the IElasticSearch API)
1.1.0-beta.1   ← pre-release for testing (visible on NuGet.org via "Show prerelease")
```

---

## 7. Troubleshooting

### "publish-all.ps1 cannot be loaded because running scripts is disabled on this system"

On Windows, the PowerShell ExecutionPolicy rejects unsigned scripts by default. Bypass it **for that terminal session only** — do not make a system-wide change:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
./publish-all.ps1 -DryRun
```

The `-Scope Process` flag is critical: the bypass ends when the terminal closes, it is not permanent. Do not use `-Scope CurrentUser` or `-Scope LocalMachine` — you will forget it later and it becomes a system security hole.

### "Response status code does not indicate success: 403"

Wrong API key or scope. NuGet.org → API Keys → check that the key's glob pattern matches `NetCoreBackend.NArchitecture.*`.

### "A package with id 'X' and version '1.0.0' already exists"

The same version has been pushed before. The `--skip-duplicate` flag already skips this silently. Bump the version.

### "Unable to load the service index"

No network access to the NuGet feed. Does `https://api.nuget.org/v3/index.json` open in a browser? Check the proxy/firewall.

### "Failed to publish symbol package"

If the `.snupkg` push returns 503, it is not a problem — symbol packages go to a separate feed (symbols.nuget.org) and are usually processed with a delay. `dotnet nuget push` retries automatically.

### Wrong README in the pack output

Each csproj packs the `README.md` from its own folder. If you see the wrong README, the `Exists('$(MSBuildProjectDirectory)\README.md')` condition in `Directory.Build.props` may not be working correctly — check the project folder's actual path.

### Build clean, NU5128 on pack

"PackageReadmeFile is set but file is missing" — there is no `README.md` in the project folder, but Directory.Build.props cannot add it to the pack. Create a README.md for the missing library, or clear the property in that project with `<PackageReadmeFile></PackageReadmeFile>`.

### "The package version is invalid"

The `<Version>` format does not conform to SemVer. `1.0.0`, `1.0.0-beta.1` are OK; `v1.0` or `1.0` are wrong.

---

## 8. After publishing

### 8.1 Indexing

It takes 5-15 minutes for a package to appear in the NuGet feed. During this time:

- `https://www.nuget.org/packages/NetCoreBackend.NArchitecture.Core.Outbox` → "Package not found"
- `dotnet add package` → restore fail

Be patient. It is a one-time delay; subsequent versions are indexed faster.

### 8.2 Check the NuGet page

What should be present on each package's page:
- ✅ MIT license badge
- ✅ README view (the render of the folder's README.md)
- ✅ Description
- ✅ Tags
- ✅ The "Dependencies" list is correct
- ✅ Only 1.0.0 in the "Versions" tab
- ❌ "Source repository" link (no public repo yet, left empty)
- ❌ "Project website" link (same reason)

### 8.3 Unlisting a package (not deleting)

If you want to roll back a wrong push:

```
1. https://www.nuget.org/packages/<package-id>/<version> → "Manage Package"
2. "Listing" → UNCHECK the "List package in search results" box → Save
```

An unlisted package does not appear in search results, but you keep its version — you cannot push it again. To republish the same version, it must either be left unlisted or a new version must be pushed.

---

## 9. Future

Things worth doing later (loosely ordered, as needed):

- [ ] **Open a GitHub repo** and fill in `RepositoryUrl` and `PackageProjectUrl` in Directory.Build.props — the "Source repository" / "Project website" links appear on the NuGet page.
- [ ] Add **SourceLink** — when a consumer uses the `.snupkg` files, they can step into the package's original source from the IDE. A single package: `Microsoft.SourceLink.GitHub`, works automatically.
- [ ] **CI/CD pipeline** — automatic publish on merge to `main` via GitHub Actions / Azure DevOps. Replaces the manual `publish-all.ps1`.
- [ ] **PackageIcon** — add an icon to Directory.Build.props (PNG, 128x128). The brand appears on the NuGet page.
- [ ] **Central Version** — delete the `<Version>1.0.0</Version>` lines from the csproj files, leave a single value in Directory.Build.props.
- [ ] **Shorten the PackageId** — `NetCoreBackend.NArchitecture.Core.X` is too long. After the repo goes public, `NArchitecture.Core.X` or another short namespace becomes possible.

---

## 10. Related files

- [`Directory.Build.props`](./Directory.Build.props) — common metadata applied to all csproj files
- [`publish-all.ps1`](./publish-all.ps1) — the automation script
- [`Directory.Packages.props`](./Directory.Packages.props) — CPM (Central Package Management) — central package version registry
- [`README.md`](./README.md) — the project's main README
- [`SETUP.md`](./SETUP.md) — how a consuming application uses these packages
