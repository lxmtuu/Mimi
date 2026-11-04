# Góp ý cho Mimi / Contributing

Cảm ơn bạn đã ghé. Mimi là trình phát nhạc desktop cho Windows viết bằng
C#/WPF/.NET 8, phát triển theo từng phase trong [`ROADMAP.md`](docs/ROADMAP.md).
Tài liệu này nói **cách** đóng góp; roadmap nói **cái gì** đang được làm.

## Trước khi viết code

1. Mở một issue mô tả vấn đề hoặc đề xuất, trước khi làm PR lớn. Một PR 800 dòng
   không có issue thường phải quay lại làm rõ phạm vi.
2. Đọc [`ROADMAP.md`](docs/ROADMAP.md) mục 2 (nguyên tắc IP: *Miku-inspired, không
   phải Miku chính thức*) và mục 8.5 (cổng pháp lý cho nguồn trực tuyến).
3. Việc đã nằm trong danh sách **không làm** thì không nhận PR, dù code đẹp:
   trích xuất âm thanh từ YouTube, chặn quảng cáo, phá DRM, dùng API không
   chính thức, nhúng client secret.

## Yêu cầu môi trường

- Windows 10 1809 trở lên (app dùng WASAPI, SMTC, WebView2).
- .NET SDK 8.0.
- Không cần Visual Studio; `dotnet` CLI là đủ.

```powershell
git clone https://github.com/lxmtuu/Mimi.git
cd Mimi
dotnet restore Mimi.sln
dotnet build Mimi.sln --configuration Release
dotnet test tests/Mimi.Tests/Mimi.Tests.csproj --configuration Release
dotnet run --project .\src\Mimi.App\Mimi.App.csproj
```

CI chạy đúng ba lệnh `restore` / `build` / `test` trên `windows-latest` với cấu
hình Release. Máy nào build được thì CI cũng phải xanh — nếu chỉ xanh ở một
trong hai chỗ, đó là lỗi cần sửa trước khi review.

## Quy ước commit

`type(scope): câu mô tả` — ví dụ `fix(lyrics): stop the list jumping to the last
line`. `type` dùng một trong: `feat`, `fix`, `test`, `docs`, `build`, `chore`,
`refactor`. Câu mô tả viết ở thể mệnh lệnh, mô tả **hành vi** chứ không phải
tên tệp.

Phần thân commit nên trả lời: chuyện gì đã sai, vì sao cách sửa này đúng, và
cái gì chứng minh điều đó. Một commit không có phần "vì sao" sẽ bị hỏi lại.

## Quy ước code

- **Không hard-code chuỗi hiển thị.** Mọi chữ người dùng thấy đi qua `Loc[...]`
  hoặc `Loc.Format(...)`, thêm khoá vào **cả** `UiStrings.resx` và
  `UiStrings.vi.resx`. `LocalizationServiceTests` gác parity hai tệp.
- **Không hard-code màu trong `.xaml`.** Dùng brush/colour token của theme;
  `NoSurfaceKeepsAHardCodedColour` và `ThemeBrushSyncTests` sẽ fail.
- **Tham chiếu theme phải là `DynamicResource`**, không phải `StaticResource`:
  brush đã nhận lúc load là instance cố định, đổi theme sẽ không tới được nó.
- **Không đặt `StaticResource` cho `FocusVisualStyle`** (hoặc bất cứ thứ gì có
  thể ra `DependencyProperty.UnsetValue`); `FocusVisualStyleTests` gác điều này.
- Thêm package phải cập nhật [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)
  và, nếu là thư viện chạy trong bản phát hành, ghi cả giấy phép.
- Thay đổi dữ liệu người dùng phải đi kèm đường migration trong
  `UserDataService` và test round-trip cho schema mới.

## Test

- Test mới đi cùng thay đổi, không đi sau. Một PR sửa hành vi mà không có test
  sẽ bị hỏi "cái gì sẽ báo nếu điều này quay lại?".
- Với bug đã thấy trên máy thật: test phải **fail trước** bản sửa. Nếu không
  dựng được test như vậy, ghi lý do trong PR và bổ sung bằng chứng khác.
- Hành vi phụ thuộc Windows thật (âm thanh, SMTC, DPI) thì test phần logic tách
  được, và ghi rõ phần nào cần kiểm tay — đừng đánh dấu `PASS` cho thứ chưa
  chạy.

## Pull request

Một PR nên làm một việc. Trong phần mô tả, nêu:

1. **Vì sao** — hành vi nào đang sai hoặc thiếu, thấy ở đâu.
2. **Đã làm gì** — và nếu có nhiều cách, vì sao chọn cách này.
3. **Chứng minh** — link CI, ảnh chụp trước/sau nếu là giao diện, log nếu là
   lỗi runtime.
4. **Chưa làm** — phần cố ý để lại, để người review không phải tự đoán.

Đóng góp của bạn được nhận theo giấy phép MIT của dự án (xem
[`LICENSE`](LICENSE)). Không gửi tài nguyên (hình, âm thanh, font) mà bạn không
có quyền cấp phép lại.

## Báo lỗi bảo mật

Không mở issue công khai. Xem [`SECURITY.md`](SECURITY.md).

## Câu hỏi

Mở issue với nhãn `question`. Nếu câu hỏi là "làm sao chạy được", kèm phiên bản
Windows, số hiệu bản dựng ở footer (`Mimi 0.7.0 · <commit>`), và 20 dòng log
quanh lỗi trong `%LOCALAPPDATA%\Mimi\logs`.
