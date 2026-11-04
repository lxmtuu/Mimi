# Lớp màu nền của theme — bản kiểm tra

Tài liệu này trả lời một câu hỏi cụ thể: **chín token bề mặt của theme xếp
lên nhau có đúng thứ tự và có đủ tương phản để nhìn ra không?** Nó ghi lại
kết quả đo thật trên 4 theme sẵn có (04/11/2026) và các bất biến đã được khoá
bằng test, để lần chỉnh màu sau không làm phẳng giao diện.

`ROADMAP.md` vẫn là hồ sơ quyết định; đây là hồ sơ kỹ thuật của lớp bề mặt.

## 1. Thứ tự lớp (đo bằng luminance tương đối)

Thứ tự bắt buộc, tối → sáng: **App < Sunken < Panel < Raised**, rồi phía trên thẻ:
**Raised < Control < Selected < Border**. `Header` chỉ cần sáng hơn `Panel` mà nó
gắn lên, vì thanh tiêu đề thuộc về danh sách chứ không thuộc thang nổi.

| Token | neon-stage | sakura-dream | midnight-live | mono-focus |
|---|---|---|---|---|
| `AppBackground` | `#070B14` | `#160D18` | `#050812` | `#0D0D0F` |
| `SunkenBackground` | `#0A1120` | `#180C1E` | `#070D1A` | `#111114` |
| `PanelBackground` | `#0D1424` | `#211222` | `#0A1020` | `#16161A` |
| `HeaderBackground` | `#101B30` | `#2E1A33` | `#111E36` | `#232329` |
| `RaisedBackground` | `#131D33` | `#2B1830` | `#101A30` | `#1E1E23` |
| `ControlBackground` | `#1B2842` | `#37203C` | `#182744` | `#2A2A30` |
| `SelectedBackground` | `#17325C` | `#47233F` | `#123A52` | `#34343B` |
| `Border` | `#243653` | `#533154` | `#263B63` | `#3A3A42` |
| `Divider` | `#1A2740` | `#402544` | `#1C2C4E` | `#2C2C33` |

Cả 4 theme đều đúng thứ tự này; test `ThemeLayerTests` chặn mọi palette đi lệch.

## 2. Bước nhảy giữa hai lớp liền kề

Tỉ lệ tương phản giữa lớp dưới và lớp trên (càng gần 1,00 thì càng khó thấy):

| Cặp lớp | neon-stage | sakura-dream | midnight-live | mono-focus | Mức tối thiểu |
|---|---|---|---|---|---|
| Panel trên nền cửa sổ | 1.07 | 1.06 | 1.06 | 1.08 | 1.05:1 |
| Thẻ nổi trên panel | 1.10 | 1.09 | 1.09 | 1.09 | 1.08:1 |
| Vùng lõm trong thẻ | 1.12 | 1.15 | 1.12 | 1.14 | — |
| Nút trên thẻ | 1.14 | 1.12 | 1.16 | 1.16 | 1.10:1 |
| Mục đang chọn | 1.15 | 1.10 | 1.24 | 1.15 | 1.05:1 |
| Header trên panel | 1.07 | 1.12 | 1.14 | 1.15 | — |

**Đọc bảng này thế nào:** bước `Panel → Raised` chỉ 1,09–1,10:1. Mắt người
không phân biệt được hai mảng màu chênh dưới ~1,10:1 khi không có cạnh, nên thẻ
nổi **bắt buộc phải có viền** — đó là lý do `MimiCardStyle` ra đời (mục 4).

## 3. Nơi từng lớp thực sự được vẽ

| Token | Vai trò | Xuất hiện ở |
|---|---|---|
| `AppBackground` | Nền cửa sổ (layer đáy) | Window, Stage, hộp thoại |
| `SunkenBackground` | Vùng lõm: ô nhập, vùng sau danh sách, khung video | TextBox, list nền, khung YouTube |
| `PanelBackground` | Panel/thẻ lớn của từng màn hình | Nav rail, panel Thư viện/Radio/Online/YouTube/Settings |
| `HeaderBackground` | Thanh tiêu đề bảng | Header của bảng Thư viện |
| `RaisedBackground` | Thẻ nổi nằm trên panel | 13 thẻ Settings/Up Next (MimiCardStyle) |
| `ControlBackground` | Nền nút bấm, ô chọn | Button, ComboBox, CheckBox |
| `SelectedBackground` | Dòng/mục đang chọn | Hàng đang chọn, nav đang mở |
| `Border` | Đường viền | Viền panel, viền bảng |
| `Divider` | Đường phân cách | Vạch ngăn trong nav, giữa các nhóm |

Không token nào là "khai báo cho có": test `EverySurfaceTokenIsActuallyPaintedSomewhere`
đỏ nếu một token bề mặt không còn chỗ nào vẽ.

## 4. Đã sửa: thẻ nổi không có cạnh

Trước bản này, thẻ `Raised` không có `BorderThickness` cũng không có shadow — chúng
chỉ hơn panel sau lưng 1,09:1, tức là gần như phẳng. **13 thẻ** (10 thẻ mục trong
Settings và 3 thẻ Up Next ở panel Now Playing) nay dùng `MimiCardStyle`:

```xml
<Style x:Key="MimiCardStyle" TargetType="{x:Type Border}">
    <Setter Property="Background" Value="{StaticResource RaisedBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource FieldBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="12"/>
</Style>
```

Đường viền lấy từ `FieldBorderBrush` — màu được trộn từ panel + accent của chính
theme, nên đổi theme là viền đổi theo. **Không đổi một mã màu nào** trong bảng màu
đã duyệt; chỉ thêm một đường 1 px để lớp `Raised` đọc ra được.

