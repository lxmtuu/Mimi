# Playback Engine — investigation log

> Cập nhật: 2026-10-31
> Branch: `arena/cdda374d-mimi`
> Code checkpoint khi ghi bản đầu: `2babf5c`
> Implementation checkpoint: `2137840`
> Windows CI: [`41138155858`](https://github.com/lxmtuu/Mimi/actions/runs/41138155858) — success

## Mục tiêu

Hoàn tất kiểm tra sâu P0 Playback Engine trước khi mở rộng các hạng mục Phase 4. Trọng tâm hiện tại là bảo đảm thông báo kết thúc bài không bị mất, không xử lý lặp và không tác động nhầm sang track/generation mới khi callback bị trì hoãn bởi WPF Dispatcher hoặc tác vụ bất đồng bộ.

## Công việc đã thực hiện

### 1. Playback-end generation, gate và coordinator

Bản triển khai đã commit tại `2137840` đặt generation tại audio engine thay vì chỉ lấy generation hiện tại khi ViewModel nhận event:

- `AudioService.PlaybackGeneration` tăng khi `Load`, `Stop` hoặc `Seek` bắt đầu một playback run khác.
- Khi output dừng, `AudioService` chụp generation của reader/output vừa dừng trong lock và gửi qua `PlaybackEndedEventArgs`; vì vậy event của bài cũ không bị gán nhầm cho bài mới nếu `Load` chen vào trước khi subscriber xử lý.
- `PlaybackEndNotificationGate.TryQueue(generation)` gộp notification trùng của cùng generation; `Complete(generation)` chỉ giải phóng đúng generation đó.
- `PlaybackEndCoordinator` kiểm tra generation trước phần việc async và kiểm tra lại sau khi await hoàn tất, rồi mới cho phép Repeat/Next.

Các file chính là `src/Mimi.Core/Playback/PlaybackEndedEventArgs.cs`, `PlaybackEndNotificationGate.cs`, `PlaybackEndCoordinator.cs`, `src/Mimi.Core/Services/IAudioPlayer.cs`, `src/Mimi.Audio/AudioService.cs` và `src/Mimi.App/ViewModels/MainWindowViewModel.cs`.

### 2. Unit tests cho gate/coordinator

Có 5 test cho gate và 4 test cho coordinator, bao phủ:

1. Gộp notification trùng trong cùng generation, kể cả khi queue đồng thời.
2. Generation mới vẫn queue được khi notification cũ còn pending.
3. Complete generation cũ không xóa pending của generation mới.
4. Complete generation hiện tại cho phép queue lại.
5. Callback cũ không tiêu thụ callback mới đang pending.
6. Generation đổi trong lúc phần async đang chờ thì callback cũ không được advance.
7. Pending được giải phóng khi phần async ném exception.

### 3. Luồng tích hợp trong ViewModel

`MainWindowViewModel` hiện xử lý như sau:

- `AudioPlayer_PlaybackEnded` lấy generation từ `PlaybackEndedEventArgs` và gọi `TryQueue` trước khi enqueue vào Dispatcher.
- Dispatcher operation bị abort hoặc enqueue ném exception đều gọi `Complete(eventGeneration)`.
- `ProcessPlaybackEndedAsync` chụp đúng track đã kết thúc trước `await CompleteCurrentPlaybackAsync()`.
- `PlaybackEndCoordinator` kiểm tra `audioPlayer.PlaybackGeneration` trước và sau await; `finally` trong coordinator giải phóng pending của generation.
- `AdvanceAfterPlaybackEnded` chỉ Repeat/Next khi `CurrentTrack` vẫn là đúng object đã chụp trước await.
- Mọi `TryPlayTrack` đều đi qua `AudioService.Load`; `Stop`/`Seek` cũng đổi generation nên thao tác can thiệp trong lúc Dispatcher/async đang chờ sẽ làm callback cũ hết hiệu lực.

Test deterministic của coordinator giữ phần async chưa hoàn tất bằng `TaskCompletionSource`, đổi generation ở giữa await, rồi xác nhận hành động advance không chạy. Đây là test logic/coordinator, không phải bằng chứng đã tái hiện race trên WPF thật trước bản sửa.

### 4. Natural-end classification

`PlaybackStopClassifier` đã được commit cùng 7 test case. Bản committed giữ nguyên ngữ nghĩa đã qua A12: output hiện tại dừng không có exception và reader đã nạp có `Length > 0` thì là natural end. Classifier **không** dùng `Position >= Length` làm hard gate, vì WASAPI/MP3 đã từng báo dừng khi vị trí còn thiếu khoảng 12ms so với duration ước tính; deliberate Stop vẫn được `AudioService` unsubscribe trước, còn output cũ bị loại bằng kiểm tra sender. Position/duration của reader vừa kết thúc được chụp trong lock chỉ để log đúng bài.

## Kết quả kiểm thử đã chạy

### Working tree Windows khi ghi bản đầu

Lệnh chạy trên Windows:

```powershell
dotnet test .\tests\Mimi.Tests\Mimi.Tests.csproj -c Release --no-restore --logger 'console;verbosity=minimal'
```

Kết quả được báo cáo ngày 2026-10-31 là **107 passed / 0 failed / 0 skipped**, exit code `0`. Kết quả này thuộc working tree chưa commit ở thời điểm đó, không phải cây thư mục của `4a926e0` trên GitHub.

### Bản triển khai đã commit

Commit `2137840` bổ sung 16 test case mới: 5 cho gate, 4 cho coordinator và 7 cho classifier. Windows CI [`41138155858`](https://github.com/lxmtuu/Mimi/actions/runs/41138155858) đã hoàn tất `success`, gồm restore, Release build và unit tests. CI xác nhận code/test đã commit; nó không thay thế một lượt chạy runtime trên Windows thật để xác nhận lại A12 sau thay đổi event/generation.

## Trạng thái điều tra

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Generation từ audio engine | Implemented tại `2137840` | Event mang generation được chụp trong `AudioService` |
| Gate/coordinator | Implemented tại `2137840` | Pending theo generation; kiểm tra lại sau await |
| Unit tests mới | PASS trên Windows CI | 16 case mới trong run `41138155858` |
| Callback cũ còn chờ Dispatcher | PASS ở coordinator logic | Test thứ tự A cũ/B mới; chưa phải WPF integration test toàn phần |
| Generation đổi trong lúc `await` | PASS ở coordinator logic | Test giữ async bằng `TaskCompletionSource` |
| Race condition thực tế | Đã phòng vệ, chưa tái hiện runtime trước bản sửa | Không ghi nhận là bug runtime đã xác nhận |
| Release build | PASS trên Windows CI | Cùng run `41138155858` |
| Retest runtime A12 sau `2137840` | Chưa chạy | Cần một lượt Windows thật cho natural-end auto-advance |

## Kế hoạch tiếp theo

1. Trên Windows thật tại checkpoint mới, phát fixture Cyan tới hết bài và xác nhận A12 vẫn tự chuyển sang Magenta.
2. Chạy tiếp lượt vận hành Shuffle/Repeat của Phase 4 closeout; không dùng riêng unit tests này để đóng P4-A hoặc Phase 4.
3. Nếu sau này tái hiện được race ở tầng WPF/Dispatcher ngoài coordinator seam, ghi log generation/track trước khi sửa thêm.

## Phạm vi và an toàn thay đổi

- Không thay đổi cấu hình audio endpoint, driver hoặc thiết bị vật lý.
- Checkout Windows từng được ghi có các thay đổi working tree cùng chủ đề và có thể trùng tên file với bản đã commit tại `2137840`; trước khi pull, hãy kiểm tra `git status`, sao lưu/stash và đối chiếu, không reset hoặc ghi đè mù.
- Tài liệu này chỉ ghi nhận điều tra Playback Engine; không thay thế `ROADMAP.md` hoặc Phase 4 closeout.
