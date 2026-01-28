using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Webkho_20241021.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace Webkho_20241021.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSettingsProvider _emailSettingsProvider;
        private readonly string _baseUrl;

        public EmailService(
            IConfiguration configuration,
            ApplicationDbContext context,
            IEmailSettingsProvider emailSettingsProvider)
        {
            _configuration = configuration;
            _context = context;
            _emailSettingsProvider = emailSettingsProvider;
            _baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://stu.vn";
        }


        private string? GetEffectiveEmail(string? maNguoidung, string? tenNguoidung, string? emailFromNguoidung)
        {
            // 1. Nếu bảng nguoidungs đã có email thì dùng luôn
            if (!string.IsNullOrWhiteSpace(emailFromNguoidung))
            {
                return emailFromNguoidung;
            }

            // 2. Fallback sang bảng User theo mã nhân viên (manv)
            if (!string.IsNullOrWhiteSpace(maNguoidung))
            {
                var userByMa = _context.User.FirstOrDefault(u => u.manv == maNguoidung);
                if (userByMa != null && !string.IsNullOrWhiteSpace(userByMa.Email))
                {
                    return userByMa.Email;
                }
            }

            // 3. Fallback theo tên nếu cần
            if (!string.IsNullOrWhiteSpace(tenNguoidung))
            {
                var userByName = _context.User.FirstOrDefault(u => u.Name == tenNguoidung);
                if (userByName != null && !string.IsNullOrWhiteSpace(userByName.Email))
                {
                    return userByName.Email;
                }
            }

            return null;
        }


        private string BuildEmailHeader()
        {
            var logoUrl = $"{_baseUrl}/images/logo.png";
            //var logoUrl = $"https://cdn.nhanlucnganhluat.vn/uploads/images/B937CC5F/logo/2019-11/logo2.jpg";
            return $@"
<table width='100%' cellpadding='0' cellspacing='0' style='margin-bottom:20px;'>
  <tr>
    <td align='left' style='padding:10px 0;'>
      <img src='{logoUrl}' alt='STU Logo' style='height:48px;' />
    </td>
    <td align='right' style='padding:10px 0; font-size:18px; font-weight:bold; color:#2c3e50;'>
      STU JSC
    </td>
  </tr>
</table>";
        }



        private string BuildEmailFooter()
        {
            var year = DateTime.Now.Year;
            var logoUrl = $"{_baseUrl}/images/logo.png";
            //var logoUrl = $"https://cdn.nhanlucnganhluat.vn/uploads/images/B937CC5F/logo/2019-11/logo2.jpg";
            return $@"
<hr style='border:none;border-top:1px solid #e0e0e0;margin:30px 0;' />



<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:20px;"">
  <tr>
    <!-- Logo -->
    <td width=""80"" valign=""top"">
      <img src=""{logoUrl}"" alt=""STU Logo"" style=""height:48px;display:block;"" />
    </td>

    <!-- Spacer -->
    <td width=""20"">
      &nbsp;
    </td>

    <!-- Text -->
    <td valign=""top"" style=""font-size:13px;color:#555;line-height:1.6;"">
      <strong>Hệ thống Quản lý Kho STU</strong><br/>
      Địa chỉ: Số 8 đường Thạch Bàn (Đảo Cầu Vồng), Long Biên, Hà Nội<br/>
      Điện thoại: (84-24) 3636 2814<br/>
      FAX: (84-24) 3633 1640<br/>
      Email: <a href=""mailto:info@stu.com.vn"">info@stu.com.vn</a>
    </td>
  </tr>
</table>


<p style='font-size:12px;color:#999;margin-top:16px;'>
  © {year} Hệ thống Quản lý Kho STU.
</p>";
        }



        private string BuildEmailTemplate(string title, string contentHtml)
        {
            var header = BuildEmailHeader();
            var footer = BuildEmailFooter();

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
</head>
<body style='margin:0;padding:0;font-family:Arial,Helvetica,sans-serif;color:#333;'>

<table width='100%' cellpadding='0' cellspacing='0'>
  <tr>
    <td align='left'>
      <table width='600' cellpadding='0' cellspacing='0' style='padding:20px;'>

        <tr>
          <td>
            {header}
          </td>
        </tr>

        <tr>
          <td style='font-size:20px;font-weight:bold;color:#2c3e50;padding-bottom:16px;'>
            {title}
          </td>
        </tr>

        <tr>
          <td style='font-size:14px;line-height:1.7;'>
            {contentHtml}
          </td>
        </tr>

        <tr>
          <td>
            {footer}
          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>

</body>
</html>";
        }


        private string GetConfigValue(string configKey, string defaultValue = "")
        {
            // Đọc từ appsettings.json
            var configValue = _configuration[configKey];
            if (!string.IsNullOrEmpty(configValue))
            {
                Debug.WriteLine($"✓ Đọc {configKey} từ appsettings.json");
                return configValue;
            }
            
            // Nếu không có, dùng giá trị mặc định
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Debug.WriteLine($"⚠ Sử dụng giá trị mặc định cho {configKey}");
                return defaultValue;
            }
            
            Debug.WriteLine($"⚠ KHÔNG TÌM THẤY {configKey} trong appsettings");
            return defaultValue;
        }

        private async Task<(string smtpServer, int smtpPort, string fromEmail, string fromPassword, string fromName)> GetSmtpSettingsAsync()
        {
            var dbSettings = await _emailSettingsProvider.GetAsync();

            // Fallback từ appsettings nếu DB chưa có
            var fallbackSmtpServer = _configuration["EmailSettings:StuEmailSettings:SmtpServer"]
                ?? _configuration["EmailSettings:SmtpServer"]
                ?? "pro01.emailserver.vn";
            var fallbackSmtpPort = int.TryParse(
                    _configuration["EmailSettings:StuEmailSettings:SmtpPort"] ?? _configuration["EmailSettings:SmtpPort"],
                    out var portFromConfig)
                ? portFromConfig
                : 465;
            var fallbackFromEmail = _configuration["EmailSettings:FromEmail"] ?? "";
            var fallbackFromPassword = _configuration["EmailSettings:StuEmailSettings:FromPassword"]
                ?? _configuration["EmailSettings:FromPassword"];
            var fallbackFromName = _configuration["EmailSettings:StuEmailSettings:FromName"]
                ?? _configuration["EmailSettings:FromName"]
                ?? "STU JSC";

            var smtpServer = string.IsNullOrWhiteSpace(dbSettings?.SmtpServer)
                ? fallbackSmtpServer
                : dbSettings.SmtpServer;
            var smtpPort = dbSettings?.SmtpPort > 0 ? dbSettings.SmtpPort : fallbackSmtpPort;
            var fromEmail = string.IsNullOrWhiteSpace(dbSettings?.FromEmail)
                ? fallbackFromEmail
                : dbSettings.FromEmail;
            var fromPassword = string.IsNullOrWhiteSpace(dbSettings?.FromPassword)
                ? (fallbackFromPassword ?? string.Empty)
                : dbSettings.FromPassword!;
            var fromName = string.IsNullOrWhiteSpace(dbSettings?.FromName)
                ? fallbackFromName
                : dbSettings.FromName!;
            
            Debug.WriteLine($"🔍 Đang đọc cấu hình email...");
            Debug.WriteLine($"   FromEmail: {fromEmail ?? "(null)"}");
            Debug.WriteLine($"   SmtpServer: {smtpServer}");
            Debug.WriteLine($"   SmtpPort: {smtpPort}");
            
            if (string.IsNullOrWhiteSpace(fromPassword))
            {
                Debug.WriteLine($"❌ KHÔNG TÌM THẤY PASSWORD! Kiểm tra EmailSettings trong DB hoặc appsettings.json");
                fromPassword = "";
            }
            else
            {
                Debug.WriteLine($"✓ Đã tìm thấy password (độ dài: {fromPassword.Length})");
            }
            
            return (smtpServer, smtpPort, fromEmail ?? "", fromPassword ?? "", fromName);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            // Khai báo biến ở ngoài để các catch block có thể truy cập
            string smtpServer = "";
            int smtpPort = 465;
            string fromEmail = "";
            string fromPassword = "";
            string fromName = "";
            
            try
            {
                Debug.WriteLine("===== BẮT ĐẦU GỬI EMAIL =====");
                Debug.WriteLine($"To: {toEmail}");
                Debug.WriteLine($"Subject: {subject}");
                Debug.WriteLine($"Body length: {body?.Length ?? 0}");

                if (string.IsNullOrEmpty(toEmail))
                {
                    Debug.WriteLine("⚠️ Email người nhận rỗng, bỏ qua gửi.");
                    return false;
                }

                // Lấy cấu hình SMTP dựa trên FromEmail (email gửi đi)
                (smtpServer, smtpPort, fromEmail, fromPassword, fromName) = await GetSmtpSettingsAsync();

                Debug.WriteLine($"SMTP: {smtpServer}:{smtpPort}");
                Debug.WriteLine($"FromEmail: {fromEmail}");
                Debug.WriteLine($"FromPassword empty? {string.IsNullOrEmpty(fromPassword)}");

                if (string.IsNullOrEmpty(fromEmail))
                {
                    Debug.WriteLine("❌ LỖI: Thiếu FromEmail trong EmailSettings. Kiểm tra appsettings.json hoặc appsettings.Development.json");
                    return false;
                }

                if (string.IsNullOrEmpty(fromPassword))
                {
                    Debug.WriteLine("❌ LỖI: Thiếu FromPassword trong EmailSettings!");
                    Debug.WriteLine($"   Giá trị hiện tại: (rỗng)");
                    Debug.WriteLine("   Cách khắc phục:");
                    Debug.WriteLine("   1. Kiểm tra file appsettings.json có tồn tại không");
                    Debug.WriteLine("   2. Đảm bảo có section EmailSettings với FromPassword");
                    Debug.WriteLine("   Ví dụ cấu hình:");
                    Debug.WriteLine("      \"EmailSettings\": {");
                    Debug.WriteLine("        \"FromPassword\": \"your-email-password\"");
                    Debug.WriteLine("      }");
                    return false;
                }

                // Use MailKit for better SMTP support, especially for port 465
                using (var client = new SmtpClient())
                {
                    try
                    {
                        // Set timeout
                        client.Timeout = 30000; // 30 seconds

                        // Port 465 requires SSL from the start (implicit SSL)
                        // Port 587 uses STARTTLS (explicit SSL)
                        SecureSocketOptions sslOption;
                        if (smtpPort == 465)
                        {
                            Debug.WriteLine("Using port 465 - implicit SSL required");
                            // Try SslOnConnect first, if fails try Auto
                            sslOption = SecureSocketOptions.SslOnConnect;
                        }
                        else
                        {
                            Debug.WriteLine($"📧 Using port {smtpPort} - STARTTLS will be used");
                            sslOption = SecureSocketOptions.StartTls;
                        }

                        Debug.WriteLine($"🔌 Đang kết nối đến {smtpServer}:{smtpPort}...");
                        
                        // Try to connect - if SSL certificate validation fails, try with Auto
                        try
                        {
                            await client.ConnectAsync(smtpServer, smtpPort, sslOption);
                        }
                        catch (MailKit.Security.SslHandshakeException sslEx)
                        {
                            Debug.WriteLine($"⚠️ SSL handshake failed, thử với Auto mode...");
                            Debug.WriteLine($"Lỗi: {sslEx.Message}");
                            // Retry with Auto which is more lenient
                            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.Auto);
                        }
                        
                        Debug.WriteLine("✅ Đã kết nối thành công");

                        // Authenticate
                        Debug.WriteLine($"🔐 Đang xác thực với {fromEmail}...");
                        await client.AuthenticateAsync(fromEmail, fromPassword);
                        Debug.WriteLine("✅ Xác thực thành công");

                        // Create message using MimeKit
                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(fromName, fromEmail));
                        message.To.Add(new MailboxAddress("", toEmail));
                        message.Subject = subject;
                        
                        var bodyBuilder = new BodyBuilder
                        {
                            HtmlBody = body
                        };
                        message.Body = bodyBuilder.ToMessageBody();

                        Debug.WriteLine("👉 Đang gửi email...");
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);
                        
                        Debug.WriteLine($"✅ Email sent successfully to {toEmail}");
                        return true;
                    }
                    catch (MailKit.Security.AuthenticationException authEx)
                    {
                        Debug.WriteLine($"❌ Lỗi xác thực: {authEx.Message}");
                        Debug.WriteLine($"Chi tiết: {authEx}");
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        throw;
                    }
                    catch (MailKit.Net.Smtp.SmtpCommandException smtpEx)
                    {
                        Debug.WriteLine($"❌ Lỗi SMTP command: {smtpEx.Message}");
                        Debug.WriteLine($"Status Code: {smtpEx.StatusCode}");
                        Debug.WriteLine($"Chi tiết: {smtpEx}");
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        throw;
                    }
                    catch (System.Net.Sockets.SocketException socketEx)
                    {
                        Debug.WriteLine($"❌ Lỗi kết nối mạng: {socketEx.Message}");
                        Debug.WriteLine($"Error Code: {socketEx.ErrorCode}");
                        Debug.WriteLine($"Chi tiết: {socketEx}");
                        Debug.WriteLine($"⚠️ Có thể do firewall chặn port {smtpPort} hoặc không thể kết nối đến {smtpServer}");
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        throw;
                    }
                    catch (System.OperationCanceledException timeoutEx)
                    {
                        Debug.WriteLine($"❌ Lỗi timeout: {timeoutEx.Message}");
                        Debug.WriteLine($"⚠️ Kết nối đến {smtpServer}:{smtpPort} bị timeout");
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        throw;
                    }
                }
            }
            catch (MailKit.Security.AuthenticationException authEx)
            {
                Debug.WriteLine($"❌ Lỗi xác thực email đến {toEmail}: {authEx.Message}");
                Debug.WriteLine($"⚠️ Kiểm tra lại username và password trong appsettings.json hoặc biến môi trường");
                Debug.WriteLine(authEx.ToString());
                return false;
            }
            catch (MailKit.Net.Smtp.SmtpCommandException smtpEx)
            {
                Debug.WriteLine($"❌ Lỗi SMTP command khi gửi email đến {toEmail}: {smtpEx.Message}");
                Debug.WriteLine($"Status Code: {smtpEx.StatusCode}");
                Debug.WriteLine($"⚠️ Server SMTP không chấp nhận lệnh hoặc có lỗi trong quá trình gửi");
                Debug.WriteLine(smtpEx.ToString());
                return false;
            }
            catch (System.Net.Sockets.SocketException socketEx)
            {
                Debug.WriteLine($"❌ Lỗi kết nối mạng khi gửi email đến {toEmail}: {socketEx.Message}");
                Debug.WriteLine($"Error Code: {socketEx.ErrorCode}");
                Debug.WriteLine($"⚠️ KHẢ NĂNG CAO: Firewall trên server đang chặn port {smtpPort}");
                Debug.WriteLine($"⚠️ Hoặc server không thể kết nối đến {smtpServer}:{smtpPort}");
                Debug.WriteLine($"⚠️ Giải pháp: Mở port {smtpPort} (outbound) trên firewall của server");
                Debug.WriteLine(socketEx.ToString());
                return false;
            }
            catch (System.OperationCanceledException timeoutEx)
            {
                Debug.WriteLine($"❌ Timeout khi gửi email đến {toEmail}: {timeoutEx.Message}");
                Debug.WriteLine($"⚠️ Kết nối đến SMTP server bị timeout - có thể do mạng chậm hoặc firewall");
                Debug.WriteLine(timeoutEx.ToString());
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi không xác định khi gửi email đến {toEmail}: {ex.Message}");
                Debug.WriteLine($"Loại lỗi: {ex.GetType().FullName}");
                Debug.WriteLine(ex.ToString());
                return false;
            }
        }

        public async Task SendNotificationToDepartmentHeadAsync(string maYeucau, string nguoiYeuCau, string boPhan)
        {
            // Tìm trưởng phòng của bộ phận
            var truongPhong = _context.nguoidungs
                .FirstOrDefault(n => n.Bophan == boPhan && n.Chucvu == "Trưởng BP");

            if (truongPhong != null)
            {
                var toEmail = GetEffectiveEmail(truongPhong.MaNguoidung, truongPhong.TenNguoidung, truongPhong.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho Trưởng BP bộ phận {boPhan} (MaNguoidung = {truongPhong.MaNguoidung}). Bỏ qua gửi mail.");
                    return;
                }

                // Xác định area phù hợp để deep-link cho trưởng BP
                string areaSegment = boPhan switch
                {
                    "BP kỹ thuật" => "TruongBPKythuat",
                    "BP kho" => "TruongBPKho",
                    "BP mua hàng" => "TruongBPMuahang",
                    "BP kế toán" => "TruongBPKetoan",
                    _ => "TruongBPKythuat"
                };

                var yeucauUrl = $"{_baseUrl}/{areaSegment}/Yeucau/Yeucau?search={Uri.EscapeDataString(maYeucau)}";

                var subject = $"Yêu cầu vật tư mới cần phê duyệt - {maYeucau}";
                var contentHtml = $@"
<p>Kính gửi <strong>{truongPhong.TenNguoidung}</strong>,</p>

<p>Bạn có một yêu cầu vật tư mới cần phê duyệt:</p>

<p>
<strong>Mã yêu cầu:</strong> {maYeucau}<br/>
<strong>Người yêu cầu:</strong> {nguoiYeuCau}<br/>
<strong>Bộ phận:</strong> {boPhan}<br/>
<strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}
</p>

<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Mở yêu cầu trong hệ thống
  </a>
</p>

<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>

<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>

<p style='font-size:13px;color:#555;'>
  Đây là email tự động, vui lòng không trả lời email này.
</p>

<p style='font-size:13px;color:#555;'>
  Nếu bạn có thắc mắc, vui lòng liên hệ bộ phận IT hoặc đăng nhập vào hệ thống.
</p>";




                var body = BuildEmailTemplate("Thông báo yêu cầu vật tư mới", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        // =========================
        // PHIẾU NHẬP KHO - EMAIL FLOW
        // =========================
        public async Task SendNotificationOnNhapKhoCreatedAsync(string maNhapkho)
        {
            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkho);
            if (phieunhapkho == null) return;

            // Xác định người tạo/ người yêu cầu (ưu tiên từ yeucau nếu có)
            nguoidungs? nguoiTao = null;
            yeucau? yeucau = null;

            if (!string.IsNullOrWhiteSpace(phieunhapkho.MaYeucau))
            {
                yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);
                if (yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaNguoidung))
                {
                    nguoiTao = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
                }
            }

            if (nguoiTao == null && !string.IsNullOrWhiteSpace(phieunhapkho.MaNguoidung))
            {
                nguoiTao = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung);
            }

            // DUYỆT ĐƠN GIẢN:
            // - Có dự án -> gửi QLDA duyệt (không phân biệt NV hay Trưởng BP)
            // - Không có dự án -> gửi Giám đốc duyệt
            if (!string.IsNullOrWhiteSpace(phieunhapkho.MaDuan))
            {
                await SendNotificationToProjectManagerOnNhapKhoNeedApprovalAsync(maNhapkho);
            }
            else
            {
                await SendNotificationToDirectorOnNhapKhoNeedApprovalAsync(maNhapkho);
            }
        }

        public async Task SendNotificationToDepartmentHeadOnNhapKhoNeedApprovalAsync(string maNhapkho, string boPhan, string nguoiTao)
        {
            var truongPhong = _context.nguoidungs.FirstOrDefault(n => n.Bophan == boPhan && n.Chucvu == "Trưởng BP");
            if (truongPhong == null) return;

            var toEmail = GetEffectiveEmail(truongPhong.MaNguoidung, truongPhong.TenNguoidung, truongPhong.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho Trưởng BP bộ phận {boPhan} (MaNguoidung = {truongPhong.MaNguoidung}). Bỏ qua gửi mail.");
                return;
            }

            string areaSegment = boPhan switch
            {
                "BP kỹ thuật" => "TruongBPKythuat",
                "BP kho" => "TruongBPKho",
                "BP mua hàng" => "TruongBPMuahang",
                "BP kế toán" => "TruongBPKetoan",
                _ => "TruongBPKythuat"
            };

            var url = $"{_baseUrl}/{areaSegment}/Yeucau/Phieunhapkho?search={Uri.EscapeDataString(maNhapkho)}";

            var subject = $"Phiếu nhập kho mới cần theo dõi/duyệt - {maNhapkho}";
            var contentHtml = $@"
<p>Kính gửi <strong>{truongPhong.TenNguoidung}</strong>,</p>
<p>Bộ phận <strong>{boPhan}</strong> vừa tạo một phiếu nhập kho:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  <li><strong>Người tạo:</strong> {nguoiTao}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p style='margin:20px 0;'>
  <a href='{url}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Mở phiếu nhập kho trong hệ thống
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {url}
</p>";

            var body = BuildEmailTemplate("Thông báo phiếu nhập kho mới", contentHtml);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToProjectManagerOnNhapKhoNeedApprovalAsync(string maNhapkho)
        {
            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkho);
            if (phieunhapkho == null || string.IsNullOrWhiteSpace(phieunhapkho.MaDuan)) return;

            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
            if (duan == null || string.IsNullOrWhiteSpace(duan.MaNguoiQLDA)) return;

            var qlda = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == duan.MaNguoiQLDA);
            if (qlda == null) return;

            var toEmail = GetEffectiveEmail(qlda.MaNguoidung, qlda.TenNguoidung, qlda.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($" Không tìm được email cho QLDA MaNguoidung = {qlda.MaNguoidung}. Bỏ qua gửi mail.");
                return;
            }

            var url = $"{_baseUrl}/QuanLiDuAn/Yeucau/Phieunhapkho?search={Uri.EscapeDataString(maNhapkho)}";
            var subject = $"Phiếu nhập kho cần QLDA duyệt - {maNhapkho}";
            var contentHtml = $@"
