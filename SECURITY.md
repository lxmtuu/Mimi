# Bảo mật & quyền riêng tư / Security & privacy

## Báo lỗi bảo mật

**Không** mở issue công khai cho lỗ hổng bảo mật. Dùng
[GitHub Security Advisory](https://github.com/lxmtuu/Mimi/security/advisories/new)
của repo (mục *Security* → *Report a vulnerability*), hoặc mở issue chỉ ghi
"cần liên hệ riêng về bảo mật" nếu bạn không dùng được tính năng đó.

Vui lòng kèm: phiên bản (footer hiện `Mimi 0.7.0 · <commit>`), bước tái hiện,
ảnh hưởng dự kiến, và mức độ bạn muốn được ghi công. Mục tiêu phản hồi ban đầu:
7 ngày.

## Phiên bản được hỗ trợ

| Phiên bản | Trạng thái |
|---|---|
| `0.7.x` | Nhận báo lỗi bảo mật |
| `0.6.x` và cũ hơn | Không hỗ trợ; nâng lên 0.7.x |

## Mô hình mối đe doạ

Mimi là ứng dụng desktop chạy **trong tài khoản người dùng**, không có server
của dự án, không có telemetry, không có tài khoản Mimi. Vì vậy bề mặt rủi ro
thật sự là:

1. **Dữ liệu người dùng trên máy** — thư viện, settings, log. Ai đọc được ổ đĩa
   của bạn thì đọc được chúng; Mimi không mã hoá chúng (không thể bảo vệ dữ liệu
   khỏi chính người dùng máy).
2. **Khoá API của bạn** — khoá YouTube Data API do bạn tự đặt; xem mục dưới.
3. **Nội dung từ mạng** — metadata từ Radio Browser, Audius, YouTube, và trang
   player nhúng. Mọi thứ được coi là dữ liệu không tin cậy: không chạy script từ
   nguồn ngoài, không dùng chuỗi từ API để dựng đường dẫn tệp, không tự mở URL
   từ metadata mà không qua kiểm tra `http(s)`.

## Dữ liệu Mimi lưu ở đâu

Tất cả nằm trong `%LOCALAPPDATA%\Mimi` (một số đường dẫn legacy trong
`%LOCALAPPDATA%\MikuVerseMusic` chỉ được **đọc** để migration):

| Tệp | Nội dung |
|---|---|
| `library.db` | Catalog SQLite: đường dẫn tệp, metadata, playlist, lịch sử nghe |
| `settings.json` | Tuỳ chọn, vị trí cửa sổ, phiên nghe đang tạm dừng, offset lời, theme |
| `logs\mimi-*.log` | Log chẩn đoán, xoay vòng theo ngày |
| `themes\*.json` | Theme cộng đồng bạn tự thêm |

Xoá cả thư mục là xoá sạch dữ liệu của Mimi. Không có bản sao nào ở nơi khác.

## Khoá YouTube

Mimi **không** có ô nhập khoá trong giao diện và **không** lưu khoá vào
`settings.json`. Khoá được đọc từ:

- biến môi trường `MIMI_YOUTUBE_API_KEY`, hoặc
- tệp `youtube-api-key.txt` trong thư mục dữ liệu (tự tạo, tự quản lý quyền).

Khoá không bao giờ được ghi vào log, không hiện trong UI, không gửi đi đâu
ngoài `googleapis.com`.

## Mimi không làm gì

- Không có telemetry, không có "phone home", không có mã định danh người dùng.
- Không tải, không trích xuất, không cache dữ liệu âm thanh hay video từ dịch
  vụ trực tuyến. Phát là phát trực tiếp qua API/player chính thức.
- Không chặn quảng cáo, không phá DRM, không vượt kiểm soát truy cập.
- Không đọc tệp ngoài những gì bạn chọn hoặc thư mục bạn thêm vào thư viện.

## Nếu bạn phát hiện Mimi gửi dữ liệu đi đâu đó

Đó là lỗi bảo mật, không phải "tính năng". Báo theo mục đầu tiên của tệp này.
