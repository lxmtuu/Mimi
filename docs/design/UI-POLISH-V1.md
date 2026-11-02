# Rà soát thẩm mỹ & bố cục giao diện Mimi — đề xuất UI Polish v1

Tài liệu này ghi kết quả rà soát trực quan ngày 02/11/2026 đối chiếu trên
ảnh chụp bản chạy thật tại `7f38323` (Windows, theme Neon Stage, tiếng
Anh, cửa sổ ~1320 DIP) và mã `MainWindow.xaml` hiện hành. Đây là **đề
xuất chờ chủ dự án duyệt**, chưa phải quyết định đã chốt trong
`ROADMAP.md`.

Nguyên tắc bất biến khi thực hiện:

- Mọi màu phải đi qua token theme + `DynamicResource` như hiện tại, để cả
  4 theme sẵn có **và theme cộng đồng** đều đẹp theo palette riêng —
  không hard-code màu ngoài token.
- Không thêm chuỗi hiển thị hard-coded (test gác cổng
  `HardcodedUiStringTests` đang bảo vệ việc này); nút chuyển từ chữ sang
  icon vẫn phải giữ `AutomationProperties.Name` + `ToolTip` song ngữ.
- Không đổi hành vi/phím tắt/luồng phát nhạc; đây là đợt làm đẹp thuần
  trình bày. Nghiệm thu bằng ảnh chụp trước/sau trên Windows thật ở 2
  kích thước cửa sổ × 2 theme × VI/EN.

## Ấn tượng chung & điểm đang tốt (giữ nguyên)

- Bố cục 3 cột (điều hướng · thư viện · Now Playing) đúng chất trình
  nghe nhạc desktop hiện đại, dễ định hướng.
- Font Be Vietnam Pro render sạch; hệ màu navy + cyan nhất quán, mascot
  đã có mặt ở hero tạo điểm nhận diện riêng.
- Nút Play lớn màu accent là điểm nhấn đúng chỗ; thẻ hero bo góc lớn +
  gradient tạo cảm giác "sân khấu" như định hướng thiết kế.

Vấn đề chính không nằm ở ý tưởng mà ở **độ hoàn thiện**: vài lỗi bố cục
nhỏ làm chữ dính/tràn, một số control còn mang template mặc định của WPF
(không theo theme), và hệ thống icon/khoảng cách/bán kính bo chưa nhất
quán nên tổng thể chưa "gọn" như tiềm năng của nó.

## Nhóm A — Lỗi gọn gàng, nên sửa ngay (P0)

### A1. Chữ dính nhau do `DockPanel` thiếu `LastChildFill="False"` (lỗi hệ thống)

`DockPanel` mặc định cho phần tử con cuối cùng lấp đầy phần còn lại bất
kể `DockPanel.Dock="Right"`, khiến nhãn bên phải dính sát nhãn bên trái
thay vì nằm ở mép phải. Trong ảnh chụp thấy rõ:

- `VOLUME71%` (Now Playing) — nhãn phần trăm dính vào chữ VOLUME.
- `UP NEXT0 songs` — số lượng dính vào tiêu đề.
- Footer: `…DEVELOPMENT BUILD` + `A personal music space` dính thành
  `BUILDA personal…`.

Cùng mẫu lỗi còn xuất hiện ở hàng thời gian tiến trình (`0:00` trái/phải),
nhãn Volume trong Settings và hàng FPS của Stage engine. Đáng chú ý là
codebase đã có chỗ làm đúng (`DockPanel LastChildFill="False"` ở khu tiến
trình quét) — chỉ cần rà soát mọi `DockPanel` dạng trái/phải và thống
nhất một mẫu. Đây là lỗi rẻ nhất mà hiệu quả "gọn" cao nhất.

### A2. Hàng nút điều khiển phát nhạc bị tràn cột

Hàng `−10s · ⏮ · ▶ · ⏭ · +10s` dùng `StackPanel` ngang căn giữa với
padding lớn; ở bề rộng cửa sổ mặc định, hai nút ±10s bị cắt mép trong
ảnh chụp. Đề xuất: thu gọn bằng icon (xem B4) và/hoặc chuyển sang lưới
cân đối để không bao giờ tràn; đồng thời kiểm tra lại ở `MinWidth`.

