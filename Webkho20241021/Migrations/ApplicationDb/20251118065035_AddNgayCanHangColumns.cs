using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Webkho_20241021.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddNgayCanHangColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NgayCanHang",
                table: "yeucau",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayCanHang",
                table: "vtyeucau",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SanPhamNhaCC",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MaSanpham = table.Column<string>(type: "longtext", nullable: false),
                    NhaCC = table.Column<string>(type: "longtext", nullable: false),
                    DonGiaMacDinh = table.Column<decimal>(type: "decimal(20,6)", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GhiChu = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanPhamNhaCC", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SanPhamNhaCC");

            migrationBuilder.DropColumn(
                name: "NgayCanHang",
                table: "yeucau");

            migrationBuilder.DropColumn(
                name: "NgayCanHang",
                table: "vtyeucau");
        }
    }
}
