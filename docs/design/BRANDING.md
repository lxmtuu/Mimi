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
- `src/Mimi.App/Assets/mimi-256.png`: bản 256×256 được đóng
  gói dạng WPF `Resource` và dùng làm dấu hiệu thương hiệu ở
  góc trái header trong cửa sổ chính, đồng thời dùng cho tài
  liệu khi cần bản xem trước.
- `src/Mimi.App/Assets/mimi.ico`: icon ứng dụng dạng ICO gồm 9
  cỡ 16, 20, 24, 32, 40, 48, 64, 128, 256 px, được nối bằng
  `<ApplicationIcon>Assets\mimi.ico</ApplicationIcon>` trong
  `Mimi.App.csproj` cho file EXE, đồng thời đóng gói dạng WPF
  `Resource` và đặt làm thuộc tính `Icon` của cửa sổ chính cho
  thanh tiêu đề/Alt-Tab. Icon này thay thế icon tạm trước đó
  (ô xanh ngọc chữ M đen).

Riêng nút Taskbar còn một lớp nữa vì Mimi dùng AppUserModelID
tường minh `Mimi.App`: Windows có thể lấy icon của nhóm Taskbar
từ shortcut/relaunch identity của Shell dù icon trên thanh
tiêu đề đã đúng. Khi chạy, app vì vậy trích ICO nhúng thành file
độc lập `%LocalAppData%\Mimi\icons\mimi-<12 ký tự hash>.ico`,
rồi công bố file đó qua các thuộc tính
`System.AppUserModel.ID/RelaunchCommand/RelaunchIconResource/
RelaunchDisplayNameResource` trên chính HWND cửa sổ chính bằng
`SHGetPropertyStoreForWindow`; các thuộc tính được xóa lúc cửa
sổ đóng. Các giá trị relaunch được viết trước, AppUserModelID
của cửa sổ được đóng dấu sau cùng, vì property store của HWND áp
dụng từng lần ghi ngay lập tức. Tên file chứa hash nội dung nên
mỗi lần đổi icon sẽ có đường dẫn mới, không phụ thuộc việc
Explorer tự làm mới cache cho đường dẫn cũ. Shortcut identity ở
Start Menu và các giá trị icon trong đăng ký Explorer cũng dùng
cùng file ICO độc lập này khi được tạo/cập nhật. Nếu shortcut
Start Menu của Mimi đã tồn tại, app còn tự kiểm tra và sửa
target/IconLocation/AppUserModelID của nó ở mỗi lần khởi động
(rồi báo Explorer làm mới khi có thay đổi); app không tự tạo
shortcut mới ở bước này nếu người dùng chưa chọn đăng ký
Explorer.

Lưu ý về nút đã ghim trên Taskbar từ trước: shortcut ghim cũ
đã được Windows tạo trước khi app công bố relaunch identity ở
trên vẫn có thể giữ icon riêng của nó. Cách xử lý một lần là
đóng app, bỏ ghim nút cũ, chạy bản build mới rồi ghim lại từ
nút đang chạy.

`tests/Mimi.Tests/AppIconTests.cs` gác định dạng ICO/các cỡ
chuẩn, tính vuông của hai PNG và toàn bộ wiring
(`ApplicationIcon`, hai mục `Resource`, logo ở header,
`Window.Icon`, file ICO độc lập cho Shell và thuộc tính
relaunch của cửa sổ).

## Cách xử lý từ ảnh concept

1. Ảnh concept nền trắng được tách nền bằng flood fill từ các góc
   (Pillow, ngưỡng 80) để lấy đúng ô tile bo góc; phép quét pixel
   xác nhận nốt nhạc trên đầu nằm gọn bên trong tile (mép tile tại
   y≈75, điểm hồng cao nhất của nốt tại y≈138 trên khung 1024).
2. Crop theo khung tile (874×874), sau đó gọt ~2 px viền alpha
   (MinFilter 5 trên kênh alpha đã đệm trong suốt) để bỏ dải sáng
   anti-alias còn sót từ nền trắng, cho master cuối 870×870.
3. Thu 256×256 bằng Lanczos cho `mimi-256.png`. Riêng
   `mimi.ico` được đóng thủ công từ master theo kiểu tương
   thích cổ điển: frame 16–64 px mã hoá BMP/DIB 32-bit không
   nén, frame 128/256 px nén PNG (256 bắt buộc PNG). Không
   dùng cách xuất ICO mặc định của Pillow vì nó nén PNG cho
   mọi frame — WPF vẫn đọc được (thanh tiêu đề/header hiện
   đúng) nhưng đường trích icon cỡ nhỏ của Windows Shell cho
   taskbar/shortcut tin cậy nhất với frame BMP; bản ICO thuần
   PNG từng khiến taskbar giữ nguyên icon tạm cũ.

Khi sửa logo, hãy sửa ở `mimi-logo.png` rồi xuất lại
`mimi-256.png` và `mimi.ico` theo đúng các cỡ cùng kiểu mã
hoá frame ở bước 3 để test gác không bị vỡ.
