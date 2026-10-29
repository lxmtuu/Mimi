# Mimi Windows Acceptance Kit

Bộ tài liệu và công cụ này biến các cổng nghiệm thu trong
[`ROADMAP.md`](../../ROADMAP.md) thành những phiên kiểm tra lặp lại được trên
Windows thật. Nó phục vụ trước hết cho Phase 4 (`v0.5.0`), đồng thời gom các
khoản nợ nghiệm thu còn mở của Phase 0–3 để không bị quên trước khi sang
Phase 5 hoặc nghĩ tới phát hành cộng đồng.

> `ROADMAP.md` vẫn là nơi ghi **quyết định và trạng thái chính thức**. Bộ kit
> này chỉ quy định **cách kiểm tra và cách ghi bằng chứng**; một kết quả chỉ
> làm thay đổi trạng thái Phase sau khi đã được tổng hợp vào `ROADMAP.md`.

## Các file trong bộ kit

| File | Mục đích |
|---|---|
| [`WINDOWS-ACCEPTANCE-CHECKLIST.md`](WINDOWS-ACCEPTANCE-CHECKLIST.md) | Checklist từng phiên kiểm tra, thao tác, kết quả kỳ vọng và tiêu chí Pass/Fail |
| [`RESULT-TEMPLATE.md`](RESULT-TEMPLATE.md) | Mẫu báo cáo ngắn để sao chép cho mỗi đợt kiểm tra |
| [`../../tools/Analyze-LoopbackCsv.ps1`](../../tools/Analyze-LoopbackCsv.ps1) | Phân tích CSV loopback: phân bố khoảng callback, tín hiệu, chuỗi im lặng và các khoảng lớn nhất |
| [`../../tools/Inspect-SmtcSession.ps1`](../../tools/Inspect-SmtcSession.ps1) | Chẩn đoán GSMTC chỉ-đọc cho A7/A8/A9: lấy mẫu metadata, trạng thái, cờ điều khiển và timeline Windows nhận được mà không gửi lệnh phát/seek |

## Thứ tự phiên khuyến nghị

| Phiên | Nội dung | Cổng liên quan |
|---|---|---|
| **S0** | Chuẩn bị, sao lưu và cổng build/test tự động | Điều kiện nền cho mọi phiên |
| **S1** | Flyout SMTC, taskbar, media keys và phím tắt | P4-A, P4-B, P4-C |
| **S2** | Đổi, rút/ngắt, disable và kết nối lại thiết bị âm thanh | P4-D, P4-E, P4-F |
| **S3** | A/B WASAPI ↔ WaveOut dài hơn bằng loopback | P4-G, quyết định D-03 |
| **S4** | Nợ nghiệm thu cũ: định dạng, visualizer, thư viện/dữ liệu, contributor build | Phase 0–3 |
| **S5** | Tổng hợp lỗi, cập nhật ROADMAP và quyết định đóng cổng | P4-H và các Exit tương ứng |

Hãy làm S1 trước vì không cần phần cứng bổ sung và có khả năng phát hiện lỗi
tương tác thực tế cao. S2 tách riêng vì thao tác disable/rút thiết bị có thể
làm mất output tạm thời. S3 có thể chạy nền trong nhiều buổi nghe, còn S4 có
thể chia nhỏ theo điều kiện file/dữ liệu sẵn có.

## Quy ước trạng thái

Một cổng chỉ được ghi một trong các trạng thái sau ở cấp tổng:

| Trạng thái | Ý nghĩa |
|---|---|
| `PASS` | Mọi hạng mục bắt buộc của cổng đã được thao tác thật và đạt kết quả kỳ vọng |
| `PARTIAL` | Một phần đã có bằng chứng đạt, nhưng còn hạng mục bắt buộc chưa kiểm tra |
| `FAIL` | Ít nhất một hành vi bắt buộc sai và tái hiện được |
| `BLOCKED` | Chưa thể kiểm tra vì thiếu phần cứng, file mẫu, quyền hoặc tính năng Windows không hiện diện |
| `NOT RUN` | Chưa thực hiện |
| `N/A` | Hạng mục con không áp dụng trong môi trường đó, có lý do cụ thể; không tự nâng thành `PASS` |

