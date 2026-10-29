# Mimi — Starter Build

A Windows desktop music-player foundation built with C# / WPF / .NET 8 and NAudio.

## Current starter features
- Dark anime-inspired cyan/pink desktop UI
- Local audio file selection and folder scan
- Play / pause, next / previous, volume and seek
- Basic library list and search
- Playback engine isolated in `Services/AudioService.cs`

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

Planned next milestones:
1. Persist library, playlists, and settings with SQLite.
2. Add robust metadata extraction and cover art.
3. Add audio analysis + real-time FFT visualizer.
4. Add lyrics through a licensed/authorized source.
5. Define provider adapters only for services that explicitly permit the intended playback integration.
6. Add automated tests, diagnostics, installer, update strategy, accessibility, and license audit.

## Rename note
This app was previously named **MikuVerse Music** and is now **Mimi** (project `Mimi.csproj`, namespace `Mimi`, output `Mimi.exe`). On first launch, an existing library/favorites saved under `%LocalAppData%\MikuVerseMusic` are copied automatically to `%LocalAppData%\Mimi`; the old folder is left untouched.

## Branding and assets
The current UI uses original abstract shapes and colors. Before distributing any build, review all fonts, images, audio samples, icons, dependencies, and trademarks for appropriate licensing. A commercial product should have its own identity unless rights for third-party character assets are secured.
