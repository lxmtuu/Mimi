# File mẫu

| File | Dùng để |
|---|---|
| [`sunset-drive.json`](sunset-drive.json) | Theme cộng đồng mẫu (schema v1) — chép vào `%LocalAppData%\Mimi\themes` rồi nạp lại từ Cài đặt. Xem [`docs/COMMUNITY-THEMES.md`](../COMMUNITY-THEMES.md). |
| [`karaoke-sample.lrc`](karaoke-sample.lrc) | Lời mẫu **Enhanced LRC** có timestamp từng từ — dùng để tự kiểm tra nhịp tô karaoke trên máy thật. |

## Dùng `karaoke-sample.lrc`

Mimi tìm lời theo **cùng thư mục, cùng tên gốc** với file nhạc
(`song.flac` → `song.lrc`), nên file mẫu phải được đổi tên cho khớp bài
bạn muốn thử:

```powershell
Copy-Item ".\docs\examples\karaoke-sample.lrc" "D:\Nhac\BaiHatCuaBan.lrc"
```

(Đổi `D:\Nhac\BaiHatCuaBan` thành đường dẫn bài thật, bỏ phần đuôi file nhạc.
Ví dụ bài `D:\Nhac\senbonzakura.mp3` thì file lời là `D:\Nhac\senbonzakura.lrc`.)

Sau đó trong Mimi: mở bài đó → thẻ **Lời bài hát** ở Now Playing, hoặc mở
**Stage**. Nhãn nguồn phải hiện `.lrc file`; nếu hiện nguồn khác thì file chưa
được nhận (sai tên, sai thư mục, hoặc file rỗng/lớn hơn 2 MiB).

### Cần thấy gì

Thời gian trong file mẫu chạy từ **0:06 đến 0:52**, từ nào cũng có mốc riêng.
Khi phát:

1. Từ đã hát chuyển sang màu accent, từ chưa hát vẫn mờ — **đổi theo từng từ**,
   không phải đổi cả dòng một lần. Đây là điều duy nhất cần nhìn.
2. Dòng ở `0:20` — *"Dòng này không có word timing, nên chỉ tô cả dòng."* —
   phải tô **cả dòng** như trước, không bị tách từ.
3. Dòng ở `0:24` là tiếng Nhật, mỗi ký tự một mốc: chữ phải sáng dần theo ký tự
   và **không được sinh dấu cách** giữa các chữ.
4. Nút `−0.1s`/`+0.1s` trong thẻ Lyrics phải làm cả **dòng và từ** trượt theo
   cùng một lượng — nếu chỉ dòng trượt thì offset ở mức từ đang sai.

File mẫu ngắn hơn bài thật nên phần lời sẽ "hết" trước khi bài hết; đó là bình
thường, không phải lỗi. Muốn khớp nhạc thật thì sửa mốc trong file.

### Vì sao nội dung là chữ tự viết

Lời trong file mẫu do dự án tự viết cho mục đích kiểm thử, không trích từ bài
hát nào. Đừng dùng file mẫu làm lời cho bài hát thật khi phát hành hay chia sẻ —
nó chỉ để nhìn nhịp tô.

### Định dạng mong đợi

- **UTF-8** (có hay không có BOM đều được; UTF-16 có BOM cũng đọc được). File
  lưu bằng ANSI trên Windows sẽ làm hỏng dấu tiếng Việt.
- Mỗi dòng: `[mm:ss.xx]` rồi tới các cặp `<mm:ss.xx>chữ`.
- Phần chữ sau khi bỏ hết tag phải **giống hệt** chữ ghép từ các từ — nếu khác,
  dòng sẽ hiển thị khác nhau tuỳ theo việc app có hiểu tag hay không. Bất biến
  này có test gác: `tests/Mimi.Tests/KaraokeSampleFileTests.cs` đọc chính file
  mẫu này, nên file không thể lệch khỏi parser mà CI vẫn xanh.
- Không có cú pháp chú thích: dòng không nhận ra sẽ thành **một dòng lời**. Vì
  thế hướng dẫn nằm ở đây, không nằm trong file `.lrc`.