### A3. Bảng thư viện: tiêu đề cột bị cắt, căn lề chưa thống nhất

- Cột ảnh bìa rộng 54px nhưng tiêu đề `COVER` bị cắt thành `COVE`; cột
  `FORMAT` cũng bị cắt mép. Hoặc bỏ chữ tiêu đề cột bìa (chỉ để trống/
  icon), hoặc nới cột.
- Chữ tiêu đề cột đang căn giữa trong khi nội dung ô căn trái → mắt phải
  "nhảy" khi quét cột. Đề xuất: cột chữ căn trái cả tiêu đề lẫn ô; cột
  số (`YEAR`, `TIME`) căn phải cả hai.
- Đường kẻ dọc giữa các cột tiêu đề tạo cảm giác lưới cứng của GridView
  cổ điển; làm mềm bằng cách bỏ kẻ dọc, giảm tương phản nền header.

### A4. Ô ảnh bìa trống khi bài hát không có bìa

`CellTemplate` cột bìa chỉ có `Image`; bài không có `CoverThumbnailPath`
thì ô trống trơn, trông như lỗi tải ảnh (cả 4 dòng trong ảnh chụp đều
trống). Đề xuất: luôn vẽ ô placeholder 36×36 bo góc bằng
`CoverGradientBrush` + ký hiệu ♫ mờ phía dưới ảnh thật — có bìa thì ảnh
đè lên, không có bìa vẫn có "ô gạch" đẹp và nhất quán. Áp dụng tương tự
cho ảnh thu nhỏ trong hàng đợi Up Next.

### A5. Ô tìm kiếm trông như một khối trống

