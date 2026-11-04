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
- đọc timestamp từng từ dạng `<mm:ss.xx>` của Enhanced LRC vào
  `LyricLine.WordTimings` (bỏ khỏi chữ hiển thị), nên tô sáng được từng từ;
- văn bản thường không có timestamp cho lyrics nhúng chưa đồng bộ.

## Tô sáng từng từ (karaoke)

Enhanced LRC có timestamp cho từng từ/âm tiết nằm ngay trong dòng:

```
[00:12.34]<00:12.34>あ<00:12.80>の<00:13.10>空
[00:20.00]<00:20.00>Hello <00:20.45>from <00:20.90>the <00:21.30>stage
```

`LyricsTextParser` tách các mốc này ra khỏi chữ và lưu thành `LyricWord`.
**Bất biến quan trọng:** ghép toàn bộ `Words` phải ra đúng `Line.Text` — nếu
không, cùng một file sẽ hiển thị khác nhau tuỳ app có hiểu tag hay không. Vì
vậy văn bản trước tag đầu tiên được gộp vào từ đầu tiên, và chỉ cắt khoảng
trắng ở hai đầu dòng (không chèn space), để tiếng Nhật/Trung không bị thêm dấu
cách.

Mỗi lần timer vị trí (100 ms) chạy:

1. `FindCurrentLineIndex` chọn dòng như cũ;
2. `FindCurrentWordIndex` trả về từ đang hát trong dòng đó, dùng **cùng phép
   tính offset** với dòng (`position + DocumentOffset - userDelay`), nên
   `[offset:]` của file và delay ±0,1 giây vẫn đúng ở mức từ;
3. `MainWindowViewModel` chỉ phát tín hiệu khi **dòng hoặc từ** đổi, và chỉ
   cập nhật từ đang hát — không dựng lại danh sách.

Hiển thị: `KaraokeTextBlock` (kế thừa `TextBlock`) vẽ mỗi từ một `Run`, từ đã
hát dùng `SungBrush`, từ còn lại dùng `PendingBrush`. Chỉ **dòng đang hát** được
tô từng từ; dòng trước/sau giữ nguyên kiểu chữ cũ. Dòng không có word timing
hiển thị như text thường, nên file `.lrc` thường không đổi gì.

| Bề mặt | Từ đã hát | Từ còn lại |
|---|---|---|
| Now Playing | `AccentBrush` | `TextMutedBrush` |
| Stage (chữ lớn) | `AccentBrush` + glow accent | `TextSecondaryBrush` |

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

- Tải lyrics trực tuyến hoặc bất kỳ nguồn nhạc trực tuyến nào.
- Chỉnh sửa/tạo file `.lrc` trong app.
- Tự nhận diện encoding ANSI không có BOM ngoài UTF-8 mặc định.

## Kiểm chứng tự động

- `LyricsTextParserTests`: sắp xếp/mở rộng nhiều timestamp, Enhanced
  LRC, offset của file + delay người dùng và lyrics thường.
- `LyricsKaraokeTests`: tách từ có/không có dấu cách (Latin và Nhật), bất
  biến "ghép từ = nguyên dòng", văn bản trước tag đầu tiên, con trỏ từ
  theo vị trí phát, cả hai quy ước offset ở mức từ, và dòng không có
  word timing.
- `KaraokeTextBlockTests`: dòng thường vẫn là text, mỗi từ là một `Run`,
  màu đổi theo tiến độ, và bỏ word timing thì quay về text thường.
- `FileLyricsServiceTests`: ưu tiên sidecar, fallback embedded, sidecar
  rỗng và trường hợp không có lyrics. Hai test tích hợp dùng
  `FileLyricsService` mặc định với một file FLAC tối thiểu được chính
  TagLib# ghi trường `Lyrics`, xác thực cả lời nhúng thường không
  timestamp lẫn lời nhúng dạng LRC có timestamp qua đường đọc thật,
  không qua delegate giả.
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
đầu tiên còn ở vị trí kế tiếp. Đường đọc lời nhúng có test tích hợp thật trên Windows CI
(`41614648545`), và ngày 03/11/2026 chủ dự án đã xác nhận trực tiếp
trên Windows thật rằng bài chỉ có lyrics nhúng không đồng bộ hiển
thị đạt. Cùng lượt nghiệm thu đó, tiêu chí dòng `.lrc` khớp nhịp
trong khoảng ±100 ms sau offset cũng được xác nhận đạt; hai gate
runtime còn lại của Lyrics coi như đã đóng trên nhánh làm việc.

## Kiểm tra nhịp tô từng từ trên máy thật

Tô sáng từng từ chỉ chứng minh được bằng mắt: parser và offset có test,
nhưng "từ sáng đúng lúc" thì phải nhìn. Để không phải tự dựng file
Enhanced LRC, dự án có sẵn một file mẫu:

- [`docs/examples/karaoke-sample.lrc`](../examples/karaoke-sample.lrc) — lời
  tự viết (không trích bài hát nào), 12 dòng có timestamp nằm trong
  0:06–0:52, trong đó **11 dòng có word timing**, **1 dòng cố ý không có**,
  và **1 dòng tiếng Nhật** mỗi ký tự một mốc.
- Hướng dẫn chép/đổi tên và danh sách điều cần thấy:
  [`docs/examples/README.md`](../examples/README.md).

File mẫu không thể lệch khỏi parser mà CI vẫn xanh:
`KaraokeSampleFileTests` đọc chính file đó và kiểm tra bất biến "ghép các từ
ra đúng chữ của dòng", mốc từ không lùi, không có dòng chú thích (parser không
có cú pháp comment — dòng lạ sẽ thành một dòng lời).

Bốn điều cần thấy khi chạy, theo thứ tự dễ nhầm:

1. **Từng từ** sáng dần trong dòng đang hát, không phải cả dòng đổi một lần.
2. Dòng ở `0:20` tô **cả dòng** — dòng không có word timing phải giữ nguyên
   hành vi cũ, không bị tách từ.
3. Dòng tiếng Nhật ở `0:24` sáng theo **ký tự** và **không sinh dấu cách**.
4. Nút `−0.1s`/`+0.1s` làm **cả dòng và từ** trượt cùng một lượng — nếu chỉ
   dòng trượt thì offset ở mức từ đang sai.

**Kết quả 04/11/2026:** chủ dự án đã chạy và xác nhận đạt — tô theo từng từ,
dòng không có word timing vẫn tô cả dòng, và tiếng Nhật không sinh dấu cách.
Tiêu chí karaoke của Phase 6 vì thế `PASS` về mặt runtime; phần offset ở mức từ
(điều 4) đã có test gác và cũng được xác nhận trong cùng lượt.
