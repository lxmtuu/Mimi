# Online catalog (Audius) — thiết kế lát đầu tiên

Ngày: **03/11/2026** · Settings schema: **v8** · Trạng thái: đã triển khai trên
nhánh làm việc, chờ nghiệm thu runtime trên Windows thật.

## Mục tiêu

Trong Mimi, gõ tên bài hoặc nghệ sĩ → danh sách kết quả → bấm một lần là phát
ngay, audio chạy qua **engine của Mimi** (seek được, có timeline, có SMTC,
visualizer dùng chung đường tín hiệu).

Nguồn được chọn là **Audius** vì đây là catalog công khai có API đọc/stream
không cần credential của nhà phát triển. YouTube và Spotify không nằm trong
đường phát của Mimi; lý do ghi ở
[`../legal/youtube-music-player.md`](../legal/youtube-music-player.md) và
[`../legal/rejected-platforms.md`](../legal/rejected-platforms.md).

## Ranh giới cứng

- Chỉ đọc/tìm/stream. Không tải, không ghi file, không proxy, không chuyển mã.
- Không cache audio/metadata/artwork ra đĩa; không có offline mode.
- Không credential, không Bearer Token, không API secret trong repo hay binary.
  Danh tính ứng dụng dùng tham số công khai `app_name=Mimi`.
- Track bị khoá quyền hoặc không chắc chắn bị **loại trước khi tới UI**: thiếu
  `is_streamable`, `is_stream_gated=true`, `is_available=false`,
  `allowed_api_keys` không rỗng, `access.stream=false`, thiếu nghệ sĩ, hoặc
  duration không hợp lệ.
- Item online **không** trở thành `Track`, không vào queue, history, favorites,
  lyrics hay phiên lưu tự động.
- Có công tắc cục bộ trong Cài đặt; tắt là ẩn mục, huỷ tìm kiếm, dừng track.

## Luồng dữ liệu

```
OnlineSearchText ──SearchOnlineCommand──▶ IOnlineCatalogService.SearchTracksAsync
                                              │  GET /v1/tracks/search?query=…&app_name=Mimi
                                              ▼
                                   lọc quyền/khả dụng (AudiusCatalogService)
                                              ▼
                              ObservableCollection<OnlineTrackItemViewModel>
                                              │  click row (một lần) hoặc nút play
                                              ▼
                       StartOnlineTrackAsync → audioPlayer.LoadRemoteAudio(streamUrl)
                                              ▼
                    GET /v1/tracks/{id}/stream?app_name=Mimi → 302 → content node (Range)
```

`LoadRemoteAudio` khác `LoadStream`: track online là **hữu hạn**, nên giữ
`TotalTime` và cho phép seek, trong khi radio vẫn là live với duration 0.

## Đồng bộ nguồn phát

Ba nguồn dùng chung một audio engine, nên chuyển nguồn dùng chung một giao
thức: `BeginLocalPlaybackAttempt()` tăng `playbackSourceVersion`, bỏ adoption
radio, bỏ track online hiện tại; `LoadRemoteAudio`/`LoadStream`/`Load` chạy
trong `playbackSourceLoadSync` và chỉ thắng nếu version còn khớp. Vì vậy không
thể có chuyện một track online cũ "chen" vào sau khi người dùng đã bấm bài khác.

- Mở panel Radio → đóng panel Online. Mở panel Online → đóng Radio.
- Chọn bài online → dừng/huỷ đài đang chờ kết nối.
- Chọn file local → bỏ track online.
- Next/Previous khi đang phát online → đi theo danh sách kết quả hiện tại.
- Hết bài tự nhiên → sang kết quả kế tiếp; hết danh sách thì dừng.

## UI

- Sidebar: mục **Nhạc trực tuyến** (ẩn khi công tắc tắt hoặc service không có).
- Panel: ô tìm kiếm (Enter để tìm), trạng thái + progress, danh sách kết quả có
  artwork, tiêu đề, nghệ sĩ · thời lượng, giấy phép, nút **Nguồn** mở trang
  canonical trên audius.co, nút phát/tạm dừng.
- Một click trái trên hàng = phát ngay (dùng `ItemClickCommandBehavior`, bỏ qua
  click vào nút trong hàng).
- SMTC: tiêu đề/ nghệ sĩ, album "Audius", timeline hữu hạn, cho phép seek.

## Chưa làm trong lát này

- Hàng đợi online bền vững, playlist, favorite, follow, upload, tài khoản.
- Artwork cache ngoài phiên; lyrics online.
- Provider khác (Navidrome/Jellyfin, Jamendo, Internet Archive).
- Tắt provider từ xa (remote kill switch) và trang privacy/attribution đầy đủ.

## Kiểm thử

- `AudiusCatalogServiceTests`: parse + lọc quyền, endpoint công khai không
  credential, query rỗng không gọi mạng, lỗi HTTP ném ra thay vì trả rỗng.
- `OnlineCatalogFeatureWiringTests`: engine dùng `LoadRemoteAudio`, không có
  dependency trích xuất nào trong `src/`, item online không tạo `Track`/queue,
  công tắc tắt hoạt động, đủ khóa song ngữ.
- `UserDataServiceTests`: round-trip schema v8 gồm `OnlineCatalogEnabled`.
