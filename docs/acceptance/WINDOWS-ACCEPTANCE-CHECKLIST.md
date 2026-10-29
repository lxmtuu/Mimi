# Windows Acceptance Checklist

Checklist này dùng cho các phiên nghiệm thu thủ công trên Windows thật.
Đừng sửa trực tiếp file này để ghi kết quả của một lần chạy; hãy sao chép
[`RESULT-TEMPLATE.md`](RESULT-TEMPLATE.md) ra ngoài repo hoặc dán nội dung vào
báo cáo/PR. Kết quả tổng hợp sau cùng mới được đưa vào
[`ROADMAP.md`](../../ROADMAP.md).

Ký hiệu trong báo cáo: `PASS`, `PARTIAL`, `FAIL`, `BLOCKED`, `NOT RUN`,
`N/A`. Một cổng chỉ là `PASS` khi mọi hạng mục bắt buộc của nó đều đạt.

## Lượt chạy tối thiểu đầu tiên

Nếu thời gian có hạn, hãy làm **S0 + S1** trước. Đây là phần cho bằng chứng
mới cao nhất với chi phí thấp nhất: không cần thiết bị âm thanh thứ hai và
không cần disable endpoint.

---

## S0 — Chuẩn bị và cổng tự động nền

### Ghi lại môi trường

- [ ] Ngày/giờ bắt đầu và múi giờ.
- [ ] Windows edition, version và OS build (Settings → System → About).
- [ ] Máy/GPU nếu sẽ kiểm tra visualizer.
- [ ] Endpoint âm thanh mặc định theo đúng tên Windows hiển thị.
- [ ] Nhánh và commit:

```powershell
git branch --show-current
git rev-parse --short HEAD
git status --short
```

`git status --short` nên không in thay đổi nào. Nếu có thay đổi local, ghi rõ
chúng là gì; không âm thầm coi bản có thay đổi là bản tại commit đã ghi.

### Bảo vệ dữ liệu trước các phiên rủi ro

Bắt buộc sao lưu trước S2, test migration và mọi thử nghiệm tắt app đột ngột:

1. Đóng Mimi hoàn toàn.
2. Xác nhận không còn tiến trình:

```powershell
Get-Process Mimi -ErrorAction SilentlyContinue
```

3. Sao chép toàn bộ thư mục dữ liệu:

```powershell
$backup = Join-Path $HOME ("Mimi-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA "Mimi") `
  -Destination $backup -Recurse
