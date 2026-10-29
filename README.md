# Mimi — Development Build

A Windows desktop music-player foundation built with C# / WPF / .NET 8 and NAudio.

## Current starter features
- Dark anime-inspired cyan/pink desktop UI with the Mimi app icon
- Local audio file selection, Explorer drag-and-drop, and a resilient
  recursive folder scan that reports directories it could not read instead of
  discarding the whole result. Folder/file scans run in the background with
  progress and cancellation, and unchanged files reuse known metadata based
  on file size + modified time
- Embedded metadata and cover art read with TagLib-Sharp (title, artist,
  album, genre, year, duration, bitrate, and sample rate), with a filename
  fallback for missing or damaged tags; title, artist, album, genre, year,
  time, and file format are shown in the library. Cover art is cached as
  512px JPEGs for Now Playing and 96px JPEG thumbnails for the track list,
  with the original embedded bytes kept only as a decode/write fallback
- Sortable library columns (title, artist, album, genre, year, duration,
  and format), metadata filters for artist/album/genre/format/playback
  state, and Extended multi-select for batch favorites, playlist additions,
  playlist removal, and library removal
- Play / pause, next / previous, volume and seek; next / previous and
  auto-advance follow the visible filtered list and skip unplayable files
- Vietnamese diacritic-insensitive search over track metadata. When SQLite
  is available, the search box uses debounced FTS5 prefix queries and
  relevance ordering in Library/Favorites, while preserving playlist and
  Recently played ordering; an immediate in-memory filter remains the
  fallback while typing or if the catalog search is unavailable
- Playlist management in the sidebar: create, rename, delete, play, add the
  selected song(s) from the track list's **Add to playlist** menu, remove
  songs, and reorder them by dragging or with the Up/Down actions
- Direct favorite toggles, a Recently played view backed by playback
  history, and track removal (right-click or Delete key)
- A Settings screen for playback/session, startup/library, Stage Engine,
  and data/diagnostic options. Mimi remembers its window placement,
  visualizer mode, library view/search/filters/sorting, and reopens the
  previous song at its saved position **paused, never auto-playing**
- Library tracks, favorites, playback errors, and play history are restored
  from the SQLite catalog at `%LocalAppData%\Mimi\library.db` (schema v1,
  WAL + transactions); version 2 `settings.json` atomically stores volume,
  preferences, the library-view/window state, and the paused session. On
  first use, an
  existing legacy `library.json` / `favorites.json` pair is imported only
  when the catalog is empty, then archived as `.bak` files. If SQLite cannot
  be opened, Mimi falls back to the legacy JSON library instead of failing
  startup. The same catalog stores ordered playlists and supplies the FTS5
  index used by the main-window search box
- Rolling diagnostic logs in `%LocalAppData%\Mimi\logs`, with global exception
  logging so unexpected failures are traceable
- Stage Engine visualizer (beta): worker-thread 32-band FFT with dB/AGC
  scaling, Neon Spectrum / Twin Wave / Halo Circle modes, fullscreen Stage
  (`F11`), live FPS/draw-time diagnostics, sampled before the volume gain,
  and fading out while paused or stopped
- Modular MVVM architecture with dependency injection and automated tests;
  Windows Release builds and unit tests are verified by GitHub Actions.
  This remains Phase 3 in development (`v0.4.0`), not the complete Phase 3
  exit: folder watching, file associations, and measured 10,000-track
  performance remain open items in `ROADMAP.md`

## Requirements
- Windows 10/11 with Windows Terminal or PowerShell
- Git
- .NET 8 SDK (not only the .NET Runtime)
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

### Detailed Windows Terminal setup — Hướng dẫn setup chi tiết

Mimi là ứng dụng WPF, vì vậy hãy chạy các lệnh dưới đây trong **Windows
Terminal** hoặc **PowerShell** trên Windows 10/11. Visual Studio không bắt
buộc nếu bạn chỉ muốn build và chạy từ Terminal.

#### 1. Kiểm tra hoặc cài Git và .NET 8 SDK

Kiểm tra các công cụ đã có:

```powershell
git --version
dotnet --list-sdks
```

Bạn cần thấy phiên bản Git và ít nhất một .NET SDK bắt đầu bằng `8.`. Nếu
chưa có, cài bằng `winget`:

```powershell
winget install -e --id Git.Git --source winget
winget install -e --id Microsoft.DotNet.SDK.8 --source winget
```

Sau khi cài, đóng Terminal và mở lại để Windows nhận biến môi trường mới,
rồi chạy lại hai lệnh kiểm tra ở trên. Lưu ý phải cài **.NET 8 SDK**; chỉ có
.NET Runtime thì chạy được ứng dụng đã build nhưng không build được source.