<p>Kính gửi <strong>{qlda.TenNguoidung}</strong>,</p>
<p>Bạn có một phiếu nhập kho thuộc dự án <strong>{duan.TenDuan}</strong> cần duyệt:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  <li><strong>Dự án:</strong> {duan.TenDuan} ({duan.MaDuan})</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p style='margin:20px 0;'>
  <a href='{url}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Mở phiếu nhập kho trong hệ thống
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {url}
</p>";

            var body = BuildEmailTemplate("Thông báo phiếu nhập kho cần duyệt", contentHtml);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToDirectorOnNhapKhoNeedApprovalAsync(string maNhapkho)
        {
            var giamDoc = _context.nguoidungs.Where(n => n.Chucvu == "Giám đốc").ToList();
            if (!giamDoc.Any()) return;

            var url = $"{_baseUrl}/Giamdoc/Yeucau/Phieunhapkho?search={Uri.EscapeDataString(maNhapkho)}";

            foreach (var gd in giamDoc)
            {
                var toEmail = GetEffectiveEmail(gd.MaNguoidung, gd.TenNguoidung, gd.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho Giám đốc MaNguoidung = {gd.MaNguoidung}. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Phiếu nhập kho cần Giám đốc duyệt - {maNhapkho}";
                var contentHtml = $@"
<p>Kính gửi <strong>{gd.TenNguoidung}</strong>,</p>
<p>Bạn có một phiếu nhập kho cần phê duyệt:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p style='margin:20px 0;'>
  <a href='{url}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Xem và phê duyệt phiếu nhập kho
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {url}
</p>";

                var body = BuildEmailTemplate("Thông báo phiếu nhập kho cần duyệt", contentHtml);
                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToRequesterOnNhapKhoStatusAsync(string maNhapkho, string trangThai, string? ghiChu = null)
        {
            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkho);
            if (phieunhapkho == null) return;

            // Lấy người tạo/yêu cầu
            string? maNguoiYeuCau = null;
            if (!string.IsNullOrWhiteSpace(phieunhapkho.MaYeucau))
            {
                var yc = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);
                if (yc != null && !string.IsNullOrWhiteSpace(yc.YCMaNguoidung))
                {
                    maNguoiYeuCau = yc.YCMaNguoidung;
                }
            }

            if (string.IsNullOrWhiteSpace(maNguoiYeuCau) && !string.IsNullOrWhiteSpace(phieunhapkho.MaNguoidung))
            {
                maNguoiYeuCau = phieunhapkho.MaNguoidung;
            }

            if (string.IsNullOrWhiteSpace(maNguoiYeuCau)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNguoiYeuCau);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi cập nhật trạng thái nhập kho. Bỏ qua gửi mail.");
                return;
            }

            var url = $"{_baseUrl}/NhanvienKho/Yeucau/Phieunhapkho?search={Uri.EscapeDataString(maNhapkho)}";
            var subject = $"Cập nhật phiếu nhập kho - {maNhapkho}";
            var ghiChuHtml = string.IsNullOrWhiteSpace(ghiChu) ? "" : $"<p><strong>Ghi chú:</strong> {System.Net.WebUtility.HtmlEncode(ghiChu)}</p>";

            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Phiếu nhập kho của bạn đã được cập nhật:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  <li><strong>Trạng thái:</strong> <span style='color:#27ae60;'><strong>{trangThai}</strong></span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
{ghiChuHtml}
<p style='margin:20px 0;'>
  <a href='{url}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Xem phiếu nhập kho
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {url}
</p>";

            var body = BuildEmailTemplate("Thông báo cập nhật phiếu nhập kho", contentHtml);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToEmployeeAsync(string maYeucau, string nguoiYeuCau, string trangThai)
        {
            var nguoiDung = _context.nguoidungs
                .FirstOrDefault(n => n.TenNguoidung == nguoiYeuCau || n.MaNguoidung == nguoiYeuCau);

            if (nguoiDung != null)
            {
                var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu '{nguoiYeuCau}' (MaNguoidung = {nguoiDung.MaNguoidung}). Bỏ qua gửi mail.");
                    return;
                }

                var subject = $"Cập nhật trạng thái yêu cầu vật tư - {maYeucau}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Yêu cầu vật tư của bạn đã được cập nhật:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Trạng thái:</strong> <span style='color: #27ae60;'>{trangThai}</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
";

                var body = BuildEmailTemplate("Thông báo cập nhật yêu cầu vật tư", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToProjectManagerAsync(string maYeucau, string maDuan)
        {
            Debug.WriteLine("[EmailService/SendNotificationToProjectManagerAsync] START");
            Debug.WriteLine($"MaYeucau={maYeucau}, MaDuan={maDuan}");

            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == maDuan);
            if (duan != null && !string.IsNullOrEmpty(duan.MaNguoiQLDA))
            {
                var qlda = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == duan.MaNguoiQLDA);
                if (qlda != null)
                {
                    // Ưu tiên dùng email trong bảng nguoidungs, tránh đụng Identity User gây lỗi cột
                    var toEmail = !string.IsNullOrWhiteSpace(qlda.Email)
                        ? qlda.Email
                        : GetEffectiveEmail(qlda.MaNguoidung, qlda.TenNguoidung, qlda.Email);

                    Debug.WriteLine($"[EmailService/SendNotificationToProjectManagerAsync] QLDA={qlda.MaNguoidung}, RawEmail={qlda.Email}, EffectiveEmail={toEmail}");

                    if (string.IsNullOrWhiteSpace(toEmail))
                    {
                        Debug.WriteLine($"⚠️ Không tìm được email cho QLDA MaNguoidung = {qlda.MaNguoidung}. Bỏ qua gửi mail.");
                        return;
                    }

                    // Lấy thêm thông tin yêu cầu để hiển thị giống mail Trưởng BP
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
                    var nguoiYeuCau = yeucau?.NguoiYeucau ?? "";
                    var boPhan = yeucau?.Bophan ?? "";

                    // Deep-link về area QLDA
                    var yeucauUrl = $"{_baseUrl}/QuanLiDuAn/Yeucau/Yeucau?search={Uri.EscapeDataString(maYeucau)}";

                    var subject = $"Yêu cầu vật tư mới cần phê duyệt - {maYeucau}";
                    var contentHtml = $@"
<p>Kính gửi <strong>{qlda.TenNguoidung}</strong>,</p>

<p>Bạn có một yêu cầu vật tư mới cần phê duyệt cho dự án <strong>{duan.TenDuan}</strong>:</p>

<p>
<strong>Mã yêu cầu:</strong> {maYeucau}<br/>
<strong>Dự án:</strong> {duan.TenDuan} ({maDuan})<br/>
<strong>Người yêu cầu:</strong> {nguoiYeuCau}<br/>
<strong>Bộ phận:</strong> {boPhan}<br/>
<strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}
</p>

<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Mở yêu cầu trong hệ thống
  </a>
</p>

<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>

<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>

<p style='font-size:13px;color:#555;'>
  Đây là email tự động, vui lòng không trả lời email này.
</p>

<p style='font-size:13px;color:#555;'>
  Nếu bạn có thắc mắc, vui lòng liên hệ bộ phận IT hoặc đăng nhập vào hệ thống.
</p>";
                    var body = BuildEmailTemplate("Thông báo yêu cầu vật tư mới", contentHtml);

                    Debug.WriteLine("[EmailService/SendNotificationToProjectManagerAsync] CALL SendEmailAsync to QLDA");
                    var sent = await SendEmailAsync(toEmail, subject, body);
                    Debug.WriteLine($"[EmailService/SendNotificationToProjectManagerAsync] SendEmailAsync result={sent}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ Không tìm thấy bản ghi nguoidungs cho MaNguoiQLDA = {duan.MaNguoiQLDA}");
                }
            }
            else
            {
                Debug.WriteLine($"⚠️ Không tìm thấy dự án hoặc MaNguoiQLDA rỗng. MaDuan={maDuan}");
            }
        }

        public async Task SendNotificationToDirectorAsync(string maYeucau)
        {
            // Lấy thông tin yêu cầu để hiển thị đầy đủ giống mail Trưởng BP
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            var nguoiYeuCau = yeucau?.NguoiYeucau ?? "";
            var boPhan = yeucau?.Bophan ?? "";

            // Deep-link về area Giám đốc
            var yeucauUrl = $"{_baseUrl}/Giamdoc/Yeucau/Yeucau?search={Uri.EscapeDataString(maYeucau)}";

            var giamDoc = _context.nguoidungs
                .Where(n => n.Chucvu == "Giám đốc")
                .ToList();

            foreach (var gd in giamDoc)
            {
                var toEmail = GetEffectiveEmail(gd.MaNguoidung, gd.TenNguoidung, gd.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho Giám đốc MaNguoidung = {gd.MaNguoidung}. Bỏ qua gửi mail cho người này.");
                    continue;
                }

                var subject = $"Yêu cầu vật tư mới cần phê duyệt - {maYeucau}";
                var contentHtml = $@"
<p>Kính gửi <strong>{gd.TenNguoidung}</strong>,</p>

<p>Bạn có một yêu cầu vật tư mới cần phê duyệt:</p>

<p>
<strong>Mã yêu cầu:</strong> {maYeucau}<br/>
<strong>Người yêu cầu:</strong> {nguoiYeuCau}<br/>
<strong>Bộ phận:</strong> {boPhan}<br/>
<strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}
</p>

<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Mở yêu cầu trong hệ thống
  </a>
</p>

<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>

<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>

<p style='font-size:13px;color:#555;'>
  Đây là email tự động, vui lòng không trả lời email này.
</p>

<p style='font-size:13px;color:#555;'>
  Nếu bạn có thắc mắc, vui lòng liên hệ bộ phận IT hoặc đăng nhập vào hệ thống.
</p>";

                var body = BuildEmailTemplate("Thông báo yêu cầu vật tư mới", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToWarehouseAsync(string maYeucau, bool coHang)
        {
            var nhanVienKho = _context.nguoidungs
                .Where(n => n.Bophan == "BP kho")
                .ToList();

            foreach (var nv in nhanVienKho)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên kho MaNguoidung = {nv.MaNguoidung}. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = coHang
                    ? $"Yêu cầu vật tư có hàng trong kho - {maYeucau}"
                    : $"Yêu cầu vật tư cần mua hàng - {maYeucau}";

                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Bạn có một yêu cầu vật tư {(coHang ? "có hàng trong kho" : "cần mua hàng")}:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Trạng thái:</strong> <span style='color: {(coHang ? "#27ae60" : "#e74c3c")};'>{(coHang ? "Có hàng - Chờ xuất kho" : "Không có hàng - Cần mua hàng")}</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>";

                var body = BuildEmailTemplate("Thông báo yêu cầu vật tư", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToPurchasingAsync(string maYeucau)
        {
            var nhanVienMuaHang = _context.nguoidungs
                .Where(n => n.Bophan == "BP mua hàng")
                .ToList();

            foreach (var nv in nhanVienMuaHang)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên mua hàng MaNguoidung = {nv.MaNguoidung}. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Yêu cầu vật tư cần mua hàng - {maYeucau}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Bạn có một yêu cầu vật tư cần mua hàng:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>";

                var body = BuildEmailTemplate("Thông báo yêu cầu mua hàng", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToRequesterOnIssueAsync(string maYeucau, string maXuatkho)
        {
            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] BẮT ĐẦU - MaYeucau={maYeucau}, MaXuatkho={maXuatkho}");
            
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null)
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ❌ Không tìm thấy yêu cầu với MaYeucau = {maYeucau}");
                return;
            }
            
            if (string.IsNullOrEmpty(yeucau.YCMaNguoidung))
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ❌ Yêu cầu {maYeucau} không có YCMaNguoidung");
                return;
            }
            
            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] YCMaNguoidung = {yeucau.YCMaNguoidung}");
            
            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null)
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ❌ Không tìm thấy người dùng với MaNguoidung = {yeucau.YCMaNguoidung}");
                return;
            }
            
            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] Tìm thấy người dùng: {nguoiDung.TenNguoidung}, Email trong DB: {nguoiDung.Email ?? "(null)"}");
            
            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ❌ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung}, TenNguoidung = {nguoiDung.TenNguoidung}. Bỏ qua gửi mail.");
                return;
            }
            
            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] Email người nhận: {toEmail}");

            // Lấy danh sách vật tư đã xuất kho - chỉ lấy vật tư thuộc về mã yêu cầu này
            var vatTuXuatKho = _context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == maXuatkho 
                          && vt.MaYeucau == maYeucau 
                          && vt.TrangThai == "Đã xuất kho")
                .ToList();
            
            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] Số lượng vật tư tìm được: {vatTuXuatKho.Count}");
            if (!vatTuXuatKho.Any())
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ⚠️ Không tìm thấy vật tư nào với điều kiện: MaXuatkho={maXuatkho}, MaYeucau={maYeucau}, TrangThai='Đã xuất kho'");
            }

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuXuatKho.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:left;padding:10px;'>Hãng SX</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                foreach (var vt in vatTuXuatKho)
                {
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.HangSX ?? ""}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "</tbody></table>";
            }

            var phieuxuatkho = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == maXuatkho);
            var thoiGian = phieuxuatkho?.NgayXuatkho ?? DateTime.Now;

            var subject = $"Vật tư đã được xuất kho - {maYeucau}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Vật tư từ yêu cầu của bạn đã được xuất kho:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Mã phiếu xuất kho:</strong> {maXuatkho}</li>
  <li><strong>Thời gian:</strong> {thoiGian:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Danh sách vật tư xuất kho:</strong></p>
{tableRows}
<p>Vui lòng đến kho để nhận vật tư.</p>";

            var body = BuildEmailTemplate("Thông báo xuất kho", contentHtml);

            Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] Gọi SendEmailAsync...");
            var emailSent = await SendEmailAsync(toEmail, subject, body);
            if (emailSent)
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ✅ Đã gửi email thành công cho {toEmail}");
            }
            else
            {
                Debug.WriteLine($"[EmailService/SendNotificationToRequesterOnIssueAsync] ❌ Gửi email thất bại cho {toEmail}");
            }
        }

        public async Task SendNotificationToWarehouseOnNhapKhoAsync(string maNhapkho)
        {
            var nhanVienKho = _context.nguoidungs
                .Where(n => n.Bophan == "BP kho")
                .ToList();

            foreach (var nv in nhanVienKho)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên kho MaNguoidung = {nv.MaNguoidung}. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Phiếu nhập kho cần xử lý - {maNhapkho}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Bạn có một phiếu nhập kho cần xử lý:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  <li><strong>Trạng thái:</strong> <span style='color: #27ae60;'>Chờ nhập kho</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>";

                var body = BuildEmailTemplate("Thông báo phiếu nhập kho", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToWarehouseOnXuatKhoAsync(string maXuatkho, string maYeucau)
        {
            var nhanVienKho = _context.nguoidungs
                .Where(n => n.Bophan == "BP kho")
                .ToList();

            // Lấy thông tin phiếu xuất kho
            var phieuxuatkho = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == maXuatkho);
            if (phieuxuatkho == null) return;

            // Lấy danh sách vật tư đã xuất kho
            var vatTuXuatKho = _context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == maXuatkho && vt.TrangThai == "Đã xuất kho")
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuXuatKho.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:left;padding:10px;'>Hãng SX</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                foreach (var vt in vatTuXuatKho)
                {
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.HangSX ?? ""}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "</tbody></table>";
            }

            var thoiGian = phieuxuatkho.NgayXuatkho ?? DateTime.Now;

            foreach (var nv in nhanVienKho)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên kho MaNguoidung = {nv.MaNguoidung} khi xuất kho. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Đã xuất kho - {maXuatkho}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Phiếu xuất kho đã được hoàn thành:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Mã phiếu xuất kho:</strong> {maXuatkho}</li>
  <li><strong>Thời gian:</strong> {thoiGian:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Danh sách vật tư đã xuất kho:</strong></p>
{tableRows}
<p>Vui lòng kiểm tra và cập nhật hệ thống.</p>";

                var body = BuildEmailTemplate("Thông báo đã xuất kho", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToRequesterOnNhapKhoAsync(string maNhapkho)
        {
            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkho);
            if (phieunhapkho == null) return;

            // Lấy thông tin người yêu cầu
            string? maNguoiYeuCau = null;
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);
                if (yeucau != null)
                {
                    maNguoiYeuCau = yeucau.YCMaNguoidung;
                }
            }

            if (string.IsNullOrEmpty(maNguoiYeuCau) && !string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
            {
                maNguoiYeuCau = phieunhapkho.MaNguoidung;
            }

            if (string.IsNullOrEmpty(maNguoiYeuCau)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNguoiYeuCau);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi nhập kho. Bỏ qua gửi mail.");
                return;
            }

            // Lấy danh sách vật tư đã nhập kho
            var vatTuNhapKho = _context.vtphieunhapkho
                .Where(vt => vt.MaNhapkho == maNhapkho && vt.TrangThai == "Đã nhập kho")
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuNhapKho.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:left;padding:10px;'>Hãng SX</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                foreach (var vt in vatTuNhapKho)
                {
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.HangSX ?? ""}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "</tbody></table>";
            }

            var thoiGian = phieunhapkho.NgayNhapkho ?? DateTime.Now;
            var maYeucau = phieunhapkho.MaYeucau ?? "";

            var subject = $"Vật tư đã được nhập kho - {maNhapkho}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Vật tư đã được nhập kho thành công:</p>