$backup
```

Không sao lưu khi app đang chạy vì SQLite có thể đang có file WAL chưa được
checkpoint. Không dùng bản dữ liệu duy nhất cho test tắt giữa lúc ghi.

### Cổng tự động

Tại thư mục gốc repo:

```powershell
dotnet restore .\Mimi.sln
dotnet build .\Mimi.sln --configuration Release --no-restore
dotnet test .\tests\Mimi.Tests\Mimi.Tests.csproj --configuration Release --no-build
```

Ghi lại:

- [ ] Restore thành công.
- [ ] Release build: số warning/error.
- [ ] Unit test: passed/failed/skipped và tổng số.
- [ ] Đường dẫn `Mimi.exe` Release sẽ dùng cho các phiên thủ công.

Exe Release hiện tại:

```text
.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe
```

Cổng S0 đạt không làm bất kỳ cổng thủ công nào thành `PASS`; nó chỉ xác nhận
bản được đem đi nghiệm thu build/test được.

### Vật liệu kiểm tra nên chuẩn bị

- Ít nhất ba bài phát tốt liên tiếp trong cùng một danh sách/playlist.
- Ít nhất hai bài có metadata và ảnh bìa khác nhau cho SMTC.
- Nếu dùng bộ fixture tổng hợp có manifest/hash phát hành, hãy tính lại
  SHA-256 và kích thước của từng file thực tế trước khi nhập vào app. Hash
  không khớp là `Observation` về provenance: kết quả chức năng vẫn gắn với
  đúng bộ byte đã nhập, nhưng trước khi chốt hồ sơ phải đóng băng đầy đủ
  SHA-256/kích thước và tóm tắt tag của bộ thực tế trong báo cáo ngoài repo.
- Một đoạn nhạc có tín hiệu liên tục, tránh intro im lặng khi đo loopback.
- Nếu có sẵn: file mono, các sample rate khác nhau và các định dạng trong S4.
- Ghi giờ hệ thống ngay trước từng kịch bản quan trọng để đối chiếu log tại
  `%LocalAppData%\Mimi\logs\mimi-YYYYMMDD.log`.

---

## S1 — Tương tác Windows ở chế độ `auto`

Đóng mọi tiến trình Mimi, xoá biến override khỏi phiên PowerShell rồi mở bản
Release:

```powershell
Remove-Item Env:MIMI_AUDIO_OUTPUT -ErrorAction SilentlyContinue
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe'
```

Phát một bài có title/artist/album/cover. Ghi endpoint và backend hiển thị
dưới **VOLUME**/trong mục **Audio output** của Settings.

### P4-A — SMTC và Windows media flyout

Mở bề mặt media mà phiên bản Windows cung cấp. Trên Windows 11, hãy mở
**Quick Settings** bằng `Win+A` hoặc click trực tiếp cụm biểu tượng
Network/Volume/Battery ở taskbar rồi tìm thẻ media nằm phía trên/bên trong
Quick Settings. `Win+N` mở Notification Center, không phải bề mặt cần kiểm
tra. Màn hình khóa (`Win+L`) có thể dùng như bề mặt SMTC dự phòng cho các
điều khiển nó thực sự hiển thị, nhưng Quick Settings vẫn là đường chính của
P4-A.

Với fixture dài 30 giây, hãy phát lại bài đầu rồi mở Quick Settings ngay
trong lúc trạng thái còn Playing. Không kết luận thẻ media vắng mặt chỉ vì
UI Automation không đọc được cửa sổ shell; người kiểm thử phải nhìn và thao
tác trực tiếp, đồng thời ghi lại ảnh chụp đã che thông tin riêng tư nếu cần.
Ảnh chụp từ xa bị cắt, lệch tọa độ hoặc chỉ thấy một phần thẻ chỉ được ghi
là bằng chứng quan sát chưa đủ; không dùng nó để kết luận progress, nút tua
hoặc Shuffle/Repeat vắng mặt. Cần quan sát trực tiếp hoặc ảnh chụp toàn bộ
thẻ trước khi phân loại các điều khiển trực quan.
Nếu Mimi đang Playing mà vẫn không có thẻ, hãy ghi giờ hệ thống, kiểm tra log
SMTC và chạy một app media tham chiếu trong cùng phiên Windows để phân biệt
lỗi shell/môi trường với lỗi tích hợp của Mimi. Nếu một điều khiển không được
Windows hiển thị, ghi tên điều khiển đó là `BLOCKED/N/A` kèm Windows build
thay vì tự coi là đạt.

Riêng với A7/A8, thẻ Quick Settings gốc của Windows 11 có thể không vẽ seek
bar hay mốc thời gian dù app đã xuất bản timeline lên SMTC. Việc không nhìn
thấy thanh tiến trình trên thẻ vì vậy **chưa đủ** để kết luận Mimi không gửi
timeline. Trước khi quyết định sửa code, hãy chạy chẩn đoán GSMTC chỉ-đọc ở
mục ngay sau A12 để kiểm tra dữ liệu Windows thực sự nhận được.

Nhãn nguồn ở đầu thẻ là app identity, không phải metadata của bài. Với bản
sửa identity, hãy thoát hẳn Mimi cũ, chạy bản mới, bật **Register Mimi with
Windows for this account** trong Settings (tắt rồi bật lại nếu tùy chọn đã
bật từ bản trước), khởi động lại Mimi rồi mới ghi nhãn thực tế. Kỳ vọng ở
tầng GSMTC là `SourceAppUserModelId = Mimi.App`; nhãn mong muốn trên thẻ là
`Mimi`. Nếu vẫn là `Unknown app`, giữ nguyên `Minor/Observation` và không
đánh trượt A1 chỉ vì nhãn nguồn.

- [ ] **A1 Metadata:** title đúng bài hiện tại; artist/album đúng ở những vị
  trí Windows có hiển thị.
- [ ] **A2 Ảnh bìa:** đúng bài, không còn ảnh của bài trước sau khi chuyển
  bài.
- [ ] **A3 Trạng thái:** Play/Pause trên flyout phản ánh app sau mỗi thao
  tác từ cả flyout lẫn trong app.
- [ ] **A4 Play/Pause từ flyout:** mỗi lần bấm chỉ đổi trạng thái đúng một
  lần.
- [ ] **A5 Next:** chuyển đúng một bài theo ngữ cảnh queue; metadata/cover
  được cập nhật.
- [ ] **A6 Previous:** nếu bài đã phát hơn 3 giây, lần đầu quay về đầu bài;
  bấm lại gần đầu bài mới quay về bài đã nghe thật trước đó.
- [ ] **A7 Timeline:** vị trí tăng khi phát và đứng yên khi Pause, không
  vượt duration.
- [ ] **A8 Seek tuyệt đối:** nếu Windows cho kéo timeline, seek tới khoảng
  25%, 75% và gần cuối; vị trí trong app khớp trong sai số hiển thị khoảng
  một giây.
- [ ] **A9 FastForward/Rewind:** nếu được hiển thị, mỗi thao tác tương ứng
  tua +10/-10 giây và bị kẹp ở hai đầu bài.
- [ ] **A10 Shuffle đồng bộ hai chiều:** đổi trong app thì flyout đổi; đổi
  từ flyout (nếu có) thì app và queue áp dụng đúng.
- [ ] **A11 Repeat đồng bộ hai chiều:** Off/All/One trong app tương ứng với
  trạng thái Windows hiển thị; thay đổi từ flyout (nếu có) được app nhận.
- [ ] **A12 Kết thúc bài/queue:** trạng thái cuối không bị kẹt ở Playing;
  bài tự chuyển nếu queue còn bài đúng quy tắc Repeat/Shuffle.

`PASS` yêu cầu mọi điều khiển Windows thực sự hiển thị đều hoạt động và đồng
bộ; không cần coi nút Stop là tiêu chí vì Mimi cố ý không bật Stop trong SMTC.

#### Chẩn đoán A7/A8 bằng GSMTC (chỉ-đọc, không thay thế thao tác trên thẻ)

Chuẩn bị lệnh trong **Windows PowerShell 5.1** tại thư mục gốc repo, phát lại
bài 30 giây từ gần đầu bài, rồi chạy ngay khi Mimi còn `Playing`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Inspect-SmtcSession.ps1 `
  -SourceFilter Mimi `
  -Samples 12 `
  -IntervalMilliseconds 1000 `
  -JsonPath .\artifacts\acceptance\smtc-a7.json
```

