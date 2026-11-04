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

## Vòng focus: nơi duy nhất một giá trị có thể "mất"

Ngày 04/11/2026, hộp thoại mới bắt được lỗi đầu tiên nó tồn tại để bắt:

```
InvalidOperationException: '{DependencyProperty.UnsetValue}'
is not a valid value for property 'FocusVisualStyle'.
```

Xảy ra khi chủ dự án **bấm F11 mở Stage rồi đóng lại**.

### Vì sao phải bấm F11 mới thấy

WPF chỉ đọc `FocusVisualStyle` khi focus đổi **do bàn phím** —
`KeyboardNavigation.ShowFocusVisual` được gọi từ `OnGotKeyboardFocus`. Bấm chuột
không hỏi tới vòng focus. Đóng một cửa sổ làm cửa sổ còn lại được kích hoạt
(`HwndKeyboardInputProvider.OnSetFocus`), và đường đó **cũng tính là bàn phím**.

Nên một giá trị hỏng nằm im suốt nhiều tuần dùng chuột, rồi nổ đúng lúc người
dùng vừa thoát Stage — thời điểm tệ nhất để hiện một hộp thoại.

### Giá trị "mất" là gì

`DependencyProperty.UnsetValue` không phải `null`: nó là giá trị nội bộ của WPF
nghĩa là "thuộc tính này không có giá trị". Một `Setter` giữ nó sẽ ném lỗi ngay
khi ai đó **đọc** thuộc tính. Trước bản sửa, cả 9 chỗ đặt `FocusVisualStyle`
trong Mimi đều dùng `{StaticResource MimiFocusVisualStyle}` — một tham chiếu
được giải quyết **một lần** lúc style được materialise; nếu lúc đó key chưa thấy
được, giá trị là `UnsetValue` và nó nằm đó chờ.

Đây đúng là bài học repo đã ghi cho brush: *"A brush that an element picked up
with `StaticResource` is fixed at that moment"* — và đã có test cấm `StaticResource`
cho brush. `FocusVisualStyle` không phải brush nên lọt lưới test đó.

### Đã sửa

| Trước | Sau |
|---|---|
| `MimiFocusVisualStyle` khai báo trong `App.xaml`, **sau** `<MergedDictionaries>` | Chuyển sang [`Themes/FocusVisual.xaml`](../../src/Mimi.App/Themes/FocusVisual.xaml), merge **đầu tiên** |
| 9 chỗ dùng `{StaticResource ...}` | Toàn bộ `{DynamicResource ...}` |

`DynamicResource` không thể ra `UnsetValue`: trường hợp xấu nhất là **không có
vòng focus**, không phải một exception. Vòng focus vẫn vẽ, vì key có mặt trước
khi bất cứ style nào dùng nó.

Test `FocusVisualStyleTests` giữ cả ba điều: không `StaticResource` cho
`FocusVisualStyle`, key khai báo đúng một lần, và dictionary của nó được merge
trước mọi dictionary dùng nó.
