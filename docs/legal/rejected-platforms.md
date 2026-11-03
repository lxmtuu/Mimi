# Spotify và YouTube — nền tảng không đạt cổng Mimi

Ngày rà soát: **03/11/2026**

Tài liệu này ghi lại vì sao các đường tắt thường được nghĩ tới — lấy
stream Spotify/YouTube vào một desktop player của bên thứ ba — không nằm
trong lộ trình của Mimi.

## Spotify

### Kết luận

**KHÔNG ĐẠT** cho mục tiêu biến Mimi thành Windows desktop player phát
full-track từ catalog Spotify, đặc biệt khi Mimi còn tích hợp thư viện
local và các nguồn khác.

### Căn cứ chính thức

- Spotify Developer Terms phân biệt Streaming SDA và Non-Streaming SDA.
  Giấy phép phát triển/phân phối Streaming SDA bị giới hạn ở private
  personal use, Approved Devices và việc tuân thủ Developer Policy.
- Developer Policy quy định streaming bản ghi âm nhạc chỉ dành cho
  Premium subscribers.
- Policy cấm tạo sản phẩm tích hợp stream/content từ service khác; cấm
  đồng bộ sound recording với visual media; cấm segue/mix/remix/overlap
  Spotify Content với audio khác; và cấm mimic/replicate hoặc thay thế
  core user experience của Spotify nếu không có prior written permission.
- Policy đặt hạn chế thương mại nghiêm ngặt lên Streaming SDA.
- Việc hiển thị Spotify Content yêu cầu attribution bằng Spotify Marks;
  metadata/cover art/Audio Preview Clips phải link về Spotify và không
  được cung cấp như standalone service/product.
- Không có trong hồ sơ này một phê duyệt Approved Device/written
  permission dành cho Mimi trên Windows WPF.

### Hệ quả

- Không dùng Web API preview URL như full-track streaming.
- Không trích xuất stream bằng công cụ không chính thức, không điều khiển
  ẩn app Spotify để giả lập playback trong Mimi và không cache/tải
  Spotify Content ngoài cơ chế được phép.
- Một tính năng Non-Streaming độc lập như mở liên kết sang Spotify hoặc
  quản lý playlist bằng API là bài toán khác, không đáp ứng mục tiêu
  catalog phát trong Mimi và không thuộc prototype Phase 6.

### Nguồn

- Spotify Developer Terms: <https://developer.spotify.com/terms>
- Spotify Developer Policy: <https://developer.spotify.com/policy>

## YouTube

### Kết luận

**KHÔNG ĐẠT** cho việc phát riêng audio hoặc dùng YouTube như nguồn nhạc
nền trong Mimi.

### Căn cứ chính thức

YouTube API Services Developer Policies quy định API Client không được:

- download, import, backup, cache hoặc store copies của YouTube
  audiovisual content nếu không có prior written approval;
- làm nội dung khả dụng cho offline playback;
- separate, isolate hoặc modify audio/video components của YouTube
  audiovisual content;
- promote separately các audio/video components;
- tạo hoặc quảng bá tính năng phát nội dung từ background player — player
  không hiển thị trong page/tab/screen người dùng đang xem;
- modify/block quảng cáo hoặc chức năng của YouTube player.

### Hệ quả

- Không dùng `yt-dlp`, extractor hoặc URL stream tách riêng để đưa audio
  YouTube vào engine/SMTC/visualizer/Stage của Mimi.
- Không ẩn official player để chỉ nghe tiếng.
- Một embedded YouTube player hiển thị đầy đủ là trải nghiệm video riêng,
  không phải provider âm thanh cho Mimi và không thuộc prototype Phase 6.

### Nguồn

- YouTube API Services Developer Policies:
  <https://developers.google.com/youtube/terms/developer-policies>
- YouTube API Services Terms of Service:
  <https://developers.google.com/youtube/terms/api-services-terms-of-service>