`SearchTextBox` không có icon kính lúp, không có chữ gợi ý (placeholder)
— trong ảnh chụp nó là một phiến tối trống nổi giữa trang. Đề xuất: thêm
icon tìm kiếm bên trái + watermark song ngữ ("Tìm bài hát…" / "Search
songs…") hiện khi ô trống.

### A6. Trạng thái trống của Up Next

Khi hàng đợi trống, `ListView` cao cố định 190px chỉ còn khung viền rỗng
— trông như control hỏng. Đề xuất: chữ trạng thái trống mờ ở giữa khung
(tái dùng khóa đã có nếu phù hợp), và/hoặc thu chiều cao khi trống.

### A7. Nút chữ bị cắt trong sidebar

Hàng `Play · Rename · Delete` của playlist bị cắt thành `Renam`. Ba nút
này là ứng viên hoàn hảo để chuyển thành icon (▶ · ✎ · 🗑 dạng Path)
kèm ToolTip — vừa hết cắt chữ vừa gọn (xem B4).

## Nhóm B — Hệ thống thị giác (P1)

### B1. Thanh tiêu đề cửa sổ đang trắng, ngược tông toàn app

Đây là điểm "lệch tông" lớn nhất trong ảnh chụp: dải trắng của Windows
nằm trên một giao diện navy tối. Đề xuất mức 1 (rẻ, hiệu quả cao): bật
**immersive dark mode cho caption** qua DWM
(`DWMWA_USE_IMMERSIVE_DARK_MODE`, và trên Windows 11 đặt thêm màu
caption/chữ theo token nền app). Chưa cần custom chrome toàn phần (kéo
thả/snap tự xử lý sẽ tốn và rủi ro hơn nhiều).

### B2. ComboBox chưa được "theo theme"

Năm ô lọc (nghệ sĩ/album/thể loại/định dạng/trạng thái) đang mang mặt
nút mặc định sáng màu, chữ trắng trên nền xám nhạt, tương phản thấp và
lạc hẳn khỏi bề mặt tối xung quanh. Cần một `ControlTemplate` ComboBox
dùng brush của theme (nền `ControlBackground`, mũi tên accent, danh sách
thả xuống nền `PanelBackground` + mục chọn `SelectedBackground`). Đây
có lẽ là thay đổi đơn lẻ làm giao diện "lên đời" rõ nhất sau title bar.

### B3. Slider và thanh cuộn còn nguyên dạng mặc định

- Slider tiến trình/âm lượng: rãnh trắng mảnh, núm vuông trắng. Đề xuất
  template riêng: phần đã qua tô `Accent` (tiến trình có thể gradient
  Accent → AccentSecondary), núm tròn trắng/accent chỉ hiện rõ khi hover
  hoặc kéo — đúng ngôn ngữ của các trình nghe nhạc hiện đại.
- Thanh cuộn dọc bên phải Now Playing là scrollbar mặc định khổ lớn,
  rãnh sáng màu rất lộ. Đề xuất scrollbar mảnh (8–10px), thumb bo tròn
  theo `Border`/`ControlBackground`, áp dụng toàn app qua style ngầm
  định.

### B4. Icon hoá các nút phụ (đúng gợi ý của chủ dự án)

Giữ chữ cho hành động chính (CTA), chuyển các nút phụ/lặp lại thành icon
Path 16–20px thống nhất một nét vẽ, luôn kèm ToolTip + tên trợ năng:

| Vị trí hiện tại | Đề xuất |
|---|---|
| Playlist: `Play` `Rename` `Delete` | ▶ · ✎ (bút chì) · thùng rác |
| `−10s` / `+10s` | icon tua ngược/tiến kèm số 10 nhỏ |
| `Shuffle Off` / `Repeat Off` (nút chữ rộng) | icon shuffle/repeat dạng toggle: tắt = màu muted, bật = accent + chấm/gạch dưới; Repeat One thêm badge "1" |
| `Mute` | icon loa / loa gạch chéo theo trạng thái |
| `Clear` (bộ lọc) | icon phễu có dấu ✕ hoặc giữ chữ nhưng nhỏ gọn hơn |
| `↑` `↓` (hàng đợi) | giữ nguyên (đã là icon) nhưng đồng bộ kích thước/nét |

Lợi ích phụ: hàng điều khiển phát nhạc hết tràn (A2), sidebar hết cắt
chữ (A7), và giao diện bớt "nhiều chữ" hẳn.

### B5. Điều hướng sidebar: tách "màn hình" khỏi "hành động", rõ trạng thái chọn

Hiện Home/Library/Favorites/Recently (màn hình) và Add files/Scan
folder (hành động) cùng một kiểu nút, và trạng thái đang chọn khó phân
biệt. Đề xuất:

- Mục đang chọn: nền `SelectedBackground` + thanh accent mảnh ở mép
  trái (hoặc icon tô accent), các mục còn lại nền trong suốt, hover mới
  nổi nhẹ.
- Nhóm hành động thêm nhạc: tách bằng khoảng lặng + kiểu nút viền
  (outline) nhẹ hơn nút điều hướng, hoặc gộp thành một nút `+ Add music`
  mở menu nhỏ (Files/Folder).

### B6. Hero & mascot: bỏ cảm giác "hình dán trong hộp"

Mascot hiện nằm trong thẻ bo góc có viền mảnh; bản thân tranh đã có viền
sticker trắng + nền navy riêng nên thành hiệu ứng hai lớp khung, và hình
hơi nhỏ so với chiều cao hero trong khi vùng gradient giữa hero còn
trống nhiều. Đề xuất:

- Bỏ `BorderBrush` của thẻ, tăng kích thước (≈150–160px), thêm bóng đổ
  mềm để hình "nổi" trên gradient thay vì bị đóng khung; hoặc neo hình
  sát đáy hero cho cảm giác nhân vật đứng trong sân khấu.
- Tránh chữ mô tả bị "mồ côi" một từ ở dòng cuối (ảnh chụp: "yours." rơi
  riêng một dòng) bằng cách nới nhẹ `MaxWidth` của khối chữ khi khung
  rộng.
- (Tùy chọn) thêm 2–3 chip thông tin nhanh dưới nút CTA — ví dụ số bài
  trong thư viện — chỉ khi có dữ liệu thật, không bày số liệu trang trí.

### B7. Thống nhất bán kính bo & nhịp khoảng cách

Hiện radius dao động 8 (nút nav) · 10 · 12 (thẻ) · 16–20 (hero/cover) ·
pill (CTA) mà chưa có quy luật; padding thẻ 10/12/14/24 lẫn lộn. Đề
xuất thang chính thức: radius `8` (nút/ô nhập) · `12` (thẻ) · `16`
(thẻ lớn/cover) · `20` (hero); padding thẻ chuẩn `16`; khoảng cách theo
bội số 4. Ghi thang này vào một resource dictionary dùng chung để các
màn hình sau bám theo.

### B8. Phân cấp bề mặt rõ hơn một nấc

Nền app/panel/raised hiện chênh nhau rất ít nên các thẻ "chìm" vào nền.
Không cần đổi palette của các theme: chỉ cần dùng viền 1px `Border`
nhất quán cho thẻ chính và shadow rất nhẹ — vừa đủ để mắt tách lớp.

## Nhóm C — Điểm nhấn & cảm xúc (P2)

- **Gradient accent có kiểm soát**: nút Play và phần tiến trình đã qua
  dùng gradient `Accent → AccentSecondary`; trái tim yêu thích đang bật
  tô `AccentHot`; thanh accent cho dòng đang phát trong bảng. Vì lấy từ
  token nên mỗi theme tự có "màu sân khấu" riêng, kể cả theme cộng đồng.
- **Hero giàu sức sống hơn**: gradient hero của Neon Stage hiện hơi
  ngả xám; có thể tăng nhẹ độ bão hoà ở riêng dải Hero* mà không đụng
  các bề mặt còn lại (chỉ là tinh chỉnh giá trị token của 4 theme sẵn
  có; theme cộng đồng giữ nguyên của tác giả).
- **Now Playing khi chưa chọn bài**: thẻ visualizer đang phẳng và trống;
  cân nhắc đặt mascot nhỏ mờ sau vòng tròn sóng khi chưa có bài (ẩn đi
  khi phát nhạc có bìa) để trạng thái nghỉ vẫn có hồn.
- **Hàng đang phát trong bảng**: thêm chỉ dấu sóng nhạc động nhỏ hoặc
  thanh accent ở đầu dòng thay vì chỉ đổi nền — giúp tìm vị trí bài đang
  phát trong danh sách dài.
- **Trạng thái trống có minh hoạ**: thư viện chưa có bài / tìm kiếm
  không ra kết quả / playlist trống dùng mascot thu nhỏ + một dòng gợi ý
  hành động, thay vì khoảng trắng.
- **Chuyển động nhẹ**: fade 120–180ms khi đổi theme/chuyển view là đủ;
  tránh animation nặng làm giảm cảm giác nhanh của app desktop.

## Lộ trình thực hiện đề xuất

| Lát | Nội dung | Rủi ro/chạm |
|---|---|---|
| **UI Polish 1 — "Gọn"** | Nhóm A: sửa DockPanel dính chữ (rà toàn bộ), hàng phát nhạc hết tràn, tiêu đề cột + căn lề bảng, placeholder bìa, ô tìm kiếm có icon + watermark, trạng thái trống Up Next | Rất thấp; chủ yếu XAML trong `MainWindow.xaml`, 0–2 khóa resx mới |
| **UI Polish 2 — "Đẹp hệ thống"** | Nhóm B: title bar tối (DWM), template ComboBox/Slider/ScrollBar theo theme, icon hoá nút phụ, trạng thái chọn sidebar, thang radius/spacing, hero mascot hoà nền | Trung bình; thêm style/template dùng chung + vài Path icon; cần nghiệm thu cả 4 theme sẵn có + 1 theme cộng đồng |
| **UI Polish 3 — "Có hồn"** | Nhóm C: gradient accent có kiểm soát, chỉ dấu bài đang phát, trạng thái trống có mascot, chuyển động nhẹ | Thấp–trung bình; làm sau khi nền B ổn định |

Mỗi lát nghiệm thu bằng bộ ảnh chụp cố định: cửa sổ mặc định + cửa sổ
hẹp (gần MinWidth) × Neon Stage + một theme tương phản khác (Mono Focus)
× VI + EN, để bảo đảm không tràn chữ ở cả hai ngôn ngữ — tiếng Việt
thường dài hơn nên đây là phép thử bắt buộc sau khi icon hoá.

*Tài liệu rà soát lập ngày 02/11/2026 từ ảnh chụp của chủ dự án. Chủ dự
án đã duyệt triển khai lát 1 + lát 2; lát 3 để lại sau.*

## Trạng thái triển khai (02/11/2026)

### Lát 1 — “Gọn” (`1ecc826`)

- Đã rà và sửa các `DockPanel` trái/phải còn thiếu
  `LastChildFill="False"`: nhãn Volume, Up Next, footer, thời gian tiến
  trình, Settings và Stage metrics không còn bị kéo dính sang trái; nút
  tạo playlist không còn bị kéo rộng thành pill.
- Bảng thư viện đã bỏ tiêu đề “COVER”, chỉnh lại ngân sách bề rộng cột,
  căn trái tiêu đề/ô chữ và căn phải Year/Duration/Format. Hàng không có
  bìa nay hiện ô placeholder gradient có ký hiệu ♫ ở cả thư viện và Up
  Next.
- Ô tìm kiếm đã có icon kính lúp và watermark song ngữ; Up Next trống có
  dòng trạng thái ngay trong khung; Play/Rename/Delete của playlist đã
  thành nút icon có ToolTip + tên trợ năng. Hai khóa resx mới
  (`Library_SearchPlaceholder`, `Queue_EmptyState`) giữ đủ parity
  324/324 khóa EN/VI.
- Windows CI `41460739188` xanh (Release build + toàn bộ unit test).

### Lát 2 — “Đẹp hệ thống” (`4beb0d3` + sửa XAML `7015f6d`)

- `WindowChromeHelper` bật immersive dark title bar qua DWM cho cửa sổ
  chính và hộp thoại nhập tên; trên Windows 11 màu caption/chữ caption
  còn lấy trực tiếp từ token theme đang dùng và được áp lại khi đổi
  theme. Đây là xử lý best-effort: nếu DWM không khả dụng, app giữ
  caption hệ thống và vẫn chạy bình thường. Stage toàn màn hình vốn
  không có caption nên không đổi.
- Thêm `Themes/ControlStyles.xaml` dùng chung cho ComboBox/ComboBoxItem,
  Slider, ScrollBar và màu ProgressBar; mọi màu đi qua brush/token theme
  nên theme sẵn có và theme cộng đồng đều đổi theo trực tiếp. Cùng tệp
  ghi thang radius dùng chung (`8/12/16/20`).
- Shuffle/Repeat/Mute đã thành nút icon gọn: trạng thái bật dùng accent
  và nền selected, Repeat One có badge “1”, ToolTip vẫn hiển thị nhãn
  trạng thái đã bản địa hóa. Hàng transport được giảm padding/margin để
  nằm gọn trong cột Now Playing. Riêng `−10s/+10s` giữ dạng chữ/số nhỏ
  vì nghĩa trực tiếp và vẫn đủ gọn sau khi co kích thước.
- Sidebar có trạng thái chọn thật theo view/Settings; Home không còn
  luôn trông như đang được chọn, và nhóm thêm nhạc được tách bằng
  khoảng cách. Mascot hero bỏ viền đóng khung, lớn hơn và có bóng mềm;
  các thẻ cấu trúc chính thêm viền 1px để tách lớp rõ hơn.
- Lượt CI đầu `41462523443` đỏ vì `StreamGeometry` khai nội dung bằng
  TypeConverter không chấp nhận thêm thuộc tính `FillRule`; icon Repeat
  đã được vẽ lại bằng hai subpath không chồng lấn tại `7015f6d`. Windows
  CI `41462935871` sau bản sửa đã xanh.

### Follow-up — Mascot trong suốt (`011249b`)

- Chủ dự án chọn xử lý mascot trước phần Lighting & Glow/shader. Ảnh
  gốc đã duyệt được giữ nguyên hình vẽ; nền navy nướng sẵn được tách
  bằng matte nối từ biên ảnh, tận dụng đường viền sticker trắng để
  không ăn vào hoodie navy. Các nốt nhạc/lấp lánh rời và viền sticker
  được giữ; quầng sáng quanh micro được khử nền thành lớp hồng bán
  trong suốt thay vì mảng tím tối trên nền sáng.
- Asset hiện hành là PNG RGBA 548×757 (458.684 byte), đã cắt khoảng
  trong suốt thừa và có alpha 0 ở bốn góc. Hero hiển thị bằng
  `Stretch="Uniform"` trên một quầng accent làm mờ theo token theme,
  với bóng đổ bám theo alpha của nhân vật; ảnh thu nhỏ trong Settings
  cũng dùng `Uniform` và bỏ khung bo/clip hình chữ nhật.
- `BundledMascotTests` nay kiểm tra thêm IHDR 8-bit, color type 6
  (RGBA) để asset không thể âm thầm quay lại dạng PNG đặc. Windows CI
  `41466796260` đã xanh. Chủ dự án đã xác nhận mascot ổn trên bản chạy
  thật và chọn chuyển sang bước Lighting & Glow; xác nhận này đóng
  follow-up mascot ở mức runtime, nhưng không thay thế ma trận nghiệm
  thu trực quan đầy đủ của lát 1 + 2.

### Follow-up — Lighting & Glow cân bằng (`5aae07a`)

- Chủ dự án chọn phong cách **cân bằng**: nền mềm, có chiều sâu cao
  cấp nhưng điểm nhấn neon vẫn rõ; phạm vi đợt đầu chỉ gồm Hero,
  Now Playing và Stage để kiểm soát hiệu năng/rủi ro bố cục.
- `ThemeService` nay xuất thêm các biến thể alpha/transparent của
  accent phụ, accent hot, nền app và chữ chính. `ThemeBrushes.xaml`
  dùng chúng để dựng các radial glow, top highlight, sheen, vignette
  và gradient panel; mọi điểm dừng màu đều suy ra từ theme hiện hành
  nên theme cộng đồng cũng tự phát sáng theo palette riêng.
- Hero có hai nguồn sáng màu ở hai phía, vệt sáng trên và quầng nhẹ
  quanh nút hành động chính; quầng riêng sau mascot được giảm để tổng
  lượng sáng không bị cháy. Now Playing dùng nền panel chuyển sắc
  dọc, bloom mờ từ chính bìa bài hát, glow accent, sheen chéo, hào
  quang quanh thẻ bìa/visualizer và quanh nút Play/Pause. Stage có
  nguồn accent phía trên-trái, nguồn hot phía dưới-phải, vignette
  dưới visualizer, viền/hào quang cho thẻ tiêu đề và bóng chữ nhẹ.
- Đợt này chưa dùng custom HLSL `ShaderEffect`; gradient, blur và
  drop shadow của WPF được giới hạn ở các điểm nhấn tĩnh để tránh
  tăng tải cho visualizer đang chạy theo frame. Windows CI
  `41468987185` đã xanh. Cường độ sáng vẫn cần chủ dự án duyệt trên
  Windows thật; nếu quá yếu hoặc quá mạnh, chỉ cần tinh chỉnh opacity
  của các lớp này sau ảnh chụp đối chiếu.
- Ảnh Windows thật đầu tiên (kích thước mặc định, Neon Stage, EN,
  chưa chọn bài) cho thấy hướng cân bằng đã lên hình: Hero có chiều
  sâu, mascot hòa nền và panel Now Playing không còn phẳng. Cùng ảnh
  cũng lộ ba điểm cần vá: các lớp trong thẻ bìa/visualizer vẫn vuông
  góc dù Border đã đặt `CornerRadius`, quầng quanh CTA/Play còn hơi
  chìm, và chữ Album/Genre ở thư viện bị cắt cụt không có `…`.
  Follow-up `7c4bb48` thêm mask bo góc cho toàn bộ lớp bìa, tăng nhẹ
  ba quầng điểm nhấn, đồng thời chuyển Title/Artist/Album/Genre sang
  template `CharacterEllipsis` + ToolTip và cân lại bề rộng cột.
  Windows CI `41471065914` đã xanh; vẫn cần ảnh kiểm lại, một bài có
  bìa màu để đánh giá cover bloom và ảnh Stage riêng.

### Follow-up — Up Next / Stage Engine không cắt chữ (`b2989d0`)

- Ảnh cận panel phải khi đang phát và có hai bài trong Up Next cho
  thấy `Remove`, `Spectrum` và `Twin Wave` bị cắt ký tự cuối vì bề
  rộng hữu dụng bị scrollbar dọc lấy mất một phần. Style nút chế độ
  nay giảm padding/margin, hạ cỡ chữ và có template
  `CharacterEllipsis` dự phòng; thay đổi áp dụng cho cả Stage Engine
  trong Now Playing lẫn Settings.
- Play/Remove của Up Next được icon hoá bằng glyph Play/thùng rác
  sẵn có, giữ ToolTip song ngữ và tên trợ năng riêng; hai nút lên/
  xuống cũng có ToolTip. Dòng phụ của mỗi hàng đợi chuyển sang Grid
  `* / Auto / Auto` để tên nghệ sĩ dài tự rút gọn bằng `…` mà không
  đẩy mất thời lượng; bìa thu nhỏ được clip đúng góc bo 5px.
- Chỉ số `24 FPS · 0.05 ms` trong ảnh không phải dấu hiệu nghẽn vẽ:
  `VisualizerView` chủ động giới hạn tối đa 30 FPS khi cửa sổ không
  active hoặc Battery Saver bật, đúng trạng thái thường gặp khi chụp
  bằng công cụ ngoài; thời gian vẽ 0,05 ms/frame vẫn rất thấp. Nếu
  Mimi đang active trên màn hình 60 Hz mà chỉ số vẫn quanh 24 FPS,
  đó mới là việc cần điều tra riêng. Windows CI `41473516225` đã
  xanh; cần ảnh kiểm lại để xác nhận các nhãn đã đủ chữ trên Windows
  thật.
- Ảnh kiểm lại xác nhận `Spectrum` và `Twin Wave` đã đủ chữ, hàng
  icon Play/lên/xuống/xoá vừa panel và chỉ số khi Mimi active là
  `96 FPS · 0.03 ms`, khớp với giải thích throttle ở trên. Ảnh này
  đồng thời lộ một lỗi trạng thái khác: Up Next đang trống nhưng các
  nút thao tác và `Clear upcoming` vẫn sáng như bấm được, vì các lệnh
  chưa có `CanExecute` và template Button dùng chung chưa có hình
  ảnh riêng cho trạng thái disabled. Follow-up `6b0e0ae` thêm điều
  kiện cho Play/Remove/lên/xuống/Clear (gồm biên đầu/cuối), thông
  báo lại khi lựa chọn hoặc thứ tự hàng đợi đổi, và làm mờ nút
  disabled dùng chung; test mới bao phủ hàng đợi trống, mục đầu,
  mục cuối và sau khi Clear. Windows CI `41475557243` đã xanh, và
  chủ dự án đã xác nhận trên Windows thật rằng mục Up Next/Stage
  Engine cùng trạng thái mờ của các nút không khả dụng đã ổn.
  Follow-up này đóng ở mức runtime; nó không thay thế ma trận
  nghiệm thu UI Polish đầy đủ hoặc ảnh Stage/bìa màu riêng.

### Chưa nghiệm thu / để lại sau

- Các follow-up ở trên đã có ảnh/xác nhận runtime cho từng phần được
  ghi rõ, nhưng **nghiệm thu trực quan tổng thể vẫn chưa đóng**: vẫn
  cần đối chiếu ảnh trước/sau ở cửa sổ mặc định + cửa sổ hẹp × Neon Stage + Mono Focus ×
  VI/EN; các icon Path tự vẽ (đặc biệt Shuffle/Repeat), title bar tối,
  dropdown/slider/scrollbar, độ vừa của hàng transport và cường độ
  Lighting & Glow tại Hero/Now Playing/Stage cần được nhìn thực tế
  trước khi coi là duyệt thẩm mỹ.
- Phần còn lại của lát 3 “Có hồn” chưa triển khai: chỉ dấu hàng đang
  phát, trạng thái trống có minh họa mascot và chuyển động nhẹ vẫn là
  việc hậu kỳ tùy chọn. Một custom shader giới hạn vẫn có thể được
  cân nhắc sau nếu bản Lighting & Glow bằng gradient/blur chưa đủ
  “chất sân khấu” trên Windows thật.
- Đợt polish này không thay đổi hành vi phát nhạc, phím tắt, hợp đồng
  một click hay bất kỳ giới hạn/miễn trừ nào của Phase 4; cũng không bao
  gồm PR/merge vào `main`, LICENSE, tag hay đóng gói phát hành.
