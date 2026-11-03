# Lyrics offline — thiết kế lát Phase 6 đầu tiên

## Phạm vi

Lát này đưa lời bài hát offline vào thẻ **Lời bài hát** trong panel
Now Playing. Mimi chỉ dùng dữ liệu người dùng đã có trên máy; không tải
lời từ Internet và không triển khai nhà cung cấp trực tuyến.

Thứ tự nguồn:

1. File `.lrc` cùng thư mục và cùng tên gốc với file nhạc
   (ví dụ `song.flac` → `song.lrc`).
2. Lời nhúng trong tag của file nhạc, đọc bằng TagLib khi không có file
   `.lrc` usable đi kèm.

File sidecar tối đa 2 MiB. Việc đọc dùng UTF-8 và tự nhận diện BOM, nên
UTF-16 có BOM cũng được hỗ trợ qua `StreamReader`. Parser giữ tối đa
10.000 dòng để một file bất thường không làm nặng giao diện.

## Parser và đồng bộ

`LyricsTextParser` ở `Mimi.Core` hỗ trợ:

- metadata LRC dạng `[ti:]`, `[ar:]`, `[al:]`, `[by:]`, `[length:]`, v.v.
  (được bỏ qua khi hiển thị);
- một hoặc nhiều timestamp ở đầu cùng một dòng;
- timestamp `mm:ss`, `mm:ss.xx`, `mm:ss.xxx` và dạng có giờ
  `hh:mm:ss.xxx`;
- sắp xếp ổn định các dòng có timestamp;
- bỏ timestamp từng từ dạng `<mm:ss.xx>` của Enhanced LRC, nên lát này
  đồng bộ ở mức dòng, chưa tô sáng từng từ;
- văn bản thường không có timestamp cho lyrics nhúng chưa đồng bộ.

Dòng hiện tại được tìm bằng binary search mỗi khi vị trí phát thay đổi.
Timer vị trí của cửa sổ chính chạy mỗi 100 ms; dòng hiện tại được tô
accent, các dòng đã qua được làm mờ và danh sách tự cuộn để đặt dòng
hiện tại gần giữa thẻ.

## Quy ước offset

Có hai lớp offset cần phân biệt:

- Thẻ `[offset:]` trong file LRC được giữ thành `DocumentOffset`.
  Mimi áp dụng cách đọc phổ biến: giá trị dương làm lời xuất hiện sớm
  hơn, giá trị âm làm lời xuất hiện muộn hơn.
- Offset người dùng chỉnh trong giao diện dùng quy ước trực quan riêng:
  giá trị dương là **delay**, tức hiển thị lời muộn hơn. Các nút
  `−0.1s`/`+0.1s` chỉnh theo bước 100 ms, nút `0` đặt lại.

Thời điểm hiệu lực của một dòng là:

```text
timestamp gốc − [offset:] của file + delay người dùng
```

Offset người dùng được lưu theo đường dẫn file nhạc trong
`AppSettings.LyricsOffsetsByPath` (settings schema v6), giới hạn
±30 giây. Giá trị 0 không được lưu.

## Trạng thái giao diện

Thẻ Lyrics có các trạng thái: chưa chọn bài, đang tải, không tìm thấy,
không đọc được, không khả dụng, lời chưa đồng bộ và lời sẵn sàng.
Header cho biết nguồn là file `.lrc` hay lời nhúng. Nút tải lại giúp
người dùng thêm file `.lrc` trong lúc bài đang phát mà không cần
chuyển bài.

Việc nạp lyrics chạy bất đồng bộ theo `CurrentTrack`, có huỷ lượt nạp
của bài trước và không chặn đường phát nhạc nếu file lời hoặc tag bị lỗi.

Trên Stage toàn màn hình, panel lời lớn ở cạnh dưới hiển thị ba dòng
quanh vị trí phát: dòng trước và dòng kế tiếp nhỏ/mờ hơn, dòng hiện
tại lớn và phát sáng theo accent của theme. ViewModel phơi
`PreviousLyricLine`/`NextLyricLine` cùng nhịp cập nhật với dòng hiện
tại; `MainWindow` đẩy các dòng này sang `StageWindow.SetLyrics(...)`.
Panel chỉ xuất hiện với lyrics có timestamp — lyrics thường không
đồng bộ vẫn đọc tại thẻ Lyrics trong Now Playing. Giai đoạn intro
trước dòng đầu tiên hiển thị trước dòng sắp hát ở vị trí "kế tiếp".

## Chưa thuộc lát này

- Tô sáng từng từ/theo âm tiết của Enhanced LRC.
- Tải lyrics trực tuyến hoặc bất kỳ nguồn nhạc trực tuyến nào.
- Chỉnh sửa/tạo file `.lrc` trong app.
- Tự nhận diện encoding ANSI không có BOM ngoài UTF-8 mặc định.

## Kiểm chứng tự động

- `LyricsTextParserTests`: sắp xếp/mở rộng nhiều timestamp, Enhanced
  LRC, offset của file + delay người dùng và lyrics thường.
- `FileLyricsServiceTests`: ưu tiên sidecar, fallback embedded, sidecar
  rỗng và trường hợp không có lyrics.
- `UserDataServiceTests`: settings v6, clamp/loại bỏ offset 0 và
  migration từ schema cũ.
- `LocalizationServiceTests`: parity 344 khóa VI/EN và nhãn Lyrics.

Ảnh runtime ngày 03/11/2026 của chủ dự án đã xác nhận một file
`.lrc` thật được nạp và hiển thị trong thẻ Lyrics, với nhãn nguồn
`.lrc file`. Lượt kiểm tra tiếp theo cùng ngày xác nhận thêm: dòng
hiện tại được tô, nút ±0,1 giây đổi thời điểm hiện lời đúng chiều và
offset vẫn còn sau khi đóng/mở lại app. Độ lệch vài giây với bài thử
là lệch đều và chỉnh bằng offset thì khớp, phù hợp với file `.lrc`
được canh cho một bản audio khác hơn là lỗi parser.

Cùng lượt đó chủ dự án phát hiện danh sách **nhảy xuống dòng cuối**
mỗi khi dòng hiện tại đổi. Nguyên nhân: `ListBox` để chế độ cuộn
logic theo item nên `VerticalOffset`/`ViewportHeight` tính bằng số
dòng, trong khi behavior cộng chúng với tọa độ pixel từ
`TransformToAncestor`; kết quả bị clamp về cuối danh sách. Fix ép
`VirtualizingPanel.ScrollUnit="Pixel"` cho danh sách lời và behavior
tự bỏ qua bước căn giữa bằng pixel khi gặp cuộn logic. Chủ dự án đã
kiểm tra lại trên Windows thật và xác nhận hành vi cuộn đã ổn. Hai
ảnh runtime tiếp theo xác nhận panel lời lớn trên Stage hiển thị và
chuyển dòng đúng trên Windows thật, gồm cả trạng thái intro khi dòng
đầu tiên còn ở vị trí kế tiếp. Lượt thử còn lại là một bài chỉ có
lyrics nhúng không đồng bộ.
