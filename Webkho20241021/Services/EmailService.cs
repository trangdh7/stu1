using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public EmailService(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var fromEmail = _configuration["EmailSettings:FromEmail"];
                var fromPassword = _configuration["EmailSettings:FromPassword"];
                var fromName = _configuration["EmailSettings:FromName"] ?? "Hệ thống Quản lý Kho";

                if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(fromPassword))
                {
                    Console.WriteLine("⚠️ Email configuration is missing. Please configure EmailSettings in appsettings.json");
                    return false;
                }

                if (string.IsNullOrEmpty(toEmail))
                {
                    Console.WriteLine($"⚠️ Recipient email is empty. Skipping email to {toEmail}");
                    return false;
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(fromEmail, fromPassword);

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(fromEmail, fromName);
                        message.To.Add(toEmail);
                        message.Subject = subject;
                        message.Body = body;
                        message.IsBodyHtml = true;

                        await client.SendMailAsync(message);
                        Console.WriteLine($"✅ Email sent successfully to {toEmail}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending email to {toEmail}: {ex.Message}");
                return false;
            }
        }

        public async Task SendNotificationToDepartmentHeadAsync(string maYeucau, string nguoiYeuCau, string boPhan)
        {
            // Tìm trưởng phòng của bộ phận
            var truongPhong = _context.nguoidungs
                .FirstOrDefault(n => n.Bophan == boPhan && n.Chucvu == "Trưởng BP");

            if (truongPhong != null && !string.IsNullOrEmpty(truongPhong.Email))
            {
                var subject = $"Yêu cầu vật tư mới cần phê duyệt - {maYeucau}";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #2c3e50;'>Thông báo yêu cầu vật tư mới</h2>
                        <p>Kính gửi <strong>{truongPhong.TenNguoidung}</strong>,</p>
                        <p>Bạn có một yêu cầu vật tư mới cần phê duyệt:</p>
                        <ul>
                            <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                            <li><strong>Người yêu cầu:</strong> {nguoiYeuCau}</li>
                            <li><strong>Bộ phận:</strong> {boPhan}</li>
                            <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                        </ul>
                        <p>Vui lòng đăng nhập hệ thống để xem chi tiết và phê duyệt.</p>
                        <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                    </body>
                    </html>";

                await SendEmailAsync(truongPhong.Email, subject, body);
            }
        }

        public async Task SendNotificationToEmployeeAsync(string maYeucau, string nguoiYeuCau, string trangThai)
        {
            var nguoiDung = _context.nguoidungs
                .FirstOrDefault(n => n.TenNguoidung == nguoiYeuCau || n.MaNguoidung == nguoiYeuCau);

            if (nguoiDung != null && !string.IsNullOrEmpty(nguoiDung.Email))
            {
                var subject = $"Cập nhật trạng thái yêu cầu vật tư - {maYeucau}";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #2c3e50;'>Thông báo cập nhật yêu cầu vật tư</h2>
                        <p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
                        <p>Yêu cầu vật tư của bạn đã được cập nhật:</p>
                        <ul>
                            <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                            <li><strong>Trạng thái:</strong> <span style='color: #27ae60;'>{trangThai}</span></li>
                            <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                        </ul>
                        <p>Vui lòng đăng nhập hệ thống để xem chi tiết.</p>
                        <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                    </body>
                    </html>";

                await SendEmailAsync(nguoiDung.Email, subject, body);
            }
        }

        public async Task SendNotificationToProjectManagerAsync(string maYeucau, string maDuan)
        {
            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == maDuan);
            if (duan != null && !string.IsNullOrEmpty(duan.MaNguoiQLDA))
            {
                var qlda = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == duan.MaNguoiQLDA);
                if (qlda != null && !string.IsNullOrEmpty(qlda.Email))
                {
                    var subject = $"Yêu cầu vật tư dự án cần phê duyệt - {maYeucau}";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2 style='color: #2c3e50;'>Thông báo yêu cầu vật tư dự án</h2>
                            <p>Kính gửi <strong>{qlda.TenNguoidung}</strong>,</p>
                            <p>Bạn có một yêu cầu vật tư cho dự án <strong>{duan.TenDuan}</strong> cần phê duyệt:</p>
                            <ul>
                                <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                                <li><strong>Mã dự án:</strong> {maDuan}</li>
                                <li><strong>Tên dự án:</strong> {duan.TenDuan}</li>
                                <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                            </ul>
                            <p>Vui lòng đăng nhập hệ thống để xem chi tiết và phê duyệt.</p>
                            <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                        </body>
                        </html>";

                    await SendEmailAsync(qlda.Email, subject, body);
                }
            }
        }

        public async Task SendNotificationToDirectorAsync(string maYeucau)
        {
            var giamDoc = _context.nguoidungs
                .Where(n => n.Chucvu == "Giám đốc")
                .ToList();

            foreach (var gd in giamDoc)
            {
                if (!string.IsNullOrEmpty(gd.Email))
                {
                    var subject = $"Yêu cầu vật tư cần phê duyệt - {maYeucau}";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2 style='color: #2c3e50;'>Thông báo yêu cầu vật tư</h2>
                            <p>Kính gửi <strong>{gd.TenNguoidung}</strong>,</p>
                            <p>Bạn có một yêu cầu vật tư cần phê duyệt:</p>
                            <ul>
                                <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                                <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                            </ul>
                            <p>Vui lòng đăng nhập hệ thống để xem chi tiết và phê duyệt.</p>
                            <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                        </body>
                        </html>";

                    await SendEmailAsync(gd.Email, subject, body);
                }
            }
        }

        public async Task SendNotificationToWarehouseAsync(string maYeucau, bool coHang)
        {
            var nhanVienKho = _context.nguoidungs
                .Where(n => n.Bophan == "BP kho")
                .ToList();

            foreach (var nv in nhanVienKho)
            {
                if (!string.IsNullOrEmpty(nv.Email))
                {
                    var subject = coHang 
                        ? $"Yêu cầu vật tư có hàng trong kho - {maYeucau}"
                        : $"Yêu cầu vật tư cần mua hàng - {maYeucau}";
                    
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2 style='color: #2c3e50;'>Thông báo yêu cầu vật tư</h2>
                            <p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
                            <p>Bạn có một yêu cầu vật tư {(coHang ? "có hàng trong kho" : "cần mua hàng")}:</p>
                            <ul>
                                <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                                <li><strong>Trạng thái:</strong> {(coHang ? "Có hàng - Chờ xuất kho" : "Không có hàng - Cần mua hàng")}</li>
                                <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                            </ul>
                            <p>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>
                            <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                        </body>
                        </html>";

                    await SendEmailAsync(nv.Email, subject, body);
                }
            }
        }

        public async Task SendNotificationToPurchasingAsync(string maYeucau)
        {
            var nhanVienMuaHang = _context.nguoidungs
                .Where(n => n.Bophan == "BP mua hàng")
                .ToList();

            foreach (var nv in nhanVienMuaHang)
            {
                if (!string.IsNullOrEmpty(nv.Email))
                {
                    var subject = $"Yêu cầu vật tư cần mua hàng - {maYeucau}";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2 style='color: #2c3e50;'>Thông báo yêu cầu mua hàng</h2>
                            <p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
                            <p>Bạn có một yêu cầu vật tư cần mua hàng:</p>
                            <ul>
                                <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                                <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                            </ul>
                            <p>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>
                            <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                        </body>
                        </html>";

                    await SendEmailAsync(nv.Email, subject, body);
                }
            }
        }

        public async Task SendNotificationToRequesterOnIssueAsync(string maYeucau, string maXuatkho)
        {
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau != null && !string.IsNullOrEmpty(yeucau.YCMaNguoidung))
            {
                var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
                if (nguoiDung != null && !string.IsNullOrEmpty(nguoiDung.Email))
                {
                    var subject = $"Vật tư đã được xuất kho - {maYeucau}";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2 style='color: #27ae60;'>Thông báo xuất kho</h2>
                            <p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
                            <p>Vật tư từ yêu cầu của bạn đã được xuất kho:</p>
                            <ul>
                                <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
                                <li><strong>Mã phiếu xuất kho:</strong> {maXuatkho}</li>
                                <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                            </ul>
                            <p>Vui lòng đến kho để nhận vật tư.</p>
                            <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                        </body>
                        </html>";

                    await SendEmailAsync(nguoiDung.Email, subject, body);
                }
            }
        }

        public async Task SendNotificationToPurchasingOnPaymentAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang != null)
            {
                var nhanVienMuaHang = _context.nguoidungs
                    .Where(n => n.Bophan == "BP mua hàng")
                    .ToList();

                foreach (var nv in nhanVienMuaHang)
                {
                    if (!string.IsNullOrEmpty(nv.Email))
                    {
                        var subject = $"Phiếu mua hàng đã được thanh toán - {maMuahang}";
                        var body = $@"
                            <html>
                            <body style='font-family: Arial, sans-serif;'>
                                <h2 style='color: #27ae60;'>Thông báo thanh toán</h2>
                                <p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
                                <p>Phiếu mua hàng đã được kế toán thanh toán:</p>
                                <ul>
                                    <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
                                    <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau}</li>
                                    <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                                </ul>
                                <p>Vui lòng đăng nhập hệ thống để xem chi tiết.</p>
                                <p style='color: #7f8c8d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời email này.</p>
                            </body>
                            </html>";

                        await SendEmailAsync(nv.Email, subject, body);
                    }
                }
            }
        }
    }
}

