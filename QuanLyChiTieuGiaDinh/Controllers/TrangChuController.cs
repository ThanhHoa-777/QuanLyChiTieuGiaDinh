using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Services;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class TrangChuController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GiaoDichDinhKyService _giaoDichDinhKyService;

        public TrangChuController(
            ApplicationDbContext context,
            GiaoDichDinhKyService giaoDichDinhKyService)
        {
            _context = context;
            _giaoDichDinhKyService = giaoDichDinhKyService;
        }

        private async Task<NguoiDung?> LayNguoiDungDangNhapAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        private async Task<HoGiaDinh?> LayHoGiaDinhCuaNguoiDungAsync(int maNguoiDung)
        {
            var hoGiaDinh = await _context.HoGiaDinhs
                .FirstOrDefaultAsync(x => x.MaChuHo == maNguoiDung && x.TrangThai == "HoatDong");

            if (hoGiaDinh != null) return hoGiaDinh;

            var thanhVien = await _context.ThanhVienGiaDinhs
                .Include(x => x.HoGiaDinh)
                .FirstOrDefaultAsync(x => x.MaNguoiDung == maNguoiDung &&
                                     x.TrangThai == "DaXacNhan" &&
                                     _context.HoGiaDinhs.Any(h => h.MaGiaDinh == x.MaGiaDinh && h.TrangThai == "HoatDong"));

            return thanhVien?.HoGiaDinh;
        }

        private bool LaChuHo(NguoiDung nguoiDung, HoGiaDinh hoGiaDinh)
        {
            return hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung;
        }

        public async Task<IActionResult> TrangChinh(string cheDo = "gia-dinh")
        {
            // Tự động xử lý giao dịch định kỳ đến hạn
            await _giaoDichDinhKyService.XuLyGiaoDichDinhKy();

            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDungAsync(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                ViewBag.NguoiDung = nguoiDung;
                return View();
            }

            int maGiaDinh = hoGiaDinh.MaGiaDinh;
            int maNguoiDung = nguoiDung.MaNguoiDung;
            int thangHienTai = DateTime.Now.Month;
            int namHienTai = DateTime.Now.Year;

            // 1. TÀI KHOẢN TIỀN (Lọc theo chế độ Cá nhân hoặc Hộ gia đình)
            var taiKhoansQuery = _context.TaiKhoanTiens
                .Where(x => x.MaGiaDinh == maGiaDinh && x.TrangThai);

            if (cheDo == "ca-nhan")
            {
                // Chỉ lấy ví cá nhân của chính user này
                taiKhoansQuery = taiKhoansQuery.Where(x => x.MaNguoiSoHuu == maNguoiDung && !x.LaTaiKhoanGiaDinh);
            }
            else
            {
                // Lấy cả ví gia đình chung và ví cá nhân trong hộ
                taiKhoansQuery = taiKhoansQuery.Where(x => x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == maNguoiDung);
            }

            var taiKhoans = await taiKhoansQuery
                .OrderByDescending(x => x.LaTaiKhoanGiaDinh)
                .ThenBy(x => x.TenTaiKhoan)
                .ToListAsync();

            decimal tongSoDu = taiKhoans.Sum(x => x.SoDuHienTai);

            // 2. QUERY BASE GIAO DỊCH (Lọc theo chế độ Cá nhân hoặc Hộ gia đình)
            var baseGiaoDichQuery = _context.GiaoDichs
                .Where(x => x.TaiKhoanTien != null && x.TaiKhoanTien.MaGiaDinh == maGiaDinh);

            if (cheDo == "ca-nhan")
            {
                // Chỉ lấy giao dịch do chính user này thực hiện hoặc thuộc ví cá nhân của mình
                baseGiaoDichQuery = baseGiaoDichQuery.Where(x => x.MaNguoiThucHien == maNguoiDung);
            }
            else
            {
                // Lấy toàn bộ giao dịch của gia đình (cả chung lẫn các thành viên)
                baseGiaoDichQuery = baseGiaoDichQuery.Where(x => x.TaiKhoanTien.LaTaiKhoanGiaDinh || x.MaNguoiThucHien == maNguoiDung);
            }

            // 3. THỐNG KÊ THÁNG HIỆN TẠI
            var giaoDichThangNayQuery = baseGiaoDichQuery
                .Where(x => x.NgayGiaoDich.Month == thangHienTai && x.NgayGiaoDich.Year == namHienTai);

            decimal thuThangNay = await giaoDichThangNayQuery
                .Where(x => x.LoaiGiaoDich == "Thu")
                .SumAsync(x => (decimal?)x.SoTien) ?? 0;

            decimal chiThangNay = await giaoDichThangNayQuery
                .Where(x => x.LoaiGiaoDich == "Chi")
                .SumAsync(x => (decimal?)x.SoTien) ?? 0;

            decimal chenhLechThangNay = thuThangNay - chiThangNay;

            // 4. GIAO DỊCH GẦN ĐÂY (6 giao dịch mới nhất)
            var giaoDichGanDay = await baseGiaoDichQuery
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .OrderByDescending(x => x.NgayGiaoDich)
                .Take(6)
                .ToListAsync();

            // 5. TIẾT KIỆM THÁNG NÀY
            var baseLichSuTietKiemQuery = _context.LichSuTietKiems
                .Where(x => x.MucTieuTietKiem != null &&
                            (x.MucTieuTietKiem.MaTruongNhom == maNguoiDung ||
                             x.MucTieuTietKiem.ThanhVienMucTieus.Any(t => t.MaNguoiDung == maNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia)));

            decimal guiTietKiemThangNay = await baseLichSuTietKiemQuery
                .Where(x => x.LoaiGiaoDich == LoaiGiaoDichQuy.NapTien &&
                            x.NgayThucHien.Month == thangHienTai &&
                            x.NgayThucHien.Year == namHienTai)
                .SumAsync(x => (decimal?)x.SoTien) ?? 0;

            // 6. NGÂN SÁCH THÁNG HIỆN TẠI & TỶ LỆ DÙNG
            var nganSachs = await _context.NganSachs
                .Include(x => x.DanhMuc)
                .Where(x => x.MaGiaDinh == maGiaDinh &&
                            x.Thang == thangHienTai &&
                            x.Nam == namHienTai)
                .ToListAsync();

            decimal tongHanMucNganSach = nganSachs.Sum(x => x.SoTienHanMuc);
            decimal tongDaChiNganSach = nganSachs.Sum(x => x.SoTienDaSuDung);

            // 7. MỤC TIÊU TIẾT KIỆM
            var mucTieuTietKiems = await _context.MucTieuTietKiems
                .Where(x => x.MaTruongNhom == maNguoiDung ||
                            x.ThanhVienMucTieus.Any(t => t.MaNguoiDung == maNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia))
                .OrderBy(x => x.TrangThai == TrangThaiMucTieu.DaHoanThanh)
                .ThenBy(x => x.NgayHan)
                .Take(4)
                .ToListAsync();

            // 8. TRUYỀN DỮ LIỆU SANG VIEW
            ViewBag.LaChuHo = LaChuHo(nguoiDung, hoGiaDinh);
            ViewBag.NguoiDung = nguoiDung;
            ViewBag.HoGiaDinh = hoGiaDinh;
            ViewBag.TaiKhoans = taiKhoans;
            ViewBag.TongSoDu = tongSoDu;

            ViewBag.ThangHienTai = thangHienTai;
            ViewBag.NamHienTai = namHienTai;
            ViewBag.ThuThangNay = thuThangNay;
            ViewBag.ChiThangNay = chiThangNay;
            ViewBag.ChenhLechThangNay = chenhLechThangNay;
            ViewBag.GuiTietKiemThangNay = guiTietKiemThangNay;

            ViewBag.NganSachs = nganSachs;
            ViewBag.TongHanMucNganSach = tongHanMucNganSach;
            ViewBag.TongDaChiNganSach = tongDaChiNganSach;

            ViewBag.MucTieuTietKiems = mucTieuTietKiems;
            ViewBag.GiaoDichGanDay = giaoDichGanDay;

            // Lưu lại chế độ xem hiện tại để View hiển thị tương ứng nếu cần
            ViewBag.CheDoXem = cheDo;

            return View();
        }
    }
}