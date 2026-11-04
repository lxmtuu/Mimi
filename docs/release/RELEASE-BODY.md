<!--
  The body of the GitHub release v0.7.0.

  This file is the single source for two things:
    1. the release notes published by .github/workflows/release.yml, and
    2. docs/release/0.7.0.md, the copy that lives in the repository.

  Keep every claim here something the repository can prove. Wording that sells a
  promise the code does not keep is the one kind of bug no test can catch.
-->

Mimi là trình nghe nhạc cho Windows: nhạc của bạn nằm trên máy bạn, không tài
khoản, không telemetry, không tải nhạc từ đâu về. Bản `0.7.0` là bản đầu tiên
**cài được như một ứng dụng bình thường** — và cũng là bản đầu tiên lời bài hát
chạy theo từng từ.

![Mimi 0.7.0](docs/assets/release/banner-vi.png)

---

## Tải về

| Tệp | Dùng khi nào |
|---|---|
| **[`Mimi-0.7.0-Setup.exe`](https://github.com/lxmtuu/Mimi/releases/download/v0.7.0/Mimi-0.7.0-Setup.exe)** | Muốn cài vào Start menu. **Không cần quyền quản trị**, không hỏi UAC. Có mục gỡ cài đặt trong *Settings → Apps*. |
| **[`Mimi-v0.7.0-win-x64.zip`](https://github.com/lxmtuu/Mimi/releases/download/v0.7.0/Mimi-v0.7.0-win-x64.zip)** | Muốn chạy ngay: giải nén ra đâu cũng được, chạy `Mimi.exe`. |

Cả hai đều **self-contained**: không cần cài .NET. Cần Windows 10 1809 trở lên
hoặc Windows 11, bản x64.

**Kiểm tra tệp tải về** (một dòng, chạy trong thư mục vừa tải):

```powershell
Get-FileHash .\Mimi-0.7.0-Setup.exe -Algorithm SHA256; Get-Content .\SHA256SUMS.txt
```

Hai chuỗi băm phải giống nhau. `SHA256SUMS.txt` đọc được bằng `sha256sum -c` trên
Linux/macOS/WSL.

> **Lần chạy đầu sẽ có cảnh báo SmartScreen "Windows protected your PC".**
> Bản dựng này **chưa được ký số**, nên Windows không biết nhà phát hành là ai.
> Bấm *More info → Run anyway*. Đây là thiếu chứng chỉ, không phải thiếu mã
> nguồn: mã nguồn và workflow sinh ra tệp này đều công khai.

---

![Có gì mới trong 0.7.0](docs/assets/release/whats-new-0.7.0.png)

## Có gì mới

### Lời bài hát

- **Lời offline** từ tệp `.lrc` cùng thư mục (cùng tên gốc với file nhạc), rồi
  mới tới lời nhúng trong tag. Hỗ trợ `mm:ss`, `mm:ss.xx`, `hh:mm:ss.xxx`, nhiều
  timestamp trên một dòng, thẻ `[offset:]`.
- **Karaoke từng từ** với Enhanced LRC: chữ sáng dần đúng nhịp. Bất biến mà tớ
  giữ: ghép các từ lại phải ra **đúng** chữ của dòng, nên cùng một tệp hiển thị
  giống nhau dù Mimi có hiểu tag hay không.
- **Lời lớn trên Stage**: ba dòng quanh vị trí đang phát.

### Stage và giao diện

- **Aurora tĩnh** làm nền Stage, cùng visualizer theo nhịp.
- **Theme Engine**: bốn theme dựng sẵn, cộng theme cộng đồng — thả một tệp JSON
  vào `%LOCALAPPDATA%\Mimi\themes` là dùng được, không cần biên dịch lại.
- **Màu nền tuỳ chỉnh** là một lớp pha **trên** theme đang chọn, không thay thế
  theme. Mặc định là xanh; hồng vẫn còn và vẫn chỉnh được.
- Giao diện **song ngữ Việt / English**, phủ 455 khoá.

### Ba nguồn trực tuyến (tuỳ chọn, tắt được từng cái)

- **Internet Radio** qua Radio Browser — phát đài trực tiếp, không giả duration.
- **Audius** — tìm và phát ngay trong engine của Mimi, có seek.
- **YouTube** — tìm bằng Data API v3 với **khoá của chính bạn**, phát trong
  player nhúng **chính thức và hiển thị** của YouTube. Không trích xuất, không
  tải, không cache âm thanh.

Mỗi nguồn có công tắc riêng trong Cài đặt; tắt thì Mimi không gọi API đó nữa.

### Bộ cài (mới trong bản này)

- Cài **theo từng người dùng** vào `%LOCALAPPDATA%\Programs\Mimi`, không UAC,
  không cần chứng chỉ.
- Một mục Start menu duy nhất, mang đúng AppUserModelID `Mimi.App` — nên Windows
  hiện tên `Mimi` chứ không phải `Unknown app`.
- **Không đụng vào thư viện của bạn.** Gỡ cài đặt chỉ hỏi một câu, mặc định là
  **giữ**; chỉ xoá `%LOCALAPPDATA%\Mimi` khi bạn trả lời rõ ràng là không giữ.
- Cài đè bản mới giữ nguyên thư viện và cài đặt (settings schema tự migrate).

---

## Sửa lỗi đáng chú ý

- Hàng danh sách không còn chuyển trắng chữ khi rê chuột trong theme tối.
- Danh sách lời không còn nhảy xuống dòng cuối mỗi lần dòng hiện tại đổi.
- Đóng Stage bằng `F11`/`Esc` không còn ném `InvalidOperationException`
  (`FocusVisualStyle`) — vòng focus được khai báo trước mọi chỗ dùng.
- Một lỗi trong vòng lặp không còn dựng chồng hộp thoại làm app trông như treo:
  mất một khung hình thay vì cả cửa sổ.
- Đổi theme không còn để lại hai theme cùng lúc.

---

## Quyền riêng tư

- Không tài khoản, **không telemetry**, không gửi gì về dự án.
- Khoá YouTube chỉ đọc từ biến môi trường `MIMI_YOUTUBE_API_KEY` hoặc tệp
  `youtube-api-key.txt` trong thư mục dữ liệu. Không có ô nhập khoá trong giao
  diện, khoá **không** bị ghi vào settings hay log.
- Log chỉ ghi host của luồng phát, không ghi URL đầy đủ của nguồn trực tuyến.
- Chi tiết: [`SECURITY.md`](https://github.com/lxmtuu/Mimi/blob/main/SECURITY.md).

---

## Những gì **chưa** xong (nói thẳng)

Bản `0.7.x` vẫn là bản **0.x**: đủ dùng hằng ngày, nhưng chưa phải `1.0`.

- **Bộ cài chưa qua lượt cài thật trên máy Windows sạch.** Nó đã compile trong CI
  và có test ghim các bất biến, nhưng chưa ai chạy nó trên một VM Win10/Win11
  trắng. Nếu bạn gặp trục trặc khi cài, mở issue kèm ảnh chụp màn hình — đó đúng
  là thứ đang cần.
- **Chưa ký số** (đang chờ chứng chỉ cho dự án mã nguồn mở).
- **Chưa có auto-update.** Tải bản mới và cài đè lên bản cũ.
- Progress/thời gian trên thẻ Quick Settings của Windows chưa hiển thị
  (`P4-A` `PARTIAL`). Độ trễ vật lý của backend âm thanh chưa đo được, nên quyết
  định D-03 vẫn tạm.
- Audius và YouTube **chưa có bản ghi nghiệm thu runtime theo từng mục** — xem
  [`ONLINE-ACCEPTANCE.md`](https://github.com/lxmtuu/Mimi/blob/main/docs/acceptance/ONLINE-ACCEPTANCE.md).

---

## Ghi công

Mimi dùng các thư viện mã nguồn mở, mỗi thư viện giữ giấy phép riêng: NAudio,
TagLibSharp (LGPL-2.1 — vì thế bản phát hành **không** đóng gói single-file, để
bạn thay thế được thư viện), Serilog, SkiaSharp, CommunityToolkit.Mvvm,
Microsoft.Data.Sqlite, WebView2. Danh sách đầy đủ kèm phiên bản và giấy phép:
[`THIRD-PARTY-NOTICES.md`](https://github.com/lxmtuu/Mimi/blob/main/THIRD-PARTY-NOTICES.md).

Giao diện dùng font **Be Vietnam Pro** (SIL OFL 1.1), đóng gói kèm theo.

**Không liên kết với ai.** "Hatsune Miku", "Vocaloid" và các nhãn liên quan thuộc
về chủ sở hữu của chúng. Mimi không được họ bảo trợ, không được họ phê duyệt, và
nhân vật trong ứng dụng là tài sản của dự án này.

---

**Giấy phép:** MIT · **Mã nguồn:** [github.com/lxmtuu/Mimi](https://github.com/lxmtuu/Mimi) ·
**Nhật ký đầy đủ:** [`CHANGELOG.md`](https://github.com/lxmtuu/Mimi/blob/main/CHANGELOG.md) ·
**Báo lỗi:** [issues](https://github.com/lxmtuu/Mimi/issues) ·
**Góp ý:** [`CONTRIBUTING.md`](https://github.com/lxmtuu/Mimi/blob/main/CONTRIBUTING.md)

Nếu Mimi có ích cho bạn, một ngôi sao ⭐ giúp người khác tìm thấy nó.
