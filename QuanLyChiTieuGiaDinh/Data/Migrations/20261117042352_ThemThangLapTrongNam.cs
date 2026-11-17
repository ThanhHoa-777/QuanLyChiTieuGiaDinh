using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChiTieuGiaDinh.Migrations
{
    /// <inheritdoc />
    public partial class ThemThangLapTrongNam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhatKyHeThongs");

            migrationBuilder.AddColumn<int>(
                name: "ThangLapTrongNam",
                table: "GiaoDichDinhKys",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThangLapTrongNam",
                table: "GiaoDichDinhKys");

            migrationBuilder.CreateTable(
                name: "NhatKyHeThongs",
                columns: table => new
                {
                    MaNhatKy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: true),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    DoiTuongTacDong = table.Column<int>(type: "int", nullable: false),
                    LoaiThaoTac = table.Column<int>(type: "int", nullable: false),
                    MaBanGhi = table.Column<int>(type: "int", nullable: true),
                    MoTaChiTiet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NoiDungCu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoiDungMoi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyHeThongs", x => x.MaNhatKy);
                    table.ForeignKey(
                        name: "FK_NhatKyHeThongs_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh");
                    table.ForeignKey(
                        name: "FK_NhatKyHeThongs_NguoiDungs_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHeThongs_MaGiaDinh",
                table: "NhatKyHeThongs",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHeThongs_MaNguoiDung",
                table: "NhatKyHeThongs",
                column: "MaNguoiDung");
        }
    }
}