Ví dụ: việc SMTC khởi tạo trong log chỉ ủng hộ trạng thái **P4-A = PARTIAL**.
P4-A chỉ có thể là `PASS` sau khi thao tác trực tiếp trên flyout. Tương tự,
một vòng loopback 20 giây giúp **P4-G = PARTIAL**, không tự chứng minh ổn định
dài hạn hay latency âm học đầu-cuối.

## Phân loại mức độ khi phát hiện vấn đề

| Mức | Ví dụ | Ảnh hưởng |
|---|---|---|
| **Blocker** | Crash/treo; mất dữ liệu; bài hợp lệ bị ghi corrupt; nhạc tự phát sang thiết bị khác; queue tự chạy khi không có output; không khôi phục được | Chặn đóng cổng và chặn tích hợp/phát hành |
| **Major** | Một nút flyout/taskbar/media key không hoạt động; thao tác bị thực hiện hai lần; trạng thái phát sai lệch kéo dài | Chặn đóng cổng liên quan |
| **Minor** | Nhãn/thông báo chưa rõ; ảnh bìa thiếu trong khi điều khiển vẫn đúng; lệch hiển thị nhỏ | Ghi backlog, không nhất thiết chặn toàn bộ Phase |
| **Observation** | Một số đo bất thường đơn lẻ chưa liên hệ được với lỗi nghe/thấy được | Theo dõi và lặp lại phép đo, chưa sửa code chỉ dựa trên suy đoán |

## Quy tắc bằng chứng và riêng tư

- Ghi commit, cấu hình build, phiên bản/build của Windows và endpoint cho
  mỗi phiên.
- Với lỗi, ghi rõ **thao tác → kỳ vọng → thực tế → số lần tái hiện**, kèm
  mốc thời gian trong log.
- Log ứng dụng nằm tại `%LocalAppData%\Mimi\logs\mimi-YYYYMMDD.log`.
- Không commit log thô, CSV loopback, ảnh chụp chứa đường dẫn cá nhân hoặc
  toàn bộ thư viện nhạc vào repo. Chỉ đưa số liệu đã tổng hợp và đoạn log tối
  thiểu cần thiết vào báo cáo/`ROADMAP.md`.
- Trước S2 và mọi test nâng cấp/migration, đóng Mimi rồi sao lưu thư mục
  `%LocalAppData%\Mimi`. Không thử “tắt giữa lúc ghi” trên bản dữ liệu duy
  nhất.
- Nếu Windows không hiển thị một điều khiển SMTC nào đó, ghi phiên bản
  Windows và đánh dấu hạng mục con là `BLOCKED` hoặc `N/A`; không kết luận
  app lỗi chỉ vì bề mặt điều khiển đó vắng mặt.

## Điểm xuất phát đã được ghi nhận ngày 29/10/2026

- S0 đã chạy lại trên Windows thật tại `a056377`: restore/Release build đạt
  0 warning/0 error; unit test 83 passed, 0 failed, 0 skipped. App mở được,
  log xác nhận WASAPI Shared trên Speakers (Realtek(R) Audio) và SMTC khởi
  tạo. **S0: `PASS`**.
- Ba CSV cũ đều có header
  `elapsed_ms,bytes,rms,peak,callback_gap_ms`; script đã nhận diện đúng,
  tạo được Markdown/JSON và cho thấy mỗi file có 353–354 dòng, khoảng 22
  giây, không có chuỗi im lặng sau tín hiệu đầu tiên.
- Checkpoint fixture trên Windows 11 tại checkout `6ca298c`: ba MP3 tổng
  hợp 30 giây hiển thị đúng metadata chính và ba cover khác nhau trong thư
  viện; playlist `Acceptance S1` có đúng thứ tự 1→2→3; người kiểm thử nghe
  được tiếng qua Speakers (Realtek(R) Audio) ở WASAPI Shared và trạng thái
  cuối là Shuffle/Repeat `Off`. Đây là tiền điều kiện của S1, chưa phải
  bằng chứng flyout.
- Đối chiếu tiếp tại `2a8b879` ghi môi trường Windows 11 Home Single
  Language, version 26H2, OS Build 26300.9711. Cả ba MP3 thực tế trên máy
  có SHA-256 khác bộ fixture đã xuất; các quan sát chức năng ở trên vẫn gắn
  với đúng những file đã được nhập/phát, nhưng provenance của fixture còn
  là `Observation` mở cho tới khi báo cáo ngoài repo đóng băng đầy đủ
  SHA-256/kích thước/tag của bộ thực tế.
