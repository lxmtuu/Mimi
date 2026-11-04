# Báo lỗi khi app đang chạy

## Hộp thoại "Mimi hit an unexpected problem…"

Hộp thoại này là `DispatcherUnhandledException` — có một exception không được
bắt trên UI thread. App **vẫn chạy**; đây là chỗ nói ra lỗi thay vì im lặng.

Trước 04/11/2026 hộp thoại chỉ có một câu chung, nên ảnh chụp màn hình không
giúp được gì: chi tiết nằm trong log, mà bắt người dùng đi tìm file log cho mỗi
lần lỗi là một thoả thuận tồi. Nay hộp thoại có thêm dòng **Details**:

```
Mimi hit an unexpected problem, but it will try to keep running.
Details were written to the log in %LocalAppData%\Mimi\logs.

Details: InvalidOperationException: Cannot scroll — at ScrollViewer.ScrollToVerticalOffset
```

Nội dung dòng đó do `CrashReport.Summary` dựng: **kiểu exception + câu thông báo
+ nơi ném lỗi**, gộp về một dòng, cắt ở 320 ký tự. Nếu exception là wrapper
(`AggregateException`, `TargetInvocationException`…), dòng đó ghi **cả hai** kiểu
— `AggregateException: IOException: …` — và câu thông báo lấy từ lỗi bên trong,
vì đó mới là câu có nghĩa.

## Một lỗi chỉ hiện một lần

`CrashReport.Signature` tạo một "dấu nhận dạng" ổn định cho mỗi lỗi: kiểu +
nơi ném + kiểu lỗi bên trong. Cùng một lỗi ở cùng một chỗ → cùng dấu, dù thông
báo khác nhau.

`App` giữ tập các dấu đã hiện trong phiên. Lần đầu: hiện hộp thoại. Các lần sau:
**chỉ ghi log**, không mở thêm hộp. Lý do rất thực tế — một lỗi nằm trong vòng
lặp 60 hình/giây hoặc trong timer 100 ms sẽ mở hàng chục hộp thoại chồng lên
nhau, che mất app và trông như app treo, trong khi app vẫn chạy bình thường.

Log vẫn ghi **mọi** lần, kèm `first occurrence=True/False`, nên đếm được tần suất.

## Ba vòng lặp không được phép ném lỗi

Có ba chỗ chạy liên tục mà người dùng không hề bấm gì, nên chúng không được phép
đẩy exception ra dispatcher:

| Chỗ | Nhịp | Xử lý |
|---|---|---|
| `VisualizerView.OnRendering` | ~60 hình/giây | `try/catch` quanh `Draw`; lỗi được ghi log **một lần** theo dấu, frame đó mất, hình tiếp tục vẽ |
| `ScrollSelectedItemIntoViewBehavior` | mỗi lần dòng lời đổi | `ScrollIntoView` + `UpdateLayout` nay nằm **trong** `try` (trước đây `UpdateLayout` ở ngoài) |
| `ThemeBrushSync.Sync` | mỗi lần đổi theme | không ném: brush lạ chỉ ghi warning, brush frozen được thay bằng instance mới |

Đây là chủ ý, không phải che lỗi: visualizer là trang trí, và một khung hình lỗi
phải có giá là **một khung hình**, không phải một hộp thoại. Chi tiết đầy đủ vẫn
vào log.

## Khi cần tìm đúng lỗi

Log nằm ở `%LOCALAPPDATA%\Mimi\logs\mimi-*.log`. Dán lệnh này vào PowerShell để
lấy phần đáng đọc:

```powershell
Select-String -Path "$env:LOCALAPPDATA\Mimi\logs\mimi-*.log" -Pattern "Unhandled UI exception","Unobserved task exception" -Context 0,12 | Select-Object -Last 40
```

Dòng `[<dấu>]` trong log khớp với `Details` trên hộp thoại, nên ảnh chụp và log
đối chiếu được với nhau.
