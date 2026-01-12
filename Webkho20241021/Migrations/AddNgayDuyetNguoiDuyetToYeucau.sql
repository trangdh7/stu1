-- Script SQL để thêm cột NgayDuyet và NguoiDuyet vào bảng yeucau
-- và đảm bảo bảng vtyeucau có các cột cần thiết
-- Sử dụng cho MySQL

-- ============================================================
-- Thêm cột vào bảng yeucau
-- ============================================================

-- Thêm cột NgayDuyet vào bảng yeucau (để lưu thời gian duyệt yêu cầu)
ALTER TABLE `yeucau`
ADD COLUMN `NgayDuyet` datetime DEFAULT NULL
AFTER `NgayCanHang`;

-- Thêm cột NguoiDuyet vào bảng yeucau (để lưu mã người duyệt)
ALTER TABLE `yeucau`
ADD COLUMN `NguoiDuyet` varchar(50) DEFAULT NULL
AFTER `NgayDuyet`;

-- ============================================================
-- Thêm cột vào bảng vtyeucau (bảng vật tư chi tiết)
-- ============================================================

-- Thêm cột NgayDuyet vào bảng vtyeucau (để lưu thời gian khi giám đốc duyệt vật tư chi tiết)
-- Lưu ý: Cột này rất quan trọng để lưu thời gian duyệt của giám đốc
ALTER TABLE `vtyeucau`
ADD COLUMN `NgayDuyet` datetime DEFAULT NULL
AFTER `NgayCanHang`;

-- Thêm cột GhiChu vào bảng vtyeucau (để lưu ghi chú khi duyệt/từ chối)
ALTER TABLE `vtyeucau`
ADD COLUMN `GhiChu` varchar(500) DEFAULT NULL
AFTER `NgayDuyet`;

-- Thêm cột SLCu vào bảng vtyeucau (số lượng cũ)
ALTER TABLE `vtyeucau`
ADD COLUMN `SLCu` int DEFAULT NULL
AFTER `SL`;

-- Thêm cột SLMoi vào bảng vtyeucau (số lượng mới)
ALTER TABLE `vtyeucau`
ADD COLUMN `SLMoi` int DEFAULT NULL
AFTER `SLCu`;
