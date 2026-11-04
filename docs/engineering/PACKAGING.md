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
| Bộ cài (installer) `.exe`/`.msi` | **Chưa** — chờ D-02 |
| Ký số (Authenticode) | **Chưa** — chờ D-07 (SignPath Foundation) |
| Auto-update | **Chưa** — phụ thuộc lựa chọn installer |
| `PublishSingleFile` | **Cố ý không dùng** — xem lý do bên dưới |

## Cách phát hành hiện tại

```powershell
# 1. Chốt số phiên bản trong src/Mimi.App/Mimi.App.csproj và src/Mimi.App/app.manifest
# 2. Ghi mục CHANGELOG
git tag v0.7.0
git push origin v0.7.0
```

Workflow `Release` sẽ: restore → build → test → publish self-contained `win-x64`
→ chép `LICENSE`, `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md` vào thư mục phát hành
→ zip → tạo/ cập nhật GitHub Release và đính kèm zip.

Kết quả: người dùng tải zip, giải nén, chạy `Mimi.exe`. Không cần cài .NET.

## Vì sao không `PublishSingleFile`

`TagLibSharp` là **LGPL-2.1**. LGPL yêu cầu người dùng có khả năng thay thế thư
viện bằng phiên bản tương thích. Gộp tất cả vào một `.exe` duy nhất làm việc đó
không còn khả thi trên thực tế. Vì vậy bản phát hành giữ thư viện ở dạng tệp
`.dll` rời — đây là lựa chọn pháp lý, không phải sở thích kỹ thuật. Chi tiết:
[`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md).

## D-02 — so sánh ba lựa chọn đóng gói

| | Velopack | Inno Setup | MSIX |
|---|---|---|---|
| Cài đặt trải nghiệm | Cài + tự cập nhật delta | Cài cổ điển, wizard quen thuộc | Cài qua App Installer của Windows |
| Auto-update | Có sẵn | Phải tự viết | Qua Store/App Installer |
| Ký số | Authenticode, ký tệp cài + gói update | Authenticode | **Bắt buộc** chứng chỉ tin cậy, khắt khe hơn |
| Giữ danh tính Windows (`AppUserModelID Mimi.App`) | Cần kiểm tra: chạy từ thư mục `%LocalAppData%` | Giữ nguyên, shortcut Start do installer tạo | Theo manifest package, khác cơ chế |
| Gỡ sạch | Có | Có | Có |
| Phức tạp CI | Trung bình (thêm `vpk` tool) | Thấp (`iscc` sau khi cài) | Cao (cần chứng chỉ + `signtool`) |
| Rủi ro chính | Cơ chế cập nhật là bên thứ ba phải hiểu | Không có auto-update | Không ký được = không cài được |

**Đề xuất (chưa chốt):** đi **Velopack** nếu ưu tiên auto-update và chấp nhận phụ
thuộc thêm; đi **Inno Setup** nếu ưu tiên đường đi ít rủi ro và chưa cần
auto-update. MSIX chỉ nên chọn khi đã có chứng chỉ ký số, vì thiếu chứng chỉ là
chặn hoàn toàn.

Điểm phải kiểm tra trước khi chốt, bất kể chọn cách nào: sau khi cài, **Start
menu và Quick Settings vẫn phải nhận ra ứng dụng là `Mimi`**, và gỡ cài đặt phải
xoá sạch shortcut đó. Phase 4 đã từng mất một lượt vì identity của bản
unpackaged, nên đây là tiêu chí nghiệm thu, không phải chi tiết.

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
- [ ] D-02: chốt cách đóng gói và làm bộ cài
- [ ] D-07: ký số
- [ ] Lượt kiểm thử phát hành trên Windows 10 & 11 **sạch** (máy ảo), gồm nâng
      cấp từ bản cũ giữ nguyên thư viện
- [ ] Tag `v1.0.0` và release công khai

Ba mục cuối cần quyết định và môi trường của chủ dự án; không tự làm.
