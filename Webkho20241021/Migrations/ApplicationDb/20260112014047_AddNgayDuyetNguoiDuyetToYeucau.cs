using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Webkho_20241021.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddNgayDuyetNguoiDuyetToYeucau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NgayDuyet",
                table: "yeucau",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiDuyet",
                table: "yeucau",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhiChu",
                table: "vtyeucau",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayDuyet",
                table: "vtyeucau",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SLCu",
                table: "vtyeucau",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SLMoi",
                table: "vtyeucau",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiengiaiNhapKho",
                table: "vtphieunhapkho",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "nguoidungs",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "excelfiles",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MaYeucau = table.Column<string>(type: "longtext", nullable: true),
                    MaDuan = table.Column<string>(type: "longtext", nullable: true),
                    TenFile = table.Column<string>(type: "longtext", nullable: true),
                    DuongDanFile = table.Column<string>(type: "longtext", nullable: true),
                    NgayUpload = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NguoiUpload = table.Column<string>(type: "longtext", nullable: true),
                    KichThuocFile = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excelfiles", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "excelfiles");

            migrationBuilder.DropColumn(
                name: "NgayDuyet",
                table: "yeucau");

            migrationBuilder.DropColumn(
                name: "NguoiDuyet",
                table: "yeucau");

            migrationBuilder.DropColumn(
                name: "GhiChu",
                table: "vtyeucau");

            migrationBuilder.DropColumn(
                name: "NgayDuyet",
                table: "vtyeucau");

            migrationBuilder.DropColumn(
                name: "SLCu",
                table: "vtyeucau");

            migrationBuilder.DropColumn(
                name: "SLMoi",
                table: "vtyeucau");

            migrationBuilder.DropColumn(
                name: "DiengiaiNhapKho",
                table: "vtphieunhapkho");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "nguoidungs");
        }
    }
}
