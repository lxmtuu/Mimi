# Theme cộng đồng cho Mimi · Community themes for Mimi

Ngoài 4 theme sẵn có, bạn có thể tự tạo theme màu cho Mimi bằng **một tệp
JSON duy nhất**. Theme v1 chỉ chứa màu — không có ảnh, code hay XAML — nên
việc nạp theme luôn an toàn. Bạn tự chịu trách nhiệm về các tệp mình nạp;
kho mã nguồn Mimi không phân phối theme của bên thứ ba.

## Tiếng Việt

### Cài một theme có sẵn

1. Mở **Cài đặt → Giao diện & Ngôn ngữ → Mở thư mục theme**
   (thư mục `%LocalAppData%\Mimi\themes`).
2. Chép tệp `.json` của theme vào thư mục đó.
3. Bấm **Tải lại theme** trong Cài đặt (hoặc mở lại Mimi).
4. Chọn theme mới trong danh sách **Chủ đề giao diện**.

Muốn thử ngay? Chép tệp mẫu
[`docs/examples/sunset-drive.json`](examples/sunset-drive.json) vào thư
mục theme rồi chọn **Sunset Drive**.

### Tự viết theme

Cách nhanh nhất: chép tệp mẫu ở trên, đổi `id` + `name`, rồi sửa từng màu.
Nếu đang chọn chính theme đó, chỉ cần sửa tệp rồi bấm **Tải lại theme** —
bản màu mới áp dụng ngay mà không cần khởi động lại. Bảng màu của 4 theme
sẵn có nằm tại `src/Mimi.Core/Theming/BuiltInThemes.cs` nếu bạn cần thêm
điểm tham chiếu.

Giá trị dưới đây lấy từ theme mặc định **Neon Stage** (nền navy đậm, nhấn
xanh dương `#3E8BFF`) — chỉ là ví dụ, bạn đổi sang màu của mình.

### Định dạng tệp (schema v1)

```json
{
  "schemaVersion": 1,
  "id": "my-theme",
  "name": "My Theme",
  "colors": {
    "AppBackground": "#070B14",
    "Accent": "#3E8BFF"
  }
}
```

- `schemaVersion` — nên khai báo; nếu có thì phải bằng `1`.
- `id` — định danh ổn định của theme: chữ thường `a-z`, số và dấu `-`,
  dài 1–64 ký tự, không bắt đầu/kết thúc bằng `-`, không chứa `--`.
  Không được trùng 4 id sẵn có (`neon-stage`, `sakura-dream`,
  `midnight-live`, `mono-focus`) — theme trùng id sẽ bị bỏ qua. Hai theme
  cộng đồng trùng id thì tệp đứng trước theo thứ tự tên tệp được dùng.
- `name` — tên hiển thị, 1–80 ký tự, được giữ nguyên văn (không dịch).
- `colors` — bắt buộc đủ **33 token** bên dưới, không thêm token lạ.
  Mỗi màu ở dạng `#RRGGBB` hoặc `#AARRGGBB` (có kênh alpha).
- Tên tệp tùy ý (khuyên dùng `<id>.json`). Mimi chỉ quét tệp `.json` nằm
  ngay trong thư mục theme (không quét thư mục con), tối đa 256 KiB/tệp.

Tệp lỗi (JSON hỏng, thiếu/sai token, màu sai định dạng...) sẽ bị **bỏ qua
và ghi vào log** thay vì làm hỏng lúc khởi động: mở **Cài đặt → Mở thư mục
log** và tìm dòng chứa `community theme`. Nếu bạn xóa tệp của theme đang
dùng rồi tải lại (hoặc mở lại app), Mimi quay về theme mặc định
**Neon Stage**.

### 33 token màu

**Nền & bề mặt**

| Token | Dùng cho |
|---|---|
| `AppBackground` | Nền cửa sổ chính |
| `PanelBackground` | Panel/thẻ ở hai bên |
| `RaisedBackground` | Thẻ/khối nổi (ví dụ khối trong Cài đặt) |
| `SunkenBackground` | Vùng lõm phía sau danh sách/ô nhập |
| `ControlBackground` | Nền nút bấm/ô nhập liệu |
| `SelectedBackground` | Dòng/mục đang được chọn |
| `HeaderBackground` | Thanh tiêu đề bảng/danh sách |
| `Border` | Đường viền |
| `Divider` | Đường phân cách |

**Màu nhấn**

| Token | Dùng cho |
|---|---|
| `Accent` | Màu nhấn chính (các biến thể phát sáng mờ được suy ra từ đây) |
| `AccentSecondary` | Màu nhấn phụ |
| `AccentHot` | Màu nhấn nóng (yêu thích, điểm nhấn mạnh) |
| `OnAccent` | Chữ/icon vẽ đè lên `Accent` |

**Chữ**

| Token | Dùng cho |
|---|---|
| `TextPrimary` | Chữ chính |
| `TextStrong` | Tiêu đề/chữ nhấn mạnh |
| `TextSecondary` | Chữ phụ |
| `TextMuted` | Gợi ý, chú thích |
| `TextFaint` | Metadata mờ |
| `TextDisabled` | Chữ bị vô hiệu hóa |

**Trạng thái**

| Token | Dùng cho |
|---|---|
| `Warning` | Icon/điểm nhấn cảnh báo |
| `WarningText` | Chữ trong thông báo cảnh báo |
| `WarningBackground` | Nền dải cảnh báo |
| `WarningBorder` | Viền dải cảnh báo |
| `DangerBackground` | Bề mặt lỗi/nguy hiểm |

