# Hướng dẫn sửa lỗi Deploy: "Can't unlink already-existing object"

## Nguyên nhân
Lỗi xảy ra vì các file DLL đang được sử dụng bởi IIS/ứng dụng đang chạy, không thể ghi đè khi untar.

## Giải pháp

### Cách 1: Dừng IIS trước khi deploy (Khuyến nghị)

Thêm step này **TRƯỚC** step deploy trong workflow:

```yaml
- name: Stop IIS Application Pool
  uses: appleboy/ssh-action@v1.0.0
  with:
    host: ${{ secrets.HOST }}
    username: ${{ secrets.USERNAME }}
    password: ${{ secrets.PASSWORD }}
    port: ${{ secrets.PORT || 22 }}
    script: |
      Import-Module WebAdministration
      Stop-WebAppPool -Name "DefaultAppPool" -ErrorAction SilentlyContinue
      Stop-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
      Start-Sleep -Seconds 5
      Get-Process | Where-Object {$_.Path -like "*C:/inetpub/wwwroot/Webkho20241021*"} | Stop-Process -Force -ErrorAction SilentlyContinue
      Start-Sleep -Seconds 2
```

Và thêm step này **SAU** step deploy:

```yaml
- name: Start IIS Application Pool
  uses: appleboy/ssh-action@v1.0.0
  with:
    host: ${{ secrets.HOST }}
    username: ${{ secrets.USERNAME }}
    password: ${{ secrets.PASSWORD }}
    port: ${{ secrets.PORT || 22 }}
    script: |
      Import-Module WebAdministration
      Start-WebAppPool -Name "DefaultAppPool"
      Start-Website -Name "Default Web Site"
```

### Cách 2: Cập nhật scp-action với options

Cập nhật step deploy hiện tại, thêm các options:

```yaml
- name: Deploy to Windows Server
  uses: appleboy/scp-action@v0.1.7
  with:
    host: ${{ secrets.HOST }}
    username: ${{ secrets.USERNAME }}
    password: ${{ secrets.PASSWORD }}
    port: ${{ secrets.PORT || 22 }}
    source: "publish/*"
    target: "C:/inetpub/wwwroot/Webkho20241021"
    rm: true              # Xóa file cũ trước
    overwrite: true       # Ghi đè file đã tồn tại
    strip_components: 1   # Bỏ thư mục publish/ khi extract
    timeout: 300s         # Tăng timeout
```

### Cách 3: Xóa file cũ bằng PowerShell

Thêm step này trước deploy:

```yaml
- name: Clean old files
  uses: appleboy/ssh-action@v1.0.0
  with:
    host: ${{ secrets.HOST }}
    username: ${{ secrets.USERNAME }}
    password: ${{ secrets.PASSWORD }}
    script: |
      $targetPath = "C:/inetpub/wwwroot/Webkho20241021"
      Import-Module WebAdministration
      Stop-WebAppPool -Name "DefaultAppPool" -ErrorAction SilentlyContinue
      Stop-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
      Start-Sleep -Seconds 3
      if (Test-Path $targetPath) {
        Remove-Item -Path "$targetPath\*" -Recurse -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
      }
```

## Lưu ý

1. **Tên Application Pool và Website**: Thay `"DefaultAppPool"` và `"Default Web Site"` bằng tên thực tế trên server của bạn.

2. **Kiểm tra tên IIS Site**: Chạy lệnh này trên server để xem tên chính xác:
   ```powershell
   Get-Website | Select-Object Name, State
   Get-WebAppPoolState | Select-Object Name, Value
   ```

3. **Backup**: Nên có cơ chế backup trước khi deploy để có thể rollback nếu cần.

4. **Downtime**: Cách 1 sẽ có downtime ngắn (vài giây) khi dừng và khởi động lại IIS.

## Workflow hoàn chỉnh mẫu

Xem file `.github/workflows/deploy.yml` đã được tạo sẵn.

