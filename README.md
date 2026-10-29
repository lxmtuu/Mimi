# Mimi — Starter Build

A Windows desktop music-player foundation built with C# / WPF / .NET 8 and NAudio.

## Current starter features
- Dark anime-inspired cyan/pink desktop UI with the Mimi app icon
- Local audio file selection and a resilient recursive folder scan that reports
  directories it could not read instead of discarding the whole result
- Play / pause, next / previous, volume and seek; next / previous and
  auto-advance follow the visible filtered list and skip unplayable files
- Library search, direct favorite toggles, and track removal (right-click or
  Delete key)
- Library, favorites and volume restored on startup from `%LocalAppData%\Mimi`
  (`library.json` / `favorites.json` / `settings.json`) with atomic JSON writes
- Real-time 32-band FFT spectrum visualizer (v1), sampled before the volume
  gain and fading out while paused or stopped
- Playback engine isolated in `Services/AudioService.cs`
- Windows Release builds verified by GitHub Actions

## Requirements
- Windows 10/11
- .NET 8 SDK
- Internet connection on first build to restore the NAudio NuGet package

## Run
Open `Mimi.csproj` in Visual Studio 2022 (with the .NET desktop development workload), or run in PowerShell from this folder:

```powershell
dotnet restore
dotnet run
```

## Notes
This is a first working foundation, not a finished commercial product. Online streaming is intentionally not implemented yet. Each streaming provider requires an official API/SDK, authorization, and compliance with that provider's playback and licensing rules. Do not scrape protected services or attempt to bypass DRM.

## Design & roadmap
See **[ROADMAP.md](ROADMAP.md)** for the full product design and phased development
roadmap (in Vietnamese): vision and IP principles for the anime × Vocaloid-inspired
"Mimi Neon Stage" design system, screen specs, target architecture, per-subsystem
designs (audio, SQLite library, 60fps visualizer, lyrics, bilingual vi/en UI),
Phase 0–7 plan with exit criteria, audit findings, and open decisions.

## Rename note
This app was previously named **MikuVerse Music** and is now **Mimi** (project `Mimi.csproj`, namespace `Mimi`, output `Mimi.exe`). On first launch, an existing library/favorites saved under `%LocalAppData%\MikuVerseMusic` are copied automatically to `%LocalAppData%\Mimi`; the old folder is left untouched.

## Branding and assets
The current UI uses original abstract shapes and colors. Before distributing any build, review all fonts, images, audio samples, icons, dependencies, and trademarks for appropriate licensing. A commercial product should have its own identity unless rights for third-party character assets are secured.
