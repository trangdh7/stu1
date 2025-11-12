# Tài Liệu Hệ Thống Quản Lý Kho

## 1. Tổng Quan Hệ Thống

Hệ thống Quản Lý Kho là một ứng dụng web quản lý toàn diện các hoạt động kho bãi, từ yêu cầu vật tư, mua hàng, nhập/xuất kho đến quản lý tồn kho và dự án. Hệ thống được xây dựng trên nền tảng ASP.NET Core với kiến trúc MVC và sử dụng MySQL làm cơ sở dữ liệu.

### 1.1. Phạm Vi và Khả Năng

**Truy cập công khai:**
- Xem tồn kho và tìm kiếm vật tư
- Xem thông tin dự án và vật tư dự án
- Quản lý thông tin cá nhân

**Không gian làm việc theo vai trò:**

**Nhân viên kỹ thuật:**
- Tạo và theo dõi yêu cầu vật tư
- Xem kho cá nhân và kho dự án
- Xác nhận nhận hàng
- Quản lý dự án (nếu là Quản lý dự án)

**Trưởng BP Kỹ thuật:**
- Duyệt yêu cầu vật tư từ nhân viên
- Quản lý dự án và vật tư dự án
- Xem báo cáo tồn kho

**Trưởng BP Kho:**
- Quản lý tồn kho tổng
- Xử lý phiếu nhập kho và phiếu xuất kho
- Chuẩn bị và xác nhận xuất kho
- Quản lý kho dự án và kho người dùng

**Trưởng BP Mua hàng:**
- Quản lý phiếu mua hàng
- Báo giá vật tư
- Theo dõi trạng thái mua hàng
- Xác nhận nhận hàng từ nhà cung cấp

**Trưởng BP Kế toán:**
- Duyệt thanh toán phiếu mua hàng
- Theo dõi chi phí mua hàng
- Quản lý tài chính liên quan đến kho

**Giám đốc:**
- Duyệt tất cả yêu cầu quan trọng
- Xem tổng quan toàn bộ hệ thống
- Quản lý người dùng và phân quyền
- Xem báo cáo và thống kê

**Tính năng chung:**
- Xác thực và phân quyền
- Thông báo real-time
- Quy trình CRUD có cấu trúc
- Khả năng kiểm tra và audit
- Quản lý bảo hành
- Nhập/xuất dữ liệu Excel

## 2. Vai Trò Người Dùng và Không Gian Làm Việc

### 2.1. Nhân Viên Kỹ Thuật

**Chức năng chính:**
- Tạo yêu cầu vật tư cho dự án hoặc cá nhân
- Xem trạng thái yêu cầu của mình
- Xem tồn kho (Tổng kho, Kho dự án, Kho cá nhân)
- Xác nhận nhận hàng sau khi kho chuẩn bị xong
- Quản lý dự án (nếu được phân công làm Quản lý dự án)
- Xem thông tin cá nhân và kho cá nhân

**Quyền hạn:**
- Chỉ có thể tạo và xem yêu cầu của chính mình
- Không thể duyệt yêu cầu
- Không thể chỉnh sửa tồn kho trực tiếp

### 2.2. Trưởng BP Kỹ Thuật

**Chức năng chính:**
- Duyệt/từ chối yêu cầu vật tư từ nhân viên trong bộ phận
- Chuyển yêu cầu lên Quản lý dự án hoặc Giám đốc nếu cần
- Quản lý dự án (tạo, chỉnh sửa, xem dự án)
- Xem báo cáo tồn kho và vật tư dự án
- Xem thông tin cá nhân

**Quyền hạn:**
- Duyệt yêu cầu trong phạm vi bộ phận
- Quản lý dự án được phân công
- Không thể chỉnh sửa tồn kho trực tiếp

### 2.3. Trưởng BP Kho

**Chức năng chính:**
- Quản lý tồn kho tổng (thêm, sửa, xóa vật tư)
- Xử lý phiếu nhập kho (kiểm tra, xác nhận, cập nhật tồn kho)
- Xử lý phiếu xuất kho (kiểm tra tồn kho, chuẩn bị hàng, xác nhận xuất kho)
- Quản lý kho dự án (vật tư dự án)
- Quản lý kho người dùng (vật tư cá nhân)
- Tìm kiếm và tra cứu vật tư
- Nhập/xuất dữ liệu Excel
- Xem thông tin cá nhân

