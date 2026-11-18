using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Services;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    public class LichSuTietKiemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        

        public LichSuTietKiemsController(
            ApplicationDbContext context
            )
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

        private async Task NapDuLieuForm(int maNguoiDung, int? maMucTieu = null, int? maTaiKhoan = null)
        {
            // 1. Danh sách các Mục tiêu tiết kiệm người dùng tham gia và đang tích lũy/chi tiêu
            var mucTieuList = await _context.MucTieuTietKiems
                .Where(m => (m.MaTruongNhom == maNguoiDung || m.ThanhVienMucTieus.Any(t => t.MaNguoiDung == maNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia))
                            && m.TrangThai != TrangThaiMucTieu.DaHuy)
                .OrderBy(m => m.TenMucTieu)
                .ToListAsync();

            ViewBag.MaMucTieu = new SelectList(mucTieuList, "MaMucTieu", "TenMucTieu", maMucTieu);

            // 2. Danh sách Ví cá nhân của người dùng
            var taiKhoanList = await _context.TaiKhoanTiens
                .Where(t => t.MaNguoiSoHuu == maNguoiDung && t.TrangThai)
                .OrderBy(t => t.TenTaiKhoan)
                .ToListAsync();

            ViewBag.MaTaiKhoanNguon = new SelectList(taiKhoanList, "MaTaiKhoan", "TenTaiKhoan", maTaiKhoan);
        }

        // =========================================================
        // DANH SÁCH LỊCH SỬ TIẾT KIỆM (TK-03, BR48)
        // =========================================================
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

        // =========================================================
        // FORM NẠP TIỀN TÍCH LŨY THỦ CÔNG (TK-03, BR47)
        // =========================================================
        public async Task<IActionResult> Create(int? maMucTieu)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            await NapDuLieuForm(nguoiDung.MaNguoiDung, maMucTieu);

            var model = new LichSuTietKiem
            {
                MaMucTieu = maMucTieu ?? 0,
                LoaiGiaoDich = LoaiGiaoDichQuy.NapTien,
                NgayThucHien = DateTime.Now
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LichSuTietKiem lichSuTietKiem)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (lichSuTietKiem.SoTien <= 0)
            {
                ModelState.AddModelError("SoTien", "Số tiền nạp phải lớn hơn 0.");
            }

            var mucTieu = await _context.MucTieuTietKiems
                .Include(m => m.ThanhVienMucTieus)
                .FirstOrDefaultAsync(m => m.MaMucTieu == lichSuTietKiem.MaMucTieu);

            if (mucTieu == null)
            {
                ModelState.AddModelError("MaMucTieu", "Mục tiêu tiết kiệm không tồn tại.");
            }

            TaiKhoanTien? taiKhoanNguon = null;
            if (lichSuTietKiem.MaTaiKhoanNguon.HasValue)
            {
                taiKhoanNguon = await _context.TaiKhoanTiens
                    .FirstOrDefaultAsync(t => t.MaTaiKhoan == lichSuTietKiem.MaTaiKhoanNguon && t.MaNguoiSoHuu == nguoiDung.MaNguoiDung && t.TrangThai);

                if (taiKhoanNguon == null)
                {
                    ModelState.AddModelError("MaTaiKhoanNguon", "Ví cá nhân nguồn không hợp lệ.");
                }
                else if (taiKhoanNguon.SoDuHienTai < lichSuTietKiem.SoTien)
                {
                    ModelState.AddModelError("SoTien", "Số dư Ví cá nhân không đủ để nạp tích lũy.");
                }
            }

            if (ModelState.IsValid && mucTieu != null && taiKhoanNguon != null)
            {
                // 1. Trừ tiền Ví cá nhân nguồn
                taiKhoanNguon.SoDuHienTai -= lichSuTietKiem.SoTien;

                // 2. Tăng số tiền tích lũy của Quỹ
                mucTieu.SoTienHienCo += lichSuTietKiem.SoTien;

                // 3. Cập nhật tiến độ đóng góp cá nhân (BR47)
                var tv = mucTieu.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == nguoiDung.MaNguoiDung);
                if (tv != null)
                {
                    tv.SoTienDaDong += lichSuTietKiem.SoTien;
                    if (tv.SoTienDaDong >= tv.ChiTieuCanDong && tv.ChiTieuCanDong > 0)
                    {
                        tv.DaHoanThanhChiTieu = true;
                    }
                }

                // 4. Tự động chuyển đổi thành Ví Quỹ khi đạt 100% mục tiêu (BR49)
                if (mucTieu.SoTienHienCo >= mucTieu.SoTienMucTieu && !mucTieu.IsActiveWallet)
                {
                    mucTieu.IsActiveWallet = true;
                    mucTieu.TrangThai = TrangThaiMucTieu.DangChiTieu;
                }

                // 5. Lưu Lịch sử tiết kiệm
                lichSuTietKiem.MaNguoiThucHien = nguoiDung.MaNguoiDung;
                lichSuTietKiem.LoaiGiaoDich = LoaiGiaoDichQuy.NapTien;
                _context.LichSuTietKiems.Add(lichSuTietKiem);

                await _context.SaveChangesAsync();

              

                TempData["ThongBao"] = $"Đã nạp thành công {lichSuTietKiem.SoTien:N0} ₫ vào mục tiêu tiết kiệm!";
                return RedirectToAction("Details", "MucTieuTietKiems", new { id = lichSuTietKiem.MaMucTieu });
            }

            await NapDuLieuForm(nguoiDung.MaNguoiDung, lichSuTietKiem.MaMucTieu, lichSuTietKiem.MaTaiKhoanNguon);
            return View(lichSuTietKiem);
        }

        // =========================================================
        // CHI TIẾT
        // =========================================================
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

            return View(lichSu);
        }
    }
}