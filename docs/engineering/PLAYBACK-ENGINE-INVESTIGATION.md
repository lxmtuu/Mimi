# Playback Engine — investigation log

> Cập nhật: 2026-10-31
> Branch: `arena/cdda374d-mimi`
> Code checkpoint khi ghi tài liệu: `2babf5c`

## Mục tiêu

Hoàn tất kiểm tra sâu P0 Playback Engine trước khi mở rộng các hạng mục Phase 4. Trọng tâm hiện tại là bảo đảm thông báo kết thúc bài không bị mất, không xử lý lặp và không tác động nhầm sang track/generation mới khi callback bị trì hoãn bởi WPF Dispatcher hoặc tác vụ bất đồng bộ.

## Công việc đã thực hiện

### 1. Playback-end notification gate

Đã có lớp `PlaybackEndNotificationGate` với trạng thái pending gắn theo playback generation:

- `TryQueue(out eventGeneration)` gộp thông báo trùng trong cùng generation.
- `Invalidate()` tăng generation khi nguồn phát thay đổi.
- `IsCurrent(eventGeneration)` cho phép bỏ qua callback đã lỗi thời.
- `Complete(eventGeneration)` chỉ giải phóng pending của đúng generation; callback cũ không thể xóa pending của generation mới.

Các thay đổi liên quan đang nằm trong working tree ở thời điểm ghi tài liệu; xem `src/Mimi.Core/Playback/PlaybackEndNotificationGate.cs`, `src/Mimi.App/ViewModels/MainWindowViewModel.cs` và `tests/Mimi.Tests/PlaybackEndNotificationGateTests.cs`.

### 2. Unit tests cho gate

Hiện có 5 test bao phủ:

1. Gộp notification trùng trong cùng generation.
2. Generation mới vẫn queue được khi notification cũ còn pending.
3. Complete generation cũ không xóa pending của generation mới.
4. Complete generation hiện tại cho phép queue notification tiếp theo.
5. Notification sau invalidate không còn current.

### 3. Kiểm tra luồng tích hợp trong ViewModel

Đã rà soát `MainWindowViewModel`:

- `AudioPlayer_PlaybackEnded` lấy generation trước khi enqueue callback vào Dispatcher.
- `ProcessPlaybackEndedAsync` kiểm tra generation trước khi gọi `HandlePlaybackEndedAsync`.
- `finally` gọi `Complete(eventGeneration)`.
- Dispatcher operation bị abort hoặc enqueue ném exception cũng giải phóng đúng generation.
- `TryPlayTrack` invalidate notification cũ trước khi load nguồn mới.

**Điểm cần kiểm chứng tiếp:** `HandlePlaybackEndedAsync` gọi `await CompleteCurrentPlaybackAsync()` sau lần kiểm tra generation ban đầu. Generation có thể thay đổi trong lúc await; cần test xác định callback cũ có thể tiếp tục logic Repeat/Next sau khi track đã đổi hay không. Đây là rủi ro cần điều tra, chưa phải lỗi đã tái hiện.

### 3. Natural-end classification

`PlaybackStopClassifier` và `PlaybackStopClassifierTests` cũng đang có trong working tree. Classifier phân biệt natural end với output dừng do exception, kiểm tra reader length/position và tolerance theo byte; test bao phủ các biên length/position, tolerance âm và trường hợp có exception. Hạng mục này liên quan việc nhận diện kết thúc bài, nhưng không thay thế test race Dispatcher/async nêu trên.
## Kết quả kiểm thử đã chạy

Lệnh chạy trên Windows:

```powershell
dotnet test .\tests\Mimi.Tests\Mimi.Tests.csproj -c Release --no-restore --logger 'console;verbosity=minimal'
```

Kết quả tại lần chạy ngày 2026-10-31:

- **107 passed**
- **0 failed**
- **0 skipped**
- Thời gian test: khoảng 490 ms
- Exit code: `0`

Đây là kết quả full unit-test suite hiện tại; chưa chứng minh race condition tích hợp Dispatcher/async đã được tái hiện hoặc loại trừ.

## Trạng thái điều tra

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Gate generation-aware | Implemented trong working tree | Cần giữ nguyên các thay đổi hiện có |
| Unit tests cho gate | PASS | 5 test; full suite 107/107 PASS |
| Callback cũ còn chờ Dispatcher | Chưa kiểm thử tích hợp | Cần deterministic dispatcher/test seam |
| Generation đổi trong lúc `await` | Chưa kiểm thử | Ưu tiên test tiếp theo |
| Race condition thực tế | Chưa tái hiện | Không ghi nhận là bug đã xác nhận |
| Release build sau checkpoint hiện tại | Chưa chạy lại trong lượt điều tra này | Chạy sau khi hoàn thiện test/fix nếu có |

## Kế hoạch tiếp theo

1. Tạo test có điều khiển thứ tự callback: queue A → invalidate/chuyển B → queue B → chạy callback A cũ → chạy callback B.
2. Tạo test giữ `CompleteCurrentPlaybackAsync` ở trạng thái chưa hoàn tất, đổi generation trong lúc await, rồi xác nhận callback A không được thực hiện Next/Repeat trên B.
3. Nếu test cần seam, tách phần điều phối end-of-track thành thành phần nhỏ có thể kiểm thử mà không cần khởi tạo toàn bộ WPF `MainWindowViewModel`.
4. Chỉ sửa production code khi có test thất bại hoặc có lỗi luồng được chứng minh; chạy focused tests, full suite và Release build sau sửa.
5. Không đánh dấu P0/Phase 4 hoàn tất chỉ dựa trên unit tests hiện có.

## Phạm vi và an toàn thay đổi

- Không thay đổi cấu hình audio endpoint, driver hoặc thiết bị vật lý.
- Không commit/push các thay đổi code đang có trong working tree cùng với tài liệu này.
- Tài liệu này chỉ ghi nhận kết quả đã quan sát và các bước cần làm; không thay thế `ROADMAP.md` hoặc Phase 4 closeout.
