# Radio Browser — đánh giá provider

Ngày rà soát: **03/11/2026**

## Kết luận

**ĐẠT CÓ ĐIỀU KIỆN** cho một client thư mục/phát đài Internet chỉ đọc.
Radio Browser phù hợp làm prototype Online đầu tiên nếu Mimi muốn có nội dung
nghe ngay, nhưng đây là radio tuyến tính chứ không phải catalog chọn bài.

## Căn cứ chính thức

- API Reference giới thiệu webservice “can be used freely” nhưng không bảo
  đảm hoạt động/uptime.
- Tài liệu yêu cầu gửi `User-Agent` mô tả ứng dụng theo dạng
  `appname/appversion` để nhà vận hành nhận biết client.
- Station object cung cấp `url`, `url_resolved`, homepage, favicon, codec,
  bitrate, `lastcheckok`, HLS flag và các trường kiểm tra trạng thái.
- Endpoint click-counter được mô tả là nên gọi mỗi khi người dùng bắt đầu
  phát một stream. Một IP/station chỉ được tính một lần mỗi ngày.
- API có nhiều mirror và endpoint server mirrors; client không nên phụ
  thuộc cứng vào một host duy nhất khi triển khai thật.

## Điều kiện triển khai trong Mimi

- Gửi `User-Agent: Mimi/<version>` cho mọi API request.
- Ưu tiên HTTPS cho API. Tự chọn mirror lành mạnh, có timeout/backoff và
  không quét toàn bộ database vô hạn.
- Khi người dùng bấm phát, gọi click-counter đúng một lần cho lần phát đó;
  không tự vote và không tạo click giả từ thao tác hover/preload.
- Phát trực tiếp `url_resolved` sau khi click endpoint trả về thông tin;
  không proxy, ghi lại, tải xuống hoặc phân phối lại stream.
- Ẩn hoặc đánh dấu station có `lastcheckok = 0`, hỗ trợ codec/HLS theo khả
  năng thật của engine và báo lỗi stream rõ ràng.
- Hiển thị tên đài và liên kết homepage khi có. Favicon chỉ nên cache tạm
  theo phiên trong prototype đầu tiên.
- Một số stream có thể là HTTP, chuyển hướng, có quảng cáo hoặc giới hạn
  địa lý. Mimi không được vượt các giới hạn đó và phải cho người dùng biết
  stream đến trực tiếp từ đài bên thứ ba.

## Rủi ro còn lại

- Radio Browser là thư mục cộng đồng; tính hợp lệ, chất lượng và tính sẵn
  sàng của từng station/stream do đài và người gửi dữ liệu quyết định.
- Tài liệu API không phải giấy phép nội dung cho chương trình của từng đài.
  Mô hình được chấp nhận ở đây là client phát URL đài công khai trực tiếp,
  không lưu trữ hay tái phân phối nội dung.
- Không có cam kết uptime. Mimi phải giữ Online là tính năng opt-in và thư
  viện local vẫn hoạt động khi API/mirror lỗi.

## Nguồn

- Radio Browser API Reference: <https://docs.radio-browser.info/>
- Radio Browser API servers: <https://api.radio-browser.info/>
