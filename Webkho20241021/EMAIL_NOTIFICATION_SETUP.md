# Hướng dẫn cấu hình thông báo Email

## Tổng quan
Hệ thống đã được tích hợp chức năng gửi thông báo email tự động qua Gmail cho các sự kiện trong quy trình yêu cầu vật tư.

## Các điểm thông báo

1. **Nhân viên gửi yêu cầu** → Thông báo cho Trưởng phòng
2. **Trưởng phòng duyệt** → Thông báo cho:
   - Nhân viên (người yêu cầu)
   - Quản lý dự án (nếu yêu cầu có dự án)
   - Giám đốc (nếu yêu cầu không có dự án)
3. **Quản lý dự án duyệt** → Thông báo cho:
   - Nhân viên
   - Giám đốc
4. **Giám đốc duyệt** → Thông báo cho:
   - Nhân viên
   - Kho (nếu hàng có trong kho)
   - Bộ phận mua hàng (nếu hàng không có trong kho)
5. **Xuất kho** → Thông báo cho người yêu cầu
6. **Kế toán thanh toán** → Thông báo cho bộ phận mua hàng

## Cấu hình Email

### Bước 1: Cập nhật appsettings.json

Mở file `appsettings.json` và cập nhật thông tin email:

```json
"EmailSettings": {
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": "587",
  "FromEmail": "your-email@gmail.com",
  "FromPassword": "your-app-password",
  "FromName": "Hệ thống Quản lý Kho"
}
```

**Lưu ý quan trọng:**
- Đối với Gmail, bạn cần sử dụng **App Password** thay vì mật khẩu thông thường
- Để tạo App Password:
  1. Đăng nhập vào tài khoản Google
  2. Vào **Quản lý tài khoản Google** → **Bảo mật**
  3. Bật **Xác minh 2 bước** (nếu chưa bật)
  4. Tạo **Mật khẩu ứng dụng** cho "Mail"
  5. Sử dụng mật khẩu này trong `FromPassword`

### Bước 2: Cập nhật Database

Chạy script SQL để thêm cột Email vào bảng `nguoidungs`:

```sql
ALTER TABLE `nguoidungs` 
ADD COLUMN `Email` VARCHAR(255) NULL AFTER `Bophan`;
```

Hoặc chạy file: `add_email_column.sql`

### Bước 3: Cập nhật Email cho người dùng

Cập nhật email cho từng người dùng trong bảng `nguoidungs`:

```sql
UPDATE `nguoidungs` 
SET `Email` = 'email@example.com' 
WHERE `MaNguoidung` = 'MA_NGUOI_DUNG';
```

## Kiểm tra hoạt động

1. Tạo một yêu cầu vật tư mới từ tài khoản nhân viên
2. Kiểm tra email của Trưởng phòng có nhận được thông báo
3. Duyệt yêu cầu và kiểm tra các email tiếp theo

## Xử lý lỗi

Nếu email không được gửi, kiểm tra:

1. **Console logs**: Xem log trong console để biết lỗi cụ thể
2. **Cấu hình email**: Đảm bảo thông tin trong `appsettings.json` chính xác
3. **App Password**: Đảm bảo đang sử dụng App Password, không phải mật khẩu thông thường
4. **Firewall/Network**: Đảm bảo server có thể kết nối đến smtp.gmail.com:587
5. **Email người dùng**: Đảm bảo người dùng có email trong database

## Lưu ý

- Email được gửi bất đồng bộ (async) để không làm chậm quy trình xử lý
- Nếu email không được gửi, hệ thống vẫn tiếp tục hoạt động bình thường
- Email chỉ được gửi nếu người dùng có email trong database

