# Thiết kế cổng Streaming Provider

Trạng thái: **Radio Browser đã có prototype trên nhánh làm việc; chưa có provider production**

Tài liệu này cụ thể hóa mục 8.5 của ROADMAP. Hồ sơ điều khoản tương ứng nằm
tại [`docs/legal/`](../legal/README.md). Không phần nào trong tài liệu này
tự nó cấp phép triển khai; từng provider vẫn phải qua cổng pháp lý và được
chủ dự án chọn riêng trước khi viết code phát hành. Chủ dự án đã chọn riêng
Radio Browser cho lát prototype hiện tại; lựa chọn này không phê duyệt
Audius, Navidrome/Jellyfin hay bất kỳ provider nào khác.

## Nguyên tắc

1. **Online là opt-in.** Thư viện local là đường chính và vẫn hoạt động khi
   mất mạng hoặc provider bị tắt.
2. **Không có Mimi backend.** App gọi trực tiếp provider/server do người
   dùng chọn; không thu thập analytics và không proxy audio qua hạ tầng
   của dự án.
3. **Streaming không đồng nghĩa download/cache.** Mỗi provider khai báo
   quyền cache riêng; mặc định là không cache bền vững.
4. **Rights là dữ liệu bắt buộc.** Item không xác định được quyền phát
   trong mô hình của Mimi thì bị chặn ở provider adapter, không để UI thử
   phát rồi mới xử lý.
5. **Không nhúng secret của dự án.** Chỉ public client identifier được nền
   tảng xác nhận an toàn cho client mới được đóng gói. Token của người
   dùng được mã hóa bằng DPAPI `CurrentUser`.
6. **Có kill switch theo provider.** Khi điều khoản thay đổi, API bị thu
   hồi hoặc có khiếu nại quyền, provider bị tắt mà không ảnh hưởng local.

## Phân loại provider

| Loại | Ví dụ đã rà soát | Đặc điểm kiến trúc |
| --- | --- | --- |
| Public radio directory | Radio Browser | Không tài khoản, live stream, không seek/duration đáng tin cậy, có playback-started callback |
| Public on-demand catalog | Audius | Search/browse/stream theo track, giấy phép và attribution theo item, giới hạn cache theo phiên |
| User server | Navidrome/OpenSubsonic, Jellyfin | Base URL + tài khoản do người dùng cấp; nội dung thuộc server của họ; có thể seek/transcode tùy server |

Jamendo và Internet Archive đang ở trạng thái hoãn trong hồ sơ pháp lý.
Spotify và YouTube bị loại cho mô hình full-track player của Mimi.

## Hiện trạng prototype Radio Browser

Prototype hiện tại cố ý hẹp hơn hợp đồng `IStreamingProvider` đích ở phần
sau:

- Core có `RadioStation` bất biến và `IRadioStationService` chỉ gồm top
  stations, search theo tên và đăng ký station click. Station không dùng
  `Track`, file path hoặc khóa của thư viện local.
- `RadioBrowserService` khám phá mirror HTTPS qua DNS
  `all.api.radio-browser.info`, có mirror dự phòng, timeout 12 giây,
  backoff ngắn, response tối đa 2 MiB và tối đa 60 kết quả. Mirror chỉ
  được chấp nhận khi host là `api.radio-browser.info` hoặc subdomain
  của nó. Adapter loại đài `lastcheckok != 1`, HLS/playlist URL
  (`.m3u8`, `.m3u`, `.pls`, `.xspf`), URL không phải HTTP(S), URL
  chứa user-info và stream host localhost/private/link-local.
- Click-counter dùng đúng một request, không retry/failover, và chỉ được
  ViewModel gọi sau khi `Play()` thành công cho một đài mới do người dùng
  chọn. Pause/resume cùng đài không tạo click mới.
- `IAudioPlayer.LoadStream(Uri)` mở `url_resolved` trực tiếp bằng
  `MediaFoundationReader`, đưa mẫu đã decode qua visualizer/volume/output
  hiện có và đánh dấu nguồn là live. Live duration luôn bằng 0; seek, stop
  reposition và natural-end của file finite bị chặn cho nguồn này. Log load
  chỉ ghi host của stream, không ghi toàn bộ URL.
- ViewModel giữ `CurrentRadioStation` tách khỏi `CurrentTrack`, queue,
  history, favorites, lyrics và saved playback session. Kết quả Radio chỉ
  nằm trong bộ nhớ phiên; prototype không tải favicon mà dùng ô chữ cái
  đầu được tạo tại client. Khi Radio đang là nguồn hiện tại, capture
  settings giữ nguyên phiên local đã lưu trước đó thay vì ghi stream URL,
  nhưng đài không bao giờ được khôi phục ở lần mở app sau.