Ghi thêm mốc thời gian/duration nhìn thấy trong Mimi ở đầu và cuối lượt lấy
mẫu, cùng các dòng `Windows media controls`/SMTC warning đầu tiên trong log
của phiên app. Script không gửi lệnh Play/Pause/Next/Previous/seek nào.
Với mặc định 12 mẫu cách nhau 1 giây, hãy bắt đầu khi bài còn ít nhất khoảng
15 giây; nếu Summary báo `StartedNearEnd = True`, lượt đó không đủ điều kiện
để kết luận Position có đứng yên bất thường hay không.

Diễn giải tối thiểu:

| Dữ liệu probe | Phân loại tiếp theo |
|---|---|
| `StartedNearEnd = True`, Position ở sát EndTime và gần như không đổi | Lượt đo **không kết luận được**. Phát lại bài từ đầu và chạy lại; không dùng riêng lượt này để kết luận nguồn `CurrentTime` lỗi |
| `DurationSeconds` khoảng 30; `Position` và `LastUpdatedTime` cùng tăng; tuổi của `LastUpdatedTime` nhỏ | Mimi đã xuất bản timeline. Nếu thẻ Quick Settings vẫn không có bar/time, ghi A7 bề mặt hiển thị là `BLOCKED/N/A`, chưa coi là lỗi timeline của app |
| Bài thay đổi giữa mẫu đầu và mẫu cuối | Không so hiệu Position giữa hai bài; chạy lại trọn trong một bài còn đủ thời lượng |
| Start/End/Position/MinSeek/MaxSeek đều 0 và `LastUpdatedTime` là năm 1601 | Windows chưa nhận timeline; điều tra `UpdateTimelineProperties`, duration và SMTC exception đầu tiên trong log |
| `LastUpdatedTime` tăng nhưng `Position` đứng yên trong lúc app vẫn tự tăng vị trí | Điều tra nguồn `CurrentTime` dùng cho SMTC |
| `Position` tăng nhưng `EndTime`/`MaxSeekTime` bằng 0 | Điều tra nguồn duration; không kết luận chỉ từ giao diện thẻ |
| `SourceAppUserModelId = Mimi.App` | Tiến trình unpackaged đã nhận explicit app identity; vẫn phải nhìn nhãn thực tế trên thẻ mới được kết luận `Unknown app` đã hết |
| `SourceAppUserModelId` vẫn là `Mimi.exe` hoặc đường dẫn | Kiểm tra đã thoát hẳn tiến trình cũ và đang chạy đúng bản có bản sửa identity; không dùng riêng dữ kiện này để kết luận timeline lỗi |
| `IsPlaybackPositionEnabled = True` | Windows ghi nhận phiên hỗ trợ đổi vị trí, nhưng **không** biến A8 kéo-trên-thẻ thành `PASS` nếu thẻ không cho kéo |
| `IsFastForwardEnabled`/`IsRewindEnabled = True` | Chỉ chứng minh khả năng đã được quảng bá; A9 ±10 giây vẫn cần một thao tác vận hành thật từ bề mặt/thiết bị hỗ trợ |

### P4-B — Taskbar thumbnail và progress