9 `Border` nền `Raised` còn lại **cố ý không đổi**, vì chúng không phải thẻ: badge
"MIMI BETA", ô tìm kiếm, 3 pill nguồn (Radio/Online/YouTube), 3 avatar chữ cái, và
một dải 34 px trong Settings. Chúng nhỏ, tự có hình, và thêm viền sẽ làm rối mắt.

## 5. Những chỗ còn mỏng (ghi nhận, chưa đổi)

- **Panel trên nền cửa sổ: 1,06–1,08:1.** Panel đều có viền 1 px nên vẫn tách
  được, nhưng nếu muốn panel nổi hơn thì phải nâng `PanelBackground` — việc này
  đổi toàn bộ giao diện nên cần chủ dự án quyết định.
- **Sunken trên panel: 1,02–1,03:1** (rất mỏng). Trên thực tế `Sunken` hầu như chỉ
  dùng **trong thẻ Raised** (8 chỗ), nơi bước nhảy là 1,12–1,15:1 nên vẫn thấy rõ.
- **Header trên panel: 1,06:1** — thanh tiêu đề bảng dựa vào chữ đậm và đường kẻ
  dưới để đọc, không dựa vào nền.
- **Nền Stage (`StageGradientStart`) chỉ hơn nền cửa sổ 1,09–1,26:1** ở 3 theme;
  Stage vốn tối có chủ đích nên chưa đổi.

## 6. Kiểm chứng

`ThemeLayerTests` khoá 7 bất biến:

1. thứ tự chín lớp giống nhau ở cả 4 theme;
2. mọi bước nhảy liền kề không xuống dưới mức tối thiểu;
3. `Sunken` luôn tối hơn `Panel`;
4. chữ (`TextPrimary/Strong/Secondary/Muted`) đạt **AA 4,5:1** trên cả 7 bề mặt của cả 4 theme;
5. bản đồ lớp khớp với màn hình thật (nền = Window, panel = NavRail…);
6. không token bề mặt nào bị bỏ rơi;
7. mọi thẻ `Raised` có cạnh, và không thẻ mục nào quay lại kiểu "tô nền trơn".

## 7. Màu nền tuỳ chỉnh (Settings → Appearance)

Người dùng chọn **một màu nền** (6 ô màu sẵn hoặc gõ `#RRGGBB`), Mimi pha màu đó
vào **toàn bộ phần nền** của **bất kỳ theme nào đang chọn** — nên chọn theme khác
thì màu nền vẫn theo người dùng.

"Phần nền" ở đây rộng hơn 9 lớp:

| Nhóm | Token | Vì sao phải theo |
|---|---|---|
| Lớp nền (9) | App, Sunken, Panel, Header, Raised, Control, Selected, Border, Divider | thân app, khung, thẻ |
| Dải màu nền (8) | Hero ×3, Stage ×3, Cover ×2 | hero, sân khấu video, ảnh bìa — để nguyên thì thành một khối xanh giữa app hồng |
| Phủ sân khấu | StageOverlay | chính là màu cửa sổ + alpha, phải đi cùng cửa sổ |
| **Không đổi** | Accent, AccentSecondary, AccentHot, OnAccent, toàn bộ Text, Warning/Danger, WarningBackground, WarningBorder, DangerBackground | đây là màu để *nói* (nhấn, chữ, cảnh báo), không phải màu nền |

**Hai quy tắc giữ cho nó "đều":**

1. **Xoay cùng một góc, không sơn cùng một màu.** Mỗi token giữ *quan hệ sắc* với
   màu nền gốc của theme: góc xoay = sắc người dùng chọn − sắc nền theme. Neon
   (hero 211°, nền 221°) thành hồng (hero 318°, nền 328°) — vẫn lệch nhau 10° như
   tác giả theme đã đặt, nên hero vẫn là "ánh sáng" chứ không phải một mảng bệt.
2. **Độ sâu giữ nguyên.** Mỗi lớp được chiếu sáng lại về đúng luminance tương đối
   của nó, nên thứ tự lớp, bước nhảy và tương phản chữ không đổi.

**Hai chi tiết kỹ thuật, đều đã có test:**

- **Trần bão hoà 0,5.** Ở mức luminance 0,002–0,05, màu bão hoà 100 % cho ra "đen
  neon" đối chọi với accent. Chọn màu xám thì toàn bộ nền mất sắc theo (kể cả
  hero), thay vì để một dải xanh bão hoà nằm giữa app đơn sắc.
- **Sửa thang sau khi làm tròn 8-bit.** Làm tròn có thể biến bước 1,05:1 thành
  1,04:1, nên customizer tự nâng lớp thiếu sáng cho tới khi `ThemeLayerAudit`
  sạch.

Kết quả đo (4 theme × 10 màu, gồm cả `#FFFFFF`, `#000000`, `#808080`): lệch độ sâu
tối đa **0,0012**; mọi token nền đều đổi sắc; khoảng sắc của hero lệch **< 4°**;
không trường hợp nào vi phạm bước lớp hay AA 4,5:1.

Lưu vào `settings.json` ở schema **v11**: `CustomBackgroundEnabled` +
`CustomBackground` (chuẩn hoá về `#RRGGBB`; giá trị không đọc được thì tự tắt tính
năng chứ không để lại bảng màu nửa vời). Nhân bản theme vẫn chép **bảng màu gốc**:
tint là cài đặt màn hình, không phải theme.
