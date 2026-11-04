## Vì sao

<!-- Hành vi nào đang sai hoặc thiếu, thấy ở đâu. Nếu là lỗi runtime, kèm dòng
     `Details:` trên hộp thoại hoặc đoạn log liên quan. -->

## Đã làm gì

<!-- Và nếu có nhiều cách, vì sao chọn cách này. -->

## Chứng minh

- [ ] CI xanh (link run)
- [ ] Test mới đi cùng thay đổi, và test **fail trước** bản sửa
- [ ] Nếu là giao diện: ảnh trước/sau
- [ ] Nếu là lỗi runtime: log hoặc dòng `Details:` sau khi sửa

## Chưa làm

<!-- Phần cố ý để lại, để người review không phải tự đoán. -->

## Kiểm tra nhanh

- [ ] Không hard-code chuỗi hiển thị (cả `UiStrings.resx` và `UiStrings.vi.resx`)
- [ ] Không hard-code màu; tham chiếu theme đều là `DynamicResource`
- [ ] Package mới đã ghi vào `THIRD-PARTY-NOTICES.md`
- [ ] Thay đổi dữ liệu người dùng có migration + test round-trip
- [ ] Không đụng tới các hạng mục đã bị loại (trích xuất media, chặn quảng cáo, API không chính thức)
