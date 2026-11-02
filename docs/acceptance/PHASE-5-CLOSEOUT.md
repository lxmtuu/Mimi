# Phase 5 closeout — tổng kết đóng phase tại `7ef2a09`

Tài liệu này tổng kết việc **đóng Phase 5 (Song ngữ & Theme cộng đồng,
`v0.6.0`)** ngày 02/11/2026, đối chiếu với bằng chứng đã ghi trong
[`ROADMAP.md`](../../ROADMAP.md) và các xác nhận trực tiếp của chủ dự án
trên Windows thật. `ROADMAP.md` vẫn là hồ sơ quyết định/trạng thái chính
thức; tài liệu này là ảnh chụp tổng kết tại thời điểm đóng.

Phase 5 chỉ được mở sau khi chủ dự án **đóng Phase 4 theo waiver**
(01/11/2026). Việc đóng Phase 5 **không** thay đổi trạng thái của các mục
đã miễn trừ ở Phase 4: P4-A vẫn `PARTIAL`, P4-E vẫn `BLOCKED`, P4-G vẫn
`PARTIAL` và quyết định D-03 (WASAPI Shared là mặc định tạm thời, WaveOut
là fallback) vẫn chưa chốt cuối. Chi tiết ở
[PHASE-4-CLOSEOUT.md](PHASE-4-CLOSEOUT.md).

## Checkpoint đóng phase

