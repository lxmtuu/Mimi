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
Mimi báo riêng trạng thái *key bị từ chối* và chỉ người dùng chạy lại
`tools/Set-YouTubeKey.ps1` trong terminal, thay vì gộp chung vào lỗi mạng.
Giao diện **không** có ô dán key: mọi thứ về key nằm ở terminal.

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
- Dù vậy, WebView2 vẫn có trường hợp **không gửi `Referer`** (host ảo, iframe
  cross-origin). Vì thế Mimi còn chặn request tới `youtube.com` bằng
  `WebResourceRequested` và chỉ điền `Referer` khi header còn thiếu — header
  trình duyệt đã gửi thì giữ nguyên. Trang cũng khai báo `origin` và
  `widget_referrer` cho player.
- Nếu YouTube vẫn từ chối, Mimi đọc mã lỗi từ `onError` (`101`/`150`: chủ video
  tắt nhúng; `153`: không nhận diện được trang nhúng) và hiện thông báo tương
  ứng, thay vì để thanh tiến trình đứng im. Nút **Chẩn đoán trình phát** in
  `{ready,error,state,duration,position}` để biết chính xác chuyện gì xảy ra.
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
  chính thức** (`youtube.com/iframe_api`), phơi ra `window.mimi.report()` và
  `window.mimi.command(...)`. Mimi lấy `state`/`position`/`duration` bằng
  `ExecuteScriptAsync` mỗi 500 ms (cùng nhịp với timer visualizer) và gửi lệnh
  `play`/`pause`/`seek` cũng bằng `ExecuteScriptAsync` — một đường duy nhất, dễ
  thấy khi hỏng, không phụ thuộc cấu hình message channel. Nhờ vậy:
  - nút Play/Pause ở Now Playing **toggle video** (trước đây nó dừng hẳn video);
  - thanh tiến trình **chạy theo video** và kéo/click để seek được;
  - `-10s` / `+10s` seek đúng 10 giây trong video;
  - hai nhãn thời gian lấy đúng thời lượng thật của video;
  - **âm lượng** (slider + nút mute) đẩy thẳng vào `player.setVolume()`, và
    `0%` được hiểu là mute; giá trị âm lượng hiện tại cũng được truyền sang
    trang lúc mở (`&volume=`) để hai bên không lệch nhau;
  - **chuyển/lùi bài** (`Next`/`Previous`) đi trong danh sách kết quả — danh
    sách đó chính là hàng đợi của YouTube, có quấn vòng;
  - **lặp và shuffle** dùng đúng trạng thái `RepeatMode`/`IsShuffleEnabled` của
    Now Playing: `One` phát lại video (`seekTo(0)` rồi `playVideo`), `All` quấn
    vòng khi hết danh sách, `Off` dừng ở cuối; shuffle chọn ngẫu nhiên một kết
    quả khác;
  - **Phát tất cả** bắt đầu danh sách từ mục đang chọn, giống một playlist.
- **Cầu nối không được phép treo**: lệnh `ExecuteScriptAsync` phát ra đúng lúc
  trang đang điều hướng có thể không bao giờ trả về. Mỗi lần đọc trạng thái có
  watchdog 2,5 giây, và cờ "đang đọc" được xoá khi `NavigationCompleted`, nên
  một lần lỗi không làm timeline chết vĩnh viễn. Trang cũng trả `problem`
  (`api-missing` / `player-missing`) thay vì `null` để log nói rõ vì sao.
- Nút Play/Pause gửi lệnh **`toggle`**: trang tự đọc trạng thái và lật, nên báo
  cáo cũ không thể biến nút thành nút không làm gì.
- Không còn chuyện đồng hồ NAudio ghi đè lên timeline của video:
  `RefreshPlaybackPosition` thoát sớm khi `IsYouTubePlaying`.
- Video chạy hết → player báo `state = 0` → Mimi đóng player, visualizer trở
  lại (giống hết bài local kết thúc).
- Nếu chủ video tắt nhúng, player báo lỗi **101/150**; Mimi đóng player và nói
  rõ là phải dùng nút **Mở**, không im lặng.
- Nếu 3 lần poll liên tiếp không có dữ liệu, Mimi ghi cảnh báo vào log và hiện
  thông báo trong panel. Nút **Chẩn đoán** (chỉ hiện khi đang phát video) in ra
  đúng những gì trang phát trả lời: `ready`, `error`, `state`, `duration`,
  `position`. Đây là công cụ beta để khỏi phải đoán khi transport im lặng.
- Bấm lại đúng hàng đang phát, hoặc nút **Dừng** → đóng player. Code-behind
  điều hướng WebView2 sang `about:blank`, và đó chính là cách âm thanh dừng
  lại. Phím Space nay là play/pause, không còn là stop.
