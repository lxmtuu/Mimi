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

## Vòng focus: lỗi duy nhất mà một hộp thoại không giúp được gì

Ngày 04/11/2026, hộp thoại mới bắt được lỗi đầu tiên nó tồn tại để bắt:

```
InvalidOperationException: '{DependencyProperty.UnsetValue}'
is not a valid value for property 'FocusVisualStyle'.
```

Xảy ra khi mở Stage bằng F11 rồi đóng — cả lúc đang phát nhạc.

### Điều đã chắc chắn

1. **Ai đọc cũng biết chỗ đọc.** WPF gọi
   `KeyboardNavigation.ShowFocusVisual` từ `OnGotKeyboardFocus`. Nghĩa là
   `FocusVisualStyle` chỉ bị **đọc** khi focus đổi **do bàn phím** (bấm chuột
   không hỏi tới nó), hoặc khi một cửa sổ được kích hoạt lại —
   `HwndKeyboardInputProvider.OnSetFocus`. Đóng Stage làm cửa sổ chính được kích
   hoạt lại, nên đường này tính là bàn phím. Đây là lý do lỗi nằm im rất lâu rồi
   mới hiện đúng lúc thoát màn hình lớn.
2. **`UnsetValue` không phải `null`.** Nó là giá trị nội bộ của WPF nghĩa là
   *"thuộc tính này không có giá trị"*; ai **đọc** thuộc tính đang giữ nó sẽ ăn
   exception. Vì vậy lỗi không bao giờ hiện lúc set, chỉ hiện lúc đọc.
3. **Hộp thoại không có ích ở đây.** Người dùng không làm gì sai, và hộp thoại
   không có hành động nào để đề nghị. Nó chỉ chặn app.

### Điều **chưa** chắc — và vì sao

Ban đầu tôi cho rằng thủ phạm là thứ tự `StaticResource`: 9 chỗ đặt vòng focus
đều dùng `{StaticResource MimiFocusVisualStyle}`, và key này khai báo trong
`App.xaml` **sau** `<MergedDictionaries>`, trong khi 8 chỗ dùng nằm ở
`ControlStyles.xaml` (một dictionary được merge).

Kiểm chứng lại thì **giả thuyết đó không đứng vững**: cùng một dictionary đó
cũng dùng `{StaticResource MimiUiFontFamily}` với đúng kiểu tham chiếu ấy, mà
font hiển thị bình thường — nếu key thật sự vô hình thì `FontFamily` đã ném lỗi
tương tự ngay từ đầu. Vậy tham chiếu chéo kiểu này vẫn giải quyết được, và
nguyên nhân nằm ở chỗ khác: **một phần tử cụ thể** nào đó nhận keyboard focus
mang giá trị rỗng.

Vì thế hai thay đổi XAML (tách `Themes/FocusVisual.xaml` merge đầu tiên, đổi
sang `DynamicResource`) được giữ lại như **biện pháp phòng ngừa**, không phải
bằng chứng đã tìm ra gốc: `DynamicResource` không thể ra `UnsetValue`, nên
trường hợp xấu nhất là thiếu vòng focus thay vì một exception.

### Cách tìm ra phần tử thật sự

Không đọc code mà suy ra được nữa, nên app tự khai: `FocusVisualRepair`.

| Bước | Việc |
|---|---|
| Nhận dạng | `InvalidOperationException` có `FocusVisualStyle` trong thông báo (không dựa vào câu chữ đầy đủ, vì Windows có thể dịch), hoặc có frame `KeyboardNavigation.ShowFocusVisual` |
| Định danh | Ghi log **kiểu + tên + kiểu style** của phần tử đang focus: `Button 'PlayButton' (style for Button)` |
| Vá | Đặt một giá trị cục bộ thật cho `FocusVisualStyle` của **chính phần tử đó** — giá trị cục bộ thắng setter của style, nên lần đọc sau không thể ném. Resource không tìm thấy thì đặt `null`: mất vòng focus là mất thẩm mỹ, mất cửa sổ thì không |
| Im lặng | Không hiện hộp thoại cho lỗi này |

Dòng log cần tìm khi báo lỗi (một dòng):

```powershell
Select-String -Path "$env:LOCALAPPDATA\Mimi\logs\mimi-*.log" -Pattern "Recovered from an empty focus ring" | Select-Object -Last 5
```

Nó cho biết **phần tử nào** — và đó là chỗ để nhìn trong `.xaml`. Không còn phải
đoán.

### Kết quả lượt chạy đầu tiên (04/11/2026)

Trên Windows thật: phát nhạc → F11 mở Stage → đóng Stage.

- **Không còn hộp thoại.**
- **Không có** dòng `Recovered from an empty focus ring` trong log.

Kết luận đọc được từ đó: triệu chứng đã hết, nhưng **không** phải nhờ lớp thu hồi
— lớp phòng ngừa ở XAML đã đủ để giá trị không bao giờ rỗng. `FocusVisualRepair`
vì thế nằm lại như lưới đỡ cho trường hợp khác, chứ chưa chứng minh được gì về
nguyên nhân gốc. Ghi đúng như vậy để sau này không ai đọc thành "đã tìm ra gốc".

### Vì sao không chỉ "bắt lỗi rồi bỏ qua"

Nuốt exception mà không đặt giá trị thì lần focus sau lại ném tiếp, mỗi lần một
lần log, và vòng focus vĩnh viễn không vẽ được. Vá bằng giá trị cục bộ giải quyết
được cả hai: lần đọc sau **thành công**, và vòng focus **hiện ra**.

### Hộp thoại giờ mang số hiệu bản dựng

Hai ảnh chụp của cùng một lỗi trông y hệt nhau, mà một trong hai có thể đã chứa
bản sửa. Nên dòng chi tiết kết thúc bằng `[0.7.0 · <commit>]`
(`BuildInfo.Display`). Ảnh chụp tự nói nó chụp bản nào.