**Quyền hạn:**
- Toàn quyền quản lý tồn kho
- Xử lý tất cả phiếu nhập/xuất kho
- Không thể duyệt yêu cầu (trừ yêu cầu nhập kho)

### 2.4. Trưởng BP Mua Hàng

**Chức năng chính:**
- Quản lý phiếu mua hàng
- Báo giá vật tư (nhập đơn giá, thành tiền)
- Theo dõi trạng thái mua hàng (Đang chờ báo giá, Đã báo giá, Chờ thanh toán, Đã thanh toán, Đã nhận hàng)
- Xác nhận nhận hàng từ nhà cung cấp
- Tự động tạo phiếu nhập kho sau khi nhận hàng
- Xem tồn kho để tham khảo
- Xem thông tin cá nhân

**Quyền hạn:**
- Quản lý toàn bộ quy trình mua hàng
- Báo giá và cập nhật thông tin mua hàng
- Tạo phiếu nhập kho tự động

### 2.5. Trưởng BP Kế Toán

**Chức năng chính:**
- Duyệt thanh toán phiếu mua hàng
- Xem chi tiết phiếu mua hàng (đơn giá, thành tiền)
- Theo dõi trạng thái thanh toán
- Xem báo cáo tài chính liên quan đến mua hàng
- Xem thông tin cá nhân

**Quyền hạn:**
- Duyệt thanh toán phiếu mua hàng
- Xem thông tin tài chính
- Không thể chỉnh sửa tồn kho

### 2.6. Giám Đốc

**Chức năng chính:**
- Duyệt/từ chối tất cả yêu cầu quan trọng
- Xem tổng quan toàn bộ hệ thống
- Quản lý người dùng (tạo, chỉnh sửa, xóa)
- Xem tất cả yêu cầu, phiếu nhập kho, phiếu xuất kho, phiếu mua hàng
- Duyệt phiếu mua hàng có giá trị lớn
- Xem báo cáo và thống kê
- Quản lý dự án
- Xem thông tin cá nhân

**Quyền hạn:**
- Toàn quyền trong hệ thống
- Duyệt tất cả yêu cầu quan trọng
- Quản lý người dùng và phân quyền

## 3. Các Module Chính

### 3.1. Quản Lý Yêu Cầu (Yêu Cầu Vật Tư)

**Mô tả:**
Module quản lý các yêu cầu vật tư từ người dùng, từ lúc tạo yêu cầu đến khi được duyệt và xử lý.

**Tính năng:**
- Tạo yêu cầu vật tư (cho dự án hoặc cá nhân)
- Thêm danh sách vật tư vào yêu cầu (tên sản phẩm, mã sản phẩm, số lượng, đơn vị, hãng sản xuất, nhà cung cấp)
- Chọn vật tư từ tồn kho hoặc thêm vật tư mới
- Theo dõi trạng thái yêu cầu (Trưởng BP, Quản lý dự án, Giám đốc, Đã duyệt, Đang mua hàng, Đã từ chối)
- Duyệt/từ chối yêu cầu (theo vai trò)
- Tự động tạo phiếu xuất kho hoặc phiếu mua hàng sau khi duyệt

**Workflow:**
1. Nhân viên tạo yêu cầu
2. Trưởng BP duyệt → Quản lý dự án (nếu có dự án) → Giám đốc
3. Sau khi duyệt, hệ thống tự động:
   - Kiểm tra tồn kho
   - Tạo phiếu xuất kho (nếu đủ hàng)
   - Tạo phiếu mua hàng (nếu thiếu hàng hoặc không có hàng)
   - Tạo cả hai (nếu một phần đủ hàng, một phần thiếu)

### 3.2. Quản Lý Phiếu Xuất Kho

**Mô tả:**
Module quản lý quy trình xuất kho từ lúc tạo phiếu đến khi hoàn thành và cập nhật tồn kho.