- Nút **Mở** chỉ là đường lui cho video **chặn nhúng** (chủ video tắt embed);
  bình thường bấm **Phát** là video chạy ngay trong Mimi, không cần mở trình
  duyệt. Nhãn nút trong danh sách là **Phát**/**Dừng**, không còn dùng câu
  automation làm nhãn hiển thị.
- Nội dung YouTube **không** vào `Track`, queue, history, favorites, lyrics hay
  phiên lưu; không có cache media của Mimi.

## Danh sách đã lưu và menu chuột phải

- Danh sách kết quả có **menu chuột phải** giống danh sách local: Phát, Dừng,
  Phát tất cả, **Thêm/Bỏ khỏi đã lưu**, Sao chép liên kết, Mở trên YouTube.
- Nút cạnh "Phát tất cả" đổi giữa **Kết quả (n)** và **Đã lưu (n)**. Mục đã lưu
  hiện dấu ★ trên hàng.
- "Đã lưu" là danh sách **riêng của YouTube** trong `settings.json`
  (`YouTubeFavorites`, schema **v10**): chỉ gồm `videoId`, tiêu đề, kênh và URL
  thumbnail. Không có media, không có URL stream, và **không** gì ở đây đi vào
  thư viện, queue, history hay phiên lưu của Mimi. Tối đa 500 mục, id sai bị
  loại khi đọc file.
- Vì sao không dùng chung "Yêu thích" local: favourites của Mimi khoá theo
  **đường dẫn file**; một video YouTube không phải file và không được phát qua
  NAudio, nên trộn hai loại vào một danh sách sẽ làm hỏng cả hai.
- Radio và Online Music cũng có menu chuột phải, nhưng chỉ những hành động có
  nghĩa với chúng: Phát và Mở trang nguồn. Chúng chưa có danh sách đã lưu vì mỗi
  provider cần khoá nhận dạng riêng (station uuid, track id).

## Vì sao không ẩn điều khiển của YouTube

Yêu cầu "đừng hiện thanh tiêu đề / nút play của YouTube, thay bằng nút của Mimi"
**không được làm**, và đây không phải sở thích kỹ thuật. YouTube API Services –
Required Minimum Functionality quy định nguyên văn:

> You must not display overlays, frames, or other visual elements in front of any
> part of a YouTube embedded player, including player controls. Similarly, you
> must not use overlays, frames or other visual elements to obscure any part of
> an embedded player, including player controls.

> Attributes and parameters of the YouTube player, including the appearance of
> YouTube branding in the player, are specified in YouTube API documentation …
> You must not make changes to the YouTube player that are not explicitly
> described by the API documentation.

Nghĩa là: không phủ lớp nào lên player, không che, không thay giao diện của nó,
kể cả thanh tiêu đề, logo và nút play lớn của YouTube. Đây cùng nhóm lý do với
việc Mimi từ chối chặn quảng cáo và từ chối phát nền.

Cùng tài liệu đó cũng yêu cầu viewport tối thiểu **200×200 px**, và nếu player
hiện điều khiển thì phải đủ chỗ để hiện đủ điều khiển; khuyến nghị 16:9 là
**480×270**. Khung visualizer 210 px để lại chưa tới 200 px cho video, nên khi
phát video thẻ này **cao lên 300 px** và `WebView2` có `MinHeight/MinWidth = 200`.
Đó là cách hợp lệ duy nhất để phần điều khiển của YouTube bớt chật: cho nó nhiều
chỗ hơn, chứ không che nó đi.

Các hướng hợp lệ khác nếu vẫn thấy vướng:

- Đăng nhập **YouTube Premium** của chính mình trong profile WebView2 của Mimi
  để bớt quảng cáo (Mimi không chặn gì cả; đây là tài khoản của người dùng).
- Transport của Mimi: nút Play/Pause, thanh tiến trình, `±10s` vẫn điều khiển
  được video — điều khiển **song song**, không thay thế.
- Stage fullscreen (chưa làm) khi mở cũng phải giữ nguyên điều khiển của
  YouTube; không được dựng overlay riêng.

## Giới hạn phải nói thật

- Đây là **player video**, không phải audio-only. Không có tham số chính thức
  nào để tắt hình.
- Queue, visualizer và SMTC của Mimi **không** áp dụng cho nội dung YouTube:
  audio nằm trong player của họ, không qua NAudio. Seek, timeline, âm lượng,
  chuyển/lùi bài, lặp và shuffle thì **có**, nhưng qua IFrame Player API.
- Hàng đợi YouTube là **danh sách kết quả đang hiển thị**, không phải một
  playlist lưu lại: đổi từ khoá tìm kiếm là đổi hàng đợi, và không có gì của
  YouTube được ghi vào queue/history/favorites hay phiên lưu của Mimi. Playlist
  lưu hẳn các mục YouTube là việc của lát sau, nếu chủ dự án muốn.
- Có quảng cáo, trừ khi người dùng tự đăng nhập **YouTube Premium** của họ
  trong profile WebView2 của Mimi. Mimi không chặn quảng cáo.
- Cần **Microsoft Edge WebView2 Runtime**; thiếu thì chỉ log cảnh báo, phần còn
  lại của Mimi vẫn chạy.

## Chưa làm ở lát này

- **Stage**: hiện video full màn hình khi mở Stage. Sẽ làm sau khi pane ở cửa
  sổ chính được nghiệm thu, để tránh hai HWND WebView2 cùng lúc.
- Playlist/kênh, tìm theo thời lượng, lọc kết quả.
- Danh sách đã lưu cho Radio và Online Music (YouTube đã có).
- Sắp xếp lại / đặt tên cho danh sách YouTube đã lưu.
- SMTC/Quick Settings cho nội dung YouTube.
- Âm lượng: slider của Mimi vẫn chỉ điều khiển NAudio, chưa nối vào player
  (dùng nút loa trong player, hoặc âm lượng hệ thống).
