using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Webkho_20241021.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddTTToVtyeucau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GhiChuBPMuahang",
                table: "vtphieumuahang",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhiChuGiamdoc",
                table: "vtphieumuahang",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayCoHang",
                table: "vtphieumuahang",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayThanhToan",
                table: "vtphieumuahang",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayThanhToanBPMuahang",
                table: "vtphieumuahang",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayThanhToanGiamdoc",
                table: "vtphieumuahang",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "emailsettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SmtpServer = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    FromEmail = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    FromPassword = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    FromName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    UpdatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emailsettings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emailsettings");

            migrationBuilder.DropColumn(
                name: "GhiChuBPMuahang",
                table: "vtphieumuahang");

            migrationBuilder.DropColumn(
                name: "GhiChuGiamdoc",
                table: "vtphieumuahang");

            migrationBuilder.DropColumn(
                name: "NgayCoHang",
                table: "vtphieumuahang");

            migrationBuilder.DropColumn(
                name: "NgayThanhToan",
                table: "vtphieumuahang");

            migrationBuilder.DropColumn(
                name: "NgayThanhToanBPMuahang",
                table: "vtphieumuahang");

            migrationBuilder.DropColumn(
                name: "NgayThanhToanGiamdoc",
                table: "vtphieumuahang");
        }
    }
}