**Tính năng:**
- Tự động tạo phiếu xuất kho từ yêu cầu đã duyệt
- Xem danh sách phiếu xuất kho
- Kiểm tra tồn kho trước khi xuất
- Chuẩn bị hàng (cập nhật trạng thái "Đang chuẩn bị hàng")
- Thông báo người yêu cầu khi hàng sẵn sàng
- Xác nhận nhận hàng (người yêu cầu xác nhận)
- Cập nhật tồn kho sau khi xác nhận nhận hàng
- Phân loại vật tư (vật tư dự án → kho dự án, vật tư cá nhân → kho người dùng)
- Tự động tạo phiếu mua hàng nếu thiếu hàng

**Trạng thái phiếu xuất kho:**
- "Chờ xác nhận": Phiếu mới được tạo, chờ kho xử lý
- "Đang chuẩn bị hàng": Kho đang chuẩn bị vật tư
- "Chờ người yêu cầu xác nhận": Hàng đã sẵn sàng, chờ người yêu cầu xác nhận
- "Đã xác nhận nhận hàng": Người yêu cầu đã xác nhận nhận hàng
- "Hoàn thành": Đã xuất kho và cập nhật tồn kho
- "Thiếu hàng - Đã tạo phiếu mua": Không đủ hàng, đã tạo phiếu mua hàng

**Workflow:**
1. Tự động tạo phiếu xuất kho từ yêu cầu đã duyệt
2. Bộ phận kho kiểm tra tồn kho
3. Nếu đủ hàng: Chuẩn bị hàng → Thông báo người yêu cầu
4. Người yêu cầu xác nhận nhận hàng
5. Cập nhật tồn kho (trừ kho tổng, cộng kho dự án/kho người dùng)
6. Hoàn thành phiếu xuất kho

### 3.3. Quản Lý Phiếu Nhập Kho

**Mô tả:**
Module quản lý quy trình nhập kho từ lúc tạo phiếu đến khi hoàn thành và cập nhật tồn kho.

**Tính năng:**
- Tạo phiếu nhập kho thủ công hoặc tự động từ phiếu mua hàng
- Xem danh sách phiếu nhập kho
- Duyệt phiếu nhập kho (Quản lý dự án → Giám đốc → Kho)
- Kiểm tra và xác nhận nhập kho
- Cập nhật tồn kho sau khi nhập
- Quản lý thông tin vật tư (ngày nhập kho, ngày bảo hành, thời gian bảo hành)
- Tự động tạo vật tư mới nếu chưa có trong kho

**Trạng thái phiếu nhập kho:**
- "Quản lý dự án": Chờ Quản lý dự án duyệt (nếu có dự án)
- "Giám đốc": Chờ Giám đốc duyệt
- "Chờ nhập kho": Đã được duyệt, chờ kho xử lý
- "Sẵn sàng nhập kho": Sẵn sàng để nhập kho
- "Đã nhập kho": Đã nhập kho và cập nhật tồn kho
- "Đã từ chối": Đã bị từ chối

**Workflow:**
1. Tạo phiếu nhập kho (thủ công hoặc tự động từ phiếu mua hàng)
2. Quản lý dự án duyệt (nếu có dự án)
3. Giám đốc duyệt
4. Bộ phận kho xử lý nhập kho
5. Cập nhật tồn kho (cộng vào kho tổng)
6. Hoàn thành phiếu nhập kho

### 3.4. Quản Lý Phiếu Mua Hàng

**Mô tả:**
Module quản lý quy trình mua hàng từ lúc tạo phiếu đến khi nhận hàng và tạo phiếu nhập kho.

**Tính năng:**
- Tự động tạo phiếu mua hàng từ yêu cầu (khi thiếu hàng)
- Xem danh sách phiếu mua hàng
- Báo giá vật tư (nhập đơn giá, tính thành tiền)
- Duyệt phiếu mua hàng (Giám đốc → Kế toán → Mua hàng)
- Theo dõi trạng thái thanh toán
- Xác nhận nhận hàng từ nhà cung cấp
- Tự động tạo phiếu nhập kho sau khi nhận hàng
- Quản lý thông tin mua hàng (nhà cung cấp, đơn giá, thành tiền)

