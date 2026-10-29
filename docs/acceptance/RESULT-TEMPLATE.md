# Mimi Windows Acceptance Result

> Sao chép toàn bộ file này cho mỗi đợt kiểm tra. Giữ bản đã điền và dữ liệu
> thô ngoài repo nếu chúng chứa đường dẫn, tên thư viện hoặc thông tin máy
> cá nhân. Chỉ đưa kết luận đã tổng hợp vào `ROADMAP.md`.

## 1. Môi trường

| Hạng mục | Giá trị |
|---|---|
| Ngày/giờ bắt đầu |  |
| Người kiểm tra |  |
| Nhánh |  |
| Commit (`git rev-parse --short HEAD`) |  |
| `git status --short` |  |
| Cấu hình build | Release / Debug |
| Windows edition/version/build |  |
| Máy/CPU/GPU (nếu liên quan) |  |
| Độ phân giải/scaling (nếu kiểm tra visualizer) |  |
| Endpoint mặc định ban đầu |  |
| Thiết bị âm thanh thứ hai |  |
| File/playlist kiểm tra chính |  |
| Vị trí backup dữ liệu (không cần ghi nếu chia sẻ công khai) |  |

## 2. S0 — Cổng tự động

| Hạng mục | Kết quả |
|---|---|
| `dotnet restore` | PASS / FAIL |
| Release build | PASS / FAIL |
| Warning |  |
| Error |  |
| Unit tests passed |  |
| Unit tests failed |  |
| Unit tests skipped |  |
| Exe đã chạy |  |

## 3. Tóm tắt theo cổng

Điền một trạng thái cho mỗi cổng: `PASS`, `PARTIAL`, `FAIL`, `BLOCKED`,
`NOT RUN` hoặc `N/A`.

| Cổng | Trạng thái | Bằng chứng/ghi chú ngắn |
|---|---|---|
| P4-A — SMTC/flyout |  |  |
| P4-B — Taskbar |  |  |
| P4-C — Media keys/keyboard |  |  |
| P4-D — Mất thiết bị vật lý |  |  |
| P4-E — Không có output |  |  |
| P4-F — Kết nối lại |  |  |
| P4-G — A/B D-03 |  |  |
| P4-H — Chốt hồ sơ/quyết định |  |  |
| S4-A — Định dạng âm thanh |  |  |
| S4-B — Visualizer |  |  |
| S4-C — Quy mô/Explorer/watcher |  |  |
| S4-D — Upgrade/migration/termination |  |  |
| S4-E — Clean-clone contributor build |  |  |

## 4. S1 — Chi tiết tương tác

### P4-A SMTC/flyout

| ID | Kết quả | Ghi chú |
|---|---|---|
| A1 Metadata |  |  |
| A2 Ảnh bìa |  |  |
| A3 Đồng bộ trạng thái |  |  |
| A4 Play/Pause |  |  |
| A5 Next |  |  |
| A6 Previous/history |  |  |
| A7 Timeline |  |  |
| A8 Seek tuyệt đối |  |  |
| A9 FastForward/Rewind |  |  |
| A10 Shuffle hai chiều |  |  |
| A11 Repeat hai chiều |  |  |
| A12 Kết thúc bài/queue |  |  |

#### Chẩn đoán GSMTC cho A7 (nếu đã chạy probe chỉ-đọc)

Probe chỉ xác minh dữ liệu Windows nhận được; nó không thay thế quan sát
thanh tiến trình/thời gian và thao tác trực tiếp trên Quick Settings.

| Hạng mục | Giá trị |
|---|---|
| File JSON ngoài repo/ignored |  |
| Bài và trạng thái ở mẫu đầu |  |
| Position / Duration / Remaining ở mẫu đầu |  |
| Bài, trạng thái và Position ở mẫu cuối |  |
| `TimelinePublished` / `StartedNearEnd` |  |
| `RawPositionDeltaSeconds` / `LastUpdatedDeltaSeconds` |  |
| `IsPlaybackPositionEnabled` / FastForward / Rewind |  |
| Quick Settings có hiện progress/thời gian không |  |
| Các nút nhìn thấy trên thẻ; trạng thái Play/Pause lúc chụp |  |
| Nhãn nguồn app trên thẻ (ví dụ `Mimi`, `Mimi.exe`, `Unknown app`) |  |
| Kết luận tách riêng: xuất bản GSMTC và hiển thị trên thẻ |  |

### P4-B Taskbar

| ID | Kết quả | Ghi chú |
|---|---|---|
| B1 Nút theo trạng thái |  |  |
| B2 Play/Pause thumbnail |  |  |
| B3 Previous/Next thumbnail |  |  |
| B4 Progress |  |  |
| B5 Description |  |  |
| B6 Thu nhỏ/phục hồi |  |  |

### P4-C Media keys và bàn phím

| ID/Bối cảnh | Kết quả | Ghi chú |
|---|---|---|
| C1 Play/Pause vật lý |  |  |
| C2 Play/Pause riêng nếu có |  |  |
| C3 Next/Previous vật lý |  |  |
| C4 Không focus/minimized |  |  |
| C5 Không double-action |  |  |
| C6 Stop nếu có |  |  |
| C7 Ma trận focus |  |  |
| Màn hình khóa (nếu thử) |  |  |

