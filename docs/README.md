# Tài liệu Mimi — đi từ đây

Mọi tài liệu của dự án nằm trong thư mục này, trừ sáu tệp mà GitHub và bộ cài
bắt buộc phải để ở gốc repo (xem [vì sao](#vì-sao-gốc-repo-vẫn-có-tệp-markdown)).
Nếu bạn vừa clone repo và không biết đọc gì trước: **[README](../README.md)** cho
người dùng, **[ROADMAP](ROADMAP.md)** cho người phát triển.

## Bản đồ tài liệu

### Thiết kế & lộ trình

| Tài liệu | Nội dung |
|---|---|
| [`ROADMAP.md`](ROADMAP.md) | **Nguồn sự thật duy nhất**: tầm nhìn, Design System, đặc tả màn hình, kiến trúc, lộ trình Phase 0–7 với tiêu chí nghiệm thu, sổ quyết định (Decision Log) và trạng thái từng cổng |
| [`COMMUNITY-THEMES.md`](COMMUNITY-THEMES.md) | Định dạng tệp theme JSON: các khoá màu, cách thả tệp vào thư mục `themes` và nạp lại |
| [`design/BRANDING.md`](design/BRANDING.md) | Nhận diện: logo, mascot, bảng màu, quy tắc IP (*Miku-inspired*, không phải Miku chính thức) |
| [`design/THEME-LAYERS.md`](design/THEME-LAYERS.md) | Các lớp bề mặt của theme: nền, panel, control, viền, focus ring — lớp nào đổi được, lớp nào không |
| [`design/UI-POLISH-V1.md`](design/UI-POLISH-V1.md) | Đặc tả bóng đổ, ánh sáng, độ tương phản, trạng thái hover/selected |
| [`design/UI-REFRESH-V2.md`](design/UI-REFRESH-V2.md) | Bố cục compact-but-complete, nền tối xanh làm mặc định, màu nền tự pha |
| [`design/LYRICS.md`](design/LYRICS.md) | LRC và Enhanced LRC, tô sáng từng từ, ba dòng lời trên Stage |
| [`design/YOUTUBE.md`](design/YOUTUBE.md) | Tích hợp YouTube Data API v3: phạm vi, ranh giới chính sách, những gì cố ý không làm |
| [`design/ONLINE-CATALOG.md`](design/ONLINE-CATALOG.md) | Mô hình danh mục trực tuyến dùng chung cho Radio / Online / YouTube |
| [`design/STREAMING-PROVIDERS.md`](design/STREAMING-PROVIDERS.md) | Đánh giá các nguồn phát nhạc: cái nào nhận, cái nào từ chối, vì sao |

### Kỹ thuật

| Tài liệu | Nội dung |
|---|---|
| [`engineering/PACKAGING.md`](engineering/PACKAGING.md) | Đóng gói: bộ cài Inno Setup, bản portable, checksum, những tệp đi kèm bản phát hành |
| [`engineering/CRASH-REPORTING.md`](engineering/CRASH-REPORTING.md) | Ghi log xoay vòng, bắt exception toàn cục, chỗ lưu log |
| [`engineering/PLAYBACK-ENGINE-INVESTIGATION.md`](engineering/PLAYBACK-ENGINE-INVESTIGATION.md) | Điều tra WASAPI shared / WaveOut: số đo, giả thuyết, kết luận còn treo |

### Phát hành

| Tài liệu | Nội dung |
|---|---|
| [`release/README.md`](release/README.md) | Quy trình phát hành, và cách sinh lại banner, ảnh "có gì mới", nút tải, huy hiệu, ảnh giao diện |
| [`release/0.7.0.md`](release/0.7.0.md) | Ghi chú phát hành 0.7.0, bản đọc trong repo (đường dẫn ảnh tương đối) |
| [`release/RELEASE-BODY.md`](release/RELEASE-BODY.md) | Cùng nội dung, đường dẫn ảnh tính từ gốc repo, để dán vào trình soạn trang GitHub Release |

Hai tệp cuối **phải giữ cùng nội dung** — chỉ khác cách viết đường dẫn ảnh.
`ReleaseMetadataTests` so từng dòng và báo lệch.

### Nghiệm thu trên Windows

| Tài liệu | Nội dung |
|---|---|
| [`acceptance/README.md`](acceptance/README.md) | **Windows Acceptance Kit**: biến các cổng trong ROADMAP thành phiên kiểm tra lặp lại được |
| [`acceptance/WINDOWS-ACCEPTANCE-CHECKLIST.md`](acceptance/WINDOWS-ACCEPTANCE-CHECKLIST.md) | Checklist chi tiết theo phiên S0–S5 |
| [`acceptance/RESULT-TEMPLATE.md`](acceptance/RESULT-TEMPLATE.md) | Mẫu ghi kết quả một phiên |
| [`acceptance/ONLINE-ACCEPTANCE.md`](acceptance/ONLINE-ACCEPTANCE.md) | Nghiệm thu từng mục cho Audius và YouTube |
| [`acceptance/PHASE-4-CLOSEOUT.md`](acceptance/PHASE-4-CLOSEOUT.md) | Biên bản đóng Phase 4 (đóng bằng waiver của chủ dự án, 01/11/2026) |
| [`acceptance/PHASE-5-CLOSEOUT.md`](acceptance/PHASE-5-CLOSEOUT.md) | Biên bản đóng Phase 5 (song ngữ, Theme Engine, mascot) |
| [`acceptance/PHASE-6-CLOSEOUT.md`](acceptance/PHASE-6-CLOSEOUT.md) | Biên bản đóng Phase 6 (lời bài hát, cổng trực tuyến) |

### Pháp lý nguồn nhạc

| Tài liệu | Nội dung |
|---|---|
| [`legal/README.md`](legal/README.md) | Tổng hợp: nguồn nào dùng được, kèm điều kiện gì |
| [`legal/radio-browser.md`](legal/radio-browser.md) | Radio Browser: điều khoản, giới hạn tần suất, cách ghi công |
| [`legal/audius.md`](legal/audius.md) | Audius: API công khai, phạm vi sử dụng |
| [`legal/jamendo.md`](legal/jamendo.md) | Jamendo: điều kiện cấp phép nội dung |
| [`legal/internet-archive.md`](legal/internet-archive.md) | Internet Archive: nội dung công cộng và giới hạn |
| [`legal/youtube-music-player.md`](legal/youtube-music-player.md) | YouTube: những gì bị cấm (tải audio, chặn quảng cáo, phủ lên player) |
| [`legal/self-hosted-servers.md`](legal/self-hosted-servers.md) | Máy chủ tự dựng: trường hợp nào hợp lệ |
| [`legal/rejected-platforms.md`](legal/rejected-platforms.md) | Các nền tảng đã xem xét và **từ chối**, kèm lý do — để không ai đề xuất lại |

### Ví dụ

| Tài liệu | Nội dung |
|---|---|
| [`examples/README.md`](examples/README.md) | Tệp mẫu dùng được ngay: theme JSON, lời bài hát `.lrc`, cách tự thử |

## Vì sao gốc repo vẫn có tệp markdown

Sáu tệp ở gốc **không** chuyển vào đây được, mỗi tệp vì một lý do cụ thể:

| Tệp | Vì sao phải ở gốc |
|---|---|
| [`README.md`](../README.md) | Trang chủ repo — GitHub chỉ đọc README ở gốc |
| [`LICENSE`](../LICENSE) | GitHub nhận diện giấy phép ở gốc; cũng được chép vào thư mục publish |
| [`CONTRIBUTING.md`](../CONTRIBUTING.md) | GitHub nhận diện tệp hướng dẫn đóng góp ở gốc |
| [`SECURITY.md`](../SECURITY.md) | GitHub nhận diện chính sách bảo mật ở gốc; cũng được chép vào thư mục publish |
| [`CHANGELOG.md`](../CHANGELOG.md) | Được chép vào thư mục publish cùng bản phát hành |
| [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) | Là giấy phép của thư viện bên thứ ba, phải đi kèm bản phát hành |

`tools/release-art` không phải tài liệu — đó là script vẽ ảnh, xem
[`release/README.md`](release/README.md).

## Quy ước giữ cho thư mục này gọn

1. Tài liệu mới **phải** nằm trong một thư mục con của `docs/` (hoặc ngay trong
   `docs/` nếu nó là tài liệu định dạng dùng chung như `COMMUNITY-THEMES.md`).
2. Thêm tài liệu thì thêm **một dòng** vào bảng ở trên, đúng nhóm.
3. Không thêm tệp `.md` mới ở gốc repo. Gốc chỉ giữ sáu tệp trong bảng trên.
4. Tài liệu đã bị thay thế thì **xoá hẳn**, đừng để lại tệp "đã chuyển trang" —
   lịch sử Git đã giữ bản cũ.

Ba điều 1, 3, 4 được `tests/Mimi.Tests/DocumentationLayoutTests.cs` kiểm tự
động, cùng với việc mọi đường dẫn tương đối trong tài liệu phải trỏ tới tệp có
thật. Nghĩa là: quên cập nhật bảng, hoặc để lại một link gãy, CI sẽ đỏ.