**Trạng thái phiếu mua hàng:**
- "Đang chờ báo giá": Chờ bộ phận mua hàng báo giá
- "Đã báo giá": Đã báo giá, chờ Giám đốc duyệt
- "Chờ thanh toán": Đã được duyệt, chờ kế toán thanh toán
- "Đã thanh toán": Đã thanh toán, chờ mua hàng
- "Đã nhận hàng": Đã nhận hàng, tự động tạo phiếu nhập kho
- "Đã từ chối": Đã bị từ chối

**Workflow:**
1. Tự động tạo phiếu mua hàng từ yêu cầu (khi thiếu hàng)
2. Bộ phận mua hàng báo giá
3. Giám đốc duyệt
4. Kế toán thanh toán
5. Bộ phận mua hàng nhận hàng
6. Tự động tạo phiếu nhập kho
7. Hoàn thành phiếu mua hàng

### 3.5. Quản Lý Tồn Kho

**Mô tả:**
Module quản lý tồn kho tổng, kho dự án và kho người dùng.

**Tính năng:**
- Quản lý tồn kho tổng (thêm, sửa, xóa vật tư)
- Quản lý kho dự án (vật tư dự án)
- Quản lý kho người dùng (vật tư cá nhân)
- Tìm kiếm vật tư (theo tên, mã sản phẩm, hãng sản xuất, nhà cung cấp)
- Xem chi tiết vật tư (số lượng, đơn vị, ngày nhập kho, ngày bảo hành, thời gian bảo hành)
- Nhập/xuất dữ liệu Excel
- Theo dõi tồn kho real-time
- Cảnh báo khi tồn kho thấp

**Các loại kho:**
- **Tổng kho (khotongs):** Kho chính chứa tất cả vật tư
- **Kho dự án (khoduans):** Vật tư được cấp phát cho dự án
- **Kho người dùng (khonguoidungs):** Vật tư được cấp phát cho cá nhân

### 3.6. Quản Lý Dự Án

**Mô tả:**
Module quản lý thông tin dự án và vật tư dự án.

**Tính năng:**
- Tạo, chỉnh sửa, xóa dự án
- Gán Quản lý dự án
- Xem danh sách dự án
- Xem vật tư của dự án
- Theo dõi trạng thái dự án
- Quản lý thông tin dự án (tên dự án, khách hàng, ngày bắt đầu, ngày kết thúc)

**Quyền hạn:**
- Giám đốc: Toàn quyền quản lý dự án
- Trưởng BP Kỹ thuật: Quản lý dự án được phân công
- Quản lý dự án: Quản lý dự án được gán

### 3.7. Quản Lý Thông Tin Cá Nhân

**Mô tả:**
Module quản lý thông tin cá nhân và kho cá nhân.

**Tính năng:**
- Xem thông tin cá nhân (tên, mã nhân viên, chức vụ, bộ phận)
- Chỉnh sửa thông tin cá nhân
- Xem kho cá nhân (vật tư đã nhận)
- Theo dõi vật tư cá nhân (số lượng, trạng thái, ngày nhận)

## 4. Quy Trình Nghiệp Vụ

### 4.1. Quy Trình Yêu Cầu Vật Tư

1. **Tạo yêu cầu:**
   - Nhân viên đăng nhập hệ thống
   - Chọn "Tạo yêu cầu"
   - Nhập thông tin yêu cầu (tên yêu cầu, dự án nếu có)
   - Thêm danh sách vật tư (chọn từ tồn kho hoặc thêm mới)
   - Gửi yêu cầu

2. **Duyệt yêu cầu:**
   - Trưởng BP Kỹ thuật xem và duyệt yêu cầu
   - Nếu có dự án: Chuyển cho Quản lý dự án duyệt
   - Quản lý dự án duyệt → Chuyển cho Giám đốc
   - Giám đốc duyệt → Yêu cầu được duyệt

3. **Xử lý yêu cầu:**
   - Hệ thống tự động kiểm tra tồn kho
   - Tạo phiếu xuất kho (nếu đủ hàng)
   - Tạo phiếu mua hàng (nếu thiếu hàng)
   - Tạo cả hai (nếu một phần đủ, một phần thiếu)

