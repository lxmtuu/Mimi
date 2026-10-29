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
- Play / pause, queue-aware next / previous and auto-advance, ±5-second
  keyboard seek plus ±10-second buttons, volume percentage, and mute.
  Starting a song captures the visible
  filtered/sorted list as its playback context; Shuffle uses a no-repeat
  shuffle bag, Repeat supports Off / All / One, Previous follows the songs
  actually played, and unplayable candidates are skipped without committing
  queue state
- WASAPI shared-mode playback following the Windows default output, with
  WaveOut retained as a fallback. If the default device changes or
  disappears during playback, Mimi pauses, keeps the song and position,
  rebuilds only the output stream, and waits for an explicit Play press
  instead of unexpectedly moving sound to another device. The active
  output is shown in Now Playing and Settings; a missing output is
  reported separately from a corrupt music file. For diagnostics only,
  `MIMI_AUDIO_OUTPUT=wasapi|waveout|auto` can force one backend for a
  same-machine A/B comparison; it is intentionally not an end-user
  Settings switch
- Vietnamese diacritic-insensitive search over track metadata. When SQLite
  is available, the search box uses debounced FTS5 prefix queries and
  relevance ordering in Library/Favorites, while preserving playlist and
  Recently played ordering; an immediate in-memory filter remains the
  fallback while typing or if the catalog search is unavailable
- Playlist management in the sidebar: create, rename, delete, play, add the
  selected song(s) from the track list's **Add to playlist** menu, remove
  songs, and reorder them by dragging or with the Up/Down actions
- An **Up Next** playback queue with drag reordering, Up/Down buttons,
  Play-next / Add-to-queue actions for selected songs, double-click to play
  a queued song immediately, item removal, and Clear upcoming
- Windows media integration through System Media Transport Controls:
  song metadata, cover art, playback state, timeline seeking, shuffle /
  repeat, hardware media keys, and the Windows volume/media flyout.
  Mimi also has taskbar thumbnail Previous/Play/Pause/Next controls,
  taskbar progress, and focus-aware in-window keyboard shortcuts for
  playback, five-second seek, volume steps, track changes, and search
- Direct favorite toggles, a Recently played view backed by playback
  history, and track removal (right-click or Delete key)
- A Settings screen for playback/session, startup/library, Stage Engine,
  a keyboard-shortcut reference, and data/diagnostic options. Mimi
  remembers its window placement,
  visualizer mode, library view/search/filters/sorting, playback modes,
  and reopens the previous song, position, and queue **paused, never
  auto-playing**
- Optional watched folders: new or changed songs in selected folders are
  scanned after a short debounce, while externally deleted files remain in
  the library and are marked missing instead of being silently removed
- Optional per-user **Open with Mimi** registration for MP3, WAV, AIFF/AIF,
  WMA, M4A, AAC, and FLAC. Files opened from Explorer are added to the
  library and played; if Mimi is already running, the request is forwarded
  to that window instead of starting a second player. Registration does not
  change the user's Windows default music app
- Library tracks, favorites, playback errors, and play history are restored
  from the SQLite catalog at `%LocalAppData%\Mimi\library.db` (schema v1,
  WAL + transactions); version 3 `settings.json` atomically stores volume,
  mute, shuffle, repeat, preferences, the library-view/window state, and
  the paused session including its queue. Older settings versions migrate
  with defaults for the new playback options. On first use, an
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
  Phase 4 (`v0.5.0`) is **code complete — acceptance pending**: the queue /
  playback-mode, Windows media-control/keyboard, WASAPI output-device,
  and diagnostic backend-override slices are implemented. Automated
  restore/build/test has also passed on a real Windows machine at
  `a056377` (0 warnings/errors and 83 passed / 0 failed / 0 skipped), and
  short forced/auto backend runs produced measurable loopback audio on
  the same Realtek endpoint. Those roughly 22-second runs are
  preliminary: a later Windows 11 checkpoint at `6ca298c` verified three
  synthetic 30-second fixtures (metadata, distinct covers, playlist order,
  and audible WASAPI playback). Direct Quick Settings checks then passed
  A1–A6 (metadata, cover, state sync, Play/Pause, Next, and Previous).
  A read-only GSMTC probe then verified that the Magenta and clean-restart
  Cyan timelines and seek bounds reach Windows and advance while playing.
  A full-card Quick Settings screenshot shows only Previous/Play/Next—no
  visible progress/time, FastForward/Rewind, or Shuffle/Repeat—so A7's
  visual surface and A8 remain blocked on that card, A9 is source-checked
  only, and A10–A12
  plus the taskbar/media-key/device-loss and longer
  WASAPI↔WaveOut stability gates in `ROADMAP.md` remain open. Their
  repeatable manual procedures, result template, loopback CSV analyzer,
  and read-only GSMTC timeline probe live in the
  [Windows Acceptance Kit](docs/acceptance/README.md)

