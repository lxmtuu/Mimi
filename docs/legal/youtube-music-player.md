# YouTube Music — mô hình "host player chính thức"

Ngày rà soát: **03/11/2026**

Tài liệu này ghi lại **một mô hình tích hợp khác** với các mục
[YouTube bị loại](rejected-platforms.md#youtube). Đây không phải tư vấn pháp lý
và không thay đổi kết luận cũ: Mimi **không** trích xuất audio từ YouTube.

## Hai mô hình khác nhau

| | Trích xuất âm thanh (bị loại) | Host player chính thức (đang làm) |
| --- | --- | --- |
| Cách truy cập | Tự lấy luồng audio từ URL video | Điều hướng WebView2 tới `https://music.youtube.com/` |
| Ai phục vụ nội dung | Mimi tự tải và phát | Chính trang của YouTube Music |
| Player | Player riêng của Mimi | Player chính thức, không sửa |
| Quảng cáo | Bị bỏ qua | Giữ nguyên |
| Tải/cache nội dung | Có | Không |
| Rủi ro điều khoản | Cao | Không vượt cơ chế truy cập |

Mô hình thứ hai đúng với cách truy cập được YouTube nêu trong điều khoản: nội
dung chỉ được truy cập qua trang phát của dịch vụ, player nhúng, hoặc phương
thức được YouTube cho phép rõ ràng.

## Mimi làm gì

- Mở một cửa sổ riêng chứa **đúng** trang `music.youtube.com`, hiển thị, không
  ẩn, không chạy nền.
- Dùng profile WebView2 riêng để giữ **phiên đăng nhập của chính người dùng**
  giữa các lần chạy, giống một profile trình duyệt.
- Chuyển link mở cửa sổ mới sang trình duyệt mặc định của người dùng.
- Có công tắc tắt trong Cài đặt; tắt là đóng cửa sổ và ẩn mục điều hướng.

## Mimi không làm gì

- Không chặn, thay thế, che hay vô hiệu hoá quảng cáo của YouTube.
- Không sửa, che hoặc thay phần tử nào của player.
- Không tự động bấm play, không điều khiển phát bằng script từ bên ngoài.
- Không tải, không trích xuất, không chuyển mã, không lưu bản sao audio.
- Không tạo cache audio/metadata/artwork riêng; không có offline mode.
- Không đưa nội dung online vào `Track`, queue, history, favorites, lyrics
  hay phiên lưu tự động của thư viện cục bộ.

Yêu cầu tải xuống từ trang bị **từ chối**, không lưu vào chỗ người dùng không
thấy.

## Hệ quả cần nói thật với người dùng

1. **Không có quảng cáo chỉ khi người dùng tự đăng nhập YouTube Premium** của
   họ. Mimi không can thiệp để bỏ quảng cáo.
2. **Audio chạy trong player của trang, không chạy qua engine NAudio của
   Mimi.** Visualizer, queue, crossfade, gapless, SMTC và seek của Mimi không
   áp dụng cho nội dung này.
3. Cần **Microsoft Edge WebView2 Runtime**. Nếu máy chưa có, Mimi hiển thị
   thông báo rõ và nút mở trang tải runtime, không im lặng thất bại.
4. Đóng cửa sổ là dừng phát. Mimi không giữ "âm thanh ma" chạy nền sau khi
   người dùng rời đi.
5. Profile WebView2 có HTTP cache chuẩn của trình duyệt. Đó là hành vi của
   trình duyệt nhúng, không phải cache nội dung do Mimi tạo và không phải
   chức năng tải offline.

## Vì sao không dùng `yt-dlp`/savefrom/trang ripper

Đã cân nhắc và loại. Mọi biến thể — nhúng `yt-dlp`, gọi API của trang ripper,
hay tự dựng server tách audio — đều là cùng một hành vi: lấy nội dung có bản
quyền bằng cách bỏ qua cơ chế truy cập mà YouTube đặt ra. Việc chạy cục bộ,
gắn nhãn "thử nghiệm", hay không phát hành công khai không thay đổi bản chất
đó. Tiền lệ Groovy (2021) cho thấy nền tảng xử lý mô hình này bằng thư yêu cầu
dừng.

## Điều kiện trước khi phát hành

- Rà lại Điều khoản dịch vụ và Developer Policies của YouTube tại thời điểm
  phát hành.
- Không thêm bất kỳ plugin chặn quảng cáo, tải xuống hay "bypass" nào.
- Ghi rõ trong tài liệu người dùng rằng đây là player nhúng của bên thứ ba.
- Giữ công tắc tắt hoạt động được offline và không phụ thuộc mạng.