### 4.2. Quy Trình Xuất Kho

1. **Tạo phiếu xuất kho:**
   - Tự động tạo từ yêu cầu đã duyệt
   - Hoặc tạo thủ công từ phiếu nhập kho

2. **Kiểm tra tồn kho:**
   - Bộ phận kho kiểm tra tồn kho
   - Nếu đủ hàng: Chuẩn bị hàng
   - Nếu thiếu hàng: Tự động tạo phiếu mua hàng

3. **Chuẩn bị hàng:**
   - Bộ phận kho chuẩn bị vật tư
   - Cập nhật trạng thái "Đang chuẩn bị hàng"
   - Thông báo người yêu cầu

4. **Xác nhận nhận hàng:**
   - Người yêu cầu xác nhận nhận hàng
   - Cập nhật trạng thái "Đã xác nhận nhận hàng"

5. **Cập nhật tồn kho:**
   - Trừ kho tổng
   - Cộng kho dự án (nếu có dự án)
   - Cộng kho người dùng (nếu không có dự án)
   - Hoàn thành phiếu xuất kho

### 4.3. Quy Trình Nhập Kho

1. **Tạo phiếu nhập kho:**
   - Tự động tạo từ phiếu mua hàng
   - Hoặc tạo thủ công

2. **Duyệt phiếu nhập kho:**
   - Quản lý dự án duyệt (nếu có dự án)
   - Giám đốc duyệt
   - Chuyển cho bộ phận kho

3. **Nhập kho:**
   - Bộ phận kho kiểm tra vật tư
   - Xác nhận nhập kho
   - Cập nhật tồn kho (cộng vào kho tổng)
   - Hoàn thành phiếu nhập kho

### 4.4. Quy Trình Mua Hàng

1. **Tạo phiếu mua hàng:**
   - Tự động tạo từ yêu cầu (khi thiếu hàng)
   - Hoặc tạo thủ công

2. **Báo giá:**
   - Bộ phận mua hàng báo giá
   - Nhập đơn giá, tính thành tiền
   - Cập nhật trạng thái "Đã báo giá"

3. **Duyệt mua hàng:**
   - Giám đốc duyệt phiếu mua hàng
   - Cập nhật trạng thái "Chờ thanh toán"

4. **Thanh toán:**
   - Kế toán thanh toán
   - Cập nhật trạng thái "Đã thanh toán"

5. **Nhận hàng:**
   - Bộ phận mua hàng nhận hàng từ nhà cung cấp
   - Xác nhận nhận hàng
   - Tự động tạo phiếu nhập kho
   - Hoàn thành phiếu mua hàng

## 5. Tính Năng Bổ Sung

### 5.1. Thông Báo

- Thông báo real-time cho người dùng
- Thông báo yêu cầu cần duyệt
- Thông báo phiếu mua hàng cần báo giá
- Thông báo phiếu mua hàng cần thanh toán
- Thông báo phiếu nhập/xuất kho cần xử lý
- Thông báo hàng đã sẵn sàng để nhận

### 5.2. Tìm Kiếm

- Tìm kiếm vật tư (theo tên, mã sản phẩm, hãng sản xuất, nhà cung cấp)
- Tìm kiếm yêu cầu
- Tìm kiếm phiếu nhập/xuất kho
- Tìm kiếm phiếu mua hàng
- Tìm kiếm dự án

### 5.3. Báo Cáo và Thống Kê

- Báo cáo tồn kho
- Báo cáo xuất nhập kho
- Báo cáo mua hàng
- Thống kê vật tư theo dự án
- Thống kê vật tư theo người dùng

### 5.4. Nhập/Xuất Excel

- Xuất danh sách tồn kho ra Excel
- Nhập danh sách vật tư từ Excel
- Xuất phiếu nhập/xuất kho ra Excel
- Xuất báo cáo ra Excel

### 5.5. Quản Lý Bảo Hành

- Theo dõi ngày bảo hành
- Theo dõi thời gian bảo hành
- Cảnh báo khi sắp hết bảo hành

