# Script để thêm cột vào database MySQL
$connectionString = "Server=localhost;port=3306;Database=stu;user=root;Password=stu@com123;"

try {
    # Tạo kết nối MySQL
    $connection = New-Object MySql.Data.MySqlClient.MySqlConnection($connectionString)
    $connection.Open()
    
    # Thêm cột NgayTao vào bảng phieumuahang
    $command1 = New-Object MySql.Data.MySqlClient.MySqlCommand("ALTER TABLE phieumuahang ADD COLUMN NgayTao DATETIME NULL", $connection)
    $command1.ExecuteNonQuery()
    Write-Host "Đã thêm cột NgayTao vào bảng phieumuahang"
    
    # Thêm cột GhiChu vào bảng phieumuahang
    $command2 = New-Object MySql.Data.MySqlClient.MySqlCommand("ALTER TABLE phieumuahang ADD COLUMN GhiChu TEXT NULL", $connection)
    $command2.ExecuteNonQuery()
    Write-Host "Đã thêm cột GhiChu vào bảng phieumuahang"
    
    # Thêm cột GhiChu vào bảng vtphieumuahang
    $command3 = New-Object MySql.Data.MySqlClient.MySqlCommand("ALTER TABLE vtphieumuahang ADD COLUMN GhiChu TEXT NULL", $connection)
    $command3.ExecuteNonQuery()
    Write-Host "Đã thêm cột GhiChu vào bảng vtphieumuahang"
    
    Write-Host "Hoàn thành thêm cột!"
}
catch {
    Write-Host "Lỗi: $($_.Exception.Message)"
}
finally {
    if ($connection) {
        $connection.Close()
    }
}
