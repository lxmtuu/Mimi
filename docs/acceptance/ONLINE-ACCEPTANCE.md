# Nghiệm thu Online: Audius & YouTube

Hai lát này đã vào `main` và được dùng thật, nhưng **chưa có bản ghi nghiệm thu
theo từng mục**. Đây là bản ghi đó: mỗi mục một thao tác, một kết quả kỳ vọng,
một ô trạng thái.

Trạng thái chấm theo đúng quy ước của bộ kit: `PASS` chỉ khi có kết quả quan sát
được ghi lại; `PARTIAL` khi đã chạy nhưng sau đó có bản sửa chưa kiểm lại;
`NOT RUN` khi chưa ai chạy; `BLOCKED` khi không thể chạy được trên bản dựng hiện
tại. Có code và CI xanh **không** nâng một mục lên `PASS`.

> Điều kiện chung: Windows 10/11 thật, mạng hoạt động, bản dựng mới nhất từ
> `main`. Ghi lại số hiệu ở footer (`Mimi 0.7.0 · <commit>`) vào báo cáo — hai
> ảnh chụp của hai bản khác nhau trông giống hệt nhau.

## Audius — Online Music

Bật ở **Cài đặt → Nhạc trực tuyến**. Không cần khoá API; Audius dùng API công
khai, không credential.

| ID | Thao tác | Kỳ vọng | Trạng thái |
|---|---|---|---|
| AU-1 | Mở mục **Online Music** ở sidebar | Panel mở, có ô tìm kiếm, không có lỗi mạng | `NOT RUN` |
| AU-2 | Gõ một từ khoá (ví dụ tên một nghệ sĩ) rồi Enter | Danh sách kết quả có artwork, tiêu đề, nghệ sĩ · thời lượng | `NOT RUN` |
| AU-3 | Tìm một chuỗi vô nghĩa | Danh sách rỗng kèm thông báo "không tìm thấy", **không** treo và không có hộp thoại lỗi | `NOT RUN` |
| AU-4 | Một click trái lên một kết quả | Phát ngay; Now Playing hiện đúng tiêu đề/nghệ sĩ; nhạc local đang phát dừng lại | `NOT RUN` |
| AU-5 | Trong lúc phát, kéo thanh tiến trình | Seek được (nguồn hữu hạn); thời lượng hiện đúng, không phải `0:00` | `NOT RUN` |
| AU-6 | Bấm Play/Pause, rồi `−10s` / `+10s` | Toggle đúng chiều; seek đúng 10 giây | `PARTIAL` — đã chạy 03/11, sau đó có bản sửa cho luồng kết thúc bài nên phải kiểm lại |
| AU-7 | Để bài chạy tới hết | Kết thúc trong engine Mimi, **không** tự nhảy về nhạc local, không có hộp thoại lỗi | `PARTIAL` — đây đúng là lỗi đã sửa sau lượt 03/11, chưa kiểm lại |
| AU-8 | Bấm **Nguồn** trên một kết quả | Mở đúng trang canonical `audius.co` trong trình duyệt | `NOT RUN` |
| AU-9 | Kiểm tra Quick Settings của Windows khi đang phát Audius | Nhận ra `Mimi`, có tiêu đề/nghệ sĩ, timeline hữu hạn | `NOT RUN` |
| AU-10 | Tắt công tắc Online trong Cài đặt | Mục Online biến mất khỏi sidebar, bài đang phát bị gỡ khỏi Now Playing, **nhạc local không bị ảnh hưởng** | `NOT RUN` |
| AU-11 | Bật lại công tắc | Mục Online trở lại; trạng thái được giữ sau khi khởi động lại app | `NOT RUN` |
| AU-12 | Ngắt mạng rồi tìm kiếm | Báo lỗi mạng dễ hiểu; app vẫn dùng được phần local | `NOT RUN` |

**Điều phải thấy rõ:** một bài online **không** được xuất hiện trong
Thư viện / Favorites / Recently played / hàng đợi local. Đây là ranh giới thiết
kế, không phải chi tiết.

## YouTube — tìm kiếm & player nhúng

Cần khoá API của chính người dùng, đặt **từ terminal** (không có ô nhập trong
giao diện):

