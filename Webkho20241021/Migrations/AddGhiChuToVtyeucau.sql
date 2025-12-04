-- Migration: Thêm cột GhiChu vào bảng vtyeucau
-- Ngày tạo: 2025-12-04
-- Mô tả: Thêm cột GhiChu để lưu ghi chú/lý do từ chối cho vật tư yêu cầu

ALTER TABLE `vtyeucau` 
ADD COLUMN `GhiChu` TEXT NULL DEFAULT NULL COMMENT 'Ghi chú hoặc lý do từ chối vật tư' AFTER `TrangThai`;

-- Kiểm tra xem cột đã được thêm thành công
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
-- FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_SCHEMA = DATABASE()
--   AND TABLE_NAME = 'vtyeucau'
--   AND COLUMN_NAME = 'GhiChu';
