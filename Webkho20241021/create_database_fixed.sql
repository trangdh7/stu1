SET FOREIGN_KEY_CHECKS=0;
SET NAMES utf8mb4;
SET time_zone = '+00:00';

-- drop database stu;
-- create database stu;
USE stu;

-- Table `khotongs`
DROP TABLE IF EXISTS `khotongs`;
CREATE TABLE `khotongs` (
  `TenSanpham` varchar(500) DEFAULT NULL,
  `MaSanpham` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Makho` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) DEFAULT NULL,
  `NhaCC` varchar(255) DEFAULT NULL,
  `SL` int DEFAULT NULL,
  `DonVi` varchar(50) DEFAULT NULL,
  `NgayNhapkho` varchar(50) DEFAULT NULL,
  `NgayBaohanh` varchar(50) DEFAULT NULL,
  `ThoiGianBH` varchar(50) DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  `DuAn` varchar(100) default null,
  `LoaiCapPhat` VARCHAR(100) NULL,
  PRIMARY KEY (`Makho`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `user`
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user` (
  `Id` varchar(255) NOT NULL,
  `Name` longtext,
  `manv` longtext NOT NULL,
  `Chucvu` longtext,
  `Bophan` longtext,
  `UserName` varchar(256) DEFAULT NULL,
  `NormalizedUserName` varchar(256) DEFAULT NULL,
  `Email` varchar(256) DEFAULT NULL,
  `NormalizedEmail` varchar(256) DEFAULT NULL,
  `PasswordHash` longtext,
  `PhoneNumber` longtext,
  `PhoneNumberConfirmed` tinyint(1) NOT NULL,
  `LockoutEnd` datetime DEFAULT NULL,
  `LockoutEnabled` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UserNameIndex` (`NormalizedUserName`),
  KEY `EmailIndex` (`NormalizedEmail`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `user` VALUES 
('1008fbb2-9a72-4052-bbc6-4fc91f74c658','Nguyễn Tiến Hiệp','HIEPNT','Nhân viên','BP kỹ thuật','hiep','HIEP','hiepnt@stu.com',NULL,'AQAAAAIAAYagAAAAEIDVkT82hiFHnh7wg9a8uvUR7uSG6e8fkNg1X2PHG9NnNqnS3AmOmBplA1EOHr5VJQ==','0399019943',0,NULL,1),
('13296b5a-33d8-4af3-bc4e-c89bc7f78daf','Phan Thị Quỳnh','QuynhPT','Quản lí dự án','BP dự án','quynh','QUYNH','quynhstu.com.vn',NULL,'AQAAAAIAAYagAAAAEDhEYT9UOQFryMj1uDpBHa7tZnbwV++0z8sUl2tcZfXGAh/s7Qr15sxU7elUxkn0JA==','0332023172',0,NULL,1),
('132c8296-9ad8-4e37-841e-6cb19ab3db38','Hồ Sỹ Cường','CUONGHS','Nhân viên','BP kỹ thuật','cuong','CUONG','Cuonghs@stu.com','CUONGHS@STU.COM','AQAAAAIAAYagAAAAEEyxvVJgT4FwmCVTm/G2ePH4+2+1zwF8h1gDjcKGrrDUPDLqHDmR2iYo+8fOmm0EXw==','0973557738',0,NULL,1),
('15f327c4-48ba-4f73-96dd-77924b3d8c83','Đặng Thị Thu','THUDT','Trưởng BP','BP kho','thu','THU','thudt@stu.com',NULL,'AQAAAAIAAYagAAAAEMgaazPaT1Ar2QOS0mz9miMiTcsMFNbnvjzzTTAVqPamb0bQz38Xi8wt77Wfdk0fIg==','0989030761',0,NULL,1),
('254481a2-b47d-42e6-bb2f-190830838a34','Nguyễn Hoàng Nam','NAMNH','Nhân viên','BP kỹ thuật','Nam','NAM','Nam@gmail.com',NULL,'AQAAAAIAAYagAAAAEEJggIy+zwdVcfUoXjl9JOIe21+lC7LspPoyg5kimlRrXFZHyE3Heh5/I9RlUBItHg==','0123456789',0,NULL,1),
('9dcd42e9-1ffd-41b9-8b60-61f9445f98fa','Đặng Thị Thu','THUDTMH','Trưởng BP','BP mua hàng','thumh','THUMH','thudt@stu.com.vn',NULL,'AQAAAAIAAYagAAAAECgmNdvVbPZZSHXa/ScrFuD0V+XB/g7RtM98Ve1cg+Eky8fSQj9An5Jm0slDd6wkNw==','0983160903',0,NULL,1),
('bc7882af-0849-4820-9254-0cd2a3d45bb6','Nguyễn Mậu Phương','PHUONGNM','Giám đốc','BP kỹ thuật','phuongnm','PHUONGNM','phuongnm@stu.com',NULL,'AQAAAAIAAYagAAAAEGG0e08OpgB/REMm+46Vg6+7CBa8OsQko8ZowQT/c0K/GEroWLqxY9wmRkaIx9jNDg==','0983160903',0,NULL,1),
('c0d7c574-15c9-4d0f-89e1-e1a9825102f2','ADMIN','ADMIN','Admin','Admin','admin','ADMIN','admin@stu.com.vn',NULL,'AQAAAAIAAYagAAAAEGvgHKxM+4a1FzywGdIdJINnbpaFQj9iikzRI7IkcXzWHCiO8MqixaU+DavUMiKM7g==','0399019943',0,NULL,1),
('c127b200-8de2-44b0-b128-69e019930d6d','Tạ Thị Thúy','THUYTT','Trưởng BP','BP kế toán','thuy','THUY','thuytt@stu.com.vn',NULL,'AQAAAAIAAYagAAAAEKgOqnlv62bho8dSTBpxILBuG9vaWOO7IEU/SVRDMk1vsFrdWpoorIStayHC+Ux5cg==','0973557738',0,NULL,1),
('c66f20b6-3484-442d-aa53-26115c598b7c','Hoàng Văn Tuân','TUANHV','Trưởng BP','BP kỹ thuật','tuan','TUAN','tuanhv@stu.com',NULL,'AQAAAAIAAYagAAAAELRMuOM7v9LqGRkeQ4q2JTZxWIN2y8ytAfVjKV105eFdr7SN8nvNl/SId4ZZcJaHFw==','0349696137',0,NULL,1),
('d72cf654-2e2a-42ee-b4dc-322bd8345504','trọng','fsfsd','Nhân viên','BP kỹ thuật','fsdfs','FSDFS','fsdfsdf@gmail.com',NULL,'AQAAAAIAAYagAAAAEKNFJJ13tgcgz1k718I1pnXq58KL6BwMF2bQYy7qhgc7Wl6xI7u/8QE84hS8VEn5rw==','0842344242',0,NULL,1);

-- Table `nguoidungs`
DROP TABLE IF EXISTS `nguoidungs`;
CREATE TABLE `nguoidungs` (
  `TenNguoidung` varchar(50) DEFAULT NULL,
  `MaNguoidung` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Chucvu` varchar(50) DEFAULT NULL,
  `Bophan` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`MaNguoidung`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `nguoidungs` VALUES 
('ADMIN','ADMIN','Admin','Admin'),
('Hồ Sỹ Cường','CUONGHS','Nhân viên','BP kỹ thuật'),
('trọng','fsfsd','Nhân viên','BP kỹ thuật'),
('Nguyễn Tiến Hiệp','HIEPNT','Nhân viên','BP kỹ thuật'),
('Nguyễn Hoàng Nam','NAMNH','Nhân viên','BP kỹ thuật'),
('Nguyễn Mậu Phương','PHUONGNM','Giám đốc','BP kỹ thuật'),
('Đặng Thị Thu','THUDT','Trưởng BP','BP kho'),
('Đặng Thị Thu','THUDTMH','Trưởng BP','BP mua hàng'),
('Tạ Thị Thúy','THUYTT','Trưởng BP','BP kế toán'),
('Hoàng Văn Tuân','TUANHV','Trưởng BP','BP kỹ thuật');

-- Table `aspnetroles`
DROP TABLE IF EXISTS `aspnetroles`;
CREATE TABLE `aspnetroles` (
  `Id` varchar(255) NOT NULL,
  `Name` varchar(256) DEFAULT NULL,
  `NormalizedName` varchar(256) DEFAULT NULL,
  `ConcurrencyStamp` longtext,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `RoleNameIndex` (`NormalizedName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `aspnetroles` VALUES 
('2faee9c9-3196-48c1-b40c-87d8869f6002','Nhân viên-BP kỹ thuật','NHÂN VIÊN-BP KỸ THUẬT',NULL),
('62e4d649-9cca-456d-97ac-131cec2da3a9','Giám đốc','GIÁM ĐỐC',NULL),
('7289dd62-3021-4143-8652-7c2657f1369c','Admin-Admin','ADMIN-ADMIN',NULL),
('7d7a3566-b079-4a9e-b39b-422afa10689a','Trưởng BP-BP kho','TRƯỞNG BP-BP KHO',NULL),
('9cd108e3-85ec-43db-a54b-ab01d6f508aa','Trưởng BP-BP mua hàng','TRƯỞNG BP-BP MUA HÀNG',NULL),
('9ddea01b-bb77-401d-865c-4591f48761ff','Trưởng BP-BP kỹ thuật','TRƯỞNG BP-BP KỸ THUẬT',NULL),
('f1a6cce6-086b-4cc3-828f-8d444a4532cd','Trưởng BP-BP kế toán','TRƯỞNG BP-BP KẾ TOÁN',NULL);

-- Table `duans`
DROP TABLE IF EXISTS `duans`;
CREATE TABLE `duans` (
  `TenDuan` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaDuan` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `NguoiQLDA` varchar(50) DEFAULT NULL,
  `MaNguoiQLDA` varchar(50) DEFAULT NULL,
  `KhachHang` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayBatdau` datetime DEFAULT NULL,
  `NgayKetthuc` datetime DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`MaDuan`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `__efmigrationshistory`
DROP TABLE IF EXISTS `__efmigrationshistory`;
CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `__efmigrationshistory` VALUES ('20241030040303_CreateIdentitySchema','8.0.10');

-- Table `aspnetuserclaims`
DROP TABLE IF EXISTS `aspnetuserclaims`;
CREATE TABLE `aspnetuserclaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`),
  KEY `IX_AspNetUserClaims_UserId` (`UserId`),
  CONSTRAINT `FK_AspNetUserClaims_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `aspnetuserlogins`
DROP TABLE IF EXISTS `aspnetuserlogins`;
CREATE TABLE `aspnetuserlogins` (
  `LoginProvider` varchar(255) NOT NULL,
  `ProviderKey` varchar(255) NOT NULL,
  `ProviderDisplayName` longtext,
  `UserId` varchar(255) NOT NULL,
  PRIMARY KEY (`LoginProvider`,`ProviderKey`),
  KEY `IX_AspNetUserLogins_UserId` (`UserId`),
  CONSTRAINT `FK_AspNetUserLogins_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `aspnetusertokens`
DROP TABLE IF EXISTS `aspnetusertokens`;
CREATE TABLE `aspnetusertokens` (
  `UserId` varchar(255) NOT NULL,
  `LoginProvider` varchar(255) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `Value` longtext,
  PRIMARY KEY (`UserId`,`LoginProvider`,`Name`),
  CONSTRAINT `FK_AspNetUserTokens_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `khonguoidungs`
DROP TABLE IF EXISTS `khonguoidungs`;
CREATE TABLE `khonguoidungs` (
  `NDMaNguoidung` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NDMaKho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NhaCC` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `SL` int DEFAULT NULL,
  `DonVi` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayNhapkho` datetime DEFAULT NULL,
  `NgayBaohanh` datetime DEFAULT NULL,
  `ThoiGianBH` datetime DEFAULT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  PRIMARY KEY (`NDMaKho`,`NDMaNguoidung`) USING BTREE,
  KEY `FK2_NDMaNguoidung` (`NDMaNguoidung`),
  CONSTRAINT `FK2_NDMaKho` FOREIGN KEY (`NDMaKho`) REFERENCES `khotongs` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK2_NDMaNguoidung` FOREIGN KEY (`NDMaNguoidung`) REFERENCES `nguoidungs` (`MaNguoidung`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `aspnetroleclaims`
DROP TABLE IF EXISTS `aspnetroleclaims`;
CREATE TABLE `aspnetroleclaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `RoleId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`),
  KEY `IX_AspNetRoleClaims_RoleId` (`RoleId`),
  CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `aspnetroles` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `aspnetuserroles`
DROP TABLE IF EXISTS `aspnetuserroles`;
CREATE TABLE `aspnetuserroles` (
  `UserId` varchar(255) NOT NULL,
  `RoleId` varchar(255) NOT NULL,
  PRIMARY KEY (`UserId`,`RoleId`),
  KEY `IX_AspNetUserRoles_RoleId` (`RoleId`),
  CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `aspnetroles` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_AspNetUserRoles_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `aspnetuserroles` VALUES 
('1008fbb2-9a72-4052-bbc6-4fc91f74c658','2faee9c9-3196-48c1-b40c-87d8869f6002'),
('132c8296-9ad8-4e37-841e-6cb19ab3db38','2faee9c9-3196-48c1-b40c-87d8869f6002'),
('254481a2-b47d-42e6-bb2f-190830838a34','2faee9c9-3196-48c1-b40c-87d8869f6002'),
('d72cf654-2e2a-42ee-b4dc-322bd8345504','2faee9c9-3196-48c1-b40c-87d8869f6002'),
('bc7882af-0849-4820-9254-0cd2a3d45bb6','62e4d649-9cca-456d-97ac-131cec2da3a9'),
('c0d7c574-15c9-4d0f-89e1-e1a9825102f2','7289dd62-3021-4143-8652-7c2657f1369c'),
('15f327c4-48ba-4f73-96dd-77924b3d8c83','7d7a3566-b079-4a9e-b39b-422afa10689a'),
('9dcd42e9-1ffd-41b9-8b60-61f9445f98fa','9cd108e3-85ec-43db-a54b-ab01d6f508aa'),
('c66f20b6-3484-442d-aa53-26115c598b7c','9ddea01b-bb77-401d-865c-4591f48761ff'),
('c127b200-8de2-44b0-b128-69e019930d6d','f1a6cce6-086b-4cc3-828f-8d444a4532cd');

-- Table `yeucau`
DROP TABLE IF EXISTS `yeucau`;
CREATE TABLE `yeucau` (
  `TenYeucau` varchar(50) DEFAULT NULL,
  `MaYeucau` varchar(50) NOT NULL,
  `NguoiYeucau` varchar(50) DEFAULT NULL,
  `BoPhan` varchar(50) DEFAULT NULL,
  `YCMaNguoidung` varchar(50) DEFAULT NULL,
  `YCMaDuan` varchar(50) DEFAULT NULL,
  `NgayYeucau` datetime DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  `NgayCanHang` Datetime default NULL,
  PRIMARY KEY (`MaYeucau`),
  KEY `FK1_MaNguoidung` (`YCMaNguoidung`),
  KEY `FK2_MaDuan` (`YCMaDuan`),
  CONSTRAINT `FK1_MaNguoidung` FOREIGN KEY (`YCMaNguoidung`) REFERENCES `nguoidungs` (`MaNguoidung`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK2_MaDuan` FOREIGN KEY (`YCMaDuan`) REFERENCES `duans` (`MaDuan`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `excel_files` - Tạo trước các bảng phụ thuộc
DROP TABLE IF EXISTS `excel_files`;
CREATE TABLE `excel_files` (
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

-- Table `phieumuahang`
DROP TABLE IF EXISTS `phieumuahang`;
CREATE TABLE `phieumuahang` (
  `MaMuahang` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Maduan` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaNguoidung` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayMuahang` datetime DEFAULT NULL,
  `NgayTao` DATETIME NULL,
  `GhiChu` TEXT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  PRIMARY KEY (`MaMuahang`,`MaYeucau`),
  KEY `FK1_PMHMaYeucau` (`MaYeucau`),
  KEY `FK_phieumuahang_yeucau` (`Maduan`),
  KEY `FK_phieumuahang_yeucau_2` (`MaNguoidung`),
  CONSTRAINT `FK1_PMHMaYeucau` FOREIGN KEY (`MaYeucau`) REFERENCES `yeucau` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieumuahang_yeucau` FOREIGN KEY (`Maduan`) REFERENCES `duans` (`MaDuan`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieumuahang_yeucau_2` FOREIGN KEY (`MaNguoidung`) REFERENCES `nguoidungs` (`MaNguoidung`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `phieunhapkho`
DROP TABLE IF EXISTS `phieunhapkho`;
CREATE TABLE `phieunhapkho` (
  `MaNhapkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Maduan` varchar(50) DEFAULT NULL,
  `MaNguoidung` varchar(50) DEFAULT NULL,
  `NgayNhapkho` datetime DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`MaNhapkho`,`MaYeucau`),
  KEY `FK1_PNKMaYeucau` (`MaYeucau`),
  KEY `FK_phieunhapkho_yeucau` (`Maduan`),
  KEY `FK_phieunhapkho_yeucau_2` (`MaNguoidung`),
  CONSTRAINT `FK1_PNKMaYeucau` FOREIGN KEY (`MaYeucau`) REFERENCES `yeucau` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieunhapkho_yeucau` FOREIGN KEY (`Maduan`) REFERENCES `duans` (`MaDuan`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieunhapkho_yeucau_2` FOREIGN KEY (`MaNguoidung`) REFERENCES `nguoidungs` (`MaNguoidung`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `phieuxuatkho`
DROP TABLE IF EXISTS `phieuxuatkho`;
CREATE TABLE `phieuxuatkho` (
  `MaXuatkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Maduan` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaNguoidung` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayXuatkho` datetime DEFAULT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayChuanBi` DATETIME NULL,
  `NgayXacNhanNhan` DATETIME NULL,
  `NgayTao` DATETIME NULL,
  `GhiChu` VARCHAR(255),
  `NgaySanSang` DATETIME NULL,
  `NgayHoanThanh` DATETIME NULL,
  PRIMARY KEY (`MaXuatkho`,`MaYeucau`) USING BTREE,
  KEY `FK1_PXKMaYeucau` (`MaYeucau`),
  KEY `FK_phieuxuatkho_yeucau` (`Maduan`),
  KEY `FK_phieuxuatkho_yeucau_2` (`MaNguoidung`),
  CONSTRAINT `FK1_PXKMaYeucau` FOREIGN KEY (`MaYeucau`) REFERENCES `yeucau` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieuxuatkho_yeucau` FOREIGN KEY (`Maduan`) REFERENCES `duans` (`MaDuan`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_phieuxuatkho_yeucau_2` FOREIGN KEY (`MaNguoidung`) REFERENCES `nguoidungs` (`MaNguoidung`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `vtyeucau`
DROP TABLE IF EXISTS `vtyeucau`;
CREATE TABLE `vtyeucau` (
  `ID` int unsigned NOT NULL AUTO_INCREMENT,
  `VTMaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) DEFAULT NULL,
  `YCMakho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) DEFAULT NULL,
  `NhaCC` varchar(255) DEFAULT NULL,
  `SL` int DEFAULT NULL,
  `Donvi` varchar(50) DEFAULT NULL,
  `NgayNhapkho` datetime DEFAULT NULL,
  `NgayBaohanh` datetime DEFAULT NULL,
  `ThoiGianBH` datetime DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  `NgayCanHang` datetime default null,
  PRIMARY KEY (`ID`) USING BTREE,
  KEY `FK_vtyeucau_khotongs` (`YCMakho`),
  KEY `FK_vtyeucau_yeucau` (`VTMaYeucau`),
  CONSTRAINT `FK_vtyeucau_yeucau` FOREIGN KEY (`VTMaYeucau`) REFERENCES `yeucau` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtyeucau_khotongs` FOREIGN KEY (`YCMakho`) REFERENCES `khotongs` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=182 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Table `vtphieunhapkho`
DROP TABLE IF EXISTS `vtphieunhapkho`;
CREATE TABLE `vtphieunhapkho` (
  `ID` int NOT NULL AUTO_INCREMENT,
  `MaNhapkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Makho` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NhaCC` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `SL` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonVi` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayNhapkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayBaohanh` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `ThoiGianBH` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonGia` decimal(20,6) DEFAULT NULL,
  `ThanhTien` decimal(20,6) DEFAULT NULL,
  `GhiChu` VARCHAR(255),
  PRIMARY KEY (`ID`),
  KEY `FK_vtphieuxuatkho_phieuxuatkho_2` (`MaYeucau`) USING BTREE,
  KEY `FK_vtphieuxuatkho_vtyeucau` (`Makho`) USING BTREE,
  KEY `FK_vtphieunhapkho_phieunhapkho` (`MaNhapkho`),
  CONSTRAINT `FK_vtphieunhapkho_khotongs` FOREIGN KEY (`Makho`) REFERENCES `khotongs` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieunhapkho_phieunhapkho` FOREIGN KEY (`MaNhapkho`) REFERENCES `phieunhapkho` (`MaNhapkho`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieunhapkho_phieunhapkho_2` FOREIGN KEY (`MaYeucau`) REFERENCES `phieunhapkho` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `vtphieumuahang`
DROP TABLE IF EXISTS `vtphieumuahang`;
CREATE TABLE `vtphieumuahang` (
  `ID` int NOT NULL AUTO_INCREMENT,
  `MaMuahang` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Makho` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NhaCC` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `SL` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonVi` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonGia` decimal(20,6) DEFAULT NULL,
  `ThanhTien` decimal(20,6) DEFAULT NULL,
  `NgayNhapkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayBaohanh` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `ThoiGianBH` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `GhiChu` TEXT NULL,
  PRIMARY KEY (`ID`),
  KEY `FK_vtphieuxuatkho_phieuxuatkho_2` (`MaYeucau`) USING BTREE,
  KEY `FK_vtphieuxuatkho_vtyeucau` (`Makho`) USING BTREE,
  KEY `FK_vtphieumuahang_phieumuahang` (`MaMuahang`),
  CONSTRAINT `FK_vtphieumuahang_phieumuahang` FOREIGN KEY (`MaMuahang`) REFERENCES `phieumuahang` (`MaMuahang`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieumuahang_phieumuahang_2` FOREIGN KEY (`MaYeucau`) REFERENCES `phieumuahang` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieumuahang_khotongs` FOREIGN KEY (`Makho`) REFERENCES `khotongs` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=70 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `vtphieuxuatkho`
DROP TABLE IF EXISTS `vtphieuxuatkho`;
CREATE TABLE `vtphieuxuatkho` (
  `ID` int NOT NULL AUTO_INCREMENT,
  `MaXuatkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MaYeucau` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Makho` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NhaCC` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `SL` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonVi` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayNhapkho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `NgayBaohanh` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `ThoiGianBH` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `TrangThai` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DonGia` decimal(20,6) DEFAULT NULL,
  `ThanhTien` decimal(20,6) DEFAULT NULL,
  `LoaiCapPhat` varchar(50) NULL,
  PRIMARY KEY (`ID`) USING BTREE,
  KEY `FK_vtphieuxuatkho_phieuxuatkho` (`MaXuatkho`),
  KEY `FK_vtphieuxuatkho_phieuxuatkho_2` (`MaYeucau`),
  KEY `FK_vtphieuxuatkho_khotongs` (`Makho`),
  CONSTRAINT `FK_vtphieuxuatkho_phieuxuatkho` FOREIGN KEY (`MaXuatkho`) REFERENCES `phieuxuatkho` (`MaXuatkho`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieuxuatkho_phieuxuatkho_2` FOREIGN KEY (`MaYeucau`) REFERENCES `phieuxuatkho` (`MaYeucau`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_vtphieuxuatkho_khotongs` FOREIGN KEY (`Makho`) REFERENCES `khotongs` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=56 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Table `khoduans`
DROP TABLE IF EXISTS `khoduans`;
CREATE TABLE `khoduans` (
  `DAMaDuan` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TenSanpham` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `MaSanpham` varchar(50) DEFAULT NULL,
  `DAMaKho` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HangSX` varchar(100) DEFAULT NULL,
  `NhaCC` varchar(255) DEFAULT NULL,
  `SL` int DEFAULT NULL,
  `DonVi` varchar(50) DEFAULT NULL,
  `NgayNhapkho` datetime DEFAULT NULL,
  `NgayBaohanh` datetime DEFAULT NULL,
  `ThoiGianBH` datetime DEFAULT NULL,
  `TrangThai` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`DAMaDuan`,`DAMaKho`) USING BTREE,
  KEY `FK_khoduans_vtphieuxuatkho` (`DAMaKho`),
  CONSTRAINT `FK_khoduans_phieuxuatkho` FOREIGN KEY (`DAMaDuan`) REFERENCES `phieuxuatkho` (`Maduan`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_khoduans_vtphieuxuatkho` FOREIGN KEY (`DAMaKho`) REFERENCES `vtphieuxuatkho` (`Makho`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `khoduans` VALUES ('230805','Chân cắm M4 dẹt màu đỏ','24.302.1','STU4','Amass','Hợp long',2,'cái ',NULL,'2024-05-30 00:00:00','2025-05-30 00:00:00','Đã xuất kho');

SET FOREIGN_KEY_CHECKS=1;