- [ ] **B1 Nút theo trạng thái:** thumbnail hiện Previous, Next và chỉ hiện
  Play khi đang Pause hoặc Pause khi đang Playing.
- [ ] **B2 Play/Pause thumbnail:** đổi trạng thái đúng một lần và thumbnail
  không bị đóng ngoài ý muốn sau click.
- [ ] **B3 Previous/Next thumbnail:** tuân thủ cùng quy tắc queue/history
  như A5/A6.
- [ ] **B4 Progress:** thanh tiến trình trên nút taskbar tăng theo vị trí
  bài; Pause cho trạng thái paused thay vì tiếp tục chạy.
- [ ] **B5 Mô tả:** taskbar description/tooltip cho thấy đúng bài hiện tại,
  hoặc chỉ là `Mimi` khi chưa có bài.
- [ ] **B6 Thu nhỏ/phục hồi:** thao tác thumbnail khi app minimized không
  gây crash hoặc làm cửa sổ tự bật lên ngoài dự kiến.

### P4-C — Media keys vật lý và phím tắt

#### Media keys / nút trên thiết bị

Thử với phím media của bàn phím, tai nghe hoặc thiết bị Bluetooth có gửi lệnh
media. Lặp lại các thao tác chính trong ba bối cảnh: Mimi có focus, ứng dụng
khác có focus và Mimi minimized. Màn hình khóa là hạng mục nên thử nếu chính
sách máy cho phép.

- [ ] **C1 Play/Pause:** mỗi lần nhấn chỉ toggle đúng một lần.
- [ ] **C2 Play và Pause riêng:** nếu thiết bị có hai lệnh riêng, Play khi
  đang phát không pause và Pause khi đang pause không phát.
- [ ] **C3 Next/Previous:** mỗi lần chỉ đi đúng một bước queue/history.
- [ ] **C4 Không focus/minimized:** lệnh vẫn tới Mimi qua SMTC và trạng thái
  app/flyout khớp nhau.
- [ ] **C5 Không double-action:** không xảy ra play→pause ngay lập tức hoặc
  nhảy hai bài từ một lần nhấn.
- [ ] **C6 Stop:** chỉ kiểm tra nếu phím/thiết bị thực sự gửi lệnh Stop.
  Khi SMTC đang hoạt động, Stop không phải điều khiển được Mimi quảng bá;
  ghi hành vi quan sát được thay vì tự kết luận lỗi chỉ vì Windows không
  chuyển lệnh.

#### Ma trận focus của phím trong cửa sổ

| Focus hiện tại | Thao tác | Kết quả kỳ vọng |
|---|---|---|
| Vùng trống không tương tác | `Space` | Play/Pause đúng một lần |
| Vùng trống không tương tác | `←` / `→` | Tua -5/+5 giây |
| Vùng trống không tương tác | `↑` / `↓` | Volume thay đổi 5 điểm phần trăm mỗi lần |
| Mọi focus không phải ô nhập/ComboBox | `Ctrl+←` / `Ctrl+→` | Previous/Next theo queue |
| Mọi focus | `Ctrl+F` | Đóng Settings nếu đang mở, focus vào ô tìm kiếm và chọn toàn bộ text |
| Ô tìm kiếm/TextBox/ComboBox | Space, mũi tên, `Ctrl+←/→` | Chỉ sửa/duyệt text như điều khiển Windows thông thường; không đổi bài/tua |
| Button đang focus | `Space` | Kích hoạt đúng button đó như hành vi WPF gốc; không thêm một Play/Pause toàn cục thứ hai |
| Slider đang focus | Mũi tên | Chỉ điều chỉnh slider đó; không tua/volume toàn cục chồng lên |
| Danh sách/Selector đang focus | Mũi tên/Space | Điều hướng/chọn trong danh sách; không tua hoặc toggle ngoài ý muốn |
| Cửa sổ chính | `F11` | Mở/đóng Stage |
| Stage | `Esc` | Thoát Stage |
| Settings | `Esc` | Đóng Settings |

- [ ] **C7** Toàn bộ ma trận trên không làm mất text đang gõ, không đổi lựa
  chọn danh sách ngoài điều khiển đang focus và không có thao tác kép.

`PASS` cho P4-C yêu cầu cả đường media vật lý có sẵn lẫn ma trận focus đạt.
Bấm Play bằng UI Automation hoặc chuột không thay thế C1–C7.

---

## S2 — Đổi, mất và khôi phục thiết bị âm thanh

### Trước mỗi kịch bản

Ghi lại rồi giữ nguyên để đối chiếu:

