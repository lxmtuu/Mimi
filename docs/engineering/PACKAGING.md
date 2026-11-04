# Đóng gói & phát hành (Phase 7 / D-02)

Tài liệu này ghi trạng thái thật của việc đóng gói, các lựa chọn đã cân nhắc, và
việc gì còn phải làm. `ROADMAP.md` vẫn là hồ sơ quyết định chính thức.

## Đang có gì

| Thứ | Trạng thái |
|---|---|
| Build Release tự động trên mọi push/PR | **Có** — [`.github/workflows/windows-build.yml`](../../.github/workflows/windows-build.yml) |
| Quy trình phát hành bằng một workflow, chạy khi push tag `v*` | **Có** — [`.github/workflows/release.yml`](../../.github/workflows/release.yml) |
| Bản phát hành **self-contained** (không cần cài .NET) | **Có** — publish `win-x64`, đóng zip |
| Kiểm tra tag khớp `<Version>` trong `Mimi.App.csproj` | **Có** — workflow dừng nếu lệch |
| Bộ cài (installer) `.exe` | **Có** — Inno Setup 6, cài theo từng người dùng ([`installer/Mimi.iss`](../../installer/Mimi.iss)) |
| Bản portable `.zip` | **Có** — cùng một thư mục publish |
| `SHA256SUMS.txt` kèm mỗi bản phát hành | **Có** — định dạng `sha256sum -c` đọc được |
| Ký số (Authenticode) | **Chưa** — chờ D-07 (SignPath Foundation) |
| Auto-update | **Chưa, và không nằm trong v1.0** — xem mục D-02 |
| `PublishSingleFile` | **Cố ý không dùng** — xem lý do bên dưới |

## Cách phát hành hiện tại

```powershell
# 1. Chốt số phiên bản trong src/Mimi.App/Mimi.App.csproj và src/Mimi.App/app.manifest
# 2. Ghi mục CHANGELOG
git tag v0.7.0
git push origin v0.7.0
```

Workflow `Release` sẽ: restore → build → test → publish self-contained `win-x64`
→ chép `LICENSE`, `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md`, `SECURITY.md` vào thư
mục phát hành
→ zip bản portable → **compile bộ cài bằng `ISCC`** trên **cùng thư mục đó**
→ ghi `SHA256SUMS.txt` → tạo/cập nhật GitHub Release và đính kèm **ba** tệp.

Kết quả, mỗi bản phát hành có ba thứ:

| Tệp | Dùng khi nào |
|---|---|
| `Mimi-<tag>-win-x64.zip` | Muốn chạy ngay, không cài, hoặc mang theo USB |
| `Mimi-<phiên bản>-Setup.exe` | Muốn cài vào Start menu, có mục gỡ cài đặt (cài theo người dùng, **không cần admin**) |
| `SHA256SUMS.txt` | Kiểm tra tệp tải về không bị sửa: `sha256sum -c SHA256SUMS.txt` |

Không cần cài .NET cho cả hai dạng: bản publish là `--self-contained true`.

## Vì sao không `PublishSingleFile`

