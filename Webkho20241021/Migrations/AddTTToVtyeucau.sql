-- Migration: Thêm cột TT vào bảng vtyeucau
-- Ngày tạo: 2026-02-02
-- Mô tả: Lưu số thứ tự theo file Excel (ví dụ: 1, 1.1, 1.2...)

ALTER TABLE `vtyeucau`
ADD COLUMN `TT` VARCHAR(50) NULL DEFAULT NULL COMMENT 'Số thứ tự theo file Excel (vd: 1.1, 1.2)' AFTER `ID`;

-- Kiểm tra:
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
-- FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_SCHEMA = DATABASE()
--   AND TABLE_NAME = 'vtyeucau'
--   AND COLUMN_NAME = 'TT';

