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

### Đặt key bằng terminal (đường duy nhất)

Mimi **không** có ô dán key trong giao diện — đây là yêu cầu của chủ dự án: cấu
hình thuộc về terminal. Script `tools/Set-YouTubeKey.ps1` làm việc đó:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1
```

Không truyền tham số thì script hỏi key và **không hiện lại** giá trị vừa gõ.
Các lệnh khác:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1 -Status
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1 -Remove
```

`-Status` in đường dẫn file và một dạng che của key (`AIza****tu`), không in
key đầy đủ. `-Remove` xoá file.

Script kiểm tra hình dạng trước khi ghi (`YouTubeApiKey.Normalize` bên C# dùng
cùng luật): cắt khoảng trắng hai đầu, từ chối chuỗi rỗng, có dấu cách/xuống
dòng, ký tự ngoài ASCII in được (dấu ngoặc kép cong khi copy từ web), và độ dài
ngoài khoảng 20–100. Mimi cũng áp luật này khi **đọc** file, nên một file bị hỏng
được coi là *chưa có key* thay vì bị gửi lên Google và trả về 400.

### Thứ tự tìm key

Đọc lại mỗi lần tìm kiếm, nên thả file vào là dùng được ngay:

1. Biến môi trường `MIMI_YOUTUBE_API_KEY` (ưu tiên cao nhất).
2. File `youtube-api-key.txt` trong thư mục dữ liệu của Mimi
   (`%LOCALAPPDATA%\Mimi`, hoặc `Settings → Open data folder`). File chỉ chứa
   đúng key, không có `key=`, không có ngoặc kép.

Thiếu key → mục YouTube tự ẩn; không có lỗi, không có crash. Mục này được kiểm
tra lại khi cửa sổ Mimi được kích hoạt (alt-tab trở về) và khi mở Settings, nên
đặt key trong terminal rồi quay lại Mimi là mục hiện ra ngay — không cần tắt mở
lại app.

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

- Player nhúng KHÔNG được nạp bằng cách điều hướng thẳng tới
  `youtube.com/embed/...`. Cách đó không gửi `Referer`, và YouTube trả **Error
  153 – video player configuration error** (đúng lỗi đã gặp ở bản trước). Mimi
  phục vụ một trang nhỏ `player.html` từ host ảo `https://mimi-player.local`
  (`SetVirtualHostNameToFolderMapping`), trang này mới tạo iframe nhúng chính
  thức với `referrerpolicy="strict-origin-when-cross-origin"`, nên iframe có
  origin và referrer hợp lệ.
- Thư mục chứa trang đó là `%LOCALAPPDATA%\Mimi\YouTubePlayerPage`, tách khỏi
  profile Chromium (`YouTubeBrowserProfile`); Mimi ghi lại mỗi lần khởi động
  player.
- WebView2 được tạo với `--autoplay-policy=no-user-gesture-required`, vì cú
  click phát nằm ở WPF chứ không nằm trong trang.
- Trước khi khởi tạo WebView2, code chờ layout (`DispatcherPriority.Loaded`) cho
  tới khi pane hiện — WebView2 không khởi tạo được khi control còn collapsed.
- Ô video **dùng chung khung với visualizer**: `IsVisualizerSurfaceVisible =
  !IsYouTubePlaying`. Visualizer tắt khi video hiện, bật lại khi video đóng.
- Chọn video → Mimi **pause nhạc local**, **dừng Radio**, **bỏ track online**
  để chỉ còn một nguồn âm thanh.
- **Transport điều khiển được player.** Trang phát dùng **IFrame Player API
  chính thức** (`youtube.com/iframe_api`), báo `state`/`position`/`duration` về
  Mimi qua WebMessage mỗi 250 ms; Mimi gửi lệnh `play`/`pause`/`seek` ngược
  lại. Nhờ vậy:
  - nút Play/Pause ở Now Playing **toggle video** (trước đây nó dừng hẳn video);
  - thanh tiến trình **chạy theo video** và kéo/click để seek được;
  - `-10s` / `+10s` seek đúng 10 giây trong video;
  - hai nhãn thời gian lấy đúng thời lượng thật của video.
- Không còn chuyện đồng hồ NAudio ghi đè lên timeline của video:
  `RefreshPlaybackPosition` thoát sớm khi `IsYouTubePlaying`.
- Video chạy hết → player báo `state = 0` → Mimi đóng player, visualizer trở
  lại (giống hết bài local kết thúc).
- Nếu chủ video tắt nhúng, player báo lỗi **101/150**; Mimi đóng player và nói
  rõ là phải dùng nút **Mở**, không im lặng.
- Bấm lại đúng hàng đang phát, hoặc nút **Dừng** → đóng player. Code-behind
  điều hướng WebView2 sang `about:blank`, và đó chính là cách âm thanh dừng
  lại. Phím Space nay là play/pause, không còn là stop.
- Nút **Mở** chỉ là đường lui cho video **chặn nhúng** (chủ video tắt embed);
  bình thường bấm **Phát** là video chạy ngay trong Mimi, không cần mở trình
  duyệt. Nhãn nút trong danh sách là **Phát**/**Dừng**, không còn dùng câu
  automation làm nhãn hiển thị.
- Nội dung YouTube **không** vào `Track`, queue, history, favorites, lyrics hay
  phiên lưu; không có cache media của Mimi.

## Giới hạn phải nói thật

- Đây là **player video**, không phải audio-only. Không có tham số chính thức
  nào để tắt hình.
- Queue, visualizer và SMTC của Mimi **không** áp dụng cho nội dung YouTube:
  audio nằm trong player của họ, không qua NAudio. Seek và timeline thì **có**,
  nhưng thông qua IFrame Player API chứ không qua pipeline của Mimi.
- Có quảng cáo, trừ khi người dùng tự đăng nhập **YouTube Premium** của họ
  trong profile WebView2 của Mimi. Mimi không chặn quảng cáo.
- Cần **Microsoft Edge WebView2 Runtime**; thiếu thì chỉ log cảnh báo, phần còn
  lại của Mimi vẫn chạy.

## Chưa làm ở lát này

- **Stage**: hiện video full màn hình khi mở Stage. Sẽ làm sau khi pane ở cửa
  sổ chính được nghiệm thu, để tránh hai HWND WebView2 cùng lúc.
- Playlist/kênh, tìm theo thời lượng, lọc kết quả.
- Âm lượng: slider của Mimi vẫn chỉ điều khiển NAudio, chưa nối vào player
  (dùng nút loa trong player, hoặc âm lượng hệ thống).