<ul>
  <li><strong>Mã phiếu nhập kho:</strong> {maNhapkho}</li>
  {(string.IsNullOrEmpty(maYeucau) ? "" : $"<li><strong>Mã yêu cầu:</strong> {maYeucau}</li>")}
  <li><strong>Thời gian:</strong> {thoiGian:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Danh sách vật tư đã nhập kho:</strong></p>
{tableRows}
<p>Vật tư đã sẵn sàng để sử dụng.</p>";

            var body = BuildEmailTemplate("Thông báo nhập kho", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToDirectorOnBaoGiaAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang == null) return;

            // Lấy danh sách vật tư đã báo giá
            var vatTuBaoGia = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == maMuahang && vt.TrangThai == "Đã báo giá" && vt.DonGia != null && vt.DonGia > 0)
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuBaoGia.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:right;padding:10px;'>Đơn giá</th>" +
                    "<th style='text-align:right;padding:10px;'>Thành tiền</th>" +
                    "<th style='text-align:left;padding:10px;'>Ngày thanh toán</th>" +
                    "<th style='text-align:left;padding:10px;'>Ghi chú</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                decimal tongTien = 0;
                foreach (var vt in vatTuBaoGia)
                {
                    var thanhTien = vt.ThanhTien ?? (vt.DonGia ?? 0) * (vt.SL ?? 0);
                    tongTien += thanhTien;
                    
                    // Format ngày thanh toán - hiển thị cả BP Mua hàng và Giám đốc
                    var ngayThanhToanText = "";
                    if (vt.NgayThanhToanBPMuahang.HasValue || vt.NgayThanhToanGiamdoc.HasValue)
                    {
                        var parts = new List<string>();
                        if (vt.NgayThanhToanBPMuahang.HasValue)
                        {
                            parts.Add($"BP Mua hàng: {vt.NgayThanhToanBPMuahang.Value:dd/MM/yyyy}");
                        }
                        if (vt.NgayThanhToanGiamdoc.HasValue)
                        {
                            parts.Add($"Giám đốc: {vt.NgayThanhToanGiamdoc.Value:dd/MM/yyyy}");
                        }
                        ngayThanhToanText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ngayThanhToanText = "-";
                    }
                    
                    // Format ghi chú - hiển thị cả BP Mua hàng và Giám đốc
                    var ghiChuText = "";
                    if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang) || !string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang))
                        {
                            parts.Add($"<strong>BP Mua hàng:</strong> {vt.GhiChuBPMuahang}");
                        }
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                        {
                            parts.Add($"<strong>Giám đốc:</strong> {vt.GhiChuGiamdoc}");
                        }
                        ghiChuText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ghiChuText = "-";
                    }
                    
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{vt.DonGia ?? 0:N0}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{thanhTien:N0}</td>" +
                        $"<td style='padding:8px;'>{ngayThanhToanText}</td>" +
                        $"<td style='padding:8px;'>{ghiChuText}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "<tr style='background-color:#e8f5e9;font-weight:bold;'>" +
                    $"<td colspan='6' style='padding:8px;text-align:right;'>Tổng cộng:</td>" +
                    $"<td style='padding:8px;text-align:right;'>{tongTien:N0}</td>" +
                    $"<td colspan='2' style='padding:8px;'></td>" +
                    "</tr>";
                tableRows += "</tbody></table>";
            }

            var giamDoc = _context.nguoidungs
                .Where(n => n.Chucvu == "Giám đốc")
                .ToList();

            var yeucauUrl = $"{_baseUrl}/Giamdoc/Yeucau/Phieumuahang?search={Uri.EscapeDataString(maMuahang)}";

            foreach (var gd in giamDoc)
            {
                var toEmail = GetEffectiveEmail(gd.MaNguoidung, gd.TenNguoidung, gd.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho Giám đốc MaNguoidung = {gd.MaNguoidung} khi báo giá. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Báo giá cần phê duyệt - {maMuahang}";
                var contentHtml = $@"
<p>Kính gửi <strong>{gd.TenNguoidung}</strong>,</p>
<p>Bạn có một báo giá cần phê duyệt:</p>
<ul>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau ?? ""}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Chi tiết báo giá:</strong></p>
{tableRows}
<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Xem và phê duyệt báo giá
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>";

                var body = BuildEmailTemplate("Thông báo báo giá cần phê duyệt", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToRequesterOnBaoGiaAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang == null || string.IsNullOrEmpty(phieumuahang.MaYeucau)) return;

            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);
            if (yeucau == null || string.IsNullOrEmpty(yeucau.YCMaNguoidung)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi báo giá. Bỏ qua gửi mail.");
                return;
            }

            // Lấy danh sách vật tư đã báo giá
            var vatTuBaoGia = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == maMuahang && vt.TrangThai == "Đã báo giá" && vt.DonGia != null && vt.DonGia > 0)
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuBaoGia.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:right;padding:10px;'>Đơn giá</th>" +
                    "<th style='text-align:right;padding:10px;'>Thành tiền</th>" +
                    "<th style='text-align:left;padding:10px;'>Ngày thanh toán</th>" +
                    "<th style='text-align:left;padding:10px;'>Ghi chú</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                decimal tongTien = 0;
                foreach (var vt in vatTuBaoGia)
                {
                    var thanhTien = vt.ThanhTien ?? (vt.DonGia ?? 0) * (vt.SL ?? 0);
                    tongTien += thanhTien;
                    
                    // Format ngày thanh toán - hiển thị cả BP Mua hàng và Giám đốc
                    var ngayThanhToanText = "";
                    if (vt.NgayThanhToanBPMuahang.HasValue || vt.NgayThanhToanGiamdoc.HasValue)
                    {
                        var parts = new List<string>();
                        if (vt.NgayThanhToanBPMuahang.HasValue)
                        {
                            parts.Add($"BP Mua hàng: {vt.NgayThanhToanBPMuahang.Value:dd/MM/yyyy}");
                        }
                        if (vt.NgayThanhToanGiamdoc.HasValue)
                        {
                            parts.Add($"Giám đốc: {vt.NgayThanhToanGiamdoc.Value:dd/MM/yyyy}");
                        }
                        ngayThanhToanText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ngayThanhToanText = "-";
                    }
                    
                    // Format ghi chú - hiển thị cả BP Mua hàng và Giám đốc
                    var ghiChuText = "";
                    if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang) || !string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang))
                        {
                            parts.Add($"<strong>BP Mua hàng:</strong> {vt.GhiChuBPMuahang}");
                        }
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                        {
                            parts.Add($"<strong>Giám đốc:</strong> {vt.GhiChuGiamdoc}");
                        }
                        ghiChuText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ghiChuText = "-";
                    }
                    
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{vt.DonGia ?? 0:N0}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{thanhTien:N0}</td>" +
                        $"<td style='padding:8px;'>{ngayThanhToanText}</td>" +
                        $"<td style='padding:8px;'>{ghiChuText}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "<tr style='background-color:#e8f5e9;font-weight:bold;'>" +
                    $"<td colspan='6' style='padding:8px;text-align:right;'>Tổng cộng:</td>" +
                    $"<td style='padding:8px;text-align:right;'>{tongTien:N0}</td>" +
                    $"<td colspan='2' style='padding:8px;'></td>" +
                    "</tr>";
                tableRows += "</tbody></table>";
            }

            var subject = $"Báo giá đã được cập nhật - {phieumuahang.MaYeucau}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Báo giá cho yêu cầu của bạn đã được cập nhật:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau}</li>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Chi tiết báo giá:</strong></p>
{tableRows}
<p>Báo giá đang chờ phê duyệt từ Giám đốc.</p>";

            var body = BuildEmailTemplate("Thông báo báo giá", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
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
                    var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                    if (string.IsNullOrWhiteSpace(toEmail))
                    {
                        Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên mua hàng MaNguoidung = {nv.MaNguoidung} khi thông báo thanh toán. Bỏ qua gửi mail.");
                        continue;
                    }

                    var subject = $"Phiếu mua hàng đã được thanh toán - {maMuahang}";
                    var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Phiếu mua hàng đã được kế toán thanh toán:</p>
<ul>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau ?? ""}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p>Vui lòng đăng nhập hệ thống để xem chi tiết.</p>";

                    var body = BuildEmailTemplate("Thông báo thanh toán", contentHtml);

                    await SendEmailAsync(toEmail, subject, body);
                }
            }
        }
        

        public async Task SendNotificationToAccountingOnApprovalAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang == null) return;

            // Lấy danh sách vật tư đã được duyệt
            var vatTuDuyet = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == maMuahang && vt.TrangThai == "Chờ thanh toán" && vt.DonGia != null && vt.DonGia > 0)
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuDuyet.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:right;padding:10px;'>Đơn giá</th>" +
                    "<th style='text-align:right;padding:10px;'>Thành tiền</th>" +
                    "<th style='text-align:left;padding:10px;'>Ngày thanh toán</th>" +
                    "<th style='text-align:left;padding:10px;'>Ghi chú</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                decimal tongTien = 0;
                foreach (var vt in vatTuDuyet)
                {
                    var thanhTien = vt.ThanhTien ?? (vt.DonGia ?? 0) * (vt.SL ?? 0);
                    tongTien += thanhTien;
                    
                    // Format ngày thanh toán - hiển thị cả BP Mua hàng và Giám đốc
                    var ngayThanhToanText = "";
                    if (vt.NgayThanhToanBPMuahang.HasValue || vt.NgayThanhToanGiamdoc.HasValue)
                    {
                        var parts = new List<string>();
                        if (vt.NgayThanhToanBPMuahang.HasValue)
                        {
                            parts.Add($"BP Mua hàng: {vt.NgayThanhToanBPMuahang.Value:dd/MM/yyyy}");
                        }
                        if (vt.NgayThanhToanGiamdoc.HasValue)
                        {
                            parts.Add($"Giám đốc: {vt.NgayThanhToanGiamdoc.Value:dd/MM/yyyy}");
                        }
                        ngayThanhToanText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ngayThanhToanText = "-";
                    }
                    
                    // Format ghi chú - hiển thị cả BP Mua hàng và Giám đốc
                    var ghiChuText = "";
                    if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang) || !string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang))
                        {
                            parts.Add($"<strong>BP Mua hàng:</strong> {vt.GhiChuBPMuahang}");
                        }
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                        {
                            parts.Add($"<strong>Giám đốc:</strong> {vt.GhiChuGiamdoc}");
                        }
                        ghiChuText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ghiChuText = "-";
                    }
                    
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{vt.DonGia ?? 0:N0}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{thanhTien:N0}</td>" +
                        $"<td style='padding:8px;'>{ngayThanhToanText}</td>" +
                        $"<td style='padding:8px;'>{ghiChuText}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "<tr style='background-color:#e8f5e9;font-weight:bold;'>" +
                    $"<td colspan='6' style='padding:8px;text-align:right;'>Tổng cộng:</td>" +
                    $"<td style='padding:8px;text-align:right;'>{tongTien:N0}</td>" +
                    $"<td colspan='2' style='padding:8px;'></td>" +
                    "</tr>";
                tableRows += "</tbody></table>";
            }

            // Lấy danh sách trưởng kế toán
            var truongKeToan = _context.nguoidungs
                .Where(n => n.Bophan == "BP kế toán" && n.Chucvu == "Trưởng BP")
                .ToList();

            var yeucauUrl = $"{_baseUrl}/TruongBPKetoan/Yeucau/Phieumuahang?search={Uri.EscapeDataString(maMuahang)}";

            foreach (var nv in truongKeToan)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho trưởng kế toán MaNguoidung = {nv.MaNguoidung} khi giám đốc duyệt. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Phiếu mua hàng cần thanh toán - {maMuahang}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Phiếu mua hàng đã được Giám đốc duyệt và cần thanh toán:</p>