#### 2. Clone repo và chọn đúng working branch hiện tại

Nếu bạn chưa clone Mimi:

```powershell
New-Item -ItemType Directory -Force "$HOME\source" | Out-Null
Set-Location "$HOME\source"

git clone --branch arena/cdda374d-mimi https://github.com/lxmtuu/Mimi.git
Set-Location .\Mimi
```

`arena/cdda374d-mimi` hiện là working branch chứa nền tảng Phase 3
(`v0.4.0` đang phát triển). Khi branch này đã được merge, bạn có thể bỏ tùy
chọn `--branch ...` để dùng branch mặc định.

Nếu bạn đã clone repo từ trước:

```powershell
Set-Location "C:\duong-dan\toi\thu-muc\Mimi"

git fetch --all --prune
git switch arena/cdda374d-mimi
git pull --ff-only
```

Kiểm tra branch và trạng thái thư mục làm việc:

```powershell
git branch --show-current
git status
git log -1 --oneline
```

Lệnh đầu tiên phải in ra `arena/cdda374d-mimi`. Nếu `git status` cho thấy bạn
có thay đổi local chưa commit, hãy kiểm tra kỹ trước khi chuyển branch; đừng
dùng `git reset --hard` nếu chưa chắc chắn vì lệnh đó có thể làm mất thay
đổi chưa lưu.

#### 3. Restore package, build và chạy unit test

Đứng tại thư mục gốc của repo (nơi có file `Mimi.sln`), chạy lần lượt:

```powershell
dotnet restore .\Mimi.sln
dotnet build .\Mimi.sln --configuration Release --no-restore
dotnet test .\tests\Mimi.Tests\Mimi.Tests.csproj --configuration Release --no-build
```

Lần restore đầu tiên cần Internet để tải NAudio, CommunityToolkit.Mvvm,
Microsoft.Extensions.DependencyInjection, Microsoft.Data.Sqlite,
TagLibSharp, Serilog, xUnit và các package phụ thuộc. Build/test đạt khi
Terminal báo tương tự:

```text
Build succeeded.
Passed!  - Failed:     0
```

#### 4. Chạy ứng dụng

Chạy trực tiếp từ source:

```powershell
dotnet run --project .\src\Mimi.App\Mimi.App.csproj
```

Lệnh trên build cấu hình Debug nếu cần rồi mở Mimi. Nếu bạn muốn chạy bản
Release vừa build ở bước 3, dùng:

```powershell
.\src\Mimi.App\bin\Release\net8.0-windows\Mimi.exe
```

Khi cửa sổ Mimi mở, bạn có thể chọn **Add music files**, **Scan a folder**,
hoặc kéo-thả file/thư mục nhạc từ Explorer vào cửa sổ. Trong lúc quét, thanh
tiến độ và nút **Cancel** xuất hiện ở khu vực tiêu đề thư viện. Sau đó hãy
double-click một bài để phát, thử tìm bài tiếng Việt bằng từ khóa không dấu,
bấm tiêu đề cột để sắp xếp, dùng hàng **Filters** để lọc theo
nghệ sĩ/album/thể loại/định dạng/trạng thái phát, giữ `Ctrl`/`Shift` để chọn
nhiều bài cho các thao tác favorites/xóa/thêm vào playlist, mở **Recently
played** để xem lịch sử nghe, dùng nút **+** ở mục **PLAYLISTS** để tạo
playlist, chuột phải vào (các) bài đang chọn và dùng **Add to playlist**, rồi
kéo-thả bài trong playlist (hoặc dùng nút **↑ Up/↓ Down**) để đổi thứ tự.
Mở **Settings** để đổi chế độ Stage và các tuỳ chọn khôi phục, sau đó đóng
rồi mở lại Mimi để kiểm tra bài/vị trí cũ được nạp ở trạng thái tạm dừng.
Cuối cùng, bấm `F11` để mở Stage toàn màn hình (`Esc` để thoát Stage).

#### 5. Mở thư mục dữ liệu và log

Mimi lưu catalog thư viện, favorites, lịch sử nghe, settings và cache ảnh
bìa trong `%LocalAppData%\Mimi`. Mở các thư mục này từ PowerShell bằng:

```powershell
explorer "$env:LOCALAPPDATA\Mimi"
explorer "$env:LOCALAPPDATA\Mimi\logs"
```