- UI có attribution Radio Browser, metadata/tag và homepage của đài; seek
  slider/nút ±10 giây bị tắt khi live. Next/Previous chuyển đài trong danh
  sách hiện tại, còn resume tạo kết nối mới tới live edge. SMTC nhận
  metadata và nút Play/Pause/Next/Previous nhưng timeline không cho seek.
- Settings có kill switch Internet Radio lưu ở settings schema v7. Khi
  tắt, navigation/content Radio bị ẩn, catalog/search bị hủy hoặc chặn,
  đài hiện tại bị dừng và gỡ khỏi Now Playing; thư viện local không bị
  ảnh hưởng. Mô tả ngắn tại Settings nêu việc Mimi liên hệ Radio Browser
  và máy chủ đài có thể thấy địa chỉ IP của người dùng.

Chủ dự án đã nghiệm thu runtime prototype trên Windows thật ngày
03/11/2026, bao gồm cả kill switch Internet Radio trong lượt kiểm
tra gộp A/B/C. Giới hạn còn mở của prototype: đài chết, geo-block hoặc
định dạng Media Foundation không mở được
chỉ báo lỗi để người dùng chọn đài khác; chưa tự reconnect khi network
stall ngoài thao tác Play/resume; chưa có trang privacy/attribution
đầy đủ ngoài mô tả ngắn trong Settings hoặc cơ chế tắt provider từ xa. Các
mục này vẫn là cổng production, không được suy ra là đã hoàn thành từ
việc prototype chạy được ở mức code/test tự động.

## Interface đích

Pseudo-C# dưới đây là hợp đồng thiết kế, chưa phải code production:

```csharp
public interface IStreamingProvider
{
    string ProviderId { get; }
    ProviderCapabilities Capabilities { get; }

    Task<RemotePage<RemoteTrack>> SearchTracksAsync(
        string query,
        RemotePageRequest page,
        CancellationToken cancellationToken);

    Task<RemotePage<RemoteTrack>> BrowseTracksAsync(
        string? collectionId,
        RemotePageRequest page,
        CancellationToken cancellationToken);

    Task<PlaybackSource> ResolvePlaybackAsync(
        RemoteTrackId trackId,
        CancellationToken cancellationToken);

    Task ReportPlaybackStartedAsync(
        RemoteTrackId trackId,
        CancellationToken cancellationToken);
}

public sealed record ProviderCapabilities(
    bool CanSearch,
    bool CanBrowse,
    bool CanSeek,
    bool IsLive,
    bool RequiresUserAccount,
    bool AllowsPersistentMetadataCache,
    bool AllowsPersistentAudioCache,
    bool AllowsDownload);

public sealed record RemoteTrack(
    RemoteTrackId Id,
    string Title,
    string Creator,
    string? CollectionTitle,
    TimeSpan? Duration,
    Uri? ArtworkUrl,
    RightsInfo Rights);

public sealed record RightsInfo(
    string SourceName,
    Uri CanonicalUrl,
    string? LicenseName,
    Uri? LicenseUrl,
    string AttributionText,
    bool PlaybackConfirmed);

public sealed record PlaybackSource(
    Uri StreamUrl,
    string? MediaType,
    bool IsLive,
    DateTimeOffset? ExpiresAt,
    IReadOnlyDictionary<string, string> Headers);
```

Quy ước bắt buộc:

- `RemoteTrackId` gồm `ProviderId + RemoteId` dạng chuỗi. Không dùng
  đường dẫn file, không ép ID thành số và không gộp với khóa của bảng
  local tracks.
- `RightsInfo.PlaybackConfirmed` chỉ `true` khi adapter đã áp dụng bộ lọc
  giấy phép của provider. `false` nghĩa là item chỉ có thể hiển thị ở chế
  độ nghiên cứu nội bộ hoặc bị loại hoàn toàn khỏi kết quả phát hành.
- `ReportPlaybackStartedAsync` là no-op mặc định ở adapter không yêu cầu;
  Radio Browser dùng nó cho click-counter.
- Mọi method đều nhận `CancellationToken`, có timeout và phân trang có
  trần. Không có method “download toàn catalog”.
- Authentication nằm ở interface riêng, không ép provider công cộng không
  tài khoản phải giả lập login:

```csharp
public interface IProviderAuthenticator
{
    Task<ProviderAuthState> GetStateAsync(CancellationToken cancellationToken);
    Task SignInAsync(CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);
}
```

## Credential và dữ liệu người dùng

