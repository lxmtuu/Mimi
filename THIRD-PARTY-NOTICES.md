# Third-party notices / Ghi công bên thứ ba

Mimi là phần mềm tự do theo giấy phép MIT ([`LICENSE`](LICENSE)). Bản thân Mimi
thì không thể chạy nếu thiếu các thư viện và tài nguyên dưới đây, mỗi thứ có
giấy phép riêng. Tệp này là bản ghi công và là danh sách nghĩa vụ phải giữ khi
phát hành.

> **Trạng thái:** rà soát ngày 04/11/2026 trên đúng các phiên bản đang dùng.
> Kiểm tra lại mỗi lần nâng phiên bản package. Giấy phép nêu ở đây lấy từ
> metadata của kho nguồn tương ứng; khi phát hành chính thức, đối chiếu lại với
> tệp `LICENSE` đi kèm từng package trong `~/.nuget/packages`.

## Thư viện chạy trong bản phát hành

| Thư viện | Phiên bản | Giấy phép | Dùng cho |
|---|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.4.0 | MIT | `ObservableObject`, `RelayCommand` — nền MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | 8.0.1 | MIT | Lắp ráp service lúc khởi động |
| [Microsoft.Web.WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) | 1.0.2903.40 | Giấy phép SDK WebView2 của Microsoft | Host player nhúng chính thức của YouTube |
| [Serilog](https://github.com/serilog/serilog) | 4.2.0 | Apache-2.0 | Ghi log |
| [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file) | 6.0.0 | Apache-2.0 | Ghi log ra `%LOCALAPPDATA%\Mimi\logs` |
| [NAudio](https://github.com/naudio/NAudio) | 2.2.1 | MIT | WASAPI shared, WaveOut fallback, đọc file |
| [NAudio.Core](https://github.com/naudio/NAudio) | 2.2.1 | MIT | Kiểu dữ liệu âm thanh dùng trong Visualizer |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) | 8.0.8 | MIT | Catalog thư viện SQLite |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | 4.154.0 | MIT (Skia gốc: BSD-3-Clause) | Resize ảnh bìa 96/512 |
| [TagLibSharp](https://github.com/mono/taglib-sharp) | 2.3.0 | **LGPL-2.1** | Đọc metadata và lời nhúng |

### Nghĩa vụ riêng của TagLibSharp (LGPL-2.1)

TagLibSharp được liên kết **động** ở dạng thư viện rời trong thư mục xuất bản,
không sửa đổi, không nhúng vào tệp thực thi duy nhất. Điều đó giữ Mimi ở vị trí
hợp lệ với LGPL, với hai điều kiện khi phát hành:

1. **Ghi công** như bảng trên và kèm bản giấy phép LGPL-2.1 của package.
2. **Không cấm thay thế**: người dùng phải có khả năng thay thế thư viện bằng
   phiên bản tương thích của riêng họ. Vì vậy **không** gộp tất cả vào một tệp
   .exe duy nhất (không dùng `PublishSingleFile` cho tới khi có ý kiến pháp lý).

Nếu về sau cần đơn giản hoá nghĩa vụ này, có thể chuyển sang đọc tag bằng
`TagLib` gốc (C++, LGPL/MPL) hoặc một thư viện MIT — nhưng đó là quyết định
pháp lý, không phải kỹ thuật, nên ghi lại ở đây thay vì tự đổi.

## Chỉ dùng khi phát triển (không nằm trong bản phát hành)

| Thư viện | Phiên bản | Giấy phép |
|---|---|---|
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | 17.11.1 | MIT |
| [xunit](https://github.com/xunit/xunit) | 2.9.2 | Apache-2.0 |
| [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) | 2.8.2 | Apache-2.0 |

## Tài nguyên trong ứng dụng

| Tài nguyên | Nguồn | Ghi chú |
|---|---|---|
| Font **Be Vietnam Pro** (5 weight upright 400–800) | Dự án Be Vietnam Pro | SIL Open Font License 1.1 — điều khoản đầy đủ ở [`src/Mimi.App/Assets/Fonts/OFL.txt`](src/Mimi.App/Assets/Fonts/OFL.txt). Đóng gói kèm ứng dụng là được phép; **không** bán font riêng lẻ |
| Logo, icon, mascot Mimi | Vẽ cho dự án này (AI-assisted, chủ dự án chọn phương án) | Không dựa trên nhân vật bên thứ ba. Bản quyền thuộc chủ dự án Mimi — xem [mục 2 của ROADMAP](docs/ROADMAP.md) về nguyên tắc "Miku-inspired, không phải Miku chính thức" |
| Phông nền, gradient, shader Aurora | Viết cho dự án này | Trong repo, cùng giấy phép MIT của mã nguồn |

## Dịch vụ trực tuyến (không phải thư viện, nhưng có điều khoản)

Mimi chỉ dùng API chính thức. Tài khoản hoặc khoá API là của chính người dùng,
và mỗi dịch vụ có điều khoản riêng mà người dùng phải chấp nhận khi bật tính
năng:

| Dịch vụ | Vai trò | Hồ sơ |
|---|---|---|
| Radio Browser | Danh mục đài phát thanh công cộng | [`docs/legal/radio-browser.md`](docs/legal/radio-browser.md) |
| Audius | Danh mục nhạc, phát qua API chính thức | [`docs/legal/audius.md`](docs/legal/audius.md) |
| YouTube Data API v3 + IFrame Player | Tìm kiếm metadata, phát trong player chính thức **hiển thị** | [`docs/legal/youtube-music-player.md`](docs/legal/youtube-music-player.md) |

Mimi **không** tải, trích xuất hay cache dữ liệu âm thanh/ video từ bất kỳ dịch
vụ nào ở trên.

## Nhãn hiệu

"Hatsune Miku", "Vocaloid" và các nhãn hiệu liên quan thuộc về chủ sở hữu tương
ứng. Mimi **không** liên kết, bảo trợ hay được chấp thuận bởi họ. Giao diện lấy
cảm hứng từ văn hoá anime/Vocaloid nhưng dùng nhân vật và tài nguyên gốc của
dự án. Không dùng tên hay hình ảnh nhân vật có bản quyền trong bản phát hành.
