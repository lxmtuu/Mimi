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
- Phát trực tiếp `url_resolved`; không proxy, ghi lại, tải xuống hoặc
  phân phối lại stream.
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

## Hiện trạng triển khai ngày 03/11/2026

Chủ dự án đã chọn Radio Browser cho prototype Online đầu tiên trên nhánh
làm việc. Bản prototype áp dụng các điều kiện trên như sau:

- Mọi request API đi qua mirror HTTPS được khám phá từ DNS của Radio
  Browser (có mirror dự phòng), chỉ chấp nhận host
  `api.radio-browser.info` hoặc subdomain của nó, kèm
  `User-Agent: Mimi/<version>`, timeout,
  backoff và giới hạn 60 kết quả/response tối đa 2 MiB.
- Chỉ hiển thị đài có `lastcheckok = 1`, không HLS và có `url_resolved`
  HTTP(S) không chứa thông tin đăng nhập nhúng, không trỏ tới
  localhost hoặc các dải IP private/link-local. Stream được mở trực tiếp
  bằng Media Foundation; Mimi không proxy, ghi, tải xuống, lưu vào thư
  viện/history/session hoặc tìm cách vượt giới hạn của đài.
- Click-counter được gọi đúng một request sau khi một đài mới do người
  dùng chọn đã bắt đầu phát thành công. Request này không retry/failover
  để tránh đếm trùng; hover, preload, pause/resume và tự vote không tạo
  click.
- UI ghi attribution Radio Browser, hiển thị tên/metadata/tag và mở
  homepage của đài khi có. Prototype không tải favicon, nên không phát
  sinh cache favicon; ô đại diện dùng chữ cái đầu do client tạo.
- Log không ghi search query hoặc toàn bộ stream URL; lỗi stream chỉ ghi
  station UUID/loại exception và log load chỉ ghi host.

Trạng thái này chưa phải phê duyệt production. Chủ dự án đã nghiệm thu
runtime prototype trên Windows thật với mạng/đài thật ngày 03/11/2026;
vẫn cần hoàn thành các cổng phát hành còn thiếu như kill switch trong
Settings và nội dung privacy/attribution trước khi bật mặc định cho
người dùng cuối.

## Nguồn

- Radio Browser API Reference: <https://docs.radio-browser.info/>
- Radio Browser API servers: <https://api.radio-browser.info/>
