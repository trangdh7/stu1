-- Tạo bảng excel_files
USE stu;

DROP TABLE IF EXISTS `excelfiles`;
CREATE TABLE `excelfiles` (
    `ID` INT AUTO_INCREMENT PRIMARY KEY,
    `MaYeucau` VARCHAR(255),
    `MaDuan` VARCHAR(255),
    `TenFile` VARCHAR(500),
    `DuongDanFile` VARCHAR(1000),
    `NgayUpload` DATETIME,
    `NguoiUpload` VARCHAR(255),
    `KichThuocFile` BIGINT,
    KEY `FK_excel_files_yeucau` (`MaYeucau`),
    KEY `FK_excel_files_duan` (`MaDuan`),
    CONSTRAINT `FK_excel_files_yeucau` FOREIGN KEY (`MaYeucau`) REFERENCES `yeucau` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `FK_excel_files_duan` FOREIGN KEY (`MaDuan`) REFERENCES `duans` (`MaDuan`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

