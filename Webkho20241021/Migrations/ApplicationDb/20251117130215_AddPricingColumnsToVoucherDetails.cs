using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Webkho_20241021.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddPricingColumnsToVoucherDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DonGia",
                table: "vtphieunhapkho",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ThanhTien",
                table: "vtphieunhapkho",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DonGia",
                table: "vtphieuxuatkho",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ThanhTien",
                table: "vtphieuxuatkho",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DonGia",
                table: "vtphieunhapkho");

            migrationBuilder.DropColumn(
                name: "ThanhTien",
                table: "vtphieunhapkho");

            migrationBuilder.DropColumn(
                name: "DonGia",
                table: "vtphieuxuatkho");

            migrationBuilder.DropColumn(
                name: "ThanhTien",
                table: "vtphieuxuatkho");
        }
    }
}
