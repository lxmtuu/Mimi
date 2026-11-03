# Navidrome/OpenSubsonic và Jellyfin — đánh giá mô hình BYO server

Ngày rà soát: **03/11/2026**

## Kết luận

**ĐẠT theo mô hình “bring your own server”.** Đây là hướng ít rủi ro quyền
nội dung nhất vì Mimi không cung cấp catalog, không lưu trữ thư viện công
cộng và không phân phối bài hát. Người dùng tự nhập địa chỉ máy chủ của
chính họ (hoặc máy chủ họ được cấp quyền dùng), đăng nhập bằng tài khoản
của họ và chịu trách nhiệm về quyền đối với nội dung trên máy chủ đó.

Đánh giá này không biến nội dung trên một server tự host thành hợp pháp
nếu chủ server không có quyền. Nó chỉ kết luận rằng vai trò client của
Mimi tương thích với cổng thiết kế tại mục 8.5.

## Navidrome / Subsonic / OpenSubsonic

### Căn cứ kỹ thuật chính thức

- Tài liệu Navidrome cho biết server tương thích Subsonic API v1.16.1 với
  một số ngoại lệ và có endpoint `stream` trong nhóm Media Retrieval.
- Navidrome dùng ID dạng chuỗi. Mimi không được ép ID remote thành số
  nguyên hoặc đường dẫn file local.
- Tài liệu xác thực của Navidrome mô tả Subsonic authentication dùng các
  tham số `u`, `p`, `t`, `s`; externalized authentication qua reverse
  proxy là cấu hình phía server và client phải tôn trọng thông báo lỗi
  Subsonic.
- Navidrome có catalog client apps và chấp nhận ứng dụng hỗ trợ
  OpenSubsonic/Subsonic/Navidrome API nếu đáp ứng điều kiện niêm yết của
  họ. Việc phát triển client không phụ thuộc vào việc được niêm yết.

### Điều kiện bảo mật cho Mimi

- Mặc định ưu tiên HTTPS. Nếu người dùng cố tình dùng HTTP trên LAN, phải
  hiển thị cảnh báo rõ ràng và không âm thầm hạ cấp HTTPS.
- Ưu tiên token+salt của Subsonic thay cho gửi mật khẩu dạng `p` ở mỗi
  request khi server hỗ trợ. Không ghi URL có `u/p/t/s` vào log.
- Nếu phải lưu secret để tạo token cho phiên sau, chỉ lưu bằng DPAPI
  `CurrentUser`; có nút ngắt kết nối để xóa secret và dữ liệu phiên.
- Tôn trọng `downloadRole` và khả năng server công bố. Có endpoint
  `download` không có nghĩa Mimi được bật tải xuống nếu người dùng/tài
  khoản không có quyền.
- Kiểm tra chứng chỉ TLS bình thường. Không thêm tùy chọn “bỏ qua mọi lỗi
  chứng chỉ” trong prototype đầu tiên.

## Jellyfin

### Căn cứ kỹ thuật chính thức

- Jellyfin API ReDoc công bố các nhóm Authentication, Library, Artist và
  Audio, gồm endpoint lấy audio stream và luồng Quick Connect.
- Tài liệu Quick Connect của Jellyfin cho phép client mới hiển thị mã tạm
  thời để một client đã đăng nhập phê duyệt, tránh nhập mật khẩu trên
  thiết bị mới.
- Jellyfin duy trì danh sách client chính thức/bên thứ ba kết nối tới
  server của người dùng.

### Điều kiện bảo mật cho Mimi

- Ưu tiên Quick Connect; mật khẩu chỉ nhập trực tiếp tới server do người
  dùng chỉ định khi họ chọn Manual Login.
- Sau đăng nhập chỉ lưu access token và device id bằng DPAPI
  `CurrentUser`. Gửi token qua header `Authorization: MediaBrowser ...`
  với `Client="Mimi"`, `Device`, `DeviceId`, `Version`, `Token`; tránh
  `ApiKey` query parameter vì dễ lộ qua log/proxy.
- Có đăng xuất để báo session ended và xóa token local.
- Không dùng API key quản trị của server cho luồng người dùng thông
  thường.
- Ưu tiên HTTPS và kiểm tra chứng chỉ như Navidrome.

## Giới hạn sản phẩm

- Hướng này giúp nghe thư viện của chính người dùng từ xa, không giải
  quyết mong muốn “có sẵn nhiều bài như Spotify”.
- Mimi không được cung cấp danh sách server công cộng chứa nội dung của
  bên thứ ba, không chia sẻ tài khoản mẫu và không vượt quyền mà server
  cấp cho tài khoản đã đăng nhập.
- Giấy phép mã nguồn của Navidrome/Jellyfin áp dụng cho code của chính
  các dự án đó. Mimi chỉ gọi network API và không sao chép code server;
  nếu tương lai vendor SDK/client library, phải rà soát giấy phép của thư
  viện được vendor riêng.

## Nguồn

- Navidrome Subsonic API Compatibility:
  <https://www.navidrome.org/docs/developers/subsonic-api/>
- Navidrome Externalized Authentication:
  <https://www.navidrome.org/docs/usage/integration/authentication/>
- Navidrome Security Considerations:
  <https://www.navidrome.org/docs/usage/security/>
- Navidrome Client App Catalog:
  <https://www.navidrome.org/docs/developers/adding-apps/>
- Jellyfin API: <https://api.jellyfin.org/>
- Jellyfin Quick Connect:
  <https://jellyfin.org/docs/general/server/quick-connect/>
- Jellyfin Clients: <https://jellyfin.org/docs/general/clients/>