## 6. Bảo Mật và Phân Quyền

### 6.1. Xác Thực

- Đăng nhập bằng tên đăng nhập và mật khẩu
- Session management
- Timeout sau 30 phút không hoạt động

### 6.2. Phân Quyền

- Phân quyền theo vai trò (Role-based access control)
- Mỗi vai trò có quyền hạn riêng
- Không thể truy cập chức năng ngoài quyền hạn

### 6.3. Kiểm Tra và Audit

- Ghi log tất cả các thao tác quan trọng
- Theo dõi lịch sử thay đổi
- Kiểm tra tính toàn vẹn dữ liệu

## 7. Cấu Hình và Triển Khai

### 7.1. Yêu Cầu Hệ Thống

- .NET 8.0
- MySQL Database
- ASP.NET Core MVC
- Entity Framework Core
- Identity Framework

### 7.2. Cấu Hình

- Cấu hình kết nối database trong `appsettings.json`
- Cấu hình session timeout
- Cấu hình phân quyền

### 7.3. Triển Khai

- Deploy trên IIS hoặc Kestrel
- Cấu hình reverse proxy (nếu cần)
- Cấu hình SSL/TLS (nếu cần)

## 8. Hướng Dẫn Sử Dụng

### 8.1. Đăng Nhập

1. Truy cập trang đăng nhập
2. Nhập tên đăng nhập và mật khẩu
3. Click "Đăng nhập"
4. Hệ thống sẽ chuyển đến trang chủ theo vai trò

### 8.2. Tạo Yêu Cầu Vật Tư

1. Chọn "Yêu cầu" → "Tạo yêu cầu"
2. Nhập tên yêu cầu
3. Chọn dự án (nếu có)
4. Thêm danh sách vật tư
5. Click "Gửi yêu cầu"

### 8.3. Duyệt Yêu Cầu

1. Chọn "Yêu cầu" → "Danh sách yêu cầu"
2. Xem chi tiết yêu cầu
3. Click "Duyệt" hoặc "Từ chối"
4. Nhập lý do (nếu từ chối)

### 8.4. Xử Lý Phiếu Xuất Kho

1. Chọn "Phiếu xuất kho" → "Danh sách phiếu xuất kho"
2. Xem chi tiết phiếu xuất kho
3. Kiểm tra tồn kho
4. Chuẩn bị hàng
5. Thông báo người yêu cầu
6. Xác nhận xuất kho sau khi người yêu cầu xác nhận nhận hàng

### 8.5. Xử Lý Phiếu Nhập Kho

1. Chọn "Phiếu nhập kho" → "Danh sách phiếu nhập kho"
2. Xem chi tiết phiếu nhập kho
3. Duyệt phiếu nhập kho (nếu cần)
4. Xác nhận nhập kho
5. Cập nhật tồn kho

### 8.6. Xử Lý Phiếu Mua Hàng

1. Chọn "Phiếu mua hàng" → "Danh sách phiếu mua hàng"
2. Xem chi tiết phiếu mua hàng
3. Báo giá (nhập đơn giá, tính thành tiền)
4. Duyệt mua hàng (nếu là Giám đốc)
5. Thanh toán (nếu là Kế toán)
6. Xác nhận nhận hàng (nếu là Mua hàng)

## 9. Troubleshooting

### 9.1. Lỗi Đăng Nhập

- Kiểm tra tên đăng nhập và mật khẩu
- Kiểm tra kết nối database
- Kiểm tra session timeout

### 9.2. Lỗi Tồn Kho

- Kiểm tra số lượng tồn kho
- Kiểm tra trạng thái vật tư
- Kiểm tra quyền truy cập

### 9.3. Lỗi Duyệt Yêu Cầu

- Kiểm tra quyền duyệt
- Kiểm tra trạng thái yêu cầu
- Kiểm tra workflow

## 10. Liên Hệ và Hỗ Trợ

Để được hỗ trợ, vui lòng liên hệ bộ phận IT hoặc quản trị viên hệ thống.

---

**Phiên bản tài liệu:** 1.0  
**Ngày cập nhật:** 2024  
**Tác giả:** Hệ thống Quản Lý Kho


