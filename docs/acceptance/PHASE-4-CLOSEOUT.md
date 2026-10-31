# Phase 4 closeout — đối chiếu hồ sơ tại `b0cd13a`

Tài liệu này là **ảnh chụp đối chiếu** các bằng chứng Phase 4 đã được ghi
trong [`ROADMAP.md`](../../ROADMAP.md), [Windows Acceptance
Kit](README.md) và các báo cáo Windows được tổng hợp tới ngày 31/10/2026.
Nó không thay thế `ROADMAP.md`: roadmap vẫn là nơi ghi quyết định và trạng
thái chính thức. Lượt đối chiếu này không tự nâng bất kỳ hạng mục nào lên
`PASS` và chưa ghi waiver nào thay cho chủ dự án.

## Checkpoint được đối chiếu

| Hạng mục | Giá trị |
|---|---|
| Branch | `arena/cdda374d-mimi` |
| Code checkpoint | `589a1e0` — sửa nhận diện kết thúc bài tự nhiên |
| Checkpoint hồ sơ | `b0cd13a` — ghi nghiệm thu trực tiếp của chủ dự án |
| Windows CI tại `589a1e0` | [41116373549](https://github.com/lxmtuu/Mimi/actions/runs/41116373549) — success |
| Windows CI tại `b0cd13a` | [41119766835](https://github.com/lxmtuu/Mimi/actions/runs/41119766835) — success |
| Kết luận Phase 4 | **Code complete — Acceptance pending; chưa đóng** |

## Bảng trạng thái cổng

| Cổng | Trạng thái hiện hành | Bằng chứng đã ghi | Phần còn thiếu / giới hạn |
|---|---|---|---|
| Nền tảng S0 | `PASS` | Windows thật đã restore, Release build 0 warning/0 error và chạy unit test; các lượt gần nhất ghi full suite 84/84 | Không dùng riêng cổng này để kết luận các hành vi runtime của Windows/media/device |
| Regression tiến trình quét thư viện | `PASS` | Bản sửa serialize increment + `Report`; test 2 file và 32 file; retest Windows tại `57b14a3` ghi regression liên quan đạt 20/20 lượt liên tiếp | Không còn khoản lặp 20 lượt đang mở |
| P4-A — SMTC/Quick Settings | `PARTIAL` | A1–A6 `PASS`; A12 `PASS`; identity và nhãn nguồn `Mimi` đã xác nhận; GSMTC timeline được xuất bản và tiến triển | A7 trực quan/A8 và A9–A11 bị giới hạn bởi điều khiển Quick Settings không hiển thị |
| P4-B — Taskbar | `PASS` | Chủ dự án xác nhận nghiệm thu trực tiếp ngày 31/10/2026, bao phủ B1–B6 | Chi tiết từng tiểu mục chưa được gửi để ghi vào hồ sơ |
| P4-C — Media keys/keyboard | `PASS` | Chủ dự án xác nhận nghiệm thu trực tiếp ngày 31/10/2026 cho media keys/bàn phím | Chi tiết từng tiểu mục chưa được gửi để ghi vào hồ sơ |
| P4-D — Mất/ngắt thiết bị | `PASS` | Chủ dự án xác nhận nghiệm thu trực tiếp phần ngắt thiết bị; hồ sơ riêng còn có lượt đổi default endpoint và rebuild output | Endpoint, bài/vị trí và quan sát chi tiết của lượt chủ dự án chưa được ghi |
| P4-E — Không còn output | `BLOCKED` | Đã thử trong shell nâng quyền; disable 13 endpoint con không làm CoreAudio mất output, disable bốn driver cha gặp CIM Generic failure | Chưa chạy được kiểm tra app-level khi thật sự không còn output |
| P4-F — Kết nối lại | `PASS` | Chủ dự án xác nhận nghiệm thu trực tiếp phần kết nối lại; hồ sơ riêng còn có lượt đổi default endpoint và chỉ tiếp tục sau lệnh Play | Endpoint và chi tiết “chỉ tiếp tục sau Play” của lượt chủ dự án chưa được ghi |
| P4-G — A/B D-03 | `PARTIAL` | Ba lượt 5 phút hợp lệ cho mỗi backend và một control-run trên mỗi backend; loopback p99 khoảng 63,85–64,21ms | Phép đo latency âm học qua Microphone Array không nhất quán; D-03 vẫn tạm thời |
| P4-H — Chốt hồ sơ/quyết định | `OPEN` | Roadmap và lượt đối chiếu này đã gom bằng chứng hiện có | Chưa có quyết định waiver/đóng Phase và checkpoint tích hợp cuối |

`PASS` ở P4-B/P4-C/P4-D/P4-F được ghi đúng theo xác nhận nghiệm thu trực
tiếp của chủ dự án trong `ROADMAP.md`; các chi tiết tiểu mục còn thiếu
không được suy diễn thêm trong bảng này.

## Chi tiết P4-A

| Tiểu mục | Trạng thái | Ghi chú |
|---|---|---|
| A1 Metadata | `PASS` | Title/artist đúng trên thẻ |
| A2 Ảnh bìa | `PASS` | Cover đúng bài và cập nhật theo bài |
| A3 Đồng bộ trạng thái | `PASS` | Quick Settings và app đồng bộ |
| A4 Play/Pause | `PASS` | Dựa trên phiên thao tác trực tiếp đã ghi trước |
| A5 Next | `PASS` | Đã làm mới bằng thao tác thực tế tại checkpoint `9a925f3` |
| A6 Previous | `PASS` | Đã làm mới bằng thao tác thực tế tại checkpoint `9a925f3` |
| A7 Timeline GSMTC | Đã xác minh ở tầng dữ liệu | Duration/seek bounds hợp lệ; Position tiến triển khi `Playing` |
| A7 trực quan / A8 Seek | `BLOCKED/N/A` | Thẻ Quick Settings đã quan sát không hiện progress/thời gian hoặc timeline để kéo |
| A9 FastForward/Rewind trên thẻ | `BLOCKED/N/A` | Không có nút riêng trên thẻ đã quan sát |
| A10 Shuffle đồng bộ flyout | `BLOCKED/N/A` | Không có Shuffle control trên thẻ đã quan sát |
| A11 Repeat đồng bộ flyout | `BLOCKED/N/A` | Không có Repeat control trên thẻ đã quan sát |
| A12 Kết thúc bài/queue | `PASS` | Lỗi auto-advance tại `57b14a3` đã được sửa ở `589a1e0`; retest thật chuyển Cyan → Magenta |

### Bằng chứng tự động bổ sung cho Queue/Shuffle/Repeat

Báo cáo kiểm thử tại `57b14a3` ghi:

- `PlaybackQueueTests` + `ShuffleBagTests`: **11 passed / 0 failed**.
- Toàn bộ `Mimi.Tests`: **84 passed / 0 failed**.

Các hành vi logic được bao phủ gồm shuffle-bag đủ bài và không lặp trong
một chu kỳ, Previous theo lịch sử phát thực tế, tắt Shuffle khôi phục thứ
tự nguồn cho các bài sắp phát, Repeat All tạo chu kỳ kế tiếp,
Shuffle + Repeat All không bắt đầu chu kỳ mới bằng bài hiện tại, và tính
nhất quán sau thêm/xoá/di chuyển/jump/restore.

Bằng chứng trên là **automated logic PASS**, không thay thế thao tác UI:
báo cáo tại `57b14a3` chưa thao tác Shuffle/Repeat trên giao diện thật và
không xác nhận một test queue tương đương cho Repeat One. Vì định nghĩa
chính thức của A10/A11 là đồng bộ hai chiều với flyout, hai tiểu mục này
vẫn giữ `BLOCKED/N/A` ở bề mặt Quick Settings đã quan sát.

## Fixture đang dùng trên Windows

Bộ ba MP3 tại thư mục S1 trên máy Windows đã được xác minh có metadata,
duration 30.126 giây và cover JPEG nhúng riêng; SHA-256 của từng file đã
được ghi trong `ROADMAP.md` làm fingerprint của bộ thực tế. Thư mục không
kèm manifest/hash phát hành độc lập, vì vậy không tuyên bố bộ này giống hệt
từng byte với ZIP fixture đã phát hành trước đó.

## Việc còn cần trước khi đóng Phase 4

1. **Một lượt vận hành trong app cho Shuffle/Repeat:** bật Shuffle, ghi
   Up Next và thứ tự qua hai lần Next; kiểm tra Repeat Off/All/One tại điểm
   kết thúc thực tế.
2. **Quyết định waiver cho P4-A:** chủ dự án quyết định có chấp nhận hoãn
   phần A7 trực quan/A8/A9/A10/A11 vì shell không phơi bày điều khiển hay
   không. Waiver không đổi các mục này thành `PASS`.
3. **P4-E:** tạo môi trường không còn output an toàn/khôi phục được để
   chạy kiểm tra app-level, hoặc ghi waiver kèm điều kiện kiểm tra lại.
4. **P4-G/D-03:** thực hiện phép đo latency âm học đáng tin cậy, hoặc ghi
   quyết định giữ kiến trúc tạm thời WASAPI Shared mặc định + WaveOut
   fallback và hoãn riêng kết luận latency vật lý.
5. **P4-H:** ghi quyết định cuối trong `ROADMAP.md` và thực hiện checkpoint
   tích hợp riêng cho branch; merge vào `main` không tự động đóng Phase 4.

Cho tới khi các quyết định trên được ghi, trạng thái đúng vẫn là **Phase 4
chưa đóng**.


## Retest Queue/Shuffle/Repeat trên Windows thật — 31/10/2026

Checkpoint kiểm thử: `2137840` (fix stale playback end notifications), chạy trên máy Windows Yami bằng build Release đang cài trong worktree retest. Sau khi hoàn tất retest, checkout tài liệu được đặt trên nhánh tích hợp dựa trên `origin/arena/cdda374d-mimi`; không sửa mã nguồn trong lượt cập nhật hồ sơ này.

| Hạng mục | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| Repeat One | `PASS — telemetry/state` | `RepeatMode=One`, Cyan Signal vẫn là CurrentTrack sau nhiều lần kết thúc tự nhiên; Position reset về đầu bài. Chưa xác nhận trực tiếp không có âm thanh chồng luồng bằng nghe/loopback. |
| Repeat All | `PASS — runtime/state` | Quan sát chu trình tự nhiên: Cyan (08:12:11) → Amber (08:12:27) → Magenta (08:12:59) → Cyan (08:13:31). Sau khi quay lại Cyan, Upcoming được tạo lại (2 bài). |
| Shuffle bật/tắt | `PASS — runtime/state` | UI Automation bật và tắt Shuffle; `ShuffleEnabled` được lưu tương ứng `true/false`. |
| Shuffle reorder | `PASS — runtime/state` | Khi bật Shuffle, Up Next là Magenta → Amber; khi tắt, thứ tự nguồn được khôi phục là Amber → Magenta. Đây là xác nhận thao tác và sắp xếp queue, không phải thống kê chất lượng ngẫu nhiên qua nhiều chu kỳ. |
| Unit tests | `PASS` | `dotnet test tests/Mimi.Tests/Mimi.Tests.csproj -c Release --no-restore`: 100 passed, 0 failed, 0 skipped. |
| Working tree / process | `PASS` | Checkout retest `2137840` sạch; Mimi PID 20108 vẫn Responding tại lần kiểm tra cuối. |

### Cách chạy Repeat All

Bộ fixture Acceptance S1 có ba MP3 Cyan, Magenta, Amber, mỗi bài khoảng 30 giây. Đã cô lập queue kiểm thử khỏi bài cũ `merrygoround.mp3`, đưa Cyan làm bài hiện tại, để bài phát và đọc trạng thái persisted trong `%LOCALAPPDATA%\\Mimi\\settings.json`. Dữ liệu hiển thị cho thấy ba fixture lần lượt trở thành CurrentTrack, queue giảm về 0 rồi được tạo lại khi chu kỳ mới bắt đầu.

Thứ tự quan sát thực tế là Cyan → Amber → Magenta → Cyan do thứ tự nguồn hiển thị trong thư viện tại phiên thử; tiêu chí Repeat All được xác nhận là mọi fixture trong context được phát hết một lượt và queue quay vòng về một bài khác, không yêu cầu thứ tự tên file cụ thể.

### Kết luận cập nhật

- Có thể nâng bằng chứng vận hành Shuffle/Repeat từ phần còn thiếu trong closeout lên `PASS` ở phạm vi runtime/state nêu trên.
- Không thay đổi trạng thái P4-A (các điều khiển Quick Settings không hiển thị vẫn BLOCKED/N/A), P4-E (BLOCKED), P4-G/D-03 (PARTIAL), hoặc P4-H (OPEN).
- Không tuyên bố xác nhận chất lượng âm thanh đầu ra trực tiếp từ telemetry. Repeat One đặc biệt vẫn cần kiểm tra nghe/loopback nếu tiêu chí nghiệm thu yêu cầu xác nhận không chồng luồng âm thanh.
- Phase 4 vẫn chưa đóng; không tạo waiver và không đánh dấu Phase 4 DONE.

## Bổ sung rà soát P4-E và xác minh build — 31/10/2026

### Rà soát đường đi xử lý khi không có output (code review, không phải acceptance)

Đã đọc lại implementation tại checkpoint 2137840:

1. AudioService.Load() giải mã file trước, giữ reader/visualizer đã load ở trạng thái pause và gọi TryCreateOutputLocked(). Nếu Windows không có output dùng được, lỗi output không bị biến thành lỗi file nhạc.
2. TryCreateOutputLocked() thử WASAPI Shared; ở chế độ Automatic, nếu WASAPI không mở được thì thử WaveOut. Chỉ khi các backend cho phép đều không mở được mới đặt AudioOutputStatus.Unavailable và trả false.
3. Play() thử tạo output lại; nếu vẫn không thành công thì ném AudioOutputUnavailableException mang trạng thái unavailable.
4. MainWindowViewModel.TryPlayTrack() bắt riêng exception này. Với file đã load, view model giữ CurrentTrack/queue occurrence, để bài ở trạng thái pause, không đánh dấu track corrupt/missing và hiển thị thông báo output.

Đây là đánh giá luồng code, **không** chứng minh hành vi trên Windows khi CoreAudio thật sự không còn render endpoint. Bộ unit tests hiện có kiểm tra kiểu trạng thái/exception/parser, nhưng chưa có integration test cho AudioService + Windows CoreAudio trong điều kiện không output. Vì vậy P4-E tiếp tục là **BLOCKED**, không nâng trạng thái.

### Xác minh tự động bổ sung

- dotnet test tests/Mimi.Tests/Mimi.Tests.csproj -c Release --no-restore: **100 passed, 0 failed, 0 skipped**.
- dotnet build Mimi.sln -c Release --no-restore -p:OutDir=C:\Temp\MimiBuildCheck\: **Build succeeded, 0 warnings, 0 errors**. Output được tách khỏi thư mục chạy app vì Mimi PID 20108 đang giữ lock các DLL trong Release output mặc định; không đóng hoặc khởi động lại tiến trình Mimi.
- Git diff của tài liệu được kiểm tra bằng git diff --check; không sửa source code.

### P4-G/D-03 — kết luận không thay đổi

Các capture loopback số là phép đo callback/buffer trong đường âm thanh số, không phải latency từ lệnh Play đến âm thanh vật lý. Các lần đo qua Microphone Array có kết quả không nhất quán (có lần không phát hiện tone dù người dùng nghe được, có lần onset 387 ms/2370 ms); các con số này không đủ làm phép so sánh latency WASAPI–WaveOut. Không suy diễn hoặc lấy trung bình các lần mic không hợp lệ để tạo một con số latency. P4-G/D-03 vẫn **PARTIAL**; cần một phương pháp đo acoustic onset đồng bộ và lặp lại đáng tin cậy, hoặc quyết định giữ kiến trúc tạm thời mà không tuyên bố latency đã nghiệm thu.

### Giới hạn an toàn trong lượt này

Không disable endpoint/driver, không đổi default audio role, không đổi profile người dùng, không đóng Mimi và không sửa code. P4-E chỉ có thể được nghiệm thu sau khi có một lần chạy app-level trên môi trường không còn render endpoint và xác minh lại trạng thái endpoint được phục hồi đầy đủ.

### Kiểm tra khả năng dựng môi trường cô lập cho P4-E — 31/10/2026

Đã kiểm tra host Yami trước khi thực hiện bất kỳ thay đổi audio nào:

- Host là Windows 10 Home Single Language, build 26300, Acer Nitro ANV15-41.
- Hypervisor được báo hiện diện, nhưng WindowsSandbox.exe không tồn tại; các công cụ/cmdlet quản lý Hyper-V, VirtualBox và VMware không có trong PATH/session.
- Truy vấn Windows Optional Features cần elevation; phiên remote hiện tại không có quyền admin để kiểm tra/bật feature.
- Hệ thống có nhiều render endpoint vật lý và ảo (Realtek, AMD/NVIDIA HDMI, VB-Audio, AudioRelay). Vì vậy việc disable hàng loạt endpoint/driver trên host sẽ ảnh hưởng âm thanh hệ thống và không phải một phép thử cô lập.
- Mimi PID 20108 vẫn Responding; không thay đổi default endpoint, không disable/remove thiết bị, không đổi environment variable của tiến trình hiện tại và không restart ứng dụng.

Kết luận: chưa có Windows VM/Sandbox độc lập khả dụng trên host này. Không thể chứng minh P4-E bằng cách giả lập mất toàn bộ endpoint trên máy đang sử dụng mà không gây rủi ro cho audio của người dùng. P4-E tiếp tục BLOCKED. Đường kiểm thử an toàn tiếp theo là chạy acceptance trong một Windows VM/PC phụ có snapshot/rollback, hoặc bổ sung một output-factory seam có thể inject backend failure trong integration tests (ghi rõ đây là kiểm thử fault-injection, không thay thế CoreAudio acceptance thật).

## Cập nhật fault-injection P4-E - 31/10/2026

Đã bổ sung seam nội bộ cho `AudioService` để inject hai factory tạo output. Constructor production không đổi cách sử dụng: vẫn đọc `MIMI_AUDIO_OUTPUT`, khởi tạo Windows device watcher và dùng các factory NAudio mặc định. Constructor seam chỉ internal, cho phép test tắt watcher và mô phỏng lỗi backend mà không thay đổi endpoint/driver trên host.

Hai integration tests mới gọi trực tiếp `AudioService` thật và dùng WAV PCM tổng hợp ngắn:
- **Forced WASAPI:** factory WASAPI luôn ném lỗi; xác nhận WaveOut không được gọi, `Load` vẫn giữ duration/reader, output status là unavailable, `Play` ném `AudioOutputUnavailableException` và track vẫn ở trạng thái pause.
- **Automatic:** mô phỏng cả WASAPI lẫn WaveOut đều không mở được; xác nhận cả hai factory được thử, trạng thái unavailable được báo, track vẫn được decode/giữ lại và `Play` ném exception chuyên biệt.

Kết quả chạy `dotnet test tests/Mimi.Tests/Mimi.Tests.csproj -c Release --no-restore`: **102 passed, 0 failed, 0 skipped** (bao gồm 2 test mới). Đây là **integration test có fault injection** qua ranh giới AudioService/output factory, không phải mô phỏng đầy đủ Windows CoreAudio và cũng chưa kiểm tra UI/MainWindowViewModel trên máy thật. Do chưa có môi trường Windows thực sự không có render endpoint, P4-E vẫn **BLOCKED**; không được nâng thành PASS. Không disable endpoint/driver, không đổi default device/profile và không restart Mimi đang chạy.


## Cập nhật kiểm thử MainWindowViewModel P4-E - 31/10/2026

Đã bổ sung WPF/STA test harness vào `Mimi.Tests` (target Windows 10.0.19041 + `UseWPF`) và tham chiếu trực tiếp `Mimi.App`. Bài test gọi command public `PlayTrackCommand` của `MainWindowViewModel` trên STA thread thật, dùng fake service/player và không mở dialog hệ thống hoặc ghi vào thư mục dữ liệu người dùng.

Kịch bản kiểm thử:
1. Fake player nhận `Load` thành công nhưng `Play` ném `AudioOutputUnavailableException`.
2. Xác nhận `CurrentTrack` và `SelectedTrack` vẫn là Cyan; Magenta vẫn ở Upcoming, không bị queue skip.
3. Track không bị đánh dấu playback error, `HasError` hoặc `IsMissing`; trạng thái UI là paused và output label/notice đúng.
4. Xác nhận cảnh báo có tiêu đề `Audio output` và thông báo cho biết bài hát đã được load.
5. Phát sự kiện output trở lại; WPF Dispatcher xử lý cập nhật trạng thái; thông báo chuyển thành hướng dẫn nhấn Play.
6. Play lại thành công; trạng thái playing được cập nhật, notice được xóa và Upcoming vẫn giữ Magenta.

Kết quả:
- `dotnet test tests/Mimi.Tests/Mimi.Tests.csproj -c Release --no-restore -p:OutDir=C:\Temp\MimiP4ViewModelTest\`: **103 passed, 0 failed, 0 skipped**.
- `dotnet build Mimi.sln -c Release --no-restore -p:OutDir=C:\Temp\MimiP4ViewModelBuild\`: **0 warnings, 0 errors**.
- Build/test dùng output directory tách biệt vì Mimi PID 20108 đang giữ khóa các DLL trong App output; không restart/đóng ứng dụng.
- Không thao tác với endpoint/driver hoặc default audio device.

Đây là kiểm thử WPF ViewModel với fake audio player và integration test có fault injection ở tầng AudioService. Nó xác minh ứng xử ứng dụng/UI khi exception được ném ra, nhưng không thay thế nghiệm thu Windows CoreAudio thật khi không có render endpoint. Vì vậy P4-E vẫn **BLOCKED**, không được đánh dấu PASS cho đến khi chạy trên Windows VM hoặc máy phụ có thể cô lập endpoint an toàn.


## P4-E/P4-F playback interruption and explicit recovery test - 31/10/2026

Added `tests/Mimi.Tests/AudioServiceDeviceRecoveryTests.cs`. The test uses a fake `IWavePlayer` that consumes the real `IWaveProvider` supplied by `AudioService`, so the underlying `AudioFileReader` advances as it would while an output device is consuming audio.

Scenario and assertions:
1. Load a 10-second WAV fixture through the real `AudioService` using forced WASAPI factory injection (WaveOut fallback deliberately disabled).
2. Seek to 2 seconds, start playback, and have the fake output consume 1 second of provider data. Verify the reader position reaches approximately 3 seconds.
3. Raise NAudio's `PlaybackStopped` callback with `COMException` HRESULT `0x88890004` (`AUDCLNT_E_DEVICE_INVALIDATED`), representing an invalidated render endpoint.
4. After the debounced recovery attempt, the injected WASAPI factory reports that the endpoint is still disconnected. Verify output status becomes unavailable, playback stays stopped, the interruption event is raised, and the reader position is retained within 60 ms.
5. Call `Play()` explicitly after the factory makes a replacement output available. Verify output status returns to WASAPI shared, playback starts only after that explicit action, and another 500 ms of provider consumption advances from the retained position.

Results:
- Targeted recovery test: **1 passed, 0 failed**.
- Full test suite: **104 passed, 0 failed, 0 skipped**.
- Full Release solution build: **0 warnings, 0 errors**.
- `git diff --check`: PASS.
- All build outputs were directed to `C:\Temp\MimiP4DeviceRecoveryTest\` and `C:\Temp\MimiP4DeviceRecoveryBuild\`; the running Mimi process was not restarted and no physical audio endpoint or driver was changed.

Evidence boundary: this is an integration test of the real `AudioService` and reader/provider path using deterministic fake output objects and the Windows device-invalidated HRESULT. It does not unplug a physical endpoint, exercise the COM `WindowsAudioDeviceWatcher` notification callback, or establish acoustic output. Therefore P4-E remains **BLOCKED** for real endpoint-less Windows CoreAudio acceptance. This test supports the engine's interruption/position-retention/explicit-retry behavior but is not a substitute for physical device-loss acceptance.


## Integrated AudioService + MainWindowViewModel interruption test - 31/10/2026

Added an STA/WPF integration test to `tests/Mimi.Tests/MainWindowViewModelNoOutputTests.cs`. Unlike the earlier ViewModel-only test, this test wires the real `AudioService` into the real `MainWindowViewModel`, with only the low-level `IWavePlayer` output factory replaced by deterministic fakes. The fake output consumes the real audio provider so the real reader position advances.

Scenario and assertions:
1. Load and play a generated 10-second Cyan WAV through the ViewModel; Magenta remains the next queue item.
2. Consume one second from the real provider, then raise NAudio `PlaybackStopped` with `AUDCLNT_E_DEVICE_INVALIDATED (0x88890004)`.
3. Let AudioService's debounced rebuild fail once to represent the endpoint remaining disconnected. Pump the WPF Dispatcher and verify Cyan remains CurrentTrack, Magenta remains Upcoming, playback is paused, the UI output label/notice report the interruption, and Cyan is not marked missing or corrupt.
4. Verify the interruption event causes `PlaybackSession` to be saved with Cyan's path, retained position (within 60 ms), and Magenta in UpcomingTrackPaths.
5. Invoke the public ViewModel Play command after the injected factory supplies a replacement output. Verify the third factory call succeeds, playback resumes from the retained position, the notice clears, and the queue remains unchanged.

Results on the isolated retest worktree:
- Targeted integrated test: **1 passed, 0 failed**.
- Full test suite: **105 passed, 0 failed, 0 skipped**.
- Full Release solution build: **0 warnings, 0 errors**.
- Build/test outputs were isolated under `C:\\Temp\\MimiP4IntegratedRecoveryTest\\`, `C:\\Temp\\MimiP4IntegratedRecoveryFullTest\\`, and `C:\\Temp\\MimiP4IntegratedRecoveryBuild\\`.
- No physical endpoint/driver/default-device changes; running Mimi process was not restarted.

Evidence boundary: this confirms the real AudioService + ViewModel + WPF Dispatcher + in-memory settings persistence path under injected device-loss conditions. It still does not exercise the real COM `WindowsAudioDeviceWatcher` callback, unplug a physical endpoint, or prove acoustic output. P4-E therefore remains **BLOCKED** pending safe real CoreAudio endpoint-less acceptance.


## P4-E watcher notification/debounce verification — 31/10/2026

Đã bổ sung WindowsAudioDeviceNotificationTests để kiểm tra an toàn hai phần còn thiếu ở tầng watcher/service:

- Gọi trực tiếp COM callback adapter AudioDeviceNotificationClient và xác nhận Capture bị bỏ qua; Render/Console và Render/Multimedia được chuyển tiếp; DeviceAdded/Removed/StateChanged giữ đúng loại sự kiện, device ID và state.
- Phát một burst gồm 5 sự kiện DefaultChanged cách nhau 40 ms vào handler của AudioService; xác nhận timer debounce chỉ tạo đúng một output replacement sau burst, output cũ được dispose đúng một lần và không có lần khởi tạo output lặp sau 500 ms yên tĩnh.

Kết quả: targeted tests 2/2 PASS; full suite 107 passed / 0 failed / 0 skipped. Test debounce gọi handler nội bộ bằng reflection và dùng fake IWavePlayer; test callback gọi trực tiếp adapter, không đăng ký COM với CoreAudio thật. Do đó chúng xác nhận mapping + logic debounce/cleanup ở mức unit/integration có fault injection, không chứng minh Windows thực sự phát callback qua COM hoặc việc rút/cắm endpoint vật lý. P4-E vẫn BLOCKED cho tới khi có acceptance an toàn trên Windows VM/PC phụ hoặc host thử nghiệm có rollback.

Không disable endpoint/driver, không đổi default device, không restart Mimi PID 20108. Các test/build output được tách khỏi output đang chạy.


## P4-E watcher lifecycle/disposal review — 31/10/2026

Rà soát vòng đời COM watcher và timer/service đã phát hiện một điểm cải thiện an toàn: cờ disposed của WindowsAudioDeviceWatcher trước đây là bool thường và Dispose không được bảo vệ khỏi hai lời gọi đồng thời. Đã chuyển cờ sang volatile và dùng Interlocked.Exchange để chỉ một luồng thực hiện unregister/dispose enumerator; callback đang chạy hoặc callback đến muộn quan sát được trạng thái disposed và không phát event mới.

Bổ sung test AudioService_IgnoresQueuedDeviceNotificationsAfterDispose: sau Dispose hai lần, phát 100 notification giả qua handler nội bộ và chờ qua debounce window; xác nhận không tạo output mới và output hiện hữu chỉ Dispose một lần. Kết quả watcher test: **3/3 PASS**.

Giới hạn: đây là kiểm tra concurrency/lifecycle ở managed boundary với callback giả lập; không giả lập race bên trong COM runtime, không xác minh UnregisterEndpointNotificationCallback trên CoreAudio thật và không tháo endpoint vật lý. P4-E vẫn **BLOCKED** cho acceptance endpoint-less thật. Không thay đổi endpoint/driver/default device và không restart Mimi đang chạy.


## P4-E COM callable-wrapper contract hardening - 31/10/2026

Rà soát thêm hình dạng COM của `AudioDeviceNotificationClient`. Class đã được đánh dấu `[ComVisible(true)]` và triển khai `IMMNotificationClient`, nhưng chưa tắt auto-generated class interface. Đã thêm `[ClassInterface(ClassInterfaceType.None)]` để COM chỉ nhìn thấy interface được triển khai tường minh, tránh expose class interface tự sinh không cần thiết.

Bổ sung regression test kiểm tra metadata reflection: class COM-visible, class interface là `None`, và `IMMNotificationClient` vẫn được implement. Full test suite: **109 passed, 0 failed, 0 skipped**. Full Release solution build: **0 warnings, 0 errors**; `git diff --check` PASS. Build/test output được cô lập dưới `C:\Temp\MimiWatcherLifecycleTest\`.

Giới hạn bằng chứng: test xác minh metadata/managed adapter, không tạo COM callable wrapper thực tế, không gọi `RegisterEndpointNotificationCallback` / `UnregisterEndpointNotificationCallback` trên CoreAudio và không rút endpoint vật lý. P4-E vẫn **BLOCKED** cho acceptance CoreAudio thật. Không đổi endpoint/driver/default device và không restart Mimi PID 20108.


## P4-E real CoreAudio watcher registration smoke test - 31/10/2026

Sau khi harden COM interface, đã tạo harness tạm ngoài repository tại C:\Temp\MimiComWatcherSmoke, load assembly Release của Mimi.Audio và khởi tạo watcher nội bộ bằng reflection. Trên Windows host Yami, RegisterEndpointNotificationCallback thành công (IsMonitoringAvailable=True); UnregisterEndpointNotificationCallback và dispose enumerator hoàn tất không exception. Lặp **20 chu kỳ** khởi tạo/đăng ký/hủy đăng ký; cả 20/20 đều thành công, mỗi watcher được gọi Dispose hai lần để xác nhận idempotency.

Đây là smoke test COM/CoreAudio thật cho vòng đời register/unregister, mạnh hơn test metadata/fault injection trước đó. Tuy nhiên harness không chờ hoặc xác minh callback do Windows phát ra khi endpoint đổi, không tháo/cắm endpoint và không thử tình huống endpoint-less. Không thay đổi endpoint, driver hay default device; Mimi PID 20108 không bị restart. P4-E vẫn **BLOCKED** cho acceptance mất output vật lý và callback thực tế.


## P4-E concurrent COM watcher disposal stress - 31/10/2026

Mở rộng smoke harness ngoài repository để kiểm tra tranh chấp Dispose thật trên CoreAudio: lặp 100 lần tạo watcher và đăng ký COM thành công; mỗi watcher nhận 16 lời gọi Dispose đồng thời từ 16 Task. Kết quả **100/100 watcher đăng ký thành công; 1.600 lời gọi Dispose đồng thời không exception**. Đây xác nhận Interlocked guard bảo vệ đường unregister/dispose dưới cạnh tranh đa luồng trên host hiện tại.

Sau stress test, chạy lại toàn bộ test suite: **109 passed, 0 failed, 0 skipped**; Release solution build: **0 warnings, 0 errors**; `git diff --check` PASS. Output cô lập tại C:\Temp\MimiWatcherFinalTest và C:\Temp\MimiWatcherFinalBuild. Mimi PID 20108 vẫn Responding; không thay đổi endpoint/driver/default audio device, không restart app.

Giới hạn: test stress chủ động gọi Dispose đồng thời, không đồng thời kích hoạt callback COM do Windows phát ra. Chưa xác minh callback vật lý khi endpoint đổi/mất và chưa làm endpoint-less test. P4-E vẫn **BLOCKED**.

## P4-E notification subscriber exception boundary - 31/10/2026

Added a regression test that creates the production WindowsAudioDeviceWatcher, attaches a deliberately throwing Changed subscriber, and invokes the notification client adapter. The exception is contained at the watcher notification boundary and does not escape the callback adapter. The test does not claim Windows emitted a real endpoint notification; it validates exception containment after the adapter is invoked. Full test suite: 110 passed, 0 failed, 0 skipped. Release solution build: 0 warnings, 0 errors. `git diff --check` passed. No endpoint, driver, default-device, or running Mimi process changes. P4-E remains BLOCKED pending a controlled real endpoint notification and endpoint-less acceptance.