```powershell
$env:MIMI_YOUTUBE_API_KEY = "khoá-của-bạn"; dotnet run --project .\src\Mimi.App\Mimi.App.csproj
```

Hoặc đặt khoá vào `%LOCALAPPDATA%\Mimi\youtube-api-key.txt`, rồi khởi động lại.

| ID | Thao tác | Kỳ vọng | Trạng thái |
|---|---|---|---|
| YT-1 | Chạy app **không** có khoá | Mục YouTube vẫn hiện nhưng nói rõ cần khoá; **không** có ô nhập khoá | `NOT RUN` |
| YT-2 | Đặt khoá rồi mở mục YouTube | Tìm kiếm được; log **không** chứa khoá | `NOT RUN` |
| YT-3 | Tìm một từ khoá | Kết quả có tiêu đề/kênh/thời lượng | `NOT RUN` |
| YT-4 | Một click trái lên một video | Video phát **trong** Mimi (player nhúng hiển thị), visualizer nhường chỗ cho video | `NOT RUN` |
| YT-5 | Kiểm tra player | Không có **Error 153 / 101 / 150**; nếu có, thông báo nói đúng lý do chứ không để timeline đứng im | `NOT RUN` |
| YT-6 | Bấm Play/Pause trên transport của Mimi | Video toggle đúng chiều (không dừng hẳn) | `NOT RUN` |
| YT-7 | Kéo thanh tiến trình | Video seek theo; nhãn thời gian khớp thời lượng thật | `NOT RUN` |
| YT-8 | `−10s` / `+10s` | Seek đúng 10 giây trong video | `NOT RUN` |
| YT-9 | Đổi âm lượng và mute | Âm lượng video đổi theo; `0%` là mute | `NOT RUN` |
| YT-10 | Next / Previous | Chuyển trong danh sách kết quả, có quấn vòng | `NOT RUN` |
| YT-11 | Repeat `One` / `All` / `Off` | `One` phát lại video, `All` quấn vòng, `Off` dừng ở cuối | `NOT RUN` |
| YT-12 | Bấm **Phát tất cả** | Danh sách phát từ mục đang chọn như một playlist | `NOT RUN` |
| YT-13 | Chuột phải một kết quả → lưu vào danh sách | Video vào danh sách đã lưu; đếm số đúng; sống qua lần mở lại app | `NOT RUN` |
| YT-14 | F11 khi đang phát video | Vào Stage video toàn màn hình quanh **chính** player đó; thoát ra vẫn phát tiếp | `NOT RUN` |
| YT-15 | Tắt công tắc YouTube trong Cài đặt | Mục YouTube biến mất, video dừng, nhạc local không bị ảnh hưởng | `NOT RUN` |
| YT-16 | Nút **Chẩn đoán trình phát** | In `{ready,error,state,duration,position}` của player | `NOT RUN` |

**Điều phải thấy rõ:** video YouTube **không** được đưa vào Thư viện, hàng đợi,
Favorites, Recently played hay phiên nghe local — chỉ danh sách đã lưu riêng của
YouTube (đã được chủ dự án cho phép). Không có tải, trích xuất hay cache media.

## Radio Browser

Đã nghiệm thu runtime riêng ngày 03/11/2026 (xem
[`PHASE-6-CLOSEOUT.md`](PHASE-6-CLOSEOUT.md)), nên không lặp lại ở đây. Nếu cần
chạy lại: mở Radio → tìm một đài → phát → pause/resume → chuyển sang nhạc local
→ tắt công tắc Radio trong khi đang phát.

## Cách ghi kết quả

Sao chép [`RESULT-TEMPLATE.md`](RESULT-TEMPLATE.md), hoặc gửi gọn theo mẫu:

```
Build: Mimi 0.7.0 · <commit>
AU-2 PASS  — 12 kết quả, artwork hiện
AU-7 FAIL  — hết bài thì quay về nhạc local
YT-5 BLOCK — Error 153, kèm ảnh
```

Mục `FAIL` kèm dòng log hoặc ảnh là đủ để mở một vòng sửa. Mục đã `PASS` không
cần chạy lại trừ khi có bản sửa chạm vào đường đó — khi đó đánh `PARTIAL` và ghi
lý do, như AU-6/AU-7 ở trên.