Các file chính gồm `library.db` (SQLite catalog; có thể kèm
`library.db-wal`/`library.db-shm` khi ứng dụng đang ghi dữ liệu),
`settings.json` (tuỳ chọn schema v2, vị trí cửa sổ/chế độ xem và phiên
nghe đang tạm dừng), ảnh bìa đã trích xuất trong `cache\covers\` (thường là
cặp `{hash}-512.jpg` và `{hash}-96.jpg`; cache byte gốc của bản cũ được app
tự nâng cấp ở nền khi mở thư viện), và log xoay theo ngày theo mẫu
`logs\mimi-YYYYMMDD.log`. `library.json` và
`favorites.json` chỉ còn là nguồn migration/fallback của các bản cũ; sau khi
nhập thành công vào SQLite, chúng được đổi tên thành file `.bak` để làm bản
sao lưu.

#### 6. Cập nhật source ở những lần sau

Khi branch có commit mới:

```powershell
git status
git pull --ff-only
dotnet restore .\Mimi.sln
dotnet build .\Mimi.sln --configuration Release --no-restore
dotnet test .\tests\Mimi.Tests\Mimi.Tests.csproj --configuration Release --no-build
dotnet run --project .\src\Mimi.App\Mimi.App.csproj
```

#### Xử lý lỗi thường gặp

- **`dotnet` is not recognized:** đóng/mở lại Terminal sau khi cài SDK, rồi
  chạy `where.exe dotnet` và `dotnet --list-sdks`. Nếu danh sách SDK trống,
  cài lại package `Microsoft.DotNet.SDK.8` ở bước 1.
- **Không tìm thấy branch:** chạy `git fetch --all --prune`, kiểm tra bằng
  `git branch -r`, rồi thử lại `git switch arena/cdda374d-mimi`.
- **Restore lỗi `NU1301`:** kiểm tra Internet/proxy/firewall và khả năng truy
  cập NuGet, sau đó chạy lại `dotnet restore .\Mimi.sln`.
- **Build báo file đang bị khóa:** đóng cửa sổ `Mimi.exe` đang chạy rồi build
  lại. Tiến trình ứng dụng đang mở có thể khóa file trong thư mục `bin`.
- **Build hoặc test thất bại:** lưu toàn bộ output để dễ chẩn đoán, ví dụ
  `dotnet build .\Mimi.sln --configuration Release 2>&1 | Tee-Object .\build.log`,
  rồi dùng nội dung `build.log` khi báo lỗi.

## Source layout
- `src/Mimi.App` — WPF views, view models, dependency-injection composition,
  logging, dialogs, and the `Mimi.exe` entry point
- `src/Mimi.Core` — shared models, service contracts, playback-order logic,
  shuffle-bag primitives, and Vietnamese search normalization
- `src/Mimi.Audio` — NAudio playback implementation behind `IAudioPlayer`
- `src/Mimi.Library` — asynchronous audio-file discovery, TagLib-Sharp
  metadata/cover extraction, the SQLite library catalog, and JSON
  settings/legacy-library migration fallback
- `src/Mimi.Visualizer` — ring-buffer capture and testable FFT/AGC analysis
- `tests/Mimi.Tests` — xUnit tests for scanning, metadata, SQLite catalog
  search/playlists/history, user-data migration/archive, playback order,
  shuffle-bag, Vietnamese search normalization, and spectrum analysis

## Notes
This is a first working foundation, not a finished commercial product. Online streaming is intentionally not implemented yet. Each streaming provider requires an official API/SDK, authorization, and compliance with that provider's playback and licensing rules. Do not scrape protected services or attempt to bypass DRM.

## Design & roadmap
See **[ROADMAP.md](ROADMAP.md)** for the full product design and phased development
roadmap (in Vietnamese): vision and IP principles for the anime × Vocaloid-inspired
"Mimi Neon Stage" design system, screen specs, target architecture, per-subsystem
designs (audio, SQLite library, 60fps visualizer, lyrics, bilingual vi/en UI),
Phase 0–7 plan with exit criteria, audit findings, and open decisions.

## Rename note
This app was previously named **MikuVerse Music** and is now **Mimi** (application project `src/Mimi.App/Mimi.App.csproj`, namespace `Mimi`, output `Mimi.exe`). On first launch, an existing library/favorites saved under `%LocalAppData%\MikuVerseMusic` are copied automatically to `%LocalAppData%\Mimi`; when the SQLite catalog is empty, those JSON records are then imported into `library.db` and the imported JSON files are archived as `.bak`. The old `MikuVerseMusic` folder is left untouched.

## Branding and assets
The current UI uses original abstract shapes and colors. Before distributing any build, review all fonts, images, audio samples, icons, dependencies, and trademarks for appropriate licensing. A commercial product should have its own identity unless rights for third-party character assets are secured.
