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
