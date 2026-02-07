-- Chỉ 1 bảng: lưu tên nhà cung cấp. Khi gõ chữ ở ô NCC thì hiện gợi ý từ bảng này.

USE stu;

CREATE TABLE IF NOT EXISTS `NhaCungCap` (
  `ID` int NOT NULL AUTO_INCREMENT,
  `TenNhaCC` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `GhiChu` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayTao` datetime(6) DEFAULT NULL,
  `NgayCapNhat` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
