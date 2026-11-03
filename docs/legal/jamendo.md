# Jamendo — đánh giá provider

Ngày rà soát: **03/11/2026**

## Kết luận

**HOÃN.** Không tích hợp Jamendo bằng Client ID của dự án trong bản Mimi
mã nguồn mở cho tới khi Jamendo xác nhận bằng văn bản rằng cách phân phối
credential và mô hình sử dụng dự kiến phù hợp Điều khoản API.

Catalog Creative Commons của Jamendo hấp dẫn về mặt nội dung, nhưng cổng
cứng hiện tại nằm ở credentials và giới hạn non-commercial, không phải ở kỹ
thuật API.

## Căn cứ chính thức

Jamendo API Terms of Use (bản hiển thị trên dev portal ghi lần sửa
17/09/2013) quy định:

- Mỗi ứng dụng phải đăng ký một Client ID duy nhất.
- Mọi credential, gồm login/password/API key, là “strictly personal” và
  không được chia sẻ hoặc tiết lộ cho bên thứ ba trong bất kỳ trường hợp
  nào.
- Nội dung qua API được phát hành dưới Creative Commons; developer phải
  dùng từng Content đúng giấy phép của nó.
- API được dùng miễn phí cho non-commercial use. Commercial use — gồm lợi
  thế thương mại, bồi hoàn tiền, affiliate hoặc quảng cáo — phải liên hệ
  licensing@jamendo.com.
- Ứng dụng không được thiết kế chuyên để cache nội dung hoặc cung cấp
  offline access; cache chỉ trong phạm vi cần thiết cho vận hành và phải
  phản ánh thay đổi nội dung sớm nhất có thể.
- Developer phải credit Jamendo Members là creator, credit Jamendo là
  provider và đặt direct backlink từ từng Content tới trang Content trên
  Jamendo.
- Request xác thực thay mặt Member phải đi qua Jamendo Connect OAuth2.
- Jamendo có thể chấm dứt quyền dùng API theo quyết định của họ; khi chấm
  dứt phải ngừng dùng API và gỡ Content/tham chiếu Jamendo.

## Vì sao chưa đạt cổng Mimi

1. Client ID đặt trong repo/binary desktop mã nguồn mở có thể được bất kỳ
   người dùng nào trích xuất. Theo cách đọc thận trọng, đó là tiết lộ một
   credential mà điều khoản yêu cầu giữ strictly personal.
2. Mimi hiện miễn phí/mã nguồn mở, nhưng “phi thương mại” có thể thay đổi
   nếu tương lai có quảng cáo, affiliate, bán bản đóng gói hoặc mô hình tài
   trợ gắn với tính năng. Hồ sơ này không tự kết luận donations thuần túy
   là commercial use; điểm chưa rõ đó cũng cần xác nhận.
3. Giấy phép từng track không đồng nhất. Client phải đọc và áp dụng
   `license_ccurl`/điều kiện Creative Commons theo bài, đặc biệt là NC.

## Điều kiện để mở lại

- Có xác nhận bằng văn bản từ Jamendo về một trong hai mô hình:
  - Client ID có thể được phân phối công khai trong OSS desktop client; hoặc
  - người dùng cuối tự đăng ký/nhập Client ID của chính họ cho Mimi mà
    không vi phạm yêu cầu “mỗi application một Client ID”.
- Xác nhận Mimi ở trạng thái phát hành dự kiến có được coi là
  non-commercial không.
- Prototype chỉ stream, không offline; cache tối thiểu theo vận hành.
- Mỗi item hiển thị creator, Jamendo và backlink trực tiếp; lưu license
  URL và chặn nội dung nếu giấy phép không cho cách dùng dự kiến.
- Có privacy disclosure cho dữ liệu gửi tới Jamendo và OAuth2 cho mọi hành
  động tài khoản.

## Nguồn

- Jamendo API Terms of Use:
  <https://devportal.jamendo.com/api_terms_of_use>
- Jamendo Developer Portal: <https://devportal.jamendo.com/>
