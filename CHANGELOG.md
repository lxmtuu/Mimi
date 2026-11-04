# Changelog

Mọi thay đổi đáng chú ý của Mimi được ghi ở đây. Định dạng theo
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), phiên bản theo
[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Trong giai đoạn 0.x, số phụ (`0.**7**.0`) là số phase trong
[`ROADMAP.md`](ROADMAP.md), và một thay đổi phá vỡ tương thích dữ liệu người
dùng có thể xảy ra ở bất kỳ bản nào — khi đó ghi rõ ở mục **Changed** kèm đường
migration.

## [Unreleased]

Chưa có thay đổi nào sau 0.7.0.

## [0.7.0] — 2026-11-04

Phase 6 — *Lyrics & cổng Online* — cộng với các lát giao diện và độ ổn định đã
vào `main` trong cùng khoảng thời gian. Tổng kết đầy đủ và các khoản còn nợ:
[`docs/acceptance/PHASE-6-CLOSEOUT.md`](docs/acceptance/PHASE-6-CLOSEOUT.md).

### Added

- **Lời bài hát offline (`.lrc` + lời nhúng).** Mimi đọc file `.lrc` cùng thư
  mục và cùng tên gốc với file nhạc, rồi mới tới lời nhúng trong tag. Hỗ trợ
  `mm:ss`, `mm:ss.xx`, `mm:ss.xxx`, `hh:mm:ss.xxx`, nhiều timestamp trên một
  dòng, thẻ `[offset:]`, và tối đa 10.000 dòng. File sidecar tối đa 2 MiB,
  UTF-8 (có/không BOM).
- **Tô sáng từng từ (karaoke).** Timestamp từng từ của Enhanced LRC
  (`<mm:ss.xx>`) được đọc vào `LyricLine.WordTimings` và tô dần trong thẻ Lyrics
  ở Now Playing và ở chữ lớn trên Stage. Bất biến: ghép các từ ra **đúng** chữ
  của dòng, nên cùng một file hiển thị như nhau dù app có hiểu tag hay không.
- **Lời lớn trên Stage.** Ba dòng quanh vị trí phát, chỉ với lyrics có
  timestamp, có trạng thái intro.
- **Internet Radio** qua Radio Browser: khám phá mirror, lọc đài khoẻ và không
  HLS, tìm kiếm/top-vote tối đa 60 kết quả, phát `url_resolved` trực tiếp, không
  giả duration/seek cho nguồn live. Tách hẳn khỏi Track/queue/history/session
  của thư viện local.
- **Audius** trong app: tìm bài và phát ngay trong engine Mimi, có seek cho
  nguồn hữu hạn, lọc `is_streamable`/`is_stream_gated`/`is_available`/
  `allowed_api_keys` trước khi hiển thị, không tải và không cache.
- **YouTube**: tìm bằng Data API v3 với khoá của chính người dùng (đặt từ
  terminal), phát trong **player nhúng chính thức và hiển thị**, transport của
  Mimi điều khiển video, Stage video, và danh sách video đã lưu. Không trích
  xuất, không tải, không cache media.
- **Công tắc tắt cho từng nguồn trực tuyến** (Radio, Online, YouTube) trong Cài
  đặt: tắt thì huỷ catalog/tìm kiếm, ẩn mục điều hướng, chặn phát, gỡ nguồn
  khỏi Now Playing mà không đụng tới nhạc local.
- **Theme Engine mở rộng**: nạp theme cộng đồng từ JSON trong
  `%LocalAppData%\Mimi\themes`, cho phép biến theme đang dùng thành tệp chỉnh
  sửa được, và **màu nền tuỳ chỉnh** áp lên mọi theme như một lớp phủ.
- **Báo lỗi tự khai báo.** Hộp thoại lỗi giờ có dòng `Details:` nêu kiểu,
  thông báo và nơi ném lỗi, kèm `[phiên bản · commit]`. Một lỗi chỉ mở **một**
  hộp thoại mỗi phiên; các lần sau chỉ ghi log.
- **Kill switch cho Internet Radio** ở settings schema v7, Online v8, YouTube v9.

### Changed

- **Giao diện v2**: vỏ tối xanh làm mặc định, điều hướng gọn nhưng đủ, bộ lọc
  gộp vào một nút, hàng danh sách có màu hover riêng thay cho màu sáng của hệ
  thống, và thanh tải video đặt trong khung thay vì cửa sổ thứ hai.
- **Theme đổi là đổi cả app**: màu được publish cho đúng một dictionary sở hữu,
  brush được repaint tại chỗ **và** thay bằng instance mới, nên không còn cảnh
  hai theme cùng lúc (thẻ hồng trong panel xanh).
- **Màu nền tuỳ chỉnh** là lớp phủ trên theme đang chọn, không thay thế theme.
- Song ngữ VI/EN phủ **455 khoá**, gồm cả chuỗi mới của Radio, Online, YouTube,
  Settings và thông báo lỗi.
- Bản dựng tự nêu số hiệu ở footer và trong log: `Mimi 0.7.0 · <commit>`.
- Settings schema lên **11** (v6 offset lời, v7 Radio, v8 Online, v9 YouTube,
  v10 danh sách YouTube đã lưu, v11 màu nền tuỳ chỉnh). Migration tự động, dữ
  liệu cũ được giữ.

### Fixed

- Hàng danh sách không còn chuyển trắng khi rê chuột trong theme tối.
- Danh sách lời không còn nhảy xuống dòng cuối mỗi khi dòng hiện tại đổi (đơn vị
  cuộn pixel so với item).
- Một lỗi trong vòng lặp không còn dựng chồng hộp thoại làm app trông như treo:
  visualizer, cuộn danh sách lời và đồng hồ phát nhạc đều được bọc, mất một
  khung hình hoặc một nhịp thay vì cả cửa sổ.
- Đóng Stage bằng F11/Esc không còn ném
  `InvalidOperationException … FocusVisualStyle`. Vòng focus được khai báo
  trước mọi chỗ dùng và tham chiếu bằng `DynamicResource`; thêm lớp thu hồi
  lúc chạy cho phần tử mang giá trị rỗng.
- Transport của video không còn mất trạng thái khi một lượt poll bị chậm.
- Bộ lọc và nút đếm danh sách YouTube không còn bị bóp méo sau khi tìm kiếm.

### Security

- Khoá YouTube chỉ đọc từ biến môi trường `MIMI_YOUTUBE_API_KEY` hoặc
  `youtube-api-key.txt` trong thư mục dữ liệu; **không** có ô nhập khoá trong
  giao diện, không ghi khoá vào settings hay log.
- Log chỉ ghi host của stream, không ghi URL đầy đủ của nguồn trực tuyến.

### Known limitations

Nêu thẳng, vì chưa đóng:

- Progress/thời gian trên thẻ Quick Settings của Windows chưa hiển thị (`P4-A`
  `PARTIAL`); độ trễ vật lý của backend âm thanh chưa đo được nên D-03 chưa
  chốt.
- Provinence của bộ fixture Phase 4 vẫn là `Observation` (SHA-256 không khớp bộ
  đã xuất).
- Audius và YouTube chưa có bản ghi nghiệm thu runtime **theo từng mục**;
  xem [`docs/acceptance/ONLINE-ACCEPTANCE.md`](docs/acceptance/ONLINE-ACCEPTANCE.md).
- Chưa có bộ cài (`v0.7.x` phát hành dạng zip self-contained) và chưa ký số.

## [0.6.0] — 2026-11-02

Phase 5 — *Song ngữ & Theme cộng đồng*.

- Nền `.resx` cho VI/EN, chọn ngôn ngữ áp dụng ngay; mở rộng dần tới 322 khoá.
- Font **Be Vietnam Pro** (5 weight) đóng gói kèm, đặt làm font giao diện để dấu
  tiếng Việt hiển thị giống nhau trên mọi máy.
- Theme Engine với 4 theme tối (`neon-stage`, `sakura-dream`, `midnight-live`,
  `mono-focus`) và loader theme cộng đồng (JSON schema v1).
- Mascot Mimi nguyên bản v1 trong hero và cạnh nhãn phiên bản.
- Một click trái thường trên một dòng là phát ngay (thay thiết kế double-click).
- Quét hard-coded chuỗi hiển thị kèm test gác cổng.

Tổng kết: [`docs/acceptance/PHASE-5-CLOSEOUT.md`](docs/acceptance/PHASE-5-CLOSEOUT.md).

> Các bản trước 0.6.0 (`0.1.x`–`0.5.0`, Phase 0–4) chưa được ghi lại thành
> mục changelog riêng. Bản ghi kỹ thuật của chúng nằm trong git history và
> [`ROADMAP.md`](ROADMAP.md); không dựng lại ở đây để tránh chép sai ngày tháng.

[Unreleased]: https://github.com/lxmtuu/Mimi/compare/v0.7.0...HEAD
[0.7.0]: https://github.com/lxmtuu/Mimi/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/lxmtuu/Mimi/releases/tag/v0.6.0