- Endpoint Windows mặc định (vai trò Multimedia) và backend/nhãn trong Mimi.
- Bài hiện tại, vị trí phát, trạng thái Play/Pause.
- Bài đầu tiên trong **Up Next**.
- Track có badge/trạng thái lỗi hay Missing hay không.
- Mốc giờ hệ thống.

Cho Windows và app tối đa khoảng 5 giây để nhận thông báo endpoint, nhưng
hãy ghi thời gian phản ứng thực tế. Vị trí sau rebuild có thể lệch dưới một
giây do thời điểm lấy mẫu; lệch lớn hơn phải ghi rõ.

### P4-D/E/F — Kịch bản bắt buộc

#### 2.1 Đổi endpoint mặc định khi đang phát

1. Phát trên endpoint A.
2. Đổi **output mặc định** sang endpoint B trong Windows Sound/Quick
   Settings. Việc chỉ đổi thiết bị Communications không phải cùng kịch bản
   vì Mimi đi theo endpoint Render/Multimedia.
3. Quan sát mà chưa bấm Play.

Kỳ vọng:

- [ ] Không crash/treo.
- [ ] Bài và vị trí được giữ; trạng thái thành Pause.
- [ ] Không tự phát ra B.
- [ ] Nhãn chuyển sang B và banner cho biết output đã sẵn sàng/cần bấm Play.
- [ ] Bấm Play chỉ sau đó mới phát tiếp trên B từ khoảng vị trí cũ.

#### 2.2 Rút USB hoặc ngắt Bluetooth đang phát

1. Đặt thiết bị USB/Bluetooth làm endpoint đang phát.
2. Rút dây/ngắt Bluetooth/tắt thiết bị trong lúc nhạc đang chạy.
3. Không thao tác Play ngay; quan sát trạng thái.
4. Kết nối lại thiết bị cũ hoặc chọn endpoint khác rồi mới bấm Play.

Kỳ vọng:

- [ ] Không crash, không vòng lặp hộp thoại.
- [ ] Bài/vị trí được giữ ở Pause; queue không tự chuyển.
- [ ] Không phát bất ngờ qua loa khác khi tai nghe biến mất.
- [ ] Bài không bị đánh dấu corrupt.
- [ ] Khi có output trở lại, nhãn/notice cập nhật và Play thủ công phát tiếp.

Lưu ý Bluetooth có thể xuất hiện lại với profile/endpoint khác nhau
(A2DP/HFP). Ghi đúng tên endpoint trước và sau.

#### 2.3 Disable endpoint đang dùng

Có thể mở bảng Sound cổ điển bằng `mmsys.cpl`, chuột phải vào endpoint ở
tab Playback và chọn Disable. Hãy chắc chắn biết cách bật lại
(Show Disabled Devices → Enable) trước khi thực hiện.

- [ ] Lặp lại các kỳ vọng như 2.2.
- [ ] Nếu Windows chuyển mặc định sang endpoint còn lại, Mimi vẫn Pause và
  chỉ phát sau thao tác Play.
- [ ] Enable lại endpoint; Mimi nhận diện được khi nó thành default hoặc
  khi người dùng bấm Play.

#### 2.4 Không còn output khả dụng

Chỉ thực hiện khi chắc chắn khôi phục được endpoint, đặc biệt trên máy đang
điều khiển từ xa. Disable/rút mọi render endpoint khả dụng trong lúc có bài
đang nạp, sau đó:

1. Quan sát nhãn chuyển thành không có output.
2. Bấm Play một lần.
3. Bấm Next tối đa một lần.
4. Khôi phục một endpoint nhưng chưa bấm Play.
5. Bấm Play.

Kỳ vọng:

- [ ] Thông báo phân biệt rõ “không có output” với file hỏng.
- [ ] Bài hợp lệ không bị ghi nhận corrupt; trạng thái lỗi đã lưu không bị
  đặt chỉ vì thiếu output.
- [ ] Một lần Next không gây tự động lướt qua hàng loạt bài.
- [ ] Sau khi output quay lại, Mimi vẫn Pause cho tới thao tác Play.
- [ ] Play sau khôi phục phát được bài đang giữ/ứng viên đã được nhận ở
  Pause mà không phải khởi động lại app.

#### 2.5 Đổi thiết bị khi đang Pause

- [ ] Đổi hoặc rút endpoint khi bài đang Pause.
- [ ] Bấm Play sau khi đổi.

Kỳ vọng: output mở trên endpoint mặc định hiện tại, bài/vị trí không bị mất
và không xuất hiện trạng thái Playing giả khi output chưa mở được.

### Điều kiện `BLOCKED`

Nếu không có endpoint thứ hai, P4-D vật lý là `BLOCKED`. Disable endpoint duy
nhất vẫn kiểm chứng phần lớn P4-E và một phần đường khôi phục của P4-F,
nhưng không được ghi thay cho việc rút USB/ngắt Bluetooth.

