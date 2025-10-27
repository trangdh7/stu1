-- Script để thêm cột NgayTao và GhiChu vào bảng phieumuahang
USE stu;

-- Thêm cột NgayTao
ALTER TABLE phieumuahang ADD COLUMN NgayTao DATETIME NULL;

-- Thêm cột GhiChu  
ALTER TABLE phieumuahang ADD COLUMN GhiChu TEXT NULL;

-- Thêm cột GhiChu vào bảng vtphieumuahang
ALTER TABLE vtphieumuahang ADD COLUMN GhiChu TEXT NULL;