## 5. S2 — Chi tiết thiết bị

Mỗi kịch bản dùng một dòng. Ghi vị trí theo định dạng `m:ss`.

| Kịch bản | Endpoint trước → sau | Bài/vị trí trước | Bài/vị trí sau | Tự phát lại? | Corrupt/Missing mới? | Nhãn/banner | Kết quả |
|---|---|---|---|---|---|---|---|
| Đổi default khi đang phát |  |  |  |  |  |  |  |
| Rút USB/ngắt Bluetooth |  |  |  |  |  |  |  |
| Disable endpoint |  |  |  |  |  |  |  |
| Không còn output |  |  |  |  |  |  |  |
| Kết nối lại endpoint cũ |  |  |  |  |  |  |  |
| Kết nối endpoint mới |  |  |  |  |  |  |  |
| Đổi thiết bị khi Pause |  |  |  |  |  |  |  |

Ghi chú thêm về crash/hộp thoại/thời gian phản ứng:

```text

```

## 6. S3 — A/B WASAPI ↔ WaveOut

### Điều kiện đo

| Hạng mục | Giá trị/phương pháp |
|---|---|
| Công cụ loopback |  |
| Định dạng thu |  |
| File/đoạn tín hiệu |  |
| Vị trí bắt đầu mỗi lượt |  |
| CSV bắt đầu lúc nào; pre-roll/trigger offset trên timeline |  |
| Endpoint |  |
| Windows volume / app volume / mute |  |
| Sample rate/enhancements |  |
| Ngưỡng tín hiệu dùng khi phân tích |  |
| Thư mục chứa CSV gốc ngoài repo |  |

### Từng lượt chạy

| Lượt | Chế độ ép | Backend log xác nhận | Thời lượng | Buffer sau tín hiệu đầu | % có tín hiệu | First-signal từ CSV start / sau trigger (ms) | Gap p50/p95/p99/max (ms) | Gap >100ms | Chuỗi im lặng dài nhất | Glitch nghe được |
|---:|---|---|---:|---:|---:|---:|---|---:|---|---|
| 1 |  |  |  |  |  |  |  |  |  |  |
| 2 |  |  |  |  |  |  |  |  |  |  |
| 3 |  |  |  |  |  |  |  |  |  |  |
| 4 |  |  |  |  |  |  |  |  |  |  |
| 5 |  |  |  |  |  |  |  |  |  |  |
| 6 |  |  |  |  |  |  |  |  |  |  |

Nhận xét về bất thường và mốc thời gian đối chiếu log:

```text

```

Kết luận D-03 đề xuất:

```text
Giữ WASAPI mặc định / cần đo thêm / mở lại quyết định, vì:

```

## 7. S4 — Nợ nghiệm thu cũ

### Định dạng

| Định dạng | Win10 | Win11 | Scan/metadata | Play/seek | Ghi chú codec/container |
|---|---|---|---|---|---|
| `.mp3` |  |  |  |  |  |
| `.wav` |  |  |  |  |  |
| `.aiff/.aif` |  |  |  |  |  |
| `.wma` |  |  |  |  |  |
| `.m4a` |  |  |  |  |  |
| `.aac` |  |  |  |  |  |
| `.flac` |  |  |  |  |  |

### Visualizer

| Hạng mục | Kết quả/số đo |
|---|---|
| Độ phân giải/scaling/GPU |  |
| FPS/draw time từng mode |  |
| CPU tiến trình quan sát |  |
| Track nhỏ/lớn |  |
| Giảm volume |  |
| Pause decay |  |
| Unfocused/Battery Saver |  |
| Stage F11/Esc |  |
| Profiler CPU/allocation nếu có |  |

### Thư viện/dữ liệu/contributor

| Hạng mục | Kết quả/bằng chứng |
|---|---|
| Số bài khi test quy mô |  |
| Thời gian quét/quét lại |  |
| Tìm kiếm và cách đo |  |
| Cuộn/sort/filter/multi-select |  |
| Drag-drop Explorer |  |
| Open With khi app đóng/mở |  |
| Watched folders |  |
| Upgrade/migration fixture |  |
| Tắt giữa lúc ghi trên profile test |  |
| Clean clone và thời gian build/run |  |

## 8. Vấn đề phát hiện

Lặp lại khối này cho mỗi vấn đề:

### ISSUE-001 — Tên ngắn

- **Cổng/hạng mục:**
- **Mức độ:** Blocker / Major / Minor / Observation
- **Tiền điều kiện:**
- **Các bước tái hiện:**
  1.
  2.
  3.
- **Kỳ vọng:**
- **Thực tế:**
- **Số lần tái hiện:**
- **Mốc thời gian trong log:**
- **Endpoint/thiết bị liên quan:**
- **Ảnh/video/log đã giữ ngoài repo:**
- **Workaround nếu có:**

## 9. Nhật ký phiên

| Thời gian | Phiên/kịch bản | Sự kiện hoặc quyết định dừng |
|---|---|---|
|  |  |  |

## 10. Kết luận của người kiểm tra

```text
Cổng nào có thể đóng:
Cổng nào còn PARTIAL/BLOCKED và vì sao:
Có lỗi Blocker/Major nào không:
Có đề xuất merge baseline/chốt D-03/đóng Phase 4 không:
Việc tiếp theo quan trọng nhất:
```