- Settings JSON chỉ lưu cấu hình không bí mật: provider bật/tắt, base URL
  của user server, username hiển thị nếu thật cần, public client ID do
  người dùng tự nhập và lựa chọn chất lượng.
- Access token, refresh token, mật khẩu hoặc secret dùng để tạo token chỉ
  nằm trong blob DPAPI `CurrentUser`, tách theo provider/server/user.
- OAuth trên desktop dùng Authorization Code + PKCE và trình duyệt hệ
  thống. Không nhúng webview thu mật khẩu của nền tảng.
- Log phải redact `Authorization`, cookie, token, mật khẩu, Subsonic
  `p/t/s`, ApiKey query và signed stream URL.
- Đăng xuất phải xóa DPAPI blob, memory cache và session cache của
  provider.
- User-Agent dùng dạng `Mimi/<version>`; khi provider yêu cầu liên hệ,
  kèm URL repo chính thức.

## Cache và vòng đời dữ liệu

| Dữ liệu | Mặc định | Ghi chú |
| --- | --- | --- |
| Search/browse result | Bộ nhớ theo phiên | Xóa khi đóng app hoặc tắt provider |
| Artwork | Bộ nhớ hoặc temp theo phiên | Chỉ cache bền vững nếu provider cho phép rõ ràng |
| Audio buffer | Bộ nhớ/stream buffer kỹ thuật | Không biến thành file offline và không giữ sau phiên nếu điều khoản cấm |
| Favorites/history remote | Không lưu trong prototype đầu | Chỉ thêm sau khi rà soát consumer-data của provider |
| Credential | DPAPI | Không nằm trong SQLite/settings/log |

Audius là trường hợp khóa cứng nhất hiện tại: cache content/consumer data
chỉ theo phiên và phải không còn truy cập được khi phiên kết thúc. Kiến
trúc dùng mặc định này cho mọi public catalog để tránh adapter vô tình
vượt quyền.

## Tích hợp playback tương lai

Engine hiện tại là file-oriented. Khi một provider được chọn để prototype,
phần việc phải tách riêng thay vì giả lập remote stream thành file local:

1. Thêm nguồn decode từ HTTP(S) stream có cancellation, timeout, redirect
   giới hạn và header được kiểm soát.
2. Live radio có duration/seek không xác định; UI/SMTC phải biểu diễn
   trạng thái live thay vì bịa duration.
3. Signed/expiring URL phải được resolve ngay trước khi phát và resolve
   lại khi retry; không lưu URL đã ký vào settings/history.
4. Visualizer vẫn lấy mẫu sau decode như local, nhưng network stall không
   được chặn audio callback hoặc UI thread.
5. Lyrics local `.lrc`/embedded không tự áp dụng cho remote track. Online
   lyrics cần hồ sơ quyền riêng; không scrape lời theo title/artist.
6. Lỗi mạng, HTTP 401/403/429, geo-block và stream chết phải dừng ở provider
   đó, không làm hỏng queue local.

## Attribution trong UI

Mỗi remote item phải có bề mặt hiển thị hoặc mở được:

- creator/artist;
- provider/source;
- liên kết canonical tới item;
- tên và liên kết giấy phép khi có;
- lưu ý “phát trực tiếp từ nguồn bên thứ ba” với radio/user server.

Thiếu trường attribution bắt buộc của provider là lỗi adapter và item bị
chặn, không dùng placeholder chung chung để lấp.

## Thứ tự prototype khuyến nghị

1. **Radio Browser** — đã có prototype nhỏ nhất, không credential; đã
   được chủ dự án nghiệm thu network playback/live UI trên Windows
   thật ngày 03/11/2026.
2. **Navidrome/OpenSubsonic hoặc Jellyfin** — chứng minh đăng nhập, DPAPI,
   remote library và seek trên server của người dùng.
3. **Audius** — catalog on-demand, chỉ sau khi session-cache, attribution,
   alternative-license filter và OAuth PKCE đã có test.

Mỗi bước là một lát riêng có runtime acceptance trên Windows. Hoàn thành
một provider không tự phê duyệt provider tiếp theo.

## Cổng trước khi viết code phát hành

- Đọc lại hồ sơ provider và kiểm tra điều khoản chính thức tại ngày bắt
  đầu prototype.
- Chủ dự án chọn đúng một provider/mục tiêu cho lát đó.
- Viết test cho rights filter, credential redaction, session-cache
  cleanup, cancellation, rate limit và kill switch.
- Không bật provider mặc định trong bản phát hành cho tới khi có nghiệm
  thu runtime và trang attribution/privacy trong app.
