# YouTube Music — đã xem xét mô hình "host player chính thức"

Ngày rà soát: **03/11/2026**

Tài liệu này ghi lại **hai mô hình tích hợp đã cân nhắc và kết luận**, để sau
này không ai mở lại vòng tranh luận cũ. Đây không phải tư vấn pháp lý.

## Kết luận hiện tại

Mimi **không** dùng YouTube hay YouTube Music làm nguồn phát. Nhu cầu "gõ tên
bài rồi nghe ngay trong Mimi" được đáp ứng bằng
[catalog Audius trong app](../design/ONLINE-CATALOG.md), nơi audio chạy qua
engine của Mimi.

## Mô hình 1 — Trích xuất âm thanh từ YouTube (bị loại)

| | |
| --- | --- |
| Cách truy cập | Tự lấy luồng audio từ URL video (`yt-dlp`, API trang ripper, server tách audio tự dựng) |
| Ai phục vụ nội dung | Mimi tự tải rồi phát |
| Quảng cáo | Bị bỏ qua |
| Tải/cache nội dung | Có |

Lý do loại: YouTube chỉ cho phép truy cập nội dung qua trang phát, player nhúng
hoặc phương thức được cho phép rõ ràng; đồng thời cấm tải/cache nội dung khi
chưa có phê duyệt trước bằng văn bản, cấm tách riêng thành phần audio/video và
cấm phát từ player nền không hiển thị. Việc chạy cục bộ, gắn nhãn "thử nghiệm"
hay không phát hành công khai không thay đổi bản chất hành vi. Tiền lệ Groovy
(2021) cho thấy nền tảng xử lý mô hình này bằng thư yêu cầu dừng.
Chặn quảng cáo trong player nhúng cũng bị cấm tương tự và không khả thi về kỹ
thuật vì iframe khác origin.

## Mô hình 2 — Nhúng trang `music.youtube.com` trong cửa sổ Mimi (đã dựng, rồi bỏ)

Đã triển khai trên nhánh làm việc rồi **gỡ bỏ** vì không đáp ứng yêu cầu sản
phẩm: đây chỉ là một cửa sổ trình duyệt nhúng. Audio chạy trong player của
trang, nên visualizer, queue, seek, crossfade, SMTC và phiên phát của Mimi
không áp dụng được. Nó cũng thêm phụ thuộc WebView2 Runtime mà không mang lại
trải nghiệm "nhạc nằm trong Mimi".

Bài học giữ lại: nếu sau này cần lại mô hình nhúng, các ranh giới bắt buộc là
không chặn quảng cáo, không sửa player, không trích xuất hay tải nội dung,
không cache riêng, và phải hiển thị công khai — không có player nền.

## Vì sao không dùng `yt-dlp`/savefrom/trang ripper

Mọi biến thể — nhúng `yt-dlp`, gọi API của trang ripper, hay tự dựng server tách
audio — đều là cùng một hành vi: lấy nội dung có bản quyền bằng cách bỏ qua cơ
chế truy cập mà YouTube đặt ra. Mimi không triển khai bất kỳ biến thể nào, kể cả
trong nhánh thử nghiệm, vì mã nguồn ở đây được đẩy lên CI và hướng tới bản phát
hành cộng đồng.