- Người kiểm thử đã khôi phục đúng playlist, xác nhận Cyan đang là bài đầu,
  Up Next có Magenta/Amber và Shuffle/Repeat `Off`, rồi thử mở bề mặt media
  bằng `Win+N` cùng đường UI Automation nhưng chưa tiếp cận được Quick
  Settings để thao tác. Lượt thử này không tạo bằng chứng `PASS` hay `FAIL`
  cho flyout.
- Lượt thao tác trực tiếp Quick Settings ngày 29/10/2026 sau đó đã xác nhận
  **A1 metadata, A2 cover, A3 đồng bộ trạng thái, A4 Play/Pause, A5 Next và
  A6 Previous đều `PASS`**; A6 quay lại Cyan với metadata/cover trên thẻ và
  UI Mimi đồng bộ. A8 bị chặn ở bề mặt nhìn thấy do không có timeline để
  kéo; A9 mới đối chiếu được ở source, chưa chạy vận hành ±10 giây;
  A10–A12 vẫn `NOT RUN`.
- Probe GSMTC chỉ-đọc tại `7c14775` đã chạy thành công trên Windows với
  session `Mimi.exe`. Lượt Cyan đầu tiên bắt đầu ở `30.107 / 30.119` giây
  nên `Position` không đổi trong khoảng 11,26 giây là **không kết luận
  được**. Một lượt trên Magenta khi đang `Playing` cho thấy Position tăng
  từ khoảng `2.46` lên `7.29` giây trong 5,17 giây, duration khoảng
  `30.119` giây và Windows ghi nhận seek/FastForward/Rewind được bật.
  Lượt kiểm tra cùng thời điểm cũng xác nhận source tại `7c14775` đạt
  83/83 test và build 0 warning/0 error sang thư mục đầu ra riêng vì tiến
  trình Mimi đang chạy khóa DLL đầu ra mặc định.
- Lượt Cyan sạch tiếp theo tại `2a26e90` xác nhận lại kết quả trên chính
  bài từng gây nghi ngờ: playlist được phát lại từ đầu bằng UI Automation,
  cả mẫu đầu/cuối đều là Cyan ở trạng thái `Playing`, duration `30.119`
  giây, Position tăng `2.161 → 9.161` giây. `TimelinePublished = True`,
  `StartedNearEnd = False`, `RawPositionDeltaSeconds = 7.000`,
  `LastUpdatedDeltaSeconds = 6.998` và `WallClockDeltaSeconds = 7.206`.
  Quan sát Cyan đứng ở cuối bài trước đó vì vậy được khép lại như một
  lượt đo không đủ tiền điều kiện, không còn là bằng chứng nghi ngờ nguồn
  `CurrentTime`. Lượt này không xác minh riêng thao tác double-click vật
  lý vào dòng Cyan, nhưng điều đó không phải tiêu chí của A7.
- Ảnh Quick Settings chụp từ xa trong lượt Cyan sạch chỉ thấy một phần
  mép phải của bảng do sai lệch tọa độ/kích thước, nên không bổ sung bằng
  chứng đáng tin cậy về toàn bộ thẻ. Ảnh toàn thẻ do chủ dự án cung cấp
  sau đó cho thấy đúng title `Acceptance One - Cyan Signal`, artist
  `Mimi Acceptance Lab` và cover Cyan; hàng điều khiển chỉ có
  Previous/Play/Next. Không thấy progress/thời gian, FastForward/Rewind
  hay Shuffle/Repeat trên thẻ. Nút giữa là Play, nên ảnh được chụp khi
  session đang ở trạng thái có thể phát tiếp/Paused; kết hợp với báo cáo
  trực tiếp trước đó rằng không có timeline để kéo, phần hiển thị/seek
  của Quick Settings được phân loại `BLOCKED/N/A` cho bề mặt/build này.
  Đây không phải bằng chứng Mimi thiếu timeline, vì GSMTC đã xác minh
  duration, seek bounds và Position tiến triển. **A7 vẫn `PARTIAL`** vì
  tiêu chí trực quan trên thẻ không được bề mặt này phơi bày để nghiệm
  thu như một `PASS` trực tiếp.