---

## S3 — A/B WASAPI ↔ WaveOut cho P4-G/D-03

### Câu hỏi cần trả lời

Phép đo này không cần chứng minh WASAPI luôn có latency vật lý thấp hơn.
Câu hỏi của D-03 là: **trên cùng build, cùng máy và cùng endpoint, WASAPI
Shared có ổn định và phản hồi ở mức không thua kém WaveOut đủ để giữ làm mặc
định, trong khi WaveOut vẫn là fallback hay không?**

Loopback số chỉ đo những gì Windows giao cho capture endpoint. Thời gian đến
buffer có tín hiệu đầu tiên không phải latency từ lệnh Play tới âm thanh
ngoài loa.

### Kiểm soát biến số

- Cùng commit và Release executable.
- Cùng endpoint, Windows volume, app volume/mute, sample rate và
  enhancements.
- Cùng file và **cùng vị trí bắt đầu**. Trước mỗi lượt, đưa bài về một mốc
  cố định, ví dụ 0:30, ở trạng thái Pause; sau lượt đo lại đưa về mốc đó.
- Đoạn được đo phải có tín hiệu liên tục; tránh intro/đoạn lặng có chủ ý.
- Cùng định dạng loopback và cùng ngưỡng phân tích.
- Ghi rõ CSV bắt đầu trước mốc kích hoạt bao lâu. Nếu có pre-roll, mốc
  trigger phải được xác định trên cùng trục `elapsed` của CSV; không lấy
  thời gian từ dòng đầu CSV làm thời gian từ lúc bấm Play.
- Đóng Mimi hoàn toàn trước khi đổi `MIMI_AUDIO_OUTPUT`.
- Xen kẽ thứ tự, ví dụ WASAPI → WaveOut → WaveOut → WASAPI, để giảm ảnh
  hưởng của thứ tự chạy.

### Khối lượng tối thiểu khuyến nghị để xét chốt

- Ít nhất **ba lượt × năm phút cho mỗi backend**, hoặc hai lượt × mười phút
  cho mỗi backend.
- Thêm một lượt `auto` ngắn để xác nhận đường mặc định vẫn mở WASAPI.
- Trong mỗi lượt dài, áp dụng cùng kịch bản thao tác: Play, seek +10, Pause
  vài giây rồi Play, seek -10, Next và Previous. Ghi giờ của từng thao tác.
- Nếu nghe thấy pop/dropout, ghi ngay giờ hệ thống và vị trí bài; không ghi
  đè CSV của lượt đó.

### Khởi chạy từng backend

```powershell
$env:MIMI_AUDIO_OUTPUT = 'wasapi'
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe' -Wait

$env:MIMI_AUDIO_OUTPUT = 'waveout'
Start-Process -FilePath '.\src\Mimi.App\bin\Release\net8.0-windows10.0.19041.0\Mimi.exe' -Wait

Remove-Item Env:MIMI_AUDIO_OUTPUT -ErrorAction SilentlyContinue
```

Đối chiếu log thay vì chỉ tin biến môi trường:

- WaveOut forced phải có log cho biết WaveOut được mở vì bị ép cho chẩn đoán.
- WASAPI/auto phải ghi WASAPI Shared mở trên endpoint đã chọn.
- Nhãn WaveOut forced là `WaveOut diagnostic · [endpoint]`, khác
  `WaveOut fallback`.

### Phân tích CSV

```powershell
.\tools\Analyze-LoopbackCsv.ps1 `
  -Path 'C:\duong-dan\wasapi-01.csv','C:\duong-dan\waveout-01.csv' `
  -MarkdownPath '.\artifacts\acceptance\loopback-summary.md' `
  -JsonPath '.\artifacts\acceptance\loopback-summary.json'
