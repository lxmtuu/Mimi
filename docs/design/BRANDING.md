# Nhận diện thương hiệu Mimi

Tài liệu này ghi lại logo/icon chính thức của ứng dụng và cách tái
tạo bộ icon từ file gốc.

## Logo đã chốt (03/11/2026)

Chủ dự án chọn phương án **đầu mascot Mimi** trong ba concept được
thiết kế cho dự án: đầu chibi tóc tím/hồng lavender, tai nghe tai
mèo có vòng neon cyan, nốt nhạc hồng mọc trên đầu, đặt trong ô
vuông bo góc nền navy đậm — đồng bộ với mascot
`mimi-mascot-v1.png` và bảng màu Neon Stage. Hai concept còn lại
(chữ M sóng âm và tai nghe tai mèo neon dạng line-art) không được
chọn và không đưa vào repo.

Logo là hình gốc được tạo riêng cho Mimi; repo không chứa tài sản
sở hữu trí tuệ của bên thứ ba.

## File trong repo

- `src/Mimi.App/Assets/mimi-logo.png`: file gốc (master) 870×870
  RGBA sau khi xử lý, góc bo trong suốt. Đây là nguồn sự thật để
  xuất lại các cỡ khác.
- `src/Mimi.App/Assets/mimi-256.png`: bản 256×256 dùng cho hiển
  thị/tài liệu.
- `src/Mimi.App/Assets/mimi.ico`: icon ứng dụng dạng ICO gồm 9
  cỡ 16, 20, 24, 32, 40, 48, 64, 128, 256 px, được nối bằng
  `<ApplicationIcon>Assets\mimi.ico</ApplicationIcon>` trong
  `Mimi.App.csproj` nên xuất hiện ở file EXE, thanh tiêu đề,
  taskbar và Alt-Tab. Icon này thay thế icon tạm trước đó (ô xanh
  ngọc chữ M đen).

`tests/Mimi.Tests/AppIconTests.cs` gác ba điểm: file ICO còn parse
được và giữ các cỡ chuẩn, hai file PNG giữ dạng vuông, và project
file còn trỏ đúng `ApplicationIcon`.

## Cách xử lý từ ảnh concept

1. Ảnh concept nền trắng được tách nền bằng flood fill từ các góc
   (Pillow, ngưỡng 80) để lấy đúng ô tile bo góc; phép quét pixel
   xác nhận nốt nhạc trên đầu nằm gọn bên trong tile (mép tile tại
   y≈75, điểm hồng cao nhất của nốt tại y≈138 trên khung 1024).
2. Crop theo khung tile (874×874), sau đó gọt ~2 px viền alpha
   (MinFilter 5 trên kênh alpha đã đệm trong suốt) để bỏ dải sáng
   anti-alias còn sót từ nền trắng, cho master cuối 870×870.
3. Thu 256×256 bằng Lanczos cho `mimi-256.png`; `mimi.ico` được
   xuất từ chính bản 256 đó với 9 cỡ liệt kê ở trên.

Khi sửa logo, hãy sửa ở `mimi-logo.png` rồi xuất lại
`mimi-256.png` và `mimi.ico` theo đúng các cỡ này để test gác
không bị vỡ.