| Hạng mục | Giá trị |
|---|---|
| Branch | `arena/cdda374d-mimi` |
| Phiên bản app | `0.6.0` (`src/Mimi.App/Mimi.App.csproj`) |
| Code checkpoint cuối | `68bfb31` — mascot Mimi nguyên bản v1 |
| Checkpoint hồ sơ đóng phase | `7ef2a09` — chủ dự án duyệt mascot trên bản chạy thật |
| Windows CI tại `68bfb31` | [41454942516](https://github.com/lxmtuu/Mimi/actions/runs/41454942516) — success |
| Kết luận Phase 5 | **Đóng — các lát tính năng hoàn thành, CI xanh và đã nghiệm thu trên Windows thật theo báo cáo của chủ dự án** |

## Các lát của Phase 5

| Lát | Nội dung đã giao | Checkpoint chính | Bằng chứng nghiệm thu |
|---|---|---|---|
| P5-L1/L2/L3 — Song ngữ VI/EN | Nền `.resx` + chọn ngôn ngữ áp dụng ngay (settings schema v4); mở rộng 260 → 311 khóa phủ `MainWindow`/`StageWindow`/`TextPromptWindow`, hộp thoại chọn file/thư mục, prompt playlist, nhãn động, hộp thoại lỗi còn lại, sentinel bộ lọc, placeholder metadata và thông điệp đầu ra âm thanh theo mã loại | `7110bfc`, `296097f`, `ae8fd2b`, `102db47` | Test parity khóa VI/EN; chủ dự án xác nhận trực quan EN↔VI trên Windows thật tại `d3a0f2d` (ghi ở `4231ffb`) |
| P5-F1 — Font Be Vietnam Pro | Đóng gói 5 face upright 400–800 kèm OFL, đặt làm font UI mặc định để dấu tiếng Việt hiển thị nhất quán | `18458eb`, `59227f1` | `BundledFontTests` kiểm chứng weight + glyph tiếng Việt; chủ dự án xác nhận hiển thị/fallback trên Windows thật |
| P5-T1 — Theme Engine + 4 theme sẵn có | Token màu dùng chung, 4 theme tối `neon-stage`/`sakura-dream`/`midnight-live`/`mono-focus`, chọn theme áp dụng ngay trong Settings (settings schema v5, 318 khóa) | `518b13a`, `4af48e1`, `d3a0f2d` | CI [41285453093](https://github.com/lxmtuu/Mimi/actions/runs/41285453093); test tương phản AA cho các cặp chữ/nền chính; chủ dự án xác nhận chuyển theme + giữ theme sau restart (`4231ffb`) |
| Tương tác một click (phát sinh theo yêu cầu chủ dự án, thay thiết kế double-click cũ) | Một click trái thường lên dòng bài hát/hàng đợi là chuyển Now Playing và phát ngay — kể cả cú click đầu tiên sau khi mở app; Ctrl/Shift chỉ chọn; kéo thả hủy; nút lồng không kích hoạt; click bài đang chọn chỉ resume, không phát lại từ đầu. Kèm giảm khựng khi chuyển bài (tái dùng output cùng định dạng) và chuỗi vá độ tin cậy startup/click | `0e968c5`; `6ddd854`+`a6a9c4e`; `a6fa18d` → hotfix hồi quy `DeferRefresh` `1bf234a`+`d17c0a3`; `7e5f64d`+`e93fbb0`; `af97d12` | CI tại từng checkpoint (`41290251034`, `41291808205`, `41295379738`); test tích hợp `ListView` thật → `PlayTrackCommand`; chủ dự án xác nhận trên Windows thật, đóng lỗi tại `2154ff5` |
| P5-T2 — Community Theme Loader | Nạp theme JSON schema v1 từ `%LocalAppData%\Mimi\themes`, kiểm chứng đầy đủ 33 token, không đè id theme sẵn có, tải lại nóng từ Settings; tài liệu [`docs/COMMUNITY-THEMES.md`](../COMMUNITY-THEMES.md) + theme mẫu [`docs/examples/sunset-drive.json`](../examples/sunset-drive.json) (321 khóa) | `a2f6195`, `4c7fb9f`, tài liệu `9afa101` | CI [41449065769](https://github.com/lxmtuu/Mimi/actions/runs/41449065769); test loader/catalog/service/ViewModel đầu-cuối; chủ dự án thử theme mẫu trên Windows thật, đóng tại `a177b91` |
| Quét hard-coded ngôn ngữ toàn diện | Rà soát toàn bề mặt hiển thị (XAML, ViewModel/code-behind, hộp thoại file, nhãn quét/tìm kiếm, thông điệp audio); dọn 4 giá trị khởi tạo tiếng Anh trùng lặp resx; thêm test gác cổng `HardcodedUiStringTests` quét XAML + field nhãn với danh sách cho phép tường minh (brand, tên phím, tên ngôn ngữ tự gọi, glyph, đơn vị, verb Explorer) | `b850e45`, `ec4a70e`, tài liệu `84d182f` | CI [41451707352](https://github.com/lxmtuu/Mimi/actions/runs/41451707352); kết quả quét: mọi chuỗi hiển thị đều qua `Loc[...]`/`Loc.Format` |
| Mascot Mimi nguyên bản v1 | Nhân vật chibi sticker gốc của dự án (AI-assisted, chủ dự án chọn phương án), đóng gói WPF Resource 768×768 (~460 KB), hiển thị ở hero Trang chủ và cạnh nhãn phiên bản trong Settings; khóa trợ năng mới đưa bộ khóa lên **322** | `68bfb31`, tài liệu `ff3945c` | CI [41454942516](https://github.com/lxmtuu/Mimi/actions/runs/41454942516); test tài nguyên đóng gói đọc `.g.resources`; chủ dự án duyệt trên bản chạy thật (`7ef2a09`) |

## Cổng chất lượng xuyên suốt phase

- **Windows CI** (Release build + toàn bộ unit test) xanh tại mọi
  checkpoint được chốt làm bằng chứng trong phase.
- **Parity song ngữ**: `LocalizationServiceTests` bảo đảm tập khóa
  neutral và `vi` luôn bằng nhau; bộ khóa cuối phase là **322**.
- **Chống tái phát hard-coded**: `HardcodedUiStringTests` chạy trong CI.
- **Tương phản theme**: các theme sẵn có có test đạt mức AA cho các cặp
  chữ/nền chính; theme cộng đồng được kiểm chứng cấu trúc/định dạng màu
  (tương phản là trách nhiệm của tác giả theme, có hướng dẫn trong
  tài liệu).
- **Tài nguyên đóng gói** (font, mascot) đều có test đọc trực tiếp từ
  assembly.

## Không thuộc Phase 5 / còn tồn sau khi đóng

- **Light theme**: mục tùy chọn, hoãn vô thời hạn — cơ chế theme cộng
  đồng đã cho phép người dùng tự tạo theme sáng. Không tính là nợ của
  Phase 5.
- **Nợ miễn trừ Phase 4** giữ nguyên: P4-A `PARTIAL` (timeline/seek hiển
  thị trên Quick Settings), P4-E `BLOCKED` (kịch bản Windows mất hẳn mọi
  render endpoint), P4-G `PARTIAL` + D-03 tạm thời (đo độ trễ vật lý).
  Đóng Phase 5 không nâng các mục này lên `PASS`.
- **Tích hợp & phát hành chưa quyết**: toàn bộ sản phẩm vẫn nằm trên
  nhánh `arena/cdda374d-mimi`; `main` vẫn ở commit khởi tạo; chưa có
  PR/merge/tag `v0.6.0`; repo chưa có file LICENSE; chưa có cơ chế đóng
  gói cho người dùng cuối (hiện build/chạy từ mã nguồn theo README).
  Đây là các quyết định hậu-Phase 5 thuộc chủ dự án.
- **Mascot mới ở mức v1** (một tư thế, hai vị trí hiển thị); mở rộng
  thêm là việc tương lai tùy chọn. Các log chẩn đoán một lần phục vụ điều
  tra lỗi click được giữ lại có chủ đích.

## Kết luận

Phase 5 **đóng** tại `7ef2a09` ngày 02/11/2026: toàn bộ lát tính năng
trong phạm vi (song ngữ, font, Theme Engine, theme cộng đồng, tương tác
một click phát sinh trong phase, quét hard-coded, mascot) đã hoàn thành
với CI xanh và xác nhận nghiệm thu trực tiếp của chủ dự án trên Windows
thật. Các tồn đọng liệt kê ở trên thuộc phạm vi hậu-Phase 5 hoặc nợ miễn
trừ Phase 4 và không bị ảnh hưởng bởi quyết định đóng này.
