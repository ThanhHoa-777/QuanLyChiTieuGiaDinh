using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    
    [Authorize]
    public class LichSuTietKiemsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LichSuTietKiemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<NguoiDung?> LayNguoiDungDangNhap()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        // DANH SÁCH LỊCH SỬ TIẾT KIỆM (TK-03, BR48)
        // Cùng quy tắc "mục tiêu của tôi" với MucTieuTietKiemController.Index:
        // trưởng nhóm hoặc thành viên ĐÃ THAM GIA.
        public async Task<IActionResult> Index(int? maMucTieu)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var query = _context.LichSuTietKiems
                .Include(l => l.MucTieuTietKiem)
                .Include(l => l.NguoiThucHien)
                .Include(l => l.TaiKhoanNguon)
                .Include(l => l.DanhMuc)
                .Where(l => l.MucTieuTietKiem!.MaTruongNhom == nguoiDung.MaNguoiDung ||
                            l.MucTieuTietKiem!.ThanhVienMucTieus.Any(t => t.MaNguoiDung == nguoiDung.MaNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia));

            if (maMucTieu.HasValue)
            {
                query = query.Where(l => l.MaMucTieu == maMucTieu.Value);
            }

            var danhSach = await query
                .OrderByDescending(l => l.NgayThucHien)
                .ThenByDescending(l => l.MaLichSuTietKiem)
                .ToListAsync();

            ViewBag.MaMucTieu = maMucTieu;
            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;

            return View(danhSach);
        }

        // NẠP TIỀN: chuyển sang MucTieuTietKiem/NapTien (nơi duy nhất xử lý nạp)
        // Giữ lại action này để các liên kết cũ tới LichSuTietKiems/Create vẫn hoạt động.
        [HttpGet]
        public IActionResult Create(int? maMucTieu)
        {
            if (maMucTieu.HasValue)
                return RedirectToAction("NapTien", "MucTieuTietKiem", new { id = maMucTieu.Value });

            TempData["Warning"] = "Hãy chọn mục tiêu muốn nạp tiền, rồi bấm \"Nạp tiền\".";
            return RedirectToAction("Index", "MucTieuTietKiem");
        }

        // CHI TIẾT
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var lichSu = await _context.LichSuTietKiems
                .Include(l => l.MucTieuTietKiem)
                .Include(l => l.NguoiThucHien)
                .Include(l => l.TaiKhoanNguon)
                .Include(l => l.DanhMuc)
                .FirstOrDefaultAsync(l => l.MaLichSuTietKiem == id);

            if (lichSu == null) return NotFound();

            // Chỉ trưởng nhóm / thành viên đã tham gia mục tiêu mới xem được
            bool coQuyen = await _context.MucTieuTietKiems.AnyAsync(m =>
                m.MaMucTieu == lichSu.MaMucTieu &&
                (m.MaTruongNhom == nguoiDung.MaNguoiDung ||
                 m.ThanhVienMucTieus.Any(t => t.MaNguoiDung == nguoiDung.MaNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia)));

            if (!coQuyen) return Forbid();

            return View(lichSu);
        }
    }
}
