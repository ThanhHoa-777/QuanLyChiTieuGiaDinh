
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class GiaoDichDinhKyService
    {
        private readonly ApplicationDbContext _context;

        public GiaoDichDinhKyService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // XỬ LÝ TẤT CẢ GIAO DỊCH ĐỊNH KỲ ĐÃ ĐẾN HẠN
        // =========================================================
        public async Task XuLyGiaoDichDinhKy()
        {
            var homNay = DateTime.Now;

            var danhSach = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .Where(x =>
                    x.TrangThai == "HoatDong" &&
                    x.NgayChayTiepTheo <= homNay)
                .ToListAsync();

            foreach (var dinhKy in danhSach)
            {
                await XuLyMotGiaoDichDinhKy(dinhKy);
            }

            await _context.SaveChangesAsync();
        }

        // =========================================================
        // XỬ LÝ MỘT GIAO DỊCH ĐỊNH KỲ
        // =========================================================
        private async Task XuLyMotGiaoDichDinhKy(
            GiaoDichDinhKy dinhKy)
        {
            if (dinhKy.TaiKhoanTien == null)
                return;

            // Nếu đã quá ngày kết thúc
            if (dinhKy.NgayKetThuc.HasValue &&
                dinhKy.NgayChayTiepTheo.Date >
                dinhKy.NgayKetThuc.Value.Date)
            {
                dinhKy.TrangThai = "DaKetThuc";
                return;
            }

            // =====================================================
            // TẠO GIAO DỊCH THẬT
            // =====================================================

            var giaoDich = new GiaoDich
            {
                MaTaiKhoan = dinhKy.MaTaiKhoan,

                MaDanhMuc = dinhKy.MaDanhMuc,

                MaNguoiThucHien =
                    dinhKy.MaNguoiThucHien,

                SoTien = dinhKy.SoTien,

                LoaiGiaoDich =
                    dinhKy.LoaiGiaoDich,

                NgayGiaoDich =
                    dinhKy.NgayChayTiepTheo,

                GhiChu =
                    "Giao dịch tự động từ giao dịch định kỳ",

                TrangThaiDuyet = "DaDuyet"
            };

            _context.GiaoDichs.Add(giaoDich);

            // =====================================================
            // CẬP NHẬT SỐ DƯ
            // =====================================================

            if (dinhKy.LoaiGiaoDich == "Thu")
            {
                dinhKy.TaiKhoanTien.SoDuHienTai
                    += dinhKy.SoTien;
            }
            else if (dinhKy.LoaiGiaoDich == "Chi")
            {
                dinhKy.TaiKhoanTien.SoDuHienTai
                    -= dinhKy.SoTien;
            }

            // =====================================================
            // TÍNH NGÀY CHẠY TIẾP THEO
            // =====================================================

            dinhKy.NgayChayTiepTheo =
                TinhNgayTiepTheo(
                    dinhKy.NgayChayTiepTheo,
                    dinhKy.ChuKy);

            // =====================================================
            // KIỂM TRA NGÀY KẾT THÚC
            // =====================================================

            if (dinhKy.NgayKetThuc.HasValue &&
                dinhKy.NgayChayTiepTheo.Date >
                dinhKy.NgayKetThuc.Value.Date)
            {
                dinhKy.TrangThai = "DaKetThuc";
            }
        }

        // =========================================================
        // TÍNH NGÀY CHẠY TIẾP THEO
        // =========================================================
        private DateTime TinhNgayTiepTheo(
            DateTime ngayHienTai,
            string chuKy)
        {
            return chuKy switch
            {
                "HangNgay" =>
                    ngayHienTai.AddDays(1),

                "HangTuan" =>
                    ngayHienTai.AddDays(7),

                "HangThang" =>
                    ngayHienTai.AddMonths(1),

                "HangNam" =>
                    ngayHienTai.AddYears(1),

                _ =>
                    ngayHienTai.AddMonths(1)
            };
        }
    }
}