## Requirements
- Windows 10 version 1809 or later / Windows 11, with Windows Terminal or
  PowerShell
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

`arena/cdda374d-mimi` hiện là working branch chứa Phase 4
(`v0.5.0` đang phát triển). Khi branch này đã được merge, bạn có thể bỏ tùy
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
.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe
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
Mở **Settings** để đổi chế độ Stage và các tuỳ chọn khôi phục; thêm một thư
mục vào **Watched folders**, thử chép thêm bài từ Explorer để Mimi tự quét,
sau đó đóng rồi mở lại Mimi để kiểm tra bài/vị trí cũ được nạp ở trạng thái
tạm dừng. Trong **Windows integration**, bật **Show Mimi in Windows' Open
with menu**, chuột phải vào một file nhạc trong Explorer và chọn
**Open with → Mimi**; nếu Mimi đang mở, bài phải được chuyển sang chính
cửa sổ đó thay vì tạo thêm một cửa sổ Mimi thứ hai.
Hãy phát một bài để panel **Up Next** nhận hàng đợi từ danh sách đang lọc,
rồi thử kéo sắp xếp một bài, dùng **Play next/Add to queue** với (các) bài
đang chọn, bật **Shuffle**, bấm nút Repeat đủ ba trạng thái
**Off → All → One**, dùng **−10s/+10s**, bật **Mute** và kiểm tra slider vẫn
giữ phần trăm âm lượng đã đặt khi bỏ mute. Bấm **Previous** sau khi bài đã
phát hơn 3 giây để kiểm tra bài quay về đầu; bấm lại gần đầu bài để quay về
bài vừa nghe thật sự trước đó. Đóng và mở lại Mimi để kiểm tra bài, vị trí,
Up Next cùng trạng thái mute/shuffle/repeat.
Trong lúc phát, mở flyout âm lượng của Windows để kiểm tra tên bài, ảnh
bìa và các nút Previous/Play/Pause/Next; thử kéo timeline (nếu phiên bản
Windows của bạn hỗ trợ), chuyển Shuffle/Repeat từ flyout, thu nhỏ Mimi rồi
dùng phím media trên bàn phím, tai nghe hoặc màn hình khoá. Di chuột lên
biểu tượng Mimi trên taskbar để thử các nút thumbnail Previous/Play/Pause/
Next và thanh tiến trình trên taskbar. Mở mục **Keyboard Shortcuts** trong
Settings, thử `Ctrl+F`, `Ctrl+←/→`, rồi bấm vào vùng trống không tương tác
để thử Space, `←/→` tua 5 giây và `↑/↓` đổi âm lượng; các phím này chủ động
không giành quyền khi bạn đang gõ trong ô tìm kiếm hoặc dùng list/slider.
Kiểm tra nhãn nhỏ dưới **VOLUME** và mục **Audio output** trong Settings:
thông thường phải hiện `WASAPI shared · [tên thiết bị]`. Khi đang phát, hãy
đổi thiết bị xuất mặc định trong Windows Settings/Quick Settings; Mimi
phải tự tạm dừng, giữ nguyên bài và vị trí, hiện banner output rồi chỉ phát
tiếp trên thiết bị mới khi bạn bấm Play. Thử rút tai nghe USB, ngắt
Bluetooth hoặc tắt thiết bị đang dùng: Mimi không được crash, không được
đánh dấu bài là lỗi; khi cắm lại hoặc chọn thiết bị khác, nhãn output phải
cập nhật. Nếu có sẵn, thử thêm file mono và file 44.1/48/96 kHz hoặc đa
kênh để kiểm tra chuyển đổi định dạng của WASAPI shared.

**Đo A/B WASAPI ↔ WaveOut (chỉ dành cho chẩn đoán):** đóng Mimi hoàn toàn
trước mỗi lần đổi backend, vì tiến trình Mimi đã chạy sẽ giữ backend cũ và
single-instance sẽ chuyển yêu cầu mở mới sang chính tiến trình đó. Từ
PowerShell tại thư mục gốc repo, chạy lần lượt từng cặp lệnh dưới đây; biến
môi trường chỉ có hiệu lực trong cửa sổ PowerShell hiện tại và không tạo
một công tắc backend trong Settings:

```powershell
$env:MIMI_AUDIO_OUTPUT = 'wasapi'
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe' -Wait

$env:MIMI_AUDIO_OUTPUT = 'waveout'
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe' -Wait

$env:MIMI_AUDIO_OUTPUT = 'auto'
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe' -Wait

Remove-Item Env:MIMI_AUDIO_OUTPUT -ErrorAction SilentlyContinue
```

