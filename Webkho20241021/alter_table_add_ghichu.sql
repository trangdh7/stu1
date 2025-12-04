-- Script để thêm cột GhiChu vào bảng vtyeucau
-- Chạy script này trong database để thêm cột ghi chú

ALTER TABLE `vtyeucau` 
ADD COLUMN `GhiChu` VARCHAR(50) NULL DEFAULT NULL 
AFTER `TrangThai`;

-- Kiểm tra kết quả
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT 
-- FROM INFORMATION_SCHEMA.COLUMNS 
-- WHERE TABLE_SCHEMA = DATABASE() 
-- AND TABLE_NAME = 'vtyeucau' 
-- AND COLUMN_NAME = 'GhiChu';
