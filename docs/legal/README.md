# Hồ sơ cổng Online / Streaming

Ngày rà soát: **03/11/2026**

Tài liệu này là hồ sơ nghiên cứu kỹ thuật phục vụ quyết định sản phẩm của
Mimi. Đây **không phải tư vấn pháp lý**. Điều khoản nền tảng có thể thay đổi;
trước khi phát hành bất kỳ provider nào phải kiểm tra lại nguồn chính thức và,
nếu còn điểm mơ hồ ảnh hưởng tới quyền phát nội dung, phải có xác nhận bằng
văn bản của nền tảng hoặc ý kiến pháp lý phù hợp.

## Cổng quyết định của Mimi

Một provider chỉ được chuyển sang bước prototype khi đồng thời đạt các điều
kiện sau:

1. Điều khoản hoặc giấy phép chính thức cho phép rõ ràng client desktop của
   bên thứ ba tìm kiếm và phát nội dung theo đúng mô hình dự kiến.
2. Không yêu cầu scrape, trích xuất stream trái cơ chế chính thức, vượt
   DRM/kiểm soát truy cập hoặc che giấu danh tính ứng dụng.
3. Không phải nhúng client secret, bearer token dùng chung hay thông tin
   đăng nhập của nhà phát triển vào bản phân phối. Token của người dùng phải
   thuộc về chính người dùng và được mã hóa bằng Windows DPAPI.
4. Có thể hiển thị attribution, liên kết canonical và thông tin giấy phép
   theo từng nội dung khi nền tảng hoặc giấy phép yêu cầu.
5. Quyền cache/tải xuống được tách bạch. Streaming không tự động đồng nghĩa
   được cache bền vững hoặc cho tải offline.
6. Có đường tắt toàn bộ provider và xóa dữ liệu/cache khi điều khoản thay
   đổi hoặc quyền truy cập bị chấm dứt.

## Bảng kết luận

| Provider | Mô hình | Kết luận tại cổng 8.5 | Điều kiện cốt lõi |
| --- | --- | --- | --- |
| [Radio Browser](radio-browser.md) | Thư mục đài Internet công cộng | **ĐẠT CÓ ĐIỀU KIỆN** | User-Agent mô tả rõ app, gọi click-counter khi bắt đầu phát, không ghi/phân phối lại stream, hiển thị nguồn đài |
| [Audius](audius.md) | Catalog on-demand qua API/OML | **ĐẠT CÓ ĐIỀU KIỆN** | Chỉ dùng API key công khai hoặc OAuth PKCE của người dùng; không nhúng secret/bearer; chỉ cache theo phiên; attribution/link theo OML; tôn trọng alternative license và opt-out |
| [Jamendo](jamendo.md) | Catalog Creative Commons | **HOÃN** | Cần Jamendo xác nhận bằng văn bản về Client ID trong desktop app mã nguồn mở và phạm vi non-commercial; không nhúng credential của dự án theo cách hiện tại |
| [Internet Archive](internet-archive.md) | Kho lưu trữ cộng đồng | **HOÃN / KHÔNG cho catalog giải trí phổ thông** | Điều khoản giới hạn scholarship/research và quyền từng item không đồng nhất; chỉ cân nhắc bộ lọc giấy phép rất hẹp sau khi có xác nhận rõ hơn |
| [Navidrome/OpenSubsonic và Jellyfin](self-hosted-servers.md) | Máy chủ nhạc do người dùng tự quản lý | **ĐẠT theo mô hình BYO server** | Người dùng tự cung cấp server/quyền nội dung; HTTPS được khuyến nghị; credential mã hóa DPAPI; không log token |
| [Spotify](rejected-platforms.md#spotify) | Catalog thương mại | **KHÔNG** | Không có đường chính thức phù hợp cho Mimi như native Windows player tích hợp nguồn khác nếu không có phê duyệt/Approved Device riêng |
| [YouTube Music (nhúng player chính thức)](youtube-music-player.md) | Cửa sổ nhúng trang web chính thức | **ĐÃ XEM XÉT — KHÔNG CHỌN** | Hợp lệ về điều khoản nhưng không đáp ứng yêu cầu phát trong engine Mimi (không seek/queue/visualizer/SMTC); đã dựng thử rồi gỡ |
| [YouTube](rejected-platforms.md#youtube) | Video/audiovisual | **KHÔNG** | Chính sách cấm tách/phát riêng audio, background player và offline/cache trái phép |

## Thứ tự prototype được khuyến nghị

1. **Radio Browser** nếu mục tiêu là có nội dung nghe ngay với rủi ro tích
   hợp thấp nhất. Đây là radio tuyến tính, không phải catalog chọn bài.
   Chủ dự án đã chọn hướng này và prototype đã có trên nhánh làm việc;
   prototype đã được chủ dự án nghiệm thu runtime trên Windows thật
   ngày 03/11/2026 nhưng chưa phải phê duyệt production.
2. **Navidrome/OpenSubsonic hoặc Jellyfin** nếu mục tiêu là nghe thư viện
   của chính người dùng từ xa. Đây là hướng an toàn nhất về quyền nội dung
   nhưng không tạo thêm catalog công cộng.
3. **Audius** nếu mục tiêu là catalog on-demand. Bắt buộc thiết kế
   streaming-only, session-only cache và attribution theo từng track.
4. Jamendo/Internet Archive chỉ mở lại khi có xác nhận bổ sung. Spotify và
   YouTube vẫn không nằm trong lộ trình phát full-track trong Mimi.
5. **Online catalog (Audius)** là hướng đã chọn cho nhu cầu "tìm bài và nghe
   ngay trong Mimi": tìm và stream trực tiếp trong app, không credential, không
   tải/cache, lọc quyền trước khi hiển thị. Thiết kế ở
   [`../design/ONLINE-CATALOG.md`](../design/ONLINE-CATALOG.md).

## Phạm vi chưa đánh giá

- Nhà cung cấp lyrics trực tuyến. Việc tải lyrics từ dịch vụ bên thứ ba cần
  hồ sơ quyền riêng và không được suy ra từ quyền stream audio.
- SoundCloud, Deezer, Apple Music, Tidal và các catalog thương mại khác.
- Việc bán app, quảng cáo, affiliate hoặc nhận tài trợ có điều kiện. Các
  thay đổi mô hình thương mại có thể làm một kết luận “đạt có điều kiện”
  không còn hiệu lực.

Thiết kế kỹ thuật tương ứng nằm tại
[`docs/design/STREAMING-PROVIDERS.md`](../design/STREAMING-PROVIDERS.md).
