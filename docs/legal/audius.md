# Audius — đánh giá provider

Ngày rà soát: **03/11/2026**

## Kết luận

**ĐẠT CÓ ĐIỀU KIỆN** cho catalog on-demand trong một Music Player của bên
thứ ba. Audius là ứng viên catalog rõ ràng nhất vì Open Music License (OML)
nêu trực tiếp quyền của Music Player, nhưng prototype phải tuân thủ chặt
các giới hạn credential, cache và attribution dưới đây.

## Căn cứ chính thức

- API Terms cấp cho Licensee một giấy phép giới hạn, không độc quyền, có
  thể thu hồi để truy cập/dùng API nhằm phát triển ứng dụng giao tiếp với
  Audius Services qua các Node.
- OML định nghĩa “Music Players” là ứng dụng được thiết kế để phát audio
  trên Audius Protocol. Mục 1.2 cấp cho Music Player quyền toàn cầu,
  không độc quyền, royalty-free để reproduce, publicly perform, distribute,
  transmit và stream Licensed Material trong dịch vụ của Music Player.
- OML mục 1.3 cho phép Alternative License nếu Licensor cung cấp URI trong
  JSON liên kết với nội dung. Nếu có Alternative License, không được mặc
  định áp dụng quyền của OML một cách mù quáng.
- Audius Terms of Use cho biết nội dung tải lên mặc định khả dụng qua API
  theo OML, nhưng creator có thể tắt API access trong settings. Client chỉ
  được dùng dữ liệu/track mà API hiện tại trả về cho app và phải tôn trọng
  thay đổi cài đặt của creator.
- Tài liệu SDK phân biệt API Key dùng được ở frontend/backend với Bearer
  Token chỉ dành cho backend. Bearer Token có thể hành động thay mặt người
  dùng đã ủy quyền và không được lộ trong browser/mobile code. Desktop app
  phải áp dụng cùng nguyên tắc: không nhúng API Secret/Bearer Token của dự
  án.
- API Terms giới hạn việc lưu content/consumer data ở session-based
  caching trong phạm vi cần cho phiên; dữ liệu cache phải không còn truy
  cập được khi phiên kết thúc.
- API Terms cấm scraping/data harvesting/crawler để trích xuất dữ liệu,
  huấn luyện ML/AI trên nội dung, dùng API trái luật hoặc vượt tính năng
  kiểm soát được nhúng trong API/Services.
- Audius có thể chấm dứt API Terms và thay đổi API. Mimi phải có kill
  switch cho provider.

## Điều kiện triển khai trong Mimi

- Chỉ dùng public API Key cho read/search/stream công khai nếu Audius tiếp
  tục xác nhận key này an toàn ở client. Không nhúng API Secret hoặc Bearer
  Token của nhà phát triển vào repo/binary.
- Tính năng tài khoản người dùng phải dùng OAuth Authorization Code +
  PKCE; access/refresh token của từng người dùng được mã hóa bằng DPAPI
  `CurrentUser`, có đăng xuất/xóa token và không ghi log.
- Chỉ stream. Không tải offline, không xuất file audio và không cache
  audio/metadata/artwork qua phiên. Buffer kỹ thuật trong bộ nhớ/phiên
  phải bị xóa khi app đóng hoặc provider bị tắt.
- Không đồng bộ toàn catalog xuống SQLite. Chỉ giữ ID tối thiểu trong các
  tính năng do người dùng chủ động tạo nếu việc đó vẫn phù hợp giới hạn
  consumer-data/session-cache; nếu chưa chắc chắn, prototype đầu tiên
  không lưu favorites/history Audius bền vững.
- Hiển thị creator, tên track, liên kết canonical tới track và thông tin
  OML/giấy phép. Dù OML mục 1.5 nêu attribution bắt buộc khi license cho
  mục đích thương mại, Mimi áp dụng attribution trong mọi trường hợp.
- Model dữ liệu phải mang `LicenseUri`/alternative-license state. Track có
  giấy phép thay thế không tương thích hoặc không xác định được quyền
  stream trong Music Player phải bị chặn thay vì phát thử.
- Tôn trọng rate limit hiện hành của API plan, dùng phân trang có trần,
  backoff khi `429`, và gửi app identification đúng tài liệu.
- Cung cấp Audius Privacy Policy/API Terms cho người dùng theo yêu cầu của
  API Terms tại bề mặt kết nối tài khoản hoặc trang attribution.
- Không dùng dữ liệu Audius để huấn luyện AI/ML.

## Rủi ro còn lại

- Giấy phép API có thể thu hồi/chấm dứt; OML là giấy phép riêng của hệ
  sinh thái và không biến catalog thành public domain.
- Creator chịu trách nhiệm clearance theo OML, nhưng Mimi vẫn cần cơ chế
  báo cáo/gỡ track khi nhận khiếu nại và không tiếp tục phát nội dung đã
  bị API rút.
- Điều khoản cache theo phiên là ràng buộc kiến trúc lớn, khác với thư
  viện local hiện tại. Đây là điều kiện tiên quyết trong thiết kế.

## Nguồn

- Audius API Terms: <https://audius.co/documents/ApiTerms.pdf>
- Audius Open Music License: <https://audius.org/open-music-license.pdf>
- Audius Terms of Use: <https://audius.co/documents/TermsOfUse.pdf>
- Audius Developer Docs: <https://docs.audius.co/>
- Audius SDK credentials: <https://docs.audius.co/sdk/>

## Trạng thái triển khai — 03/11/2026

Lát đầu tiên đã có trên nhánh làm việc: tìm kiếm và stream trong engine Mimi
(`AudiusCatalogService` + `AudioService.LoadRemoteAudio`), không credential,
không tải/cache, lọc `is_streamable`/`is_stream_gated`/`is_available`/
`allowed_api_keys`/`access.stream` trước khi hiển thị, công tắc cục bộ trong
Cài đặt. Chưa có tài khoản/OAuth, favorite, playlist, upload hay offline mode.
Đây chưa phải phê duyệt production: còn thiếu nghiệm thu runtime trên Windows
thật, trang privacy/attribution đầy đủ trong app, và kill switch từ xa.
Chi tiết thiết kế: [`../design/ONLINE-CATALOG.md`](../design/ONLINE-CATALOG.md).
