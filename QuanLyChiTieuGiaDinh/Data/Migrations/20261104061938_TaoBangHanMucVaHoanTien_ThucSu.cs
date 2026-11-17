using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChiTieuGiaDinh.Migrations
{
    /// <inheritdoc />
    public partial class TaoBangHanMucVaHoanTien_ThucSu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HanMucChiTieus",
                columns: table => new
                {
                    MaHanMuc = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoan = table.Column<int>(type: "int", nullable: false),
                    MaThanhVien = table.Column<int>(type: "int", nullable: false),
                    SoTienHanMuc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanChot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<bool>(type: "bit", nullable: false),
                    DaThongBao = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HanMucChiTieus", x => x.MaHanMuc);
                    table.ForeignKey(
                        name: "FK_HanMucChiTieus_NguoiDungs_MaThanhVien",
                        column: x => x.MaThanhVien,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HanMucChiTieus_TaiKhoanTiens_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoanTiens",
                        principalColumn: "MaTaiKhoan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauHoanTiens",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoan = table.Column<int>(type: "int", nullable: false),
                    MaNguoiYeuCau = table.Column<int>(type: "int", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgayYeuCau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayXuLy = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauHoanTiens", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauHoanTiens_NguoiDungs_MaNguoiYeuCau",
                        column: x => x.MaNguoiYeuCau,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YeuCauHoanTiens_TaiKhoanTiens_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoanTiens",
                        principalColumn: "MaTaiKhoan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HanMucChiTieus_MaTaiKhoan",
                table: "HanMucChiTieus",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_HanMucChiTieus_MaThanhVien",
                table: "HanMucChiTieus",
                column: "MaThanhVien");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauHoanTiens_MaNguoiYeuCau",
                table: "YeuCauHoanTiens",
                column: "MaNguoiYeuCau");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauHoanTiens_MaTaiKhoan",
                table: "YeuCauHoanTiens",
                column: "MaTaiKhoan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HanMucChiTieus");

            migrationBuilder.DropTable(
                name: "YeuCauHoanTiens");
        }
    }
}
