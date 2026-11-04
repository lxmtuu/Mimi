# Bản phát hành — cách trang GitHub được dựng nên

Trang phát hành là thứ **duy nhất** phần lớn người dùng đọc trước khi bấm tải.
Nó không được biên dịch, nên không có gì báo lỗi nếu một tên tệp sai, một ảnh
chưa từng được commit, hay một câu hứa mà bản dựng không giữ. Vì thế mọi thứ ở
đây đều nằm trong repo và có test gác ([`ReleaseArtworkTests`](../../tests/Mimi.Tests/ReleaseArtworkTests.cs)).

## Một nội dung, ba nơi

| Tệp | Vai trò |
|---|---|
| [`RELEASE-BODY.md`](RELEASE-BODY.md) | **Nguồn duy nhất.** Sửa nội dung ở đây. |
| [`0.7.0.md`](0.7.0.md) | Bản đọc được ngay trong repo (đường dẫn ảnh tính từ thư mục này). |
| `dist/release-notes.md` | Bản workflow gửi lên GitHub, sinh ra lúc chạy — **không** commit. |

Khác biệt giữa hai tệp đầu chỉ là tiền tố đường dẫn ảnh: `docs/assets/release/x.png`
trong nguồn, `../assets/release/x.png` trong bản đọc tại chỗ.

## Khi phát hành một phiên bản mới

1. Sửa [`RELEASE-BODY.md`](RELEASE-BODY.md): số phiên bản, bảng tải, phần "Có gì
   mới", và **đọc lại mục "Những gì chưa xong"** — đó là mục dễ nói quá nhất.
2. Sinh lại ảnh (xem dưới) với số phiên bản mới.
3. Cập nhật bản đọc tại chỗ: đổi `docs/assets/release/` thành `../assets/release/`
   trong nội dung vừa sửa, lưu thành `docs/release/<phiên bản>.md`.
4. Đẩy tag. Workflow `Release` đóng gói, ghi checksum, và **đăng đúng nội dung
   này** (`gh release create/edit --notes-file`). Không dùng
   `--generate-notes`: nó sẽ thay bảng tải và mục "chưa xong" bằng danh sách
   commit.

Chạy lại workflow cho cùng một tag sẽ **cập nhật** nội dung (`gh release edit`),
không giữ lại bản cũ.

## Ảnh

Ảnh được **vẽ từ tài nguyên trong repo**, không dùng ảnh ngoài, không tải từ
Internet:

| Ảnh | Nguồn |
|---|---|
| `docs/assets/release/banner-vi.png`, `banner-en.png` | logo `src/Mimi.App/Assets/mimi-logo.png`, mascot đã cắt viền, font **Be Vietnam Pro** |
| `docs/assets/release/whats-new-0.7.0.png` | font + bảng màu của app |
| `docs/assets/release/mimi-mascot-cutout.png` | mascot gốc, đã bỏ viền trắng |

```powershell
# cần: pip install pillow numpy
python tools/release-art/cutout_mascot.py --output docs/assets/release/mimi-mascot-cutout.png
python tools/release-art/banner.py --version 0.7.0 --lang vi
python tools/release-art/banner.py --version 0.7.0 --lang en
python tools/release-art/whats_new.py --version 0.7.0 --output docs/assets/release/whats-new-0.7.0.png
```

Vì sao cắt viền: mascot trong `Assets/` có viền trắng dày và bóng đổ trắng — đẹp
trên nền sáng, thành quầng lạ trên nền tối. Script *flood* viền trắng từ mép trong
suốt vào, nên nét trắng **bên trong** nhân vật (điểm sáng mắt, tất, highlight)
không bị chạm vì nhân vật bao kín chúng. Tệp gốc trong `Assets/` không bị sửa.

### Ảnh đầu trang và ảnh giao diện cho README

| Ảnh | Sinh bởi |
|---|---|
| `docs/assets/readme/btn-download-*.png`, `pill-*.png` | `tools/release-art/readme_badges.py` |
| `docs/assets/readme/preview-*.png` | `tools/release-art/ui_previews.py` (dùng `ui_kit.py`, `ui_icons.py`) |

```powershell
pip install pillow numpy
python tools/release-art/readme_badges.py
python tools/release-art/ui_previews.py
```

**Nút tải và huy hiệu được vẽ ở đây, không lấy từ shields.io**, vì ba lý do: trang
không cần mạng để hiển thị đúng, màu khớp chính xác với theme của app, và **số
phiên bản được đọc từ `Mimi.App.csproj`** chứ không gõ tay — nên nó không thể lệch.

**Ba ảnh `preview-*.png` là bản dựng lại, không phải ảnh chụp màn hình.** Màu lấy
từ `BuiltInThemes.cs` (theme *Neon Stage*), nhãn từ `UiStrings*.resx`, bố cục từ
`MainWindow.xaml` (cửa sổ 1320×820, rail 210 px, cột Now Playing 270 px). README
nói rõ điều này ngay dưới ba ảnh, và `ReadmeArtworkTests` gác câu đó.

Tên bài và tên nghệ sĩ trong ảnh là **tên bịa** — dùng tên thật sẽ ngụ ý một
quyền mà dự án không có.

Ba ảnh preview lưu ở **palette 256 màu** (81 / 87 / 67 KB thay vì 262 / 200 / 225 KB
khi lưu RGBA). Vì vậy chúng **không bo góc**: PNG indexed chỉ mang được một chỉ số
trong suốt duy nhất, và PIL lặng lẽ làm phẳng alpha khi ghi, nên bo góc sẽ biến
thành bốn góc vuông đen — tệ hơn là không bo. Nền ảnh trùng màu nền app
(`#070B14`), nên trên GitHub viền vuông gần như không thấy.

### Ảnh chụp giao diện thật

Chưa có, và **không** dùng ảnh dựng thay thế: ảnh chụp màn hình giả là cách nhanh
nhất để mất lòng tin. Khi chụp được (Windows, cửa sổ 1280×800):

1. Lưu vào `docs/assets/release/screenshots/` với tên mô tả:
   `now-playing.png`, `stage.png`, `lyrics.png`, `settings.png`.
2. Chèn vào `RELEASE-BODY.md` ngay dưới phần mô tả tính năng tương ứng:

   ```markdown
   ![Now Playing](docs/assets/release/screenshots/now-playing.png)
   ```

Test `EveryPictureTheReleaseBodyShowsIsInTheRepository` sẽ báo nếu một ảnh được
nhắc tới mà chưa commit.

## Mẹo đã trả giá để biết

- **Pillow thay pixel chứ không blend.** Vẽ một hình trong suốt lên ảnh RGBA rồi
  `convert("RGB")` sẽ ra **khối màu đặc**. Mọi chi tiết mờ phải vẽ trên layer
  riêng rồi `alpha_composite`.
- **`Set-Content` ghi CRLF**, nên `sha256sum -c` sẽ tìm một tên tệp kết thúc bằng
  ký tự xuống dòng. Checksum được ghi bằng `[System.IO.File]::WriteAllText` với
  UTF-8 không BOM.
- **Đường dẫn tương đối trong repo sẽ 404 trên trang phát hành**, nên
  `render_release_notes.ps1` đổi chúng thành URL tuyệt đối.