- Cùng ảnh toàn thẻ ghi nhận nhãn nguồn là `Unknown app` dù title,
  artist và cover đúng. Đây là `Minor/Observation` riêng về app identity
  của bản unpackaged đang chạy từ thư mục build; nó không thay đổi kết
  luận metadata/track của A1/A2 và không phải nguyên nhân đã chứng minh
  cho việc thẻ không vẽ timeline.
- P4-A: `PARTIAL` (A1–A6 `PASS`; xuất bản/tiến triển timeline A7 đã đạt
  ở tầng GSMTC trên cả Magenta và Cyan, còn progress/thời gian trên thẻ
  `BLOCKED/N/A`; A8 `BLOCKED` ở bề mặt Quick Settings hiện tại; A9 chưa
  chạy vận hành; A10–A12 `NOT RUN`).
- P4-B: `NOT RUN` cho B1–B6.
- P4-C: `PARTIAL` (C5/C7 mới có bằng chứng mô phỏng/một phần focus; media
  keys vật lý chưa chạy).
- P4-D, P4-E, P4-F và P4-H: chưa đóng.
- P4-G: `PARTIAL`; D-03 vẫn tạm thời: WASAPI Shared mặc định, WaveOut
  fallback.

## Chẩn đoán nhanh timeline SMTC cho A7

Khi Quick Settings không hiện progress bar/thời gian, chạy script GSMTC
chỉ-đọc trong lúc Mimi đang `Playing` theo mục chẩn đoán A7/A8 của
[`WINDOWS-ACCEPTANCE-CHECKLIST.md`](WINDOWS-ACCEPTANCE-CHECKLIST.md). Hãy
bắt đầu khi bài còn đủ thời lượng cho toàn bộ cửa sổ lấy mẫu; Summary có
cờ `StartedNearEnd` để loại các lượt bắt đầu sát cuối bài khỏi kết luận
Position bị đóng băng. Nếu Windows nhận được duration khoảng 30 giây và
`Position`/`LastUpdatedTime` cùng tiến, vấn đề còn lại nằm ở khả năng hiển
thị của bề mặt shell thay vì bằng chứng app thiếu timeline. Nếu timeline
toàn 0 với `LastUpdatedTime` năm 1601, mới điều tra đường xuất bản timeline
và cảnh báo SMTC đầu tiên trong log. Probe không tự biến A7 trực quan, A8
seek trên thẻ hoặc A9 ±10 giây thành `PASS`.

## Phân tích nhanh CSV loopback

Từ thư mục gốc repo:

```powershell
.\tools\Analyze-LoopbackCsv.ps1 `
  -Path 'C:\duong-dan\wasapi.loopback.csv','C:\duong-dan\waveout.loopback.csv' `
  -MarkdownPath '.\artifacts\acceptance\loopback-summary.md' `
  -JsonPath '.\artifacts\acceptance\loopback-summary.json'
```

Nếu CSV chứa pre-roll và mốc kích hoạt phép thử trên cùng timeline được
biết chính xác—ví dụ trigger ở `elapsed_ms = 2000`—thêm
`-TriggerOffsetMilliseconds 2000`. Báo cáo sẽ tách
`TimeToFirstSignalFromCsvStartMs` khỏi
`TimeToFirstSignalAfterTriggerMs`. Nếu chỉ biết pre-roll “khoảng” hai giây,
không dùng hiệu số này như latency đo được.

Thư mục `artifacts/` đã được `.gitignore` loại khỏi Git. Nếu chính sách
thực thi của Windows chặn script chưa ký số, chỉ nới lỏng cho đúng cửa sổ
PowerShell đang kiểm tra rồi chạy lại lệnh trên:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Script tự nhận diện các tên cột phổ biến cho thời gian, khoảng callback,
RMS, peak và số frame/byte. Nếu công cụ thu dùng tên cột khác, hãy xem thông
báo lỗi của script và cung cấp hàng tiêu đề CSV để bổ sung alias thay vì sửa
tay dữ liệu gốc. Xem phần S3 trong checklist để biết cách diễn giải số liệu.