<ul>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau ?? ""}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Chi tiết phiếu mua hàng:</strong></p>
{tableRows}
<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Xem và xử lý thanh toán
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>";

                var body = BuildEmailTemplate("Thông báo phiếu mua hàng cần thanh toán", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToAccountingOnRequestApprovalAsync(string maYeucau)
        {
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null) return;

            // Lấy danh sách trưởng kế toán và nhân viên kế toán
            var nhanVienKeToan = _context.nguoidungs
                .Where(n => n.Bophan == "BP kế toán")
                .ToList();

            var yeucauUrl = $"{_baseUrl}/TruongBPKetoan/Yeucau/Yeucau?search={Uri.EscapeDataString(maYeucau)}";

            foreach (var nv in nhanVienKeToan)
            {
                var toEmail = GetEffectiveEmail(nv.MaNguoidung, nv.TenNguoidung, nv.Email);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    Debug.WriteLine($"⚠️ Không tìm được email cho nhân viên kế toán MaNguoidung = {nv.MaNguoidung} khi giám đốc duyệt yêu cầu. Bỏ qua gửi mail.");
                    continue;
                }

                var subject = $"Yêu cầu vật tư đã được Giám đốc duyệt - {maYeucau}";
                var contentHtml = $@"
<p>Kính gửi <strong>{nv.TenNguoidung}</strong>,</p>
<p>Yêu cầu vật tư đã được Giám đốc duyệt và có thể cần thanh toán:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Người yêu cầu:</strong> {yeucau.NguoiYeucau ?? ""}</li>
  <li><strong>Bộ phận:</strong> {yeucau.Bophan ?? ""}</li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p style='margin:20px 0;'>
  <a href='{yeucauUrl}'
     style='display:inline-block;padding:10px 18px;
            background:#27ae60;color:#fff;
            text-decoration:none;font-weight:bold;'>
     Xem chi tiết yêu cầu
  </a>
</p>
<p>Nếu nút trên không hoạt động, bạn có thể dán link sau vào trình duyệt:</p>
<p style='font-size:12px;word-break:break-all;'>
  {yeucauUrl}
</p>";

                var body = BuildEmailTemplate("Thông báo yêu cầu đã được duyệt", contentHtml);

                await SendEmailAsync(toEmail, subject, body);
            }
        }

        public async Task SendNotificationToRequesterOnApprovalAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang == null || string.IsNullOrEmpty(phieumuahang.MaYeucau)) return;

            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);
            if (yeucau == null || string.IsNullOrEmpty(yeucau.YCMaNguoidung)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi giám đốc duyệt mua hàng. Bỏ qua gửi mail.");
                return;
            }

            // Lấy danh sách vật tư đã được duyệt
            var vatTuDuyet = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == maMuahang && vt.TrangThai == "Chờ thanh toán" && vt.DonGia != null && vt.DonGia > 0)
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuDuyet.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:right;padding:10px;'>Đơn giá</th>" +
                    "<th style='text-align:right;padding:10px;'>Thành tiền</th>" +
                    "<th style='text-align:left;padding:10px;'>Ngày thanh toán</th>" +
                    "<th style='text-align:left;padding:10px;'>Ghi chú</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                decimal tongTien = 0;
                foreach (var vt in vatTuDuyet)
                {
                    var thanhTien = vt.ThanhTien ?? (vt.DonGia ?? 0) * (vt.SL ?? 0);
                    tongTien += thanhTien;
                    
                    // Format ngày thanh toán - hiển thị cả BP Mua hàng và Giám đốc
                    var ngayThanhToanText = "";
                    if (vt.NgayThanhToanBPMuahang.HasValue || vt.NgayThanhToanGiamdoc.HasValue)
                    {
                        var parts = new List<string>();
                        if (vt.NgayThanhToanBPMuahang.HasValue)
                        {
                            parts.Add($"BP Mua hàng: {vt.NgayThanhToanBPMuahang.Value:dd/MM/yyyy}");
                        }
                        if (vt.NgayThanhToanGiamdoc.HasValue)
                        {
                            parts.Add($"Giám đốc: {vt.NgayThanhToanGiamdoc.Value:dd/MM/yyyy}");
                        }
                        ngayThanhToanText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ngayThanhToanText = "-";
                    }
                    
                    // Format ghi chú - hiển thị cả BP Mua hàng và Giám đốc
                    var ghiChuText = "";
                    if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang) || !string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuBPMuahang))
                        {
                            parts.Add($"<strong>BP Mua hàng:</strong> {vt.GhiChuBPMuahang}");
                        }
                        if (!string.IsNullOrWhiteSpace(vt.GhiChuGiamdoc))
                        {
                            parts.Add($"<strong>Giám đốc:</strong> {vt.GhiChuGiamdoc}");
                        }
                        ghiChuText = string.Join("<br>", parts);
                    }
                    else
                    {
                        ghiChuText = "-";
                    }
                    
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{vt.DonGia ?? 0:N0}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{thanhTien:N0}</td>" +
                        $"<td style='padding:8px;'>{ngayThanhToanText}</td>" +
                        $"<td style='padding:8px;'>{ghiChuText}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "<tr style='background-color:#e8f5e9;font-weight:bold;'>" +
                    $"<td colspan='6' style='padding:8px;text-align:right;'>Tổng cộng:</td>" +
                    $"<td style='padding:8px;text-align:right;'>{tongTien:N0}</td>" +
                    $"<td colspan='2' style='padding:8px;'></td>" +
                    "</tr>";
                tableRows += "</tbody></table>";
            }

            var subject = $"Phiếu mua hàng đã được Giám đốc duyệt - {phieumuahang.MaYeucau}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Phiếu mua hàng cho yêu cầu của bạn đã được Giám đốc duyệt:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau}</li>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Trạng thái:</strong> <span style='color: #27ae60;'>Chờ thanh toán</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Chi tiết phiếu mua hàng:</strong></p>
{tableRows}
<p>Phiếu mua hàng đang chờ kế toán thanh toán.</p>";

            var body = BuildEmailTemplate("Thông báo phiếu mua hàng đã được duyệt", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToRequesterOnPaymentAsync(string maMuahang)
        {
            var phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
            if (phieumuahang == null || string.IsNullOrEmpty(phieumuahang.MaYeucau)) return;

            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);
            if (yeucau == null || string.IsNullOrEmpty(yeucau.YCMaNguoidung)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi kế toán thanh toán. Bỏ qua gửi mail.");
                return;
            }

            // Lấy danh sách vật tư đã thanh toán
            var vatTuThanhToan = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == maMuahang && vt.TrangThai == "Đã thanh toán" && vt.DonGia != null && vt.DonGia > 0)
                .ToList();

            // Tạo bảng vật tư
            var tableRows = "";
            if (vatTuThanhToan.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#f2f2f2;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:right;padding:10px;'>Đơn giá</th>" +
                    "<th style='text-align:right;padding:10px;'>Thành tiền</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                decimal tongTien = 0;
                foreach (var vt in vatTuThanhToan)
                {
                    var thanhTien = vt.ThanhTien ?? (vt.DonGia ?? 0) * (vt.SL ?? 0);
                    tongTien += thanhTien;
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{vt.DonGia ?? 0:N0}</td>" +
                        $"<td style='padding:8px;text-align:right;'>{thanhTien:N0}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "<tr style='background-color:#e8f5e9;font-weight:bold;'>" +
                    $"<td colspan='6' style='padding:8px;text-align:right;'>Tổng cộng:</td>" +
                    $"<td style='padding:8px;text-align:right;'>{tongTien:N0}</td>" +
                    "</tr>";
                tableRows += "</tbody></table>";
            }

            var subject = $"Phiếu mua hàng đã được thanh toán - {phieumuahang.MaYeucau}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Phiếu mua hàng cho yêu cầu của bạn đã được kế toán thanh toán:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {phieumuahang.MaYeucau}</li>
  <li><strong>Mã phiếu mua hàng:</strong> {maMuahang}</li>
  <li><strong>Trạng thái:</strong> <span style='color: #27ae60;'>Đã thanh toán</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p><strong>Chi tiết phiếu mua hàng đã thanh toán:</strong></p>
{tableRows}
<p>Phiếu mua hàng đã được thanh toán và đang chờ bộ phận mua hàng nhận hàng.</p>";

            var body = BuildEmailTemplate("Thông báo phiếu mua hàng đã thanh toán", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToRequesterOnRejectionAsync(string maYeucau, string ghiChu = "")
        {
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null || string.IsNullOrEmpty(yeucau.YCMaNguoidung)) return;

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null) return;

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung} khi giám đốc từ chối. Bỏ qua gửi mail.");
                return;
            }

            // Lấy danh sách vật tư bị từ chối
            var vatTuBiTuChoi = _context.vtyeucau
                .Where(vt => vt.VTMaYeucau == maYeucau && vt.TrangThai != null && vt.TrangThai.Contains("Đã từ chối"))
                .ToList();

            // Tạo bảng vật tư bị từ chối
            var tableRows = "";
            if (vatTuBiTuChoi.Any())
            {
                tableRows = "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;margin:20px 0;'>" +
                    "<thead style='background-color:#fee;'>" +
                    "<tr>" +
                    "<th style='text-align:left;padding:10px;'>STT</th>" +
                    "<th style='text-align:left;padding:10px;'>Mã sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Tên sản phẩm</th>" +
                    "<th style='text-align:left;padding:10px;'>Số lượng</th>" +
                    "<th style='text-align:left;padding:10px;'>Đơn vị</th>" +
                    "<th style='text-align:left;padding:10px;'>Ghi chú</th>" +
                    "</tr>" +
                    "</thead>" +
                    "<tbody>";

                int stt = 1;
                foreach (var vt in vatTuBiTuChoi)
                {
                    tableRows += "<tr>" +
                        $"<td style='padding:8px;'>{stt}</td>" +
                        $"<td style='padding:8px;'>{vt.MaSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.TenSanpham ?? ""}</td>" +
                        $"<td style='padding:8px;'>{vt.SL ?? 0}</td>" +
                        $"<td style='padding:8px;'>{vt.DonVi ?? ""}</td>" +
                        $"<td style='padding:8px;'>{(!string.IsNullOrEmpty(vt.GhiChu) ? vt.GhiChu : "")}</td>" +
                        "</tr>";
                    stt++;
                }

                tableRows += "</tbody></table>";
            }

            var subject = $"Yêu cầu vật tư bị từ chối - {maYeucau}";
            var contentHtml = $@"<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Rất tiếc, yêu cầu vật tư của bạn đã bị Giám đốc từ chối:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Trạng thái:</strong> <span style='color: #e74c3c;'>Giám đốc - Đã từ chối</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
{(string.IsNullOrWhiteSpace(ghiChu) ? "" : $"<p><strong>Ghi chú từ chối:</strong> {ghiChu}</p>")}
{(vatTuBiTuChoi.Any() ? $"<p><strong>Danh sách vật tư bị từ chối:</strong></p>{tableRows}" : "")}
<p>Vui lòng kiểm tra lại yêu cầu hoặc liên hệ Giám đốc để biết thêm chi tiết.</p>";

            var body = BuildEmailTemplate("Thông báo yêu cầu bị từ chối", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendNotificationToRequesterOnVatTuStatusChangeAsync(string maYeucau, string maXuatkho, string tenVatTu, string trangThaiMoi)
        {
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null || string.IsNullOrEmpty(yeucau.YCMaNguoidung))
            {
                Debug.WriteLine($"⚠️ Không tìm thấy yêu cầu hoặc người yêu cầu cho MaYeucau = {maYeucau}. Bỏ qua gửi mail.");
                return;
            }

            var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung);
            if (nguoiDung == null)
            {
                Debug.WriteLine($"⚠️ Không tìm thấy người dùng với MaNguoidung = {yeucau.YCMaNguoidung}. Bỏ qua gửi mail.");
                return;
            }

            var toEmail = GetEffectiveEmail(nguoiDung.MaNguoidung, nguoiDung.TenNguoidung, nguoiDung.Email);
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Debug.WriteLine($"⚠️ Không tìm được email cho người yêu cầu MaNguoidung = {nguoiDung.MaNguoidung}. Bỏ qua gửi mail.");
                return;
            }

            var subject = $"Cập nhật trạng thái vật tư - {maYeucau}";
            var contentHtml = $@"
<p>Kính gửi <strong>{nguoiDung.TenNguoidung}</strong>,</p>
<p>Trạng thái vật tư trong phiếu xuất kho đã được cập nhật:</p>
<ul>
  <li><strong>Mã yêu cầu:</strong> {maYeucau}</li>
  <li><strong>Mã phiếu xuất kho:</strong> {maXuatkho}</li>
  <li><strong>Vật tư:</strong> {tenVatTu}</li>
  <li><strong>Trạng thái mới:</strong> <span style='color: #27ae60;'>{trangThaiMoi}</span></li>
  <li><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
</ul>
<p>Vui lòng đăng nhập hệ thống để xem chi tiết.</p>";

            var body = BuildEmailTemplate("Thông báo cập nhật trạng thái vật tư", contentHtml);

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}