```

Thư mục `artifacts/` đã nằm trong `.gitignore`. Nếu script chưa ký số bị
chính sách thực thi chặn, chạy
`Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` trong đúng cửa
sổ PowerShell hiện tại rồi thử lại; không cần thay đổi chính sách toàn máy.

Nếu mốc trigger trên timeline CSV được biết chính xác, thêm ví dụ
`-TriggerOffsetMilliseconds 2000`. Chỉ số
`TimeToFirstSignalAfterTriggerMs` khi đó mới có ý nghĩa so sánh phần mềm;
`TimeToFirstSignalFromCsvStartMs` luôn bao gồm pre-roll. Nếu trigger chỉ
được ước lượng, hãy ghi nó là không xác định thay vì suy ra một latency có
vẻ chính xác.

Báo cáo ít nhất:

- số buffer và thời lượng sau buffer có tín hiệu đầu tiên;
- tỉ lệ buffer có tín hiệu;
- thời gian đến tín hiệu đầu tiên từ đầu CSV và—chỉ khi trigger được biết
  chính xác—sau trigger, kèm mô tả đây là chỉ số phần mềm;
- p50/p95/p99/max của khoảng callback;
- số khoảng >100ms và số khoảng >2 lần trung vị;
- chuỗi im lặng dài nhất sau tín hiệu đầu tiên;
- mười khoảng callback lớn nhất và RMS/peak tại đó;
- glitch nghe được kèm thời điểm.

### Ngưỡng điều tra, không phải kết luận tự động

Hãy điều tra thêm thay vì kết luận backend lỗi ngay nếu thấy:

- một chuỗi im lặng dài hơn khoảng 250ms trong đoạn chắc chắn có tiếng;
- nhiều khoảng callback lớn tụ lại trong thời gian ngắn;
- khoảng lớn đi kèm buffer không có tín hiệu hoặc tiếng glitch nghe được;
- hành vi lặp lại qua nhiều lượt của cùng backend;
- log app có output failure/rebuild bất thường tại cùng mốc giờ.

Một khoảng callback lớn đơn lẻ, buffer vẫn có tín hiệu, không có glitch nghe
được và không lặp lại được phân loại là `Observation`. Không chỉnh latency
hoặc đổi backend mặc định chỉ dựa trên một điểm như vậy.

### Tiêu chí đề xuất để chốt D-03

- [ ] Hoàn thành khối lượng A/B tối thiểu ở trên.
- [ ] Không crash/lỗi output ở cả hai backend.
- [ ] Không có chuỗi mất tín hiệu bất thường hoặc glitch nghe được chưa giải
  thích.
- [ ] WASAPI không cho thấy suy giảm ổn định có hệ thống so với WaveOut.
- [ ] Kết quả được tổng hợp vào `ROADMAP.md` cùng phương pháp đo.

Nếu hai backend tương đương về ổn định, giữ WASAPI Shared làm mặc định vì nó
là đường bám endpoint/phục hồi thiết bị đã thiết kế; WaveOut tiếp tục là
fallback. Nếu WASAPI lặp lại lỗi ổn định, D-03 phải được mở lại bằng bằng
chứng cụ thể thay vì đổi mặc định theo cảm tính.

---

## S4 — Nợ nghiệm thu của Phase 0–3

Các mục này không nhất thiết chặn riêng việc đóng Phase 4, nhưng chặn tuyên
bố sẵn sàng phát hành cộng đồng và nên được xử lý trước khi Phase 5 làm thay
đổi lớn lên UI.

### S4-A — Ma trận định dạng âm thanh

Danh sách đuôi hiện được app nhận: `.mp3`, `.wav`, `.aiff`, `.aif`, `.wma`,
`.m4a`, `.aac`, `.flac`. Với mỗi định dạng có file mẫu hợp lệ:

- [ ] File được Scan/Add nhận diện.
- [ ] Duration hợp lý; metadata/cover được đọc nếu file có tag/cover.
- [ ] Phát ít nhất 10 giây.
- [ ] Seek tới giữa và gần cuối không lỗi.
- [ ] Next sang bài khác không kẹt.
- [ ] Ghi Windows build, codec/container đáng chú ý và mọi fallback tên file.

Nếu thiếu file mẫu của một định dạng, ghi `BLOCKED`, không suy ra `PASS` từ
việc đuôi nằm trong `SupportedAudioFiles`. `.ogg/.opus` hiện chưa thuộc danh
sách hỗ trợ.

Các biến thể nên thử khi có sẵn: mono/stereo, 44.1/48/96kHz và đa kênh.

### S4-B — Stage Engine / Visualizer

Ghi độ phân giải, tỉ lệ scaling, GPU, chế độ cửa sổ và nguồn điện.

- [ ] Cả ba chế độ Neon Spectrum, Twin Wave và Halo Circle hiển thị đúng.
- [ ] Bài nhỏ không làm sóng “câm”; bài lớn không bão hoà thường trực.
- [ ] Giảm volume không làm sóng mất theo vì mẫu lấy trước volume.
- [ ] Pause/Stop làm sóng hạ dần về sàn, không đóng băng.
- [ ] Diagnostics ổn định quanh mục tiêu 60fps ±5% ở 1080p khi cửa sổ có
  focus trên máy tham chiếu.
- [ ] Khi mất focus hoặc Battery Saver, app tự hạ về khoảng 30fps theo thiết
  kế.
- [ ] Ghi draw time và CPU tổng của tiến trình sau ít nhất 30 giây ổn định.
- [ ] F11 mở Stage; Esc thoát; ẩn/hiện con trỏ phù hợp.

Task Manager chỉ cho CPU tổng của tiến trình, không chứng minh riêng ngân
sách CPU <5% một nhân của visualizer hoặc 0 allocation/frame. Hai tiêu chí
chuyên sâu đó cần profiler; chưa chạy profiler thì ghi phần tương ứng là
`PARTIAL/BLOCKED`.

### S4-C — Quy mô thư viện, Explorer và watched folders

Chỉ dùng bản sao/thư viện kiểm tra khi thao tác có thể sửa dữ liệu lớn.

- [ ] Ghi số bài thực tế. Mục tiêu Exit là 10.000 bài; thư viện nhỏ hơn chỉ
  cho kết quả `PARTIAL`.
- [ ] Quét lần đầu và quét lại: ghi thời gian, UI có phản hồi/Cancel được.
- [ ] Cuộn từ đầu tới cuối danh sách, đổi sort/filter và chọn nhiều.
- [ ] Tìm một từ khóa tiếng Việt có dấu và không dấu; ghi truy vấn, số kết
  quả và cách đo thời gian. Cảm giác “tức thì” không đủ để tuyên bố <100ms
  nếu không có đồng hồ/công cụ đo.
- [ ] Kéo-thả file và thư mục từ Explorer.
- [ ] **Open with Mimi** khi app đang đóng: app mở, thêm và phát file.
- [ ] **Open with Mimi** khi app đang mở: yêu cầu sang cửa sổ hiện tại,
  không tạo player thứ hai.
- [ ] Watched folder: thêm, sửa, đổi tên và xoá file; file xoá bên ngoài
  được đánh dấu Missing thay vì biến mất im lặng.

### S4-D — Nâng cấp, migration và tắt giữa lúc ghi

Chỉ thực hiện trên profile kiểm tra đã sao lưu/toàn quyền loại bỏ. Nếu chưa
có fixture dữ liệu cũ đáng tin cậy, ghi `BLOCKED — cần chuẩn bị fixture`,
không lấy dữ liệu đang dùng hằng ngày ra thử nghiệm phá hoại.

- [ ] Dữ liệu cũ được mở/migrate mà không mất thư viện, favorites hoặc
  settings hợp lệ.
- [ ] JSON cũ chỉ được archive sau khi import thành công theo quy tắc hiện
  hành.
- [ ] Đóng/tắt tiến trình trên **profile kiểm tra** trong lúc quét/ghi, rồi
  mở lại: SQLite/settings hoặc nguyên vẹn trạng thái cũ, hoặc nguyên vẹn
  trạng thái mới; không có file nửa vời làm app không khởi động.
- [ ] Sau kiểm tra đã khôi phục lại profile gốc từ bản sao lưu.

### S4-E — Clean-clone contributor build

- [ ] Clone repo vào một thư mục mới hoàn toàn trên Windows.
- [ ] Chỉ làm theo `README.md`, không dùng kiến thức ngoài tài liệu.
- [ ] Ghi thời gian từ clone tới lúc build/test/run thành công.
- [ ] Ghi mọi bước mơ hồ, lệnh sai hoặc điều kiện tiên quyết bị thiếu.

Máy đã có NuGet cache không phải một phép thử “máy sạch” tuyệt đối; ghi rõ
giới hạn đó. Kết quả vẫn có giá trị kiểm chứng hướng dẫn.

---

## S5 — Tổng hợp và chốt quyết định

Với mỗi `FAIL`, báo cáo tối thiểu gồm:

```text
Commit:
Windows build:
Cổng/hạng mục:
Tiền điều kiện:
Các bước tái hiện:
Kết quả kỳ vọng:
Kết quả thực tế:
Số lần tái hiện:
Mốc thời gian trong log:
Endpoint/thiết bị liên quan:
Mức độ đề xuất:
```

Quy tắc chốt:

- P4-H chỉ hoàn tất khi P4-A…P4-G đều `PASS`, hoặc một hạng mục `BLOCKED`
  được chủ dự án chấp nhận hoãn bằng văn bản trong `ROADMAP.md` kèm lý do.
  Waiver không biến kết quả thành `PASS`.
- Lỗi Blocker/Major phải được sửa và kiểm tra lại đúng kịch bản đã thất bại.
- Merge branch vào `main` là quyết định tích hợp riêng; nó không tự đóng
  Phase 4, D-03 hoặc bất kỳ Exit nào của Phase 0–3.
- Nợ Phase 0–3 còn mở không nhất thiết chặn đóng riêng Phase 4, nhưng chặn
  tuyên bố bản dựng sẵn sàng phát hành công khai.
- Chỉ tổng hợp số liệu và kết luận vào `ROADMAP.md`; dữ liệu thô tiếp tục
  được giữ ngoài repo.
