# YouTube — tìm kiếm chính thức + player nhúng hiển thị

Ngày: **03/11/2026** · Settings schema: **v9** · Trạng thái: đã triển khai trên
nhánh làm việc, chờ nghiệm thu runtime trên Windows thật.

## Nguyên tắc

Mimi **không** lấy audio từ YouTube. Không `yt-dlp`, không `ytmusicapi`,
không trang ripper, không server tách audio. Phần duy nhất Mimi làm là:

1. **Tìm kiếm** bằng **YouTube Data API v3 chính thức** (key của chính người
   dùng) → chỉ nhận metadata.
2. **Phát** bằng **IFrame player chính thức của YouTube**, luôn hiển thị.

Đây là ranh giới pháp lý, không phải lựa chọn kỹ thuật: player phải đang hiển
thị thì mới hợp lệ, nên không có "chạy nền" hay "ẩn video, chỉ nghe tiếng".

## API key của người dùng

Key **không** nằm trong source, không nằm trong binary, không được log.

### Cách dán key (đường chính)

`Settings → YOUTUBE` có ô **API key YouTube Data API v3**: dán key vào ô, bấm
**Lưu key** (hoặc Enter). Ô này dùng `PasswordBox` nên key không hiện ra khi gõ;
sau khi lưu, ô được xoá và key **không** được giữ trong view model. Ngay bên
dưới ô là đường dẫn file thật để người dùng đối chiếu, cùng nút **Xoá key**.

Cách này tồn tại vì trước đó đường dẫn file chỉ được nêu trong panel YouTube —
mà panel đó lại bị ẩn khi chưa có key, nên trên thực tế không có chỗ nào trong
app chỉ được người dùng phải làm gì.

Key được kiểm tra hình dạng trước khi ghi (`YouTubeApiKey.Normalize`): cắt
khoảng trắng hai đầu, từ chối chuỗi rỗng, chuỗi có dấu cách/xuống dòng, ký tự
ngoài ASCII in được (dấu ngoặc kép cong khi copy từ trang web), và độ dài
ngoài khoảng 20–100. Nhờ vậy lỗi copy/paste hiện ra ngay chứ không thành một
lần tìm kiếm thất bại khó hiểu.

### Thứ tự tìm key

Đọc lại mỗi lần tìm kiếm, nên thả file vào là dùng được ngay:

1. Biến môi trường `MIMI_YOUTUBE_API_KEY` (ưu tiên cao nhất).
2. File `youtube-api-key.txt` trong thư mục dữ liệu của Mimi
   (`%LOCALAPPDATA%\Mimi`, hoặc `Settings → Open data folder`). File chỉ chứa
   đúng key, không có `key=`, không có ngoặc kép.

Thiếu key → mục YouTube tự ẩn; không có lỗi, không có crash. Mục này được kiểm
tra lại khi cửa sổ Mimi được kích hoạt (alt-tab trở về), khi mở Settings, và
ngay sau khi lưu/xoá key — nên không cần tắt mở lại app.

Nếu key sai hoặc chưa bật YouTube Data API v3 cho project, API trả 400/401;
Mimi báo riêng trạng thái *key bị từ chối* và chỉ người dùng vào Settings, thay
vì gộp chung vào lỗi mạng.

Tạo key: Google Cloud Console → tạo project → bật **YouTube Data API v3** →
Create credentials → API key → API restrictions → chỉ chọn *YouTube Data API
v3*.

## Hạn mức

`search.list` tốn **100 unit** trong 10.000 unit/ngày → khoảng **100 lần tìm
mỗi ngày**. Khi hết, API trả 403 và Mimi hiển thị thông báo hạn mức riêng
(`YouTube_QuotaExceeded`), không phải lỗi mạng chung. Muốn tăng phải xin qua
quy trình của Google.

## Hành vi phát

- Ô video **dùng chung khung với visualizer**: `IsVisualizerSurfaceVisible =
  !IsYouTubePlaying`. Visualizer tắt khi video hiện, bật lại khi video đóng.
- Chọn video → Mimi **pause nhạc local**, **dừng Radio**, **bỏ track online**
  để chỉ còn một nguồn âm thanh.
- Bấm lại đúng hàng đang phát, hoặc nút **Dừng**, hoặc phím Space → đóng
  player. Code-behind điều hướng WebView2 sang `about:blank`, và đó chính là
  cách âm thanh dừng lại.
- Nút **Mở** mở trang `watch?v=` bằng trình duyệt mặc định (dùng khi video chặn
  nhúng).
- Nội dung YouTube **không** vào `Track`, queue, history, favorites, lyrics hay
  phiên lưu; không có cache media của Mimi.

## Giới hạn phải nói thật

- Đây là **player video**, không phải audio-only. Không có tham số chính thức
  nào để tắt hình.
- Seek/queue/visualizer/SMTC của Mimi **không** áp dụng cho nội dung YouTube vì
  audio nằm trong player của họ, không qua NAudio.
- Có quảng cáo, trừ khi người dùng tự đăng nhập **YouTube Premium** của họ
  trong profile WebView2 của Mimi. Mimi không chặn quảng cáo.
- Cần **Microsoft Edge WebView2 Runtime**; thiếu thì chỉ log cảnh báo, phần còn
  lại của Mimi vẫn chạy.

## Chưa làm ở lát này

- **Stage**: hiện video full màn hình khi mở Stage. Sẽ làm sau khi pane ở cửa
  sổ chính được nghiệm thu, để tránh hai HWND WebView2 cùng lúc.
- Điều khiển play/pause từ transport của Mimi vào player nhúng.
- Playlist/kênh, tìm theo thời lượng, lọc kết quả.
