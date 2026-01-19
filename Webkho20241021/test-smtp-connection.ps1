# Script kiểm tra kết nối SMTP trên Windows Server
# Chạy script này với quyền Administrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KIỂM TRA KẾT NỐI SMTP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$smtpServer = "pro01.emailserver.vn"
$smtpPort = 465

Write-Host "1. Kiểm tra DNS Resolution..." -ForegroundColor Yellow
try {
    $dnsResult = Resolve-DnsName -Name $smtpServer -ErrorAction Stop
    Write-Host "   ✅ DNS Resolution thành công" -ForegroundColor Green
    Write-Host "   IP Address: $($dnsResult[0].IPAddress)" -ForegroundColor Gray
} catch {
    Write-Host "   ❌ Không thể resolve DNS: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. Kiểm tra kết nối TCP đến port $smtpPort..." -ForegroundColor Yellow
try {
    $tcpTest = Test-NetConnection -ComputerName $smtpServer -Port $smtpPort -WarningAction SilentlyContinue
    if ($tcpTest.TcpTestSucceeded) {
        Write-Host "   ✅ Kết nối TCP thành công" -ForegroundColor Green
        Write-Host "   Remote Address: $($tcpTest.RemoteAddress)" -ForegroundColor Gray
        Write-Host "   Remote Port: $($tcpTest.RemotePort)" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ Không thể kết nối đến port $smtpPort" -ForegroundColor Red
        Write-Host "   ⚠️  Có thể do firewall chặn port $smtpPort (outbound)" -ForegroundColor Yellow
        Write-Host "   ⚠️  Giải pháp: Mở port $smtpPort (outbound) trên firewall" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ❌ Lỗi khi test kết nối: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "3. Kiểm tra Firewall Rules..." -ForegroundColor Yellow
$firewallRules = Get-NetFirewallRule | Where-Object {
    ($_.DisplayName -like "*SMTP*" -or $_.DisplayName -like "*465*" -or $_.DisplayName -like "*Email*") -and
    $_.Direction -eq "Outbound"
}

if ($firewallRules) {
    Write-Host "   ✅ Tìm thấy firewall rules liên quan:" -ForegroundColor Green
    foreach ($rule in $firewallRules) {
        Write-Host "      - $($rule.DisplayName) (Enabled: $($rule.Enabled))" -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  Không tìm thấy firewall rule cho SMTP port $smtpPort" -ForegroundColor Yellow
    Write-Host "   💡 Có thể cần tạo rule mới:" -ForegroundColor Cyan
    Write-Host "      New-NetFirewallRule -DisplayName 'SMTP Outbound 465' -Direction Outbound -LocalPort 465 -Protocol TCP -Action Allow" -ForegroundColor Gray
}

Write-Host ""
Write-Host "4. Kiểm tra biến môi trường EmailSettings..." -ForegroundColor Yellow
$emailPassword = [Environment]::GetEnvironmentVariable("EmailSettings__FromPassword", "Machine")
if ([string]::IsNullOrEmpty($emailPassword)) {
    $emailPassword = [Environment]::GetEnvironmentVariable("EmailSettings__FromPassword", "User")
}

if ([string]::IsNullOrEmpty($emailPassword)) {
    Write-Host "   ⚠️  Không tìm thấy biến môi trường EmailSettings__FromPassword" -ForegroundColor Yellow
    Write-Host "   💡 Kiểm tra appsettings.Production.json có FromPassword không rỗng" -ForegroundColor Cyan
} else {
    Write-Host "   ✅ Tìm thấy biến môi trường EmailSettings__FromPassword" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KẾT QUẢ KIỂM TRA" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Nếu tất cả đều ✅, nhưng vẫn không gửi được email:" -ForegroundColor Yellow
Write-Host "1. Kiểm tra logs của ứng dụng để xem lỗi chi tiết" -ForegroundColor White
Write-Host "2. Kiểm tra appsettings.Production.json có đúng cấu hình không" -ForegroundColor White
Write-Host "3. Kiểm tra mật khẩu email có đúng không" -ForegroundColor White
Write-Host "4. Thử test với telnet hoặc PowerShell Send-MailMessage" -ForegroundColor White
Write-Host ""
