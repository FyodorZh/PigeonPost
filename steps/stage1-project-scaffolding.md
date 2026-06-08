# Stage 1: Project Scaffolding & Dependencies

## Goal

Create the three-project solution structure and configure all NuGet dependencies.
Verify that all projects build with `dotnet build`.

## Prerequisites

- .NET 10 SDK installed (`net10.0` target framework).
- Local NuGet feed `/Users/fyodor/Development/nugets` available (already in `~/.nuget/NuGet/NuGet.Config`).
- Pontifex dev packages available in the local feed.

## Current State

The repository currently contains a single `PigeonPost.csproj` console app with
Pontifex references. We will restructure into three projects under a single solution.

## Implementation Steps

### 1.1 Create solution structure

Delete the existing `.csproj` and `.sln`, then recreate:

```
PigeonPost/
├── PigeonPost.sln
├── src/
│   ├── PigeonPost.Tun/           # class library
│   │   └── PigeonPost.Tun.csproj
│   ├── PigeonPost.Bridge/        # class library
│   │   └── PigeonPost.Bridge.csproj
│   └── PigeonPost/               # console app
│       └── PigeonPost.csproj
└── tests/
    ├── PigeonPost.Tun.Tests/
    │   └── PigeonPost.Tun.Tests.csproj
    ├── PigeonPost.Bridge.Tests/
    │   └── PigeonPost.Bridge.Tests.csproj
    └── PigeonPost.Tests/
        └── PigeonPost.Tests.csproj
```

### 1.2 PigeonPost.Tun.csproj

- SDK: `Microsoft.NET.Sdk`
- Target: `net10.0`
- `ImplicitUsings`: `false`
- `Nullable`: `enable`
- `TreatWarningsAsErrors`: `true`
- Only standard-library references (no Pontifex, no Scriba).
- P/Invoke is through `System.Runtime.InteropServices`.

### 1.3 PigeonPost.Bridge.csproj

- Same SDK properties as above.
- Package references:
  - `Pontifex` (`0.1.2-dev.0`)
  - `Pontifex.Transport.Direct` (`0.1.1-dev.0`)
  - `Pontifex.Transport.Tcp` (`0.1.1-dev.0`)
  - `Scriba` (transitive from Pontifex, but explicit reference is fine)
- Project reference: `PigeonPost.Tun`

### 1.4 PigeonPost.csproj (console app)

- `OutputType`: `Exe`
- Same SDK properties.
- Package references:
  - `Pontifex` (`0.1.2-dev.0`)
  - `Pontifex.Transport.Direct` (`0.1.1-dev.0`)
  - `Pontifex.Transport.Tcp` (`0.1.1-dev.0`)
  - `Scriba` (plus `Scriba.JsonFactory` if needed for JSON serialization of `StopReason`)
- Project references: `PigeonPost.Tun`, `PigeonPost.Bridge`

### 1.5 Test projects

**PigeonPost.Tun.Tests.csproj**
- SDK: `Microsoft.NET.Sdk`
- Target: `net10.0`
- Packages: `NUnit` (`4.x`), `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`
- Project reference: `PigeonPost.Tun`

**PigeonPost.Bridge.Tests.csproj**
- Same as above plus package references to Pontifex, Scriba.
- Project references: `PigeonPost.Tun`, `PigeonPost.Bridge`

**PigeonPost.Tests.csproj**
- Integration/e2e tests.
- Project references: `PigeonPost.Tun`, `PigeonPost.Bridge`, `PigeonPost` (if needed).

### 1.6 Directory.Build.props (optional but recommended)

Place at repo root to share common properties:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>false</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### 1.7 NuGet Config

Ensure the local feed `/Users/fyodor/Development/nugets` is available.
If not present in the repo-local `nuget.config`, create one:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="/Users/fyodor/Development/nugets" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### 1.8 Solution file

Update `PigeonPost.sln` to include all 6 projects (3 src + 3 test).

## Verification

1. `dotnet restore` succeeds for all projects.
2. `dotnet build` succeeds with zero warnings and zero errors.
3. `dotnet test` runs and reports zero tests (or passes if placeholder tests exist).

## Files to Create/Modify

| File | Action |
|------|--------|
| `Directory.Build.props` | Create |
| `nuget.config` | Create (if not exists) |
| `PigeonPost.sln` | Replace |
| `src/PigeonPost.Tun/PigeonPost.Tun.csproj` | Create |
| `src/PigeonPost.Bridge/PigeonPost.Bridge.csproj` | Create |
| `src/PigeonPost/PigeonPost.csproj` | Replace |
| `tests/PigeonPost.Tun.Tests/PigeonPost.Tun.Tests.csproj` | Create |
| `tests/PigeonPost.Bridge.Tests/PigeonPost.Bridge.Tests.csproj` | Create |
| `tests/PigeonPost.Tests/PigeonPost.Tests.csproj` | Create |
| `src/PigeonPost/Program.cs` | Delete (rewrite in Stage 8) |
| Old `PigeonPost.csproj` (root) | Delete |
| Old `bin/`, `obj/` (root) | Delete |
