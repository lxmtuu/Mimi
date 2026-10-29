# Build roadmap

## Milestone 1 — Player foundation
- Verify playback on Windows 10/11 and supported formats.
- Add keyboard/media-key controls and robust end-of-track handling.
- Improve seek interaction and error reporting.

## Milestone 2 — Local music library
- SQLite persistence and migrations.
- Metadata extraction, cover art cache, rescan changed folders.
- Playlist CRUD, favorites, history, queue.

## Milestone 3 — Real-time visualizer
- Capture decoded samples without blocking audio output.
- FFT analysis on a worker thread.
- Smooth frequency bins and publish immutable visualization frames.
- Render with SkiaSharp or Direct3D after measuring performance.
- Add spectrum, waveform, particles, neon waves, full-screen mode.

## Milestone 4 — Lyrics and online services
- Research official provider APIs and developer terms.
- Implement one provider at a time only if desktop playback integration is permitted.
- Use OAuth/secure token storage where required; never ship client secrets in the app.
- Handle region restrictions, subscriptions, attribution, and offline behavior.
- Do not proxy, scrape, download, or bypass DRM unless expressly authorized.

## Milestone 5 — Product quality
- Unit/integration tests, crash logs, performance profiling.
- Installer, signing, privacy notice, update and rollback strategy.
- License and trademark audit, accessibility, localization.