**Gradient & lớp phủ**

| Token | Dùng cho |
|---|---|
| `HeroGradientStart` / `HeroGradientMiddle` / `HeroGradientEnd` | Gradient nền khu vực Now Playing |
| `StageGradientStart` / `StageGradientMiddle` / `StageGradientEnd` | Gradient nền Stage toàn màn hình |
| `CoverGradientStart` / `CoverGradientEnd` | Gradient ảnh bìa thay thế khi bài hát không có bìa |
| `StageOverlay` | Lớp phủ mờ trên hình ảnh Stage (nên dùng `#AARRGGBB`) |

**Mẹo tương phản:** hãy giữ `TextPrimary` trên `PanelBackground` đủ tương
phản (khoảng 4.5:1 trở lên) và chọn `OnAccent` đọc rõ trên `Accent` — theme
vẫn được nạp kể cả khi tương phản thấp, nhưng chữ sẽ khó đọc.

---

## English

### Install a ready-made theme

1. Open **Settings → Appearance & Language → Open themes folder**
   (`%LocalAppData%\Mimi\themes`).
2. Copy the theme's `.json` file into that folder.
3. Press **Reload themes** in Settings (or restart Mimi).
4. Pick the new theme under **Interface theme**.

To try it right away, copy
[`docs/examples/sunset-drive.json`](examples/sunset-drive.json) into the
themes folder and select **Sunset Drive**.

### Write your own theme

The fastest path: copy the example file, change `id` + `name`, then tweak
colors. If that theme is currently selected, editing the file and pressing
**Reload themes** applies the new colors immediately. The palettes of the
four built-in themes live in `src/Mimi.Core/Theming/BuiltInThemes.cs` as
additional references.

The values below come from the default **Neon Stage** theme (deep navy base,
azure `#3E8BFF` accent) — they are an example, swap in your own colours.

### File format (schema v1)

```json
{
  "schemaVersion": 1,
  "id": "my-theme",
  "name": "My Theme",
  "colors": {
    "AppBackground": "#070B14",
    "Accent": "#3E8BFF"
  }
}
```

- `schemaVersion` — recommended; when present it must be `1`.
- `id` — the theme's stable identifier: lowercase `a-z`, digits, and `-`,
  1–64 characters, no leading/trailing `-`, no `--`. It must not reuse a
  built-in id (`neon-stage`, `sakura-dream`, `midnight-live`,
  `mono-focus`) — colliding themes are skipped. Between two community
  themes with the same id, the file that sorts first by file name wins.
- `name` — display name, 1–80 characters, shown verbatim (not localized).
- `colors` — all **33 tokens** below are required; unknown tokens are
  rejected. Each color is `#RRGGBB` or `#AARRGGBB` (with alpha).
- The file name is free-form (`<id>.json` is recommended). Mimi scans only
  `.json` files directly inside the themes folder (no subfolders), up to
  256 KiB per file.

Invalid files (broken JSON, missing/wrong tokens, malformed colors) are
**skipped and logged**, never fatal: open **Settings → Open logs** and look
for `community theme` lines. If you delete the file of the active theme and
reload (or restart), Mimi falls back to the default **Neon Stage** theme.

### The 33 color tokens

**Surfaces**

| Token | Used for |
|---|---|
| `AppBackground` | Main window background |
| `PanelBackground` | Side panels/cards |
| `RaisedBackground` | Raised cards (e.g. Settings sections) |
| `SunkenBackground` | Inset areas behind lists/inputs |
| `ControlBackground` | Button/input backgrounds |
| `SelectedBackground` | Selected row/item highlight |
| `HeaderBackground` | List/table header bars |
| `Border` | Borders |
| `Divider` | Separators |

**Accents**

| Token | Used for |
|---|---|
| `Accent` | Primary accent (soft glow variants are derived from it) |
| `AccentSecondary` | Secondary accent |
| `AccentHot` | Hot accent (favorites, strong callouts) |
| `OnAccent` | Text/icons drawn on top of `Accent` |

**Text**

| Token | Used for |
|---|---|
| `TextPrimary` | Main text |
| `TextStrong` | Headings/strong text |
| `TextSecondary` | Secondary text |
| `TextMuted` | Hints and captions |
| `TextFaint` | Faint metadata text |
| `TextDisabled` | Disabled text |

**Semantic**

| Token | Used for |
|---|---|
| `Warning` | Warning icons/accents |
| `WarningText` | Warning message text |
| `WarningBackground` | Warning banner background |
| `WarningBorder` | Warning banner border |
| `DangerBackground` | Danger/error surfaces |

**Gradients & overlay**

| Token | Used for |
|---|---|
| `HeroGradientStart` / `HeroGradientMiddle` / `HeroGradientEnd` | Now Playing backdrop gradient |
| `StageGradientStart` / `StageGradientMiddle` / `StageGradientEnd` | Fullscreen Stage backdrop gradient |
| `CoverGradientStart` / `CoverGradientEnd` | Placeholder cover-art gradient |
| `StageOverlay` | Translucent overlay above Stage visuals (use `#AARRGGBB`) |

**Contrast tip:** keep `TextPrimary` on `PanelBackground` at roughly
4.5:1 or better, and make sure `OnAccent` reads clearly on `Accent` —
low-contrast themes still load, but text becomes hard to read.
