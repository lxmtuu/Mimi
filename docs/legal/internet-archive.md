# Internet Archive — đánh giá provider

Ngày rà soát: **03/11/2026**

## Kết luận

**HOÃN / KHÔNG cho catalog nhạc giải trí phổ thông.** Internet Archive có
giá trị lớn cho lưu trữ và nghiên cứu, nhưng điều khoản truy cập cùng mức
không đồng nhất của quyền từng item chưa tạo thành một cổng rõ ràng cho
Mimi hoạt động như một catalog streaming phổ thông.

## Căn cứ chính thức

- Terms of Use của Internet Archive ghi quyền truy cập Collections được
  cấp cho “scholarship and research purposes only”.
- Người dùng tự chịu rủi ro về luật áp dụng cho nội dung và cam kết việc
  dùng chỉ giới hạn ở noninfringing hoặc fair use theo luật bản quyền.
- Nếu một tài liệu có Creative Commons/giấy phép khác, người dùng chỉ có
  thể dựa vào tuyên bố đó “to the extent you trust the declaration and
  declarer”, và Internet Archive lưu ý declarer hiếm khi là chính họ.
- Internet Archive không bảo đảm nội dung chính xác, không vi phạm hoặc
  hợp pháp để truy cập ở mọi khu vực pháp lý.
- Metadata schema cho thấy `licenseurl` và `rights` là trường tùy chọn do
  uploader định nghĩa; `possible-copyright-status` cũng không phải một
  giấy phép machine-verifiable đồng nhất.

## Hệ quả đối với Mimi

- Không thể coi toàn bộ audio trên Internet Archive là “miễn phí để
  stream” chỉ vì file truy cập công khai.
- Một giao diện giải trí tìm kiếm/phát đại trà khó khớp giới hạn
  scholarship/research trong Terms of Use.
- Việc lọc `licenseurl` giúp giảm rủi ro nhưng không tự nó chứng minh
  uploader có quyền cấp giấy phép đó cho cả recording lẫn composition.
- Không có OAuth/catalog license thống nhất để Mimi dựa vào như một
  provider thương mại/creator platform.

## Điều kiện để mở lại

Chỉ cân nhắc một chế độ “Archive research” riêng, không gọi là catalog
giải trí, khi đồng thời:

- Có xác nhận rõ hơn từ Internet Archive về việc desktop client bên thứ ba
  trình bày/phát nội dung trong phạm vi dự kiến.
- Chỉ nhận item có `licenseurl` thuộc danh sách machine-recognizable rất
  hẹp, ví dụ CC0 hoặc Creative Commons license phù hợp cách dùng; loại mọi
  item thiếu giấy phép hoặc có `rights` mâu thuẫn.
- Hiển thị creator/uploader, item canonical URL, giấy phép và disclaimer
  rằng tuyên bố quyền do uploader cung cấp.
- Không tải hàng loạt, không vượt rate/access controls và có cơ chế loại
  item ngay khi metadata/quyền thay đổi.

Ngay cả khi các điều kiện trên đạt, tính năng vẫn cần được mô tả đúng mục
đích nghiên cứu/lưu trữ thay vì hứa hẹn thay thế Spotify.

## Nguồn

- Internet Archive Terms of Use: <https://archive.org/about/terms>
- Internet Archive Metadata Schema:
  <https://archive.org/services/docs/api/metadata-schema/>
- Internet Archive Developer APIs:
  <https://archive.org/services/docs/api/>