Giữ nguyên cùng endpoint, file nhạc, mức volume của Windows/app, sample
rate và các tuỳ chọn enhancements cho cả hai lượt. Nhãn phải hiện tương ứng
`WASAPI shared · [endpoint]` hoặc `WaveOut diagnostic · [endpoint]`; log
cũng ghi backend đang bị ép. Hãy so độ trễ Play/Pause/seek và độ ổn định
(glitch/dropout, chuyển bài, đổi/rút thiết bị), đồng thời ghi rõ phương
pháp đo và điều kiện máy. Các số 100ms/200ms trong code chỉ là latency
được yêu cầu/cấu hình, **không phải latency đầu-cuối đã đo**. `auto` khôi
phục hành vi mặc định WASAPI → WaveOut fallback; giá trị không hợp lệ sẽ
được log cảnh báo và tự quay về `auto`.

Một lượt smoke test ngày 29/10/2026 tại commit `04ab178` trên endpoint
Speakers (Realtek(R) Audio) đã cho cả ba chế độ phát nhạc và có tín hiệu
loopback; phân tích lại tại `a056377` cho thấy mỗi CSV dài khoảng 22 giây.
Kết quả này không thay thế bài kiểm tra ổn định dài hơn hay các thao tác
rút/kết nối lại thiết bị vật lý; xem bảng số liệu và trạng thái từng cổng trong
`ROADMAP.md`, còn quy trình kiểm tra lặp lại được và mẫu ghi kết quả nằm
trong [Windows Acceptance Kit](docs/acceptance/README.md).

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
`settings.json` (tuỳ chọn schema v3 gồm mute/shuffle/repeat, danh sách thư
mục theo dõi, vị trí cửa sổ/chế độ xem và phiên nghe cùng hàng đợi đang
tạm dừng), ảnh bìa đã trích xuất trong
`cache\covers\` (thường là
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
- `src/Mimi.Core` — shared models, service contracts, the `PlaybackQueue`
  state machine and shuffle-bag primitive, and Vietnamese search
  normalization
- `src/Mimi.Audio` — NAudio WASAPI shared-mode playback, WaveOut fallback,
  a diagnostic-only backend override for A/B testing, and Windows
  output-device watching/recovery behind `IAudioPlayer`
- `src/Mimi.Library` — asynchronous audio-file discovery, TagLib-Sharp
  metadata/cover extraction, the SQLite library catalog, and JSON
  settings/legacy-library migration fallback
- `src/Mimi.Visualizer` — ring-buffer capture and testable FFT/AGC analysis
- `tests/Mimi.Tests` — xUnit tests for scanning, metadata, SQLite catalog
  search/playlists/history, user-data migration/archive, playback queue
  and shuffle-bag behavior, audio-output status/backend selection,
  Vietnamese search normalization, and spectrum analysis
- `docs/acceptance` — repeatable real-Windows acceptance checklist and
  result template; `ROADMAP.md` remains the decision/status record
- `tools` — diagnostic utilities that are not part of the shipped app:
  the PowerShell loopback-CSV analyzer for Phase 4 A/B measurements and a
  read-only GSMTC session inspector for SMTC timeline/control diagnosis

## Notes
This is a first working foundation, not a finished commercial product. Online streaming is intentionally not implemented yet. Each streaming provider requires an official API/SDK, authorization, and compliance with that provider's playback and licensing rules. Do not scrape protected services or attempt to bypass DRM.

## Design & roadmap
See **[ROADMAP.md](ROADMAP.md)** for the full product design and phased development
roadmap (in Vietnamese): vision and IP principles for the anime × Vocaloid-inspired
"Mimi Neon Stage" design system, screen specs, target architecture, per-subsystem
designs (audio, SQLite library, 60fps visualizer, lyrics, bilingual vi/en UI),
Phase 0–7 plan with exit criteria, audit findings, and open decisions.
For executing the remaining manual Windows gates, use the
**[Windows Acceptance Kit](docs/acceptance/README.md)** and copy its
result template for each test session.

## Rename note
This app was previously named **MikuVerse Music** and is now **Mimi** (application project `src/Mimi.App/Mimi.App.csproj`, namespace `Mimi`, output `Mimi.exe`). On first launch, an existing library/favorites saved under `%LocalAppData%\MikuVerseMusic` are copied automatically to `%LocalAppData%\Mimi`; when the SQLite catalog is empty, those JSON records are then imported into `library.db` and the imported JSON files are archived as `.bak`. The old `MikuVerseMusic` folder is left untouched.

## Branding and assets
The current UI uses original abstract shapes and colors. Before distributing any build, review all fonts, images, audio samples, icons, dependencies, and trademarks for appropriate licensing. A commercial product should have its own identity unless rights for third-party character assets are secured.
