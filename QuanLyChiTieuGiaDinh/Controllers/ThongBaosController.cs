using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class ThongBaosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongBaosController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<NguoiDung?> LayNguoiDungDangNhapAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);
        }

        // =====================================================
        // TRUNG TÂM THÔNG BÁO (TAB CÁ NHÂN & TAB GIA ĐÌNH)
        // =====================================================
        public async Task<IActionResult> Index(string tab = "CaNhan", string filter = "tat-ca")
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var phamViCurrent = tab.Equals("GiaDinh", StringComparison.OrdinalIgnoreCase) ? PhamViThongBao.GiaDinh : PhamViThongBao.CaNhan;
            var query = _context.ThongBaos.Where(x => x.MaNguoiDung == nguoiDung.MaNguoiDung);

            ViewBag.CountChuaDocCaNhan = await query.CountAsync(x => x.PhamVi == PhamViThongBao.CaNhan && !x.DaDoc);
            ViewBag.CountChuaDocGiaDinh = await query.CountAsync(x => x.PhamVi == PhamViThongBao.GiaDinh && !x.DaDoc);

            var filteredQuery = query.Where(x => x.PhamVi == phamViCurrent);
            if (filter.Equals("chua-doc", StringComparison.OrdinalIgnoreCase))
            {
                filteredQuery = filteredQuery.Where(x => !x.DaDoc);
            }

            var thongBaos = await filteredQuery.OrderBy(x => x.DaDoc).ThenByDescending(x => x.NgayTao).ToListAsync();
            ViewBag.TabCurrent = tab;
            ViewBag.FilterCurrent = filter;

            return View(thongBaos);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var thongBao = await _context.ThongBaos.FirstOrDefaultAsync(x => x.MaThongBao == id && x.MaNguoiDung == nguoiDung.MaNguoiDung);
            if (thongBao == null) return NotFound();

            if (!thongBao.DaDoc)
            {
                thongBao.DaDoc = true;
                await _context.SaveChangesAsync();
            }
            return View(thongBao);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhDauTatCaDaDoc(string tab = "CaNhan")
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var phamViCurrent = tab.Equals("GiaDinh", StringComparison.OrdinalIgnoreCase) ? PhamViThongBao.GiaDinh : PhamViThongBao.CaNhan;
            var thongBaos = await _context.ThongBaos.Where(x => x.MaNguoiDung == nguoiDung.MaNguoiDung && x.PhamVi == phamViCurrent && !x.DaDoc).ToListAsync();

            if (thongBaos.Any())
            {
                foreach (var thongBao in thongBaos) thongBao.DaDoc = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { tab });
        }

        // =====================================================
        // XỬ LÝ LỜI MỜI DIRECTLY (CHẤP NHẬN / TỪ CHỐI TỪ THÔNG BÁO)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChapNhanLoiMoi(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var thongBao = await _context.ThongBaos.FirstOrDefaultAsync(x => x.MaThongBao == id && x.MaNguoiDung == nguoiDung.MaNguoiDung);
            if (thongBao == null || thongBao.IsThaoTacXong) return NotFound();

            // Tìm lời mời tương ứng dựa vào email
            var loiMoi = await _context.LoiMoiGiaDinhs
                .Where(l => l.EmailNguoiNhan == nguoiDung.Email && l.TrangThai == "ChoXacNhan")
                .OrderByDescending(l => l.NgayGui)
                .FirstOrDefaultAsync();

            if (loiMoi == null)
            {
                thongBao.IsThaoTacXong = true;
                thongBao.DaDoc = true;
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Lời mời này không còn tồn tại hoặc đã được thu hồi.";
                return RedirectToAction(nameof(Index), new { tab = thongBao.PhamVi.ToString() });
            }

            bool daThuocHo = await _context.ThanhVienGiaDinhs.AnyAsync(tv => tv.MaNguoiDung == nguoiDung.MaNguoiDung && tv.TrangThai == "DaXacNhan")
                          || await _context.HoGiaDinhs.AnyAsync(h => h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");

            if (daThuocHo)
            {
                TempData["ErrorMessage"] = "Bạn đã thuộc một hộ gia đình khác. Vui lòng rời hộ hiện tại trước khi chấp nhận.";
                return RedirectToAction(nameof(Index), new { tab = thongBao.PhamVi.ToString() });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                loiMoi.TrangThai = "DaXacNhan";
                _context.ThanhVienGiaDinhs.Add(new ThanhVienGiaDinh
                {
                    MaGiaDinh = loiMoi.MaGiaDinh,
                    MaNguoiDung = nguoiDung.MaNguoiDung,
                    VaiTro = "ThanhVien",
                    NgayThamGia = DateTime.Now,
                    TrangThai = "DaXacNhan"
                });

                thongBao.IsThaoTacXong = true;
                thongBao.DaDoc = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Đã chấp nhận lời mời gia nhập hộ gia đình thành công!";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi hệ thống khi chấp nhận lời mời.";
            }

            return RedirectToAction(nameof(Index), new { tab = thongBao.PhamVi.ToString() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhDauDaDoc(int id)
        {
            var thongBao = await _context.ThongBaos.FindAsync(id);
            if (thongBao == null)
            {
                return NotFound();
            }

            // Đánh dấu đã đọc
            thongBao.DaDoc = true;

            _context.ThongBaos.Update(thongBao);
            await _context.SaveChangesAsync();

            // Quay lại đúng tab hiện tại (Cá nhân hoặc Gia đình)
            string tab = thongBao.PhamVi == PhamViThongBao.GiaDinh ? "GiaDinh" : "CaNhan";

            return RedirectToAction(nameof(Index), new { tab = tab });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoiLoiMoi(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var thongBao = await _context.ThongBaos.FirstOrDefaultAsync(x => x.MaThongBao == id && x.MaNguoiDung == nguoiDung.MaNguoiDung);
            if (thongBao == null || thongBao.IsThaoTacXong) return NotFound();

            var loiMoi = await _context.LoiMoiGiaDinhs
                .Where(l => l.EmailNguoiNhan == nguoiDung.Email && l.TrangThai == "ChoXacNhan")
                .OrderByDescending(l => l.NgayGui)
                .FirstOrDefaultAsync();

            if (loiMoi != null)
            {
                loiMoi.TrangThai = "TuChoi";
            }

            thongBao.IsThaoTacXong = true;
            thongBao.DaDoc = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã từ chối lời mời gia nhập hộ gia đình.";
            return RedirectToAction(nameof(Index), new { tab = thongBao.PhamVi.ToString() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var thongBao = await _context.ThongBaos.FirstOrDefaultAsync(x => x.MaThongBao == id && x.MaNguoiDung == nguoiDung.MaNguoiDung);
            if (thongBao == null) return NotFound();

            var phamVi = thongBao.PhamVi.ToString();

            _context.ThongBaos.Remove(thongBao);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { tab = phamVi });
        }
    }
}