# Phase 6 closeout — Lyrics & cổng Online tại `main` `6afc8ca`

Tài liệu này tổng kết **Phase 6 (Lyrics & cổng Online, `v0.7.0`)** ở thời điểm
toàn bộ các lát của nó đã nằm trên `main`. Nó đối chiếu với bằng chứng đã ghi
trong [`ROADMAP.md`](../../ROADMAP.md) và với git history của repo, không thêm
kết luận mới nào không có căn cứ.

> `ROADMAP.md` vẫn là hồ sơ quyết định/trạng thái chính thức. Tài liệu này là
> ảnh chụp tổng kết. Chỗ nào hai bên lệch nhau thì `ROADMAP.md` đúng, và lượt
> rà soát này đã sửa lại những chỗ lệch (xem mục *Phát hiện*).

## Checkpoint

| Hạng mục | Giá trị |
|---|---|
| Nhánh | `main` |
| Commit | `6afc8ca` — merge PR #7 |
| Phiên bản app | `0.7.0` (`src/Mimi.App/Mimi.App.csproj`) — bump ngày 04/11/2026 |
| Settings schema | **11** (`AppSettings.CurrentVersion`) |
| Bộ khóa song ngữ | **455 / 455** (`UiStrings.resx` ↔ `UiStrings.vi.resx`) |
| Test | **293 khai báo** (270 `[Fact]`, 23 `[Theory]`) trên 50 file |
| CI gần nhất trên `main` | [`41792543326`](https://github.com/lxmtuu/Mimi/actions/runs/41792543326) — success |
| Cổng nghiệm thu Phase 6 | Đóng trên nhánh làm việc ngày 03/11/2026 (commit `903731b`), sau đó tích hợp vào `main` |

Chuỗi tích hợp: PR #1 `6434d84` → #2 `991ee04` → #3 `bb7616c` → #4 `094ffc3`
→ #5 `b99a683` → #6 `3e37c82` → #7 `6afc8ca`.

## Các lát của Phase 6

| Lát | Nội dung đã giao | Checkpoint chính | Bằng chứng | Runtime |
|---|---|---|---|---|
| Lyrics offline (LRC + lời nhúng) | Parser LRC/embedded ở Core, sidecar `.lrc` cùng tên rồi fallback `Tag.Lyrics`, thẻ Lyrics tự cuộn, đồng bộ theo timer 100 ms, delay ±0,1 s theo từng bài (schema v6) | `5379bc5`, `87b9f83`, `d4f6444` | `LyricsTextParser` tests, `FileLyricsServiceTests`, CI [`41614648545`](https://github.com/lxmtuu/Mimi/actions/runs/41614648545) (FLAC ghi/đọc thật bằng TagLib#) | **PASS** — chủ dự án xác nhận 03/11/2026, gồm bài chỉ có lời nhúng |
| Lời lớn trên Stage | Ba dòng quanh vị trí phát, chỉ cho lyrics có timestamp, có trạng thái intro | `faec801`, `01eb673` | Ảnh runtime | **PASS** — ảnh runtime trên Windows thật |
| Radio Browser (prototype) | API HTTPS + khám phá mirror/failover, lọc đài khoẻ/không HLS, tìm kiếm/top-vote ≤60, click-counter một lần cho mỗi lần bắt đầu đài mới, phát `url_resolved` bằng Media Foundation, tách khỏi Track/queue/history/session | `0eeff99`, `a1a5be5`, `0cc3309` | Test service/wiring/ViewModel | **PASS** cho prototype (03/11/2026) — **chưa phải provider production** |
| Kill switch Internet Radio | Schema v7, mục Settings song ngữ, tắt thì huỷ catalog/search, ẩn nav/content, chặn mở/phát, gỡ đài khỏi Now Playing mà không đụng local playback | `0eae18d` | Test round-trip/mặc định, wiring, hành vi khi đang phát | **PASS** — báo cáo Windows 03/11/2026 |
| Audius (online catalog) | Tìm và phát ngay trong engine Mimi, `LoadRemoteAudio` cho nguồn hữu hạn, lọc `is_streamable`/`is_stream_gated`/`is_available`/`allowed_api_keys`, không tải/cache, schema v8 | `479937d`, `daeb5f8`, `40c7bb0` | Test lọc dữ liệu, wiring, fake player | **PARTIAL** — đã chạy thật 03/11/2026 và sinh ra hai bản sửa (luồng kết thúc bài, giành lại thư viện local) chưa kiểm lại. Bản ghi từng mục: [`ONLINE-ACCEPTANCE.md`](ONLINE-ACCEPTANCE.md) |
| YouTube (tìm kiếm + player nhúng) | YouTube Data API v3 bằng key của người dùng (chỉ đặt từ terminal), IFrame player chính thức hiển thị trong khung Mimi, transport của Mimi điều khiển video, Stage video, danh sách đã lưu (schema v10) | `b1c5fdd`, `2d78c9b`, `7243dd8`, `14c91ba`, `4ee982c` | Test wiring/transport/no-storage guard | **PARTIAL** — đã dùng thật (kể cả xử lý Error 153 và bản sửa transport, danh sách đã lưu) nhưng chưa ghi lại từng mục. Bản ghi từng mục: [`ONLINE-ACCEPTANCE.md`](ONLINE-ACCEPTANCE.md) |
| Hồ sơ pháp lý provider | `docs/legal/` cho 8 nguồn + `docs/design/STREAMING-PROVIDERS.md` khoá hợp đồng `IStreamingProvider`, rights/attribution, cache theo phiên, DPAPI, log redaction, kill switch | `fb007fd` | Văn bản trong repo | **PASS** — đây là tiêu chí Exit của Phase 6 |

### Tiêu chí Exit của Phase 6

> bài có `.lrc` hát đúng nhịp ±100 ms sau offset; hồ sơ pháp lý kết luận rõ
> "làm/không làm" từng provider bằng văn bản.

| Tiêu chí | Kết luận |
|---|---|
| `.lrc` khớp nhịp ±100 ms sau offset | **PASS** — chủ dự án xác nhận trong lượt nghiệm thu Windows 03/11/2026 |
| Hồ sơ pháp lý kết luận từng provider | **PASS** — `docs/legal/README.md` + 8 hồ sơ, kết luận ĐẠT CÓ ĐIỀU KIỆN / ĐẠT theo mô hình tự host / HOÃN / KHÔNG |

## Cổng chất lượng xuyên suốt phase

- **Windows CI** (Release build + toàn bộ unit test) xanh ở mọi checkpoint được
  chốt làm bằng chứng.
- **Parity song ngữ** 455/455 do `LocalizationServiceTests` gác.
- **Chống hard-code**: `HardcodedUiStringTests` quét XAML và nhãn trong code.
- **Chống tái phát theme**: test không cho phép màu cứng trong `.xaml`.
- **Đường dữ liệu nhạy cảm**: key YouTube chỉ đọc từ env/file, có guard không
  lưu key vào settings/log/UI.

## Bổ sung sau khi đóng cổng (04/11/2026)

Hai lát dưới đây được làm **sau** mốc đóng cổng 03/11 và vào `main` qua PR #7.
Chúng không nằm trong bằng chứng nghiệm thu của Phase 6 ở trên.

| Lát | Nội dung | Trạng thái |
|---|---|---|
| Tô sáng **từng từ** (karaoke) | Enhanced LRC `<mm:ss.xx>` được đọc vào `LyricLine.WordTimings`; `KaraokeTextBlock` tô từng từ ở Now Playing và Stage; `FindCurrentWordIndex` dùng đúng phép offset của dòng | **Code + CI: PASS** (CI `41789451207`) · **Runtime: PASS (04/11/2026)** — chủ dự án xác nhận trên Windows thật |
| Hộp thoại lỗi tự khai báo | `CrashReport.Signature/Summary`; một lỗi chỉ mở một hộp thoại mỗi phiên; visualizer/scroll/đồng hồ phát nhạc không đẩy exception ra dispatcher | **Code + CI: PASS** (CI `41791349777`) · **Runtime: PASS (04/11/2026)** — hộp thoại đã tự khai báo đúng lỗi thật (`InvalidOperationException … FocusVisualStyle`) trên đúng thao tác gây lỗi, và sau bản sửa thì không còn hộp thoại nào ở thao tác đó |
| Vòng focus (`FocusVisualStyle`) | Phòng ngừa ở XAML (`Themes/FocusVisual.xaml` merge đầu tiên; 9 chỗ dùng đổi sang `DynamicResource`; `FocusVisualStyleTests`) **+** thu hồi lúc chạy (`FocusVisualRepair`: nhận dạng, ghi log phần tử đang focus, đặt giá trị cục bộ thật, không hiện hộp thoại; `FocusVisualRepairTests`) | **Code + test: PASS** · **Runtime: PASS (04/11/2026)** — chủ dự án chạy bản có bản sửa trên Windows thật: phát nhạc, F11 mở Stage, đóng Stage, **không còn hộp thoại**, và log **không có** dòng `Recovered from an empty focus ring`. Nghĩa là lớp phòng ngừa XAML đã đủ; lớp thu hồi lúc chạy là lưới đỡ, chưa phải kích hoạt. **Gốc rễ vẫn chưa xác định** — giả thuyết thứ tự `StaticResource` đã bị bác bỏ, và không có phần tử nào tự khai ra, nên đây là `PASS` về triệu chứng, **không** phải về nguyên nhân |

Để chạy lượt kiểm tra karaoke: file mẫu có timestamp từng từ ở
[`docs/examples/karaoke-sample.lrc`](../examples/karaoke-sample.lrc), cách dùng
ở [`docs/examples/README.md`](../examples/README.md). Bất biến "ghép từ phải ra
đúng dòng" của chính file mẫu được gác bằng test
`KaraokeSampleFileTests`, nên file không thể lệch khỏi parser mà CI vẫn xanh.

## Không thuộc Phase 6 / còn nợ sau khi đóng

- **Bump `v0.7.0`**: **đã làm** ngày 04/11/2026 (`Mimi.App.csproj` và
  `app.manifest`), được `ReleaseMetadataTests` gác để hai tệp không lệch nhau.
  Tag và release vẫn là quyết định của chủ dự án — xem mục *Quyết định cần
  chốt*.
- **Nợ miễn trừ Phase 4** giữ nguyên: P4-A `PARTIAL` (progress/seek trên thẻ
  Quick Settings), P4-E `BLOCKED` (máy Windows không còn render endpoint nào),
  P4-G `PARTIAL` + D-03 chưa chốt (đo độ trễ vật lý), provenance fixture là
  `Observation`.
- **Phase 0–3**: các cổng Exit dựa trên bằng chứng runtime vẫn chưa đủ, đúng
  như ghi chú từng phase.
- **Radio Browser production**: kill switch từ xa, privacy copy đầy đủ, timeout/
  reconnect sâu hơn và DNS rebinding vẫn là việc production/phát hành riêng.
- **Karaoke**: đã `PASS` runtime ngày 04/11/2026 theo xác nhận của chủ dự án.
- **YouTube/Audius**: nợ hồ sơ đã được chuyển thành một bản ghi từng mục —
  [`ONLINE-ACCEPTANCE.md`](ONLINE-ACCEPTANCE.md) — nhưng **các mục phần lớn vẫn
  `NOT RUN`**, vì viết ra danh sách kiểm tra không phải là đã kiểm tra. Hai mục
  Audius đã `PARTIAL` do có bản sửa sau lượt chạy 03/11.
- **Bộ cài và ký số**: thuộc Phase 7, xem
  [`docs/engineering/PACKAGING.md`](../engineering/PACKAGING.md).

## Phát hiện trong lượt rà soát này

| # | Phát hiện | Xử lý |
|---|---|---|
| 1 | Ghi chú schema trong `AppSettings` nói "Version 10 adds the optional custom background tint", nhưng git history cho thấy **v10 là danh sách YouTube đã lưu** (`4ee982c`) và **v11 mới là tint nền** (`aff717f`). Comment lệch một bậc. | Đã sửa comment (không đổi hành vi, không đổi schema) |
| 2 | Đoạn trạng thái Phase 6 trong `ROADMAP.md` vẫn nói các cổng "chờ tích hợp vào `main`", trong khi `main` đã có toàn bộ từ lâu. | Đã sửa lại theo sự thật hiện tại |
| 3 | Hai mục Audius và YouTube trong `ROADMAP.md` vẫn ghi "đã triển khai trên nhánh làm việc". | Đã sửa thành "đã vào `main`" nhưng **giữ ô chưa tick**, vì chưa có bản ghi nghiệm thu runtime |

## Quyết định cần chốt

| Mã | Việc | Vì sao chưa tự làm |
|---|---|---|
| — | Tag `v0.7.0` + Release cho Phase 6 | **Bump đã làm** (04/11/2026); tag và release vẫn là quyết định của chủ dự án. Quy trình: [`docs/engineering/PACKAGING.md`](../engineering/PACKAGING.md) |
| D-03 | Chốt WaveOut → WasapiOut | Cần phép đo độ trễ **vật lý**; loopback p99 ~64 ms không thay thế được |
| D-01 | LICENSE | Chưa có `LICENSE` trong repo; cần chủ dự án chọn (đề xuất MIT) |
| — | Mở Phase 7 | Phase 7 (`v1.0.0`) là installer, ký số, CHANGELOG/CONTRIBUTING, kiểm thử phát hành — cần chủ dự án mở |
