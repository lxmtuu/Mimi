# UI refresh v2 — nền tối xanh dương, hàng danh sách có trạng thái riêng

Tài liệu này ghi lại lượt nâng cấp UX/UI ngày 03/11/2026 sau khi chủ dự án
gửi ảnh chụp màn hình Thư viện và nêu bốn việc: điều hướng/bố cục gọn hơn,
theme gốc tối nhưng nổi bật xanh dương, theme phải đổi màu ở nhiều nơi hơn,
và sửa chỗ di chuột vào bài hát bị sáng trắng khó đọc.

## 1. Lỗi gốc: hàng danh sách sáng trắng khi di chuột

`ListView`/`ListBox` của Mimi chỉ đặt `Background="Transparent"` và
`ItemContainerStyle` vài setter, tức là **dùng template mặc định của WPF**.
Template đó vẽ trạng thái hover bằng brush của theme hệ thống (sáng), nên
trên nền tối nó thành một khối trắng và chữ biến mất. Việc override
`SystemColors.HighlightBrushKey` trước đây chỉ sửa được **ô đang chọn**,
không sửa được **ô đang hover**.

Cách sửa: Mimi tự vẽ container, ba style dùng chung trong
`src/Mimi.App/Themes/ControlStyles.xaml`.

| Style | Dùng cho |
| --- | --- |
| `MimiRowItemStyle` | `ListBoxItem` (playlist, Radio, Online, YouTube, thư mục theo dõi, lời bài hát) |
| `MimiListViewItemStyle` | `ListViewItem` không có cột (hàng đợi) |
| `MimiGridViewItemStyle` | `ListViewItem` của bảng Thư viện (giữ `GridViewRowPresenter` nên cột vẫn chuẩn) |

Trạng thái:

- **Thường**: trong suốt, chữ giữ nguyên màu.
- **Hover**: `HoverSurfaceBrush` (trộn `PanelBackground` với `Accent` 14%) +
  viền `HoverBorderBrush`. Không bao giờ dùng brush sáng của hệ thống.
- **Đang chọn**: `SelectedBackgroundBrush`, viền `SelectedBorderBrush`, và
  một thanh accent 3 px bên trái.
- **Tắt**: mờ 50%.

Ba style này là implicit style (`ListBoxItem`, `ListViewItem`) nên mọi danh
sách mới tự động theo, danh sách nào cần padding riêng thì `BasedOn`.

## 2. Theme gốc: navy đêm + xanh dương gốc

`neon-stage` (theme mặc định) được vẽ lại: nền `#070B14` → panel `#0D1424`
→ raised `#131D33`, accent **`#3E8BFF`** (xanh dương điện), accent phụ
`#4FE3FF` (cyan neon) và `#FF5CAE` (hồng neon) giữ chất Miku. `OnAccent`
`#04101F` để chữ trên nền accent vẫn đọc được. Các theme còn lại
(`sakura-dream`, `midnight-live`, `mono-focus`) không đổi id, chỉ hưởng lợi
từ phần 3.

Id theme không đổi, nên `settings.json` của người dùng đang dùng
`neon-stage` sẽ tự nhận bảng màu mới mà không cần thao tác gì.

## 3. Theme giờ phủ rộng hơn

Trước đây theme chỉ đổi những chỗ gắn brush tĩnh; các control dùng template
mặc định của WPF (hàng danh sách, ô nhập, checkbox, menu chuột phải,
tooltip) vẫn là màu sáng của Windows. Nay:

- `ThemeService` xuất thêm các màu **dẫn xuất từ chính theme**:
  `HoverSurfaceColor`, `HoverSurfaceStrongColor`, `HoverBorderColor`,
  `SelectedBorderColor`, `FieldBorderColor`, `MenuSurfaceColor`,
  `ToolTipSurfaceColor` — tất cả trộn từ `PanelBackground` +
  `SelectedBackground` + `Accent`, nên mỗi theme có sắc hover/viền/menu
  riêng thay vì xám cố định.
- `ThemeBrushes.xaml` công bố brush tương ứng; `ControlStyles.xaml` dùng
  chúng cho `TextBox`, `CheckBox`, `ContextMenu`, `MenuItem`, `Separator`,
  `ToolTip`.
- `App.xaml` thêm lớp "wash" cho `Button`: hover là một lớp accent mờ **nằm
  dưới chữ**, nên nút accent vẫn giữ màu gốc và nhãn không bị nhoè; nhấn
  xuống thì tối lại một nhịp.

## 4. Điều hướng và bố cục

- Thanh bên trái chia nhóm rõ: **Không gian của bạn** (Home, Thư viện, Yêu
  thích, Gần đây) → vạch ngăn → **Radio, Online, YouTube** → vạch ngăn →
  **Thêm nhạc / Quét thư mục** → Playlist.
- **Cài đặt** rời khỏi giữa danh sách, xuống chân thanh bên (ngay trên thẻ
  phiên bản), nên nhóm điều hướng chính ngắn hơn một mục và không lẫn với
  hành động.
- Thẻ "MIMI BETA" gọn lại (padding 10×8, chữ 10 px), header trên cùng thấp
  hơn (logo 38 px, tiêu đề 17 px) và bỏ dòng tagline trùng lặp — tagline
  chuyển sang màu mờ hơn ở góc phải.
- Nút điều hướng: padding `9,7`, chữ 12.5, màu `TextSecondary` để mục đang
  chọn (accent) nổi bật hơn.

## 5. Chưa làm / đề xuất tiếp

- Thanh lọc Thư viện hiện vẫn là 5 combobox trên một hàng; có thể thu thành
  nút "Bộ lọc" mở panel, hoặc chip lọc đang bật — cần chủ dự án chọn hướng.
- Chưa có chuyển động chuyển cảnh giữa các màn hình (fade 120 ms) vì phải
  kiểm tra ảnh hưởng tới hiệu năng WebView2/visualizer.
- Chưa có bảng "theme preview" (ô màu) trong Cài đặt; hiện chỉ có tên theme.

## 6. Kiểm chứng

- Unit test: `UiRefreshWiringTests` (style/brush wiring + mọi file XAML còn
  parse được + theme mặc định đúng xanh dương), `ThemeServiceTests`
  (màu dẫn xuất đúng công thức và độ sáng hover < 0,2 với cả 4 theme).
- **Chưa kiểm chứng bằng mắt trên Windows**: ảnh chụp bản chạy mới vẫn cần
  chủ dự án xác nhận, đặc biệt là độ tương phản hover và bảng Thư viện có
  giữ đúng độ rộng cột sau khi đổi container.
