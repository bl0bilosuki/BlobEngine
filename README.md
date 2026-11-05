# BlobEngine

BlobEngine is a Windows x64 memory inspection utility built with C# and WPF on .NET 8.

## Current repository layout

- `BlobEngine.App` — WPF desktop application.
- `BlobEngine.Core` — data types and value conversion.
- `BlobEngine.Memory` — guarded Windows process-memory access.
- `BlobEngine.Scanner` — asynchronous first/next scan engine.
- `BlobEngine.Tests` — automated tests for core value conversion.
- `BlobEngine.Scanner` and `BlobEngine.Memory` deliberately operate only on processes selected by the user. They do not bypass anti-cheat, DRM, or Windows security controls.

The older Cheat Engine 7.5 customization workflow remains in the repository for reference. The WPF implementation is the active development direction on the `wpf-rebuild` branch.

## Build locally

Install the .NET 8 SDK and run in PowerShell:

```powershell
dotnet test BlobEngine.Tests/BlobEngine.Tests.csproj -c Release
dotnet publish BlobEngine.App/BlobEngine.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The executable is written to:

`BlobEngine.App/bin/Release/net8.0-windows/win-x64/publish/BlobEngine.exe`

## GitHub Actions

Open **Actions → BlobEngine WPF Windows x64 → Run workflow**. The workflow runs the core tests, publishes a self-contained single-file x64 executable, verifies that the EXE exists, and uploads it as the `BlobEngine-Windows-x64` artifact.

## Safety

Use only with processes you own or are authorized to inspect. The project does not attempt to evade anti-cheat, DRM, process protections, or access controls. Writes must be explicitly initiated by the user.

## Licensing

The legacy upstream Cheat Engine customization is subject to its upstream GPL and third-party license obligations. Review all notices before redistributing any legacy-derived binaries or source.
