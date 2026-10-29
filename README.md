# Mimi — Development Build

A Windows desktop music-player foundation built with C# / WPF / .NET 8 and NAudio.

## Current starter features
- Dark anime-inspired cyan/pink desktop UI with the Mimi app icon
- Local audio file selection and a resilient recursive folder scan that reports
  directories it could not read instead of discarding the whole result
- Play / pause, next / previous, volume and seek; next / previous and
  auto-advance follow the visible filtered list and skip unplayable files
- Vietnamese diacritic-insensitive library search, direct favorite toggles,
  and track removal (right-click or Delete key)
- Library, favorites and volume restored on startup from `%LocalAppData%\Mimi`
  (`library.json` / `favorites.json` / `settings.json`) with atomic JSON writes
- Rolling diagnostic logs in `%LocalAppData%\Mimi\logs`, with global exception
  logging so unexpected failures are traceable
- Stage Engine visualizer (beta): worker-thread 32-band FFT with dB/AGC
  scaling, Neon Spectrum / Twin Wave / Halo Circle modes, fullscreen Stage
  (`F11`), live FPS/draw-time diagnostics, sampled before the volume gain,
  and fading out while paused or stopped
- Modular MVVM architecture with dependency injection and automated tests;
  Windows Release builds and unit tests are verified by GitHub Actions

## Requirements
- Windows 10/11
- .NET 8 SDK
- Internet connection on first build to restore NuGet packages

## Build, test, and run
Open `Mimi.sln` in Visual Studio 2022 (with the .NET desktop development
workload) and set `Mimi.App` as the startup project, or use PowerShell from
this folder:

```powershell
dotnet restore Mimi.sln
dotnet build Mimi.sln --configuration Release
dotnet test tests\Mimi.Tests\Mimi.Tests.csproj --configuration Release
dotnet run --project src\Mimi.App\Mimi.App.csproj
```

## Source layout
- `src/Mimi.App` — WPF views, view models, dependency-injection composition,
  logging, dialogs, and the `Mimi.exe` entry point
- `src/Mimi.Core` — shared models, service contracts, playback-order logic,
  shuffle-bag primitives, and Vietnamese search normalization
- `src/Mimi.Audio` — NAudio playback implementation behind `IAudioPlayer`
- `src/Mimi.Library` — audio-file discovery and JSON user-data persistence
- `src/Mimi.Visualizer` — ring-buffer capture and testable FFT/AGC analysis
- `tests/Mimi.Tests` — xUnit tests for library, persistence, search,
  playback order, shuffle-bag, migration, and spectrum analysis

## Notes
This is a first working foundation, not a finished commercial product. Online streaming is intentionally not implemented yet. Each streaming provider requires an official API/SDK, authorization, and compliance with that provider's playback and licensing rules. Do not scrape protected services or attempt to bypass DRM.

## Design & roadmap
See **[ROADMAP.md](ROADMAP.md)** for the full product design and phased development
roadmap (in Vietnamese): vision and IP principles for the anime × Vocaloid-inspired
"Mimi Neon Stage" design system, screen specs, target architecture, per-subsystem
designs (audio, SQLite library, 60fps visualizer, lyrics, bilingual vi/en UI),
Phase 0–7 plan with exit criteria, audit findings, and open decisions.

## Rename note
This app was previously named **MikuVerse Music** and is now **Mimi** (application project `src/Mimi.App/Mimi.App.csproj`, namespace `Mimi`, output `Mimi.exe`). On first launch, an existing library/favorites saved under `%LocalAppData%\MikuVerseMusic` are copied automatically to `%LocalAppData%\Mimi`; the old folder is left untouched.

## Branding and assets
The current UI uses original abstract shapes and colors. Before distributing any build, review all fonts, images, audio samples, icons, dependencies, and trademarks for appropriate licensing. A commercial product should have its own identity unless rights for third-party character assets are secured.