`TagLibSharp` là **LGPL-2.1**. LGPL yêu cầu người dùng có khả năng thay thế thư
viện bằng phiên bản tương thích. Gộp tất cả vào một `.exe` duy nhất làm việc đó
không còn khả thi trên thực tế. Vì vậy bản phát hành giữ thư viện ở dạng tệp
`.dll` rời — đây là lựa chọn pháp lý, không phải sở thích kỹ thuật. Chi tiết:
[`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md).

## D-02 — ĐÃ CHỐT: Inno Setup 6, cài theo từng người dùng

**Ngày chốt: 04/11/2026.** Bản cài là `installer/Mimi.iss`, compile bằng `ISCC.exe`
4 tệp: `LICENSE`, `THIRD-PARTY-NOTICES.md` nằm trong thư mục publish, `CHANGELOG.md`
ở tệp ZIP) và mỗi bản phát hành kèm `SHA256SUMS.txt`.

### Vì sao chọn Inno Setup

Lý do chính không phải "quen thuộc" mà là **ba ràng buộc đã có sẵn trong repo**,
không ràng buộc nào thoả được bằng Velopack hay MSIX:

1. **Chưa có chứng chỉ ký số (D-07).** MSIX **không cài được** nếu gói không được
   ký bằng chứng chỉ mà máy người dùng tin. Chọn MSIX lúc này nghĩa là chọn một
   bản cài mà phần lớn người dùng không mở được. Inno Setup cài được ngay, chỉ
   chịu cảnh báo SmartScreen — và cảnh báo đó đã được nói trước trong tệp
   `installer/BEFORE-INSTALL.txt` mà wizard hiện ra.
2. **AppUserModelID là của ứng dụng, không phải của bộ cài.** Phase 4 đã dựng
   đường nhận diện: app tự tạo `{userprograms}\Mimi.lnk`, tự đóng dấu
   `Mimi.App` lên đó, và tự sửa lại khi shortcut bị trỏ sai chỗ. Velopack quản
   lý shortcut của riêng nó và chuyển thư mục cài vào `%LocalAppData%` — tức là
   **hai chủ sở hữu cho cùng một shortcut**, đúng lớp lỗi đã từng làm Quick
   Settings hiện `Unknown app`. Inno cho phép chỉ định `AppUserModelID` ngay trên
   `[Icons]`, nên installer tạo **đúng** tệp mà app sẽ nhận là "đã đúng"
   (`WindowsAppIdentity.EnsureStartMenuShortcut` trả `AlreadyCurrent`), và không
   có mục thứ hai trong Start menu.
3. **TagLibSharp là LGPL-2.1.** Bản phát hành phải giữ thư viện ở dạng `.dll`
   rời để người dùng thay thế được. Inno đóng gói nguyên thư mục, không ép
   single-file, không cần cấu hình gì thêm. Velopack cũng làm được, nhưng MSSIX
   thì phải đóng gói theo package graph — thêm một lớp phức tạp cho một ràng buộc
   mà không giải quyết.

### Vì sao **không** auto-update trong 1.0

Auto-update của Velopack là một tiến trình nền tải và thay thế thư mục cài. Khi
bản build **chưa được ký**, mỗi lần cập nhật là một lần người dùng phải bấm qua
cảnh báo SmartScreen — trải nghiệm tệ hơn việc tự tải bản mới. Nên 1.0 phát hành
theo mô hình "tải về, cài đè lên bản cũ"; `UsePreviousAppDir=yes` giữ nguyên thư
mục cũ, `AppId` cố định giữ nguyên bản cài để **nâng cấp tại chỗ**, không sinh
bản thứ hai. Khi có chứng chỉ (D-07), bật lại auto-update là một quyết định riêng
— dữ liệu đã đủ để chọn Velopack lúc đó nếu muốn.

### Những gì bộ cài **không** được làm (và đã có test gác)

| Quy tắc | Vì sao | Gác bởi |
|---|---|---|
| Không cài vào `%LOCALAPPDATA%\Mimi` | Đó là thư viện, settings và log của người dùng; `[InstallDelete]` xoá nội dung thư mục cài | `PointingTheInstallerAtRealDataIsRefused` |
| Từ chối thư mục đã có thứ khác | Cùng lý do: `[InstallDelete]` sẽ xoá chúng | như trên |
| Không xoá dữ liệu khi gỡ, trừ khi người dùng chọn "Không" | "Gỡ cài đặt" không có nghĩa là "xoá nhạc của tôi" | `TheInstallerNeverOwnsTheLibraryFolder` |
| Chỉ ghi/xoá trong `HKEY_CURRENT_USER` | Cài theo người dùng thì không được đòi quyền admin cho việc dọn dẹp | `UninstallingRemovesTheRegistrationTheAppMade` |
| Không hard-code số phiên bản | Bộ cài, `Mimi.exe` và trang release phải cùng một số | `TheVersionComesFromTheBuildNotFromThisFile` |
| `AppId` là GUID cố định | Đổi nó biến mọi bản cài cũ thành bản song song | `TheAppIdIsAFixedGuidAndSaysWhyItMustNotChange` |

### Lượt kiểm tra trên máy thật (còn phải chạy)

Bộ cài đã compile trong CI nhưng **chưa từng được cài trên máy Windows thật**.
Các mục dưới đây là điều kiện để coi D-02 xong, không phải để tham khảo:

- [ ] Cài trên Windows 10 sạch và Windows 11 sạch, **không có quyền admin**.
- [ ] Sau khi cài, Start menu hiện **một** mục `Mimi`; Quick Settings hiện tên
      `Mimi` chứ không phải `Unknown app`.
- [ ] Mở app từ shortcut: ghim được vào taskbar, biểu tượng đúng.
- [ ] Cài đè bản mới hơn: vẫn **một** mục Start menu, thư viện và settings còn
      nguyên, không sinh thư mục `Mimi (1)`.
- [ ] Gỡ cài đặt: chương trình và shortcut biến mất, `%LOCALAPPDATA%\Mimi` còn
      nguyên; chọn "No" ở câu hỏi giữ dữ liệu thì thư mục đó bị xoá.
- [ ] Sau khi gỡ, "Open with" không còn mục Mimi trỏ vào tệp không tồn tại.
- [ ] `SHA256SUMS.txt` khớp với tệp tải về.

## D-07 — ký số

Đề xuất trong roadmap: **SignPath Foundation** (miễn phí cho dự án OSS đủ điều
kiện). Cần:

1. Dự án công khai, có giấy phép OSS rõ ràng (**đã có**: MIT).
2. Quy trình build tái lập được trong CI (**đã có**).
3. Đơn đăng ký được duyệt (việc của chủ dự án, không tự làm được).

Cho tới khi có chứng chỉ, bản phát hành **không ký** — người dùng sẽ thấy cảnh
báo SmartScreen. Điều này phải ghi rõ trong phần mô tả release để không ai nghĩ
đó là dấu hiệu phần mềm độc hại.

## Việc còn lại của Phase 7

- [x] `LICENSE` (MIT) + trang ghi công bên thứ ba
- [x] `CHANGELOG.md` theo Keep a Changelog + SemVer
- [x] `CONTRIBUTING.md`, `SECURITY.md`, template issue/PR, Dependabot
- [x] Workflow phát hành bằng một lệnh, kèm kiểm tra tag khớp phiên bản
- [x] D-02: chốt Inno Setup 6, cài theo từng người dùng, có bộ cài + checksum
- [ ] D-02: **lượt cài thật** trên Windows 10 & 11 sạch — danh sách ở mục D-02
- [ ] D-07: ký số
- [ ] Lượt kiểm thử phát hành trên Windows 10 & 11 **sạch** (máy ảo), gồm nâng
      cấp từ bản cũ giữ nguyên thư viện
- [ ] Tag `v1.0.0` và release công khai

Ba mục cuối cần quyết định và môi trường của chủ dự án; không tự làm.
