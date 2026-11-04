using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChiTieuGiaDinh.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemCacBangModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NguoiDungs",
                columns: table => new
                {
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoTen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SoDienThoai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AnhDaiDien = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NguoiDungs", x => x.MaNguoiDung);
                });

            migrationBuilder.CreateTable(
                name: "HoGiaDinhs",
                columns: table => new
                {
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenGiaDinh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MaChuHo = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayGiaiTan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiDungMaNguoiDung = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoGiaDinhs", x => x.MaGiaDinh);
                    table.ForeignKey(
                        name: "FK_HoGiaDinhs_NguoiDungs_MaChuHo",
                        column: x => x.MaChuHo,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoGiaDinhs_NguoiDungs_NguoiDungMaNguoiDung",
                        column: x => x.NguoiDungMaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung");
                });

            migrationBuilder.CreateTable(
                name: "ThongBaos",
                columns: table => new
                {
                    MaThongBao = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    PhamVi = table.Column<int>(type: "int", nullable: false),
                    LoaiThongBao = table.Column<int>(type: "int", nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DuLieuLienKet = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DaDoc = table.Column<bool>(type: "bit", nullable: false),
                    IsThaoTacXong = table.Column<bool>(type: "bit", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBaos", x => x.MaThongBao);
                    table.ForeignKey(
                        name: "FK_ThongBaos_NguoiDungs_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DanhMucs",
                columns: table => new
                {
                    MaDanhMuc = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: true),
                    MaNguoiTao = table.Column<int>(type: "int", nullable: true),
                    TenDanhMuc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenDanhMucClean = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoaiDanhMuc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BieuTuong = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MauSac = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaDanhMucCha = table.Column<int>(type: "int", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanhMucs", x => x.MaDanhMuc);
                    table.ForeignKey(
                        name: "FK_DanhMucs_DanhMucs_MaDanhMucCha",
                        column: x => x.MaDanhMucCha,
                        principalTable: "DanhMucs",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DanhMucs_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DanhMucs_NguoiDungs_MaNguoiTao",
                        column: x => x.MaNguoiTao,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LichSuChuyenChuHos",
                columns: table => new
                {
                    MaLichSu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    MaChuHoCu = table.Column<int>(type: "int", nullable: false),
                    MaChuHoMoi = table.Column<int>(type: "int", nullable: false),
                    ThoiGianChuyenGiao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuChuyenChuHos", x => x.MaLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuChuyenChuHos_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LichSuChuyenChuHos_NguoiDungs_MaChuHoCu",
                        column: x => x.MaChuHoCu,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichSuChuyenChuHos_NguoiDungs_MaChuHoMoi",
                        column: x => x.MaChuHoMoi,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoiMoiGiaDinhs",
                columns: table => new
                {
                    MaLoiMoi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    EmailNguoiNhan = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MaNguoiGui = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NgayGui = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiMoiGiaDinhs", x => x.MaLoiMoi);
                    table.ForeignKey(
                        name: "FK_LoiMoiGiaDinhs_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoiMoiGiaDinhs_NguoiDungs_MaNguoiGui",
                        column: x => x.MaNguoiGui,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MucTieuTietKiem",
                columns: table => new
                {
                    MaMucTieu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: true),
                    MaTruongNhom = table.Column<int>(type: "int", nullable: false),
                    TenMucTieu = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LoaiMucTieu = table.Column<int>(type: "int", nullable: false),
                    SoTienMucTieu = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoTienHienCo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayHan = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnhDaiDienUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActiveWallet = table.Column<bool>(type: "bit", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MucTieuTietKiem", x => x.MaMucTieu);
                    table.ForeignKey(
                        name: "FK_MucTieuTietKiem_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MucTieuTietKiem_NguoiDungs_MaTruongNhom",
                        column: x => x.MaTruongNhom,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyHeThongs",
                columns: table => new
                {
                    MaNhatKy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: true),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    LoaiThaoTac = table.Column<int>(type: "int", nullable: false),
                    DoiTuongTacDong = table.Column<int>(type: "int", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "TaiKhoanTiens",
                columns: table => new
                {
                    MaTaiKhoan = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    MaNguoiSoHuu = table.Column<int>(type: "int", nullable: true),
                    TenTaiKhoan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoaiTaiKhoan = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LoaiVi = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LaTaiKhoanGiaDinh = table.Column<bool>(type: "bit", nullable: false),
                    SoDuBanDau = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoDuHienTai = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoanTiens", x => x.MaTaiKhoan);
                    table.ForeignKey(
                        name: "FK_TaiKhoanTiens_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaiKhoanTiens_NguoiDungs_MaNguoiSoHuu",
                        column: x => x.MaNguoiSoHuu,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThanhVienGiaDinhs",
                columns: table => new
                {
                    MaThanhVien = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    VaiTro = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NgayThamGia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayRoiHo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiDungMaNguoiDung = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhVienGiaDinhs", x => x.MaThanhVien);
                    table.ForeignKey(
                        name: "FK_ThanhVienGiaDinhs_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThanhVienGiaDinhs_NguoiDungs_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ThanhVienGiaDinhs_NguoiDungs_NguoiDungMaNguoiDung",
                        column: x => x.NguoiDungMaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung");
                });

            migrationBuilder.CreateTable(
                name: "YeuCauRoiHos",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    ThoiDiemGui = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NguoiDuyet = table.Column<int>(type: "int", nullable: true),
                    ThoiDiemXuLy = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauRoiHos", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauRoiHos_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YeuCauRoiHos_NguoiDungs_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauRoiHos_NguoiDungs_NguoiDuyet",
                        column: x => x.NguoiDuyet,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NganSachs",
                columns: table => new
                {
                    MaNganSach = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenNganSach = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsGiaDinh = table.Column<bool>(type: "bit", nullable: false),
                    MaGiaDinh = table.Column<int>(type: "int", nullable: false),
                    MaNguoiTao = table.Column<int>(type: "int", nullable: false),
                    MaDanhMuc = table.Column<int>(type: "int", nullable: true),
                    SoTienHanMuc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChuKy = table.Column<int>(type: "int", nullable: false),
                    NgayBatDauChuKy = table.Column<int>(type: "int", nullable: false),
                    Thang = table.Column<int>(type: "int", nullable: false),
                    Nam = table.Column<int>(type: "int", nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayKetThuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguongCanhBaoPhanTram = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NganSachs", x => x.MaNganSach);
                    table.ForeignKey(
                        name: "FK_NganSachs_DanhMucs_MaDanhMuc",
                        column: x => x.MaDanhMuc,
                        principalTable: "DanhMucs",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NganSachs_HoGiaDinhs_MaGiaDinh",
                        column: x => x.MaGiaDinh,
                        principalTable: "HoGiaDinhs",
                        principalColumn: "MaGiaDinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NganSachs_NguoiDungs_MaNguoiTao",
                        column: x => x.MaNguoiTao,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoiMoiTietKiem",
                columns: table => new
                {
                    MaLoiMoi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMucTieu = table.Column<int>(type: "int", nullable: false),
                    MaNguoiMoi = table.Column<int>(type: "int", nullable: false),
                    EmailNhan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SoDienThoaiNhan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TokenMoi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayHetHan = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiMoiTietKiem", x => x.MaLoiMoi);
                    table.ForeignKey(
                        name: "FK_LoiMoiTietKiem_MucTieuTietKiem_MaMucTieu",
                        column: x => x.MaMucTieu,
                        principalTable: "MucTieuTietKiem",
                        principalColumn: "MaMucTieu",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoiMoiTietKiem_NguoiDungs_MaNguoiMoi",
                        column: x => x.MaNguoiMoi,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThanhVienMucTieu",
                columns: table => new
                {
                    MaThanhVienMucTieu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMucTieu = table.Column<int>(type: "int", nullable: false),
                    MaNguoiDung = table.Column<int>(type: "int", nullable: false),
                    ChiTieuCanDong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoTienDaDong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DaHoanThanhChiTieu = table.Column<bool>(type: "bit", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    NgayThamGia = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhVienMucTieu", x => x.MaThanhVienMucTieu);
                    table.ForeignKey(
                        name: "FK_ThanhVienMucTieu_MucTieuTietKiem_MaMucTieu",
                        column: x => x.MaMucTieu,
                        principalTable: "MucTieuTietKiem",
                        principalColumn: "MaMucTieu",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThanhVienMucTieu_NguoiDungs_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiaoDichDinhKys",
                columns: table => new
                {
                    MaDinhKy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoan = table.Column<int>(type: "int", nullable: false),
                    MaDanhMuc = table.Column<int>(type: "int", nullable: false),
                    MaNguoiThucHien = table.Column<int>(type: "int", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoaiGiaoDich = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChuKy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NgayLapTrongChuKy = table.Column<int>(type: "int", nullable: false),
                    IsBienDong = table.Column<bool>(type: "bit", nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayKetThuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayChayTiepTheo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaoDichDinhKys", x => x.MaDinhKy);
                    table.ForeignKey(
                        name: "FK_GiaoDichDinhKys_DanhMucs_MaDanhMuc",
                        column: x => x.MaDanhMuc,
                        principalTable: "DanhMucs",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichDinhKys_NguoiDungs_MaNguoiThucHien",
                        column: x => x.MaNguoiThucHien,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichDinhKys_TaiKhoanTiens_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoanTiens",
                        principalColumn: "MaTaiKhoan",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "LichSuTietKiem",
                columns: table => new
                {
                    MaLichSuTietKiem = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMucTieu = table.Column<int>(type: "int", nullable: false),
                    MaNguoiThucHien = table.Column<int>(type: "int", nullable: false),
                    MaTaiKhoanNguon = table.Column<int>(type: "int", nullable: true),
                    MaDanhMuc = table.Column<int>(type: "int", nullable: true),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoaiGiaoDich = table.Column<int>(type: "int", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AnhHoaDonUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuTietKiem", x => x.MaLichSuTietKiem);
                    table.ForeignKey(
                        name: "FK_LichSuTietKiem_DanhMucs_MaDanhMuc",
                        column: x => x.MaDanhMuc,
                        principalTable: "DanhMucs",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichSuTietKiem_MucTieuTietKiem_MaMucTieu",
                        column: x => x.MaMucTieu,
                        principalTable: "MucTieuTietKiem",
                        principalColumn: "MaMucTieu",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LichSuTietKiem_NguoiDungs_MaNguoiThucHien",
                        column: x => x.MaNguoiThucHien,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichSuTietKiem_TaiKhoanTiens_MaTaiKhoanNguon",
                        column: x => x.MaTaiKhoanNguon,
                        principalTable: "TaiKhoanTiens",
                        principalColumn: "MaTaiKhoan",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "LichSuHanMucs",
                columns: table => new
                {
                    MaLichSu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNganSach = table.Column<int>(type: "int", nullable: false),
                    HanMucCu = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HanMucMoi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaNguoiThayDoi = table.Column<int>(type: "int", nullable: false),
                    NgayThayDoi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuHanMucs", x => x.MaLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuHanMucs_NganSachs_MaNganSach",
                        column: x => x.MaNganSach,
                        principalTable: "NganSachs",
                        principalColumn: "MaNganSach",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LichSuHanMucs_NguoiDungs_MaNguoiThayDoi",
                        column: x => x.MaNguoiThayDoi,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiaoDichs",
                columns: table => new
                {
                    MaGiaoDich = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoan = table.Column<int>(type: "int", nullable: false),
                    MaDanhMuc = table.Column<int>(type: "int", nullable: false),
                    MaNguoiThucHien = table.Column<int>(type: "int", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoaiGiaoDich = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NgayGiaoDich = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnhHoaDon = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThaiDuyet = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TrangThaiQuyetToan = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaDinhKy = table.Column<int>(type: "int", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaoDichs", x => x.MaGiaoDich);
                    table.ForeignKey(
                        name: "FK_GiaoDichs_DanhMucs_MaDanhMuc",
                        column: x => x.MaDanhMuc,
                        principalTable: "DanhMucs",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichs_GiaoDichDinhKys_MaDinhKy",
                        column: x => x.MaDinhKy,
                        principalTable: "GiaoDichDinhKys",
                        principalColumn: "MaDinhKy",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GiaoDichs_NguoiDungs_MaNguoiThucHien",
                        column: x => x.MaNguoiThucHien,
                        principalTable: "NguoiDungs",
                        principalColumn: "MaNguoiDung",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichs_TaiKhoanTiens_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoanTiens",
                        principalColumn: "MaTaiKhoan",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DanhMuc_MaGiaDinh_TenClean_Loai",
                table: "DanhMucs",
                columns: new[] { "MaGiaDinh", "TenDanhMucClean", "LoaiDanhMuc" });

            migrationBuilder.CreateIndex(
                name: "IX_DanhMucs_MaDanhMucCha",
                table: "DanhMucs",
                column: "MaDanhMucCha");

            migrationBuilder.CreateIndex(
                name: "IX_DanhMucs_MaNguoiTao",
                table: "DanhMucs",
                column: "MaNguoiTao");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichDinhKys_MaDanhMuc",
                table: "GiaoDichDinhKys",
                column: "MaDanhMuc");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichDinhKys_MaNguoiThucHien",
                table: "GiaoDichDinhKys",
                column: "MaNguoiThucHien");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichDinhKys_MaTaiKhoan",
                table: "GiaoDichDinhKys",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichs_MaDanhMuc",
                table: "GiaoDichs",
                column: "MaDanhMuc");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichs_MaDinhKy",
                table: "GiaoDichs",
                column: "MaDinhKy");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichs_MaNguoiThucHien",
                table: "GiaoDichs",
                column: "MaNguoiThucHien");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichs_MaTaiKhoan",
                table: "GiaoDichs",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_HanMucChiTieus_MaTaiKhoan",
                table: "HanMucChiTieus",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_HanMucChiTieus_MaThanhVien",
                table: "HanMucChiTieus",
                column: "MaThanhVien");

            migrationBuilder.CreateIndex(
                name: "IX_HoGiaDinhs_MaChuHo",
                table: "HoGiaDinhs",
                column: "MaChuHo",
                unique: true,
                filter: "[TrangThai] = N'HoatDong'");

            migrationBuilder.CreateIndex(
                name: "IX_HoGiaDinhs_NguoiDungMaNguoiDung",
                table: "HoGiaDinhs",
                column: "NguoiDungMaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuChuyenChuHos_MaChuHoCu",
                table: "LichSuChuyenChuHos",
                column: "MaChuHoCu");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuChuyenChuHos_MaChuHoMoi",
                table: "LichSuChuyenChuHos",
                column: "MaChuHoMoi");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuChuyenChuHos_MaGiaDinh",
                table: "LichSuChuyenChuHos",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuHanMucs_MaNganSach",
                table: "LichSuHanMucs",
                column: "MaNganSach");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuHanMucs_MaNguoiThayDoi",
                table: "LichSuHanMucs",
                column: "MaNguoiThayDoi");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTietKiem_MaDanhMuc",
                table: "LichSuTietKiem",
                column: "MaDanhMuc");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTietKiem_MaMucTieu",
                table: "LichSuTietKiem",
                column: "MaMucTieu");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTietKiem_MaNguoiThucHien",
                table: "LichSuTietKiem",
                column: "MaNguoiThucHien");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTietKiem_MaTaiKhoanNguon",
                table: "LichSuTietKiem",
                column: "MaTaiKhoanNguon");

            migrationBuilder.CreateIndex(
                name: "IX_LoiMoiGiaDinhs_MaGiaDinh",
                table: "LoiMoiGiaDinhs",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_LoiMoiGiaDinhs_MaNguoiGui",
                table: "LoiMoiGiaDinhs",
                column: "MaNguoiGui");

            migrationBuilder.CreateIndex(
                name: "IX_LoiMoiTietKiem_MaMucTieu",
                table: "LoiMoiTietKiem",
                column: "MaMucTieu");

            migrationBuilder.CreateIndex(
                name: "IX_LoiMoiTietKiem_MaNguoiMoi",
                table: "LoiMoiTietKiem",
                column: "MaNguoiMoi");

            migrationBuilder.CreateIndex(
                name: "IX_MucTieuTietKiem_MaGiaDinh",
                table: "MucTieuTietKiem",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_MucTieuTietKiem_MaTruongNhom",
                table: "MucTieuTietKiem",
                column: "MaTruongNhom");

            migrationBuilder.CreateIndex(
                name: "IX_NganSachs_MaDanhMuc",
                table: "NganSachs",
                column: "MaDanhMuc");

            migrationBuilder.CreateIndex(
                name: "IX_NganSachs_MaGiaDinh",
                table: "NganSachs",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_NganSachs_MaNguoiTao",
                table: "NganSachs",
                column: "MaNguoiTao");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHeThongs_MaGiaDinh",
                table: "NhatKyHeThongs",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHeThongs_MaNguoiDung",
                table: "NhatKyHeThongs",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanTiens_MaGiaDinh",
                table: "TaiKhoanTiens",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanTiens_MaNguoiSoHuu",
                table: "TaiKhoanTiens",
                column: "MaNguoiSoHuu");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienGiaDinhs_MaGiaDinh_MaNguoiDung",
                table: "ThanhVienGiaDinhs",
                columns: new[] { "MaGiaDinh", "MaNguoiDung" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienGiaDinhs_MaNguoiDung",
                table: "ThanhVienGiaDinhs",
                column: "MaNguoiDung",
                unique: true,
                filter: "[TrangThai] = N'DaXacNhan'");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienGiaDinhs_NguoiDungMaNguoiDung",
                table: "ThanhVienGiaDinhs",
                column: "NguoiDungMaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienMucTieu_MaMucTieu",
                table: "ThanhVienMucTieu",
                column: "MaMucTieu");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienMucTieu_MaNguoiDung",
                table: "ThanhVienMucTieu",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_ThongBaos_MaNguoiDung",
                table: "ThongBaos",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauHoanTiens_MaNguoiYeuCau",
                table: "YeuCauHoanTiens",
                column: "MaNguoiYeuCau");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauHoanTiens_MaTaiKhoan",
                table: "YeuCauHoanTiens",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRoiHos_MaGiaDinh",
                table: "YeuCauRoiHos",
                column: "MaGiaDinh");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRoiHos_MaNguoiDung",
                table: "YeuCauRoiHos",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRoiHos_NguoiDuyet",
                table: "YeuCauRoiHos",
                column: "NguoiDuyet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiaoDichs");

            migrationBuilder.DropTable(
                name: "HanMucChiTieus");

            migrationBuilder.DropTable(
                name: "LichSuChuyenChuHos");

            migrationBuilder.DropTable(
                name: "LichSuHanMucs");

            migrationBuilder.DropTable(
                name: "LichSuTietKiem");

            migrationBuilder.DropTable(
                name: "LoiMoiGiaDinhs");

            migrationBuilder.DropTable(
                name: "LoiMoiTietKiem");

            migrationBuilder.DropTable(
                name: "NhatKyHeThongs");

            migrationBuilder.DropTable(
                name: "ThanhVienGiaDinhs");

            migrationBuilder.DropTable(
                name: "ThanhVienMucTieu");

            migrationBuilder.DropTable(
                name: "ThongBaos");

            migrationBuilder.DropTable(
                name: "YeuCauHoanTiens");

            migrationBuilder.DropTable(
                name: "YeuCauRoiHos");

            migrationBuilder.DropTable(
                name: "GiaoDichDinhKys");

            migrationBuilder.DropTable(
                name: "NganSachs");

            migrationBuilder.DropTable(
                name: "MucTieuTietKiem");

            migrationBuilder.DropTable(
                name: "TaiKhoanTiens");

            migrationBuilder.DropTable(
                name: "DanhMucs");

            migrationBuilder.DropTable(
                name: "HoGiaDinhs");

            migrationBuilder.DropTable(
                name: "NguoiDungs");
        }
    }
}
