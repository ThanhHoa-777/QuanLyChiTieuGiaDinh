
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    public class DanhMucsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DanhMucsController(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Helpers & Phân quyền

        // BR34: Hàm làm sạch chuỗi loại bỏ dấu và chuyển về chữ thường để kiểm tra so sánh trùng lặp
        private static string LamSachChuoi(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string normalizedString = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(result, @"\s+", " ");
        }

        private string TaoNoiDungDanhMuc(DanhMuc danhMuc)
        {
            return $"Tên: {danhMuc.TenDanhMuc}; " +
                   $"Loại: {danhMuc.LoaiDanhMuc}; " +
                   $"Biểu tượng: {danhMuc.BieuTuong ?? "Không có"}; " +
                   $"Màu sắc: {danhMuc.MauSac ?? "Đen"}; " +
                   $"Phạm vi: {(danhMuc.MaGiaDinh.HasValue ? $"Gia đình {danhMuc.MaGiaDinh}" : "Hệ thống")}";
        }

        private async Task<NguoiDung?> LayNguoiDungDangNhap()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);
        }

        private async Task<HoGiaDinh?> LayHoGiaDinhCuaNguoiDung(int maNguoiDung)
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

        // Ma trận phân quyền: Kiểm tra xem người dùng có quyền Sửa / Xóa danh mục hay không
        private bool CoQuyenSuaXoa(DanhMuc danhMuc, NguoiDung nguoiDung, HoGiaDinh hoGiaDinh)
        {
            if (danhMuc.MaGiaDinh == null) return false;

            if (LaChuHo(nguoiDung, hoGiaDinh)) return true;

            // Thành viên chỉ được phép sửa/xóa danh mục do chính mình tạo ra
            return danhMuc.MaNguoiTao == nguoiDung.MaNguoiDung;
        }

        private async Task NapDanhMucCha(int maGiaDinh, int? maDanhMucDangSua = null)
        {
            var danhSach = await _context.DanhMucs
                .Where(x => (x.MaGiaDinh == maGiaDinh || x.MaGiaDinh == null) &&
                            x.MaDanhMuc != maDanhMucDangSua &&
                            x.TrangThai == "Active")
                .OrderBy(x => x.LoaiDanhMuc)
                .ThenBy(x => x.TenDanhMuc)
                .ToListAsync();

            ViewBag.DanhMucCha = danhSach;
        }

        #endregion

        // =========================================================
        // INDEX - Danh sách Danh mục (BR33)
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
                return View(new List<DanhMuc>());

            // BR33: Lấy toàn bộ danh mục dùng chung của Hộ gia đình và hệ thống
            var danhSachDanhMuc = await _context.DanhMucs
                .Include(x => x.DanhMucCha)
                .Include(x => x.NguoiTao)
                .Where(x => (x.MaGiaDinh == null || x.MaGiaDinh == hoGiaDinh.MaGiaDinh) && x.TrangThai == "Active")
                .OrderBy(x => x.LoaiDanhMuc)
                .ThenBy(x => x.TenDanhMuc)
                .ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = LaChuHo(nguoiDung, hoGiaDinh);
            ViewBag.HoGiaDinh = hoGiaDinh;

            return View(danhSachDanhMuc);
        }

        // =========================================================
        // DETAILS - Chi tiết danh mục
        // =========================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMuc = await _context.DanhMucs
                .Include(x => x.DanhMucCha)
                .Include(x => x.NguoiTao)
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id &&
                    (x.MaGiaDinh == null || x.MaGiaDinh == hoGiaDinh.MaGiaDinh));

            if (danhMuc == null) return NotFound();

            ViewBag.CoQuyenSuaXoa = CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh);
            return View(danhMuc);
        }

        // =========================================================
        // CREATE - Thêm Danh mục (Màn hình chính)
        // =========================================================
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa thuộc hộ gia đình nào.";
                return RedirectToAction(nameof(Index));
            }

            await NapDanhMucCha(hoGiaDinh.MaGiaDinh);
            return View(new DanhMuc { LoaiDanhMuc = "ChiTieu", MauSac = "#0d6efd" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenDanhMuc,LoaiDanhMuc,BieuTuong,MauSac,MaDanhMucCha")] DanhMuc danhMuc)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                ModelState.AddModelError("", "Bạn chưa thuộc hộ gia đình nào.");
                return View(danhMuc);
            }

            danhMuc.TenDanhMuc = danhMuc.TenDanhMuc?.Trim() ?? string.Empty;
            string tenClean = LamSachChuoi(danhMuc.TenDanhMuc);

            // BR34 & BR35: Kiểm tra trùng lặp không phân biệt hoa thường và dấu
            bool daTonTai = await _context.DanhMucs.AnyAsync(x =>
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.LoaiDanhMuc == danhMuc.LoaiDanhMuc &&
                x.TenDanhMucClean == tenClean &&
                x.TrangThai == "Active");

            if (daTonTai)
            {
                ModelState.AddModelError("TenDanhMuc", "Danh mục này đã tồn tại trong hệ thống hộ gia đình.");
            }

            if (!ModelState.IsValid)
            {
                await NapDanhMucCha(hoGiaDinh.MaGiaDinh);
                return View(danhMuc);
            }

            danhMuc.MaGiaDinh = hoGiaDinh.MaGiaDinh;
            danhMuc.MaNguoiTao = nguoiDung.MaNguoiDung;
            danhMuc.TenDanhMucClean = tenClean;
            danhMuc.TrangThai = "Active";

            _context.DanhMucs.Add(danhMuc);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = $"Thêm danh mục \"{danhMuc.TenDanhMuc}\" thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // BR36: TẠO NHANH INLINE TỪ FORM GIAO DỊCH (API/AJAX)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> TaoNhanhInline(string tenDanhMuc, string loaiDanhMuc)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
                return Json(new { success = false, message = "Bạn chưa thuộc hộ gia đình nào." });

            if (string.IsNullOrWhiteSpace(tenDanhMuc))
                return Json(new { success = false, message = "Tên danh mục không được để trống." });

            tenDanhMuc = tenDanhMuc.Trim();
            string tenClean = LamSachChuoi(tenDanhMuc);

            // BR34, BR35: Kiểm tra nếu đã tồn tại thì tự động chọn danh mục hiện có đó
            var danhMucDaCo = await _context.DanhMucs.FirstOrDefaultAsync(x =>
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.LoaiDanhMuc == loaiDanhMuc &&
                x.TenDanhMucClean == tenClean &&
                x.TrangThai == "Active");

            if (danhMucDaCo != null)
            {
                return Json(new
                {
                    success = true,
                    maDanhMuc = danhMucDaCo.MaDanhMuc,
                    tenDanhMuc = danhMucDaCo.TenDanhMuc,
                    message = "Danh mục đã tồn tại và được chọn tự động."
                });
            }

            var danhMucMoi = new DanhMuc
            {
                TenDanhMuc = tenDanhMuc,
                TenDanhMucClean = tenClean,
                LoaiDanhMuc = string.IsNullOrWhiteSpace(loaiDanhMuc) ? "ChiTieu" : loaiDanhMuc,
                MaGiaDinh = hoGiaDinh.MaGiaDinh,
                MaNguoiTao = nguoiDung.MaNguoiDung,
                BieuTuong = "🏷️",
                MauSac = "#0d6efd",
                TrangThai = "Active"
            };

            _context.DanhMucs.Add(danhMucMoi);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                maDanhMuc = danhMucMoi.MaDanhMuc,
                tenDanhMuc = danhMucMoi.TenDanhMuc
            });
        }

        // =========================================================
        // EDIT - Sửa Danh mục
        // =========================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMuc = await _context.DanhMucs
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (danhMuc == null) return NotFound();

            // Phân quyền Sửa
            if (!CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền chỉnh sửa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            await NapDanhMucCha(hoGiaDinh.MaGiaDinh, danhMuc.MaDanhMuc);
            return View(danhMuc);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaDanhMuc,TenDanhMuc,LoaiDanhMuc,BieuTuong,MauSac,MaDanhMucCha")] DanhMuc danhMuc)
        {
            if (id != danhMuc.MaDanhMuc) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMucCu = await _context.DanhMucs
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (danhMucCu == null) return NotFound();

            if (!CoQuyenSuaXoa(danhMucCu, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền sửa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            danhMuc.TenDanhMuc = danhMuc.TenDanhMuc?.Trim() ?? string.Empty;
            string tenClean = LamSachChuoi(danhMuc.TenDanhMuc);

            // BR34: Kiểm tra trùng tên với danh mục khác trong hộ
            var daTonTai = await _context.DanhMucs.AnyAsync(x =>
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.MaDanhMuc != id &&
                x.LoaiDanhMuc == danhMuc.LoaiDanhMuc &&
                x.TenDanhMucClean == tenClean &&
                x.TrangThai == "Active");

            if (daTonTai)
            {
                ModelState.AddModelError("TenDanhMuc", "Tên danh mục này đã trùng với một danh mục khác trong hệ thống.");
            }

            if (danhMuc.MaDanhMucCha == id)
            {
                ModelState.AddModelError("MaDanhMucCha", "Không thể chọn chính danh mục này làm danh mục cha.");
            }

            if (!ModelState.IsValid)
            {
                await NapDanhMucCha(hoGiaDinh.MaGiaDinh, id);
                return View(danhMuc);
            }

            danhMucCu.TenDanhMuc = danhMuc.TenDanhMuc;
            danhMucCu.TenDanhMucClean = tenClean;
            danhMucCu.LoaiDanhMuc = danhMuc.LoaiDanhMuc;
            danhMucCu.BieuTuong = danhMuc.BieuTuong;
            danhMucCu.MauSac = danhMuc.MauSac;
            danhMucCu.MaDanhMucCha = danhMuc.MaDanhMucCha;

            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật danh mục thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DELETE - Xóa Danh mục (Ràng buộc bảo toàn dữ liệu BR37)
        // =========================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMuc = await _context.DanhMucs
                .Include(x => x.DanhMucCha)
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (danhMuc == null) return NotFound();

            if (!CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền xóa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            // BR37: Đếm số lượng giao dịch thực tế & giao dịch định kỳ liên kết
            int soGiaoDich = await _context.GiaoDichs.CountAsync(x => x.MaDanhMuc == id);
            int soGiaoDichDinhKy = await _context.GiaoDichDinhKys.CountAsync(x => x.MaDanhMuc == id);
            int tongSoGiaoDich = soGiaoDich + soGiaoDichDinhKy;

            if (tongSoGiaoDich > 0)
            {
                ViewBag.CanhBaoKhoe = $"Không thể xóa danh mục [{danhMuc.TenDanhMuc}] vì đã có {tongSoGiaoDich} giao dịch phát sinh. Việc xóa sẽ làm sai lệch báo cáo tài chính.";
                ViewBag.CoThaoTacXoa = false;
            }
            else
            {
                ViewBag.CoThaoTacXoa = true;
            }

            return View(danhMuc);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMuc = await _context.DanhMucs
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (danhMuc == null) return NotFound();

            if (!CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền xóa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            // BR37 Backend Enforcement: Tuyệt đối chặn xóa nếu đã có giao dịch phát sinh
            bool coGiaoDich = await _context.GiaoDichs.AnyAsync(x => x.MaDanhMuc == id);
            bool coGiaoDichDinhKy = await _context.GiaoDichDinhKys.AnyAsync(x => x.MaDanhMuc == id);

            if (coGiaoDich || coGiaoDichDinhKy)
            {
                TempData["ThongBao"] = $"Không thể xóa danh mục [{danhMuc.TenDanhMuc}] vì đã có giao dịch phát sinh trong lịch sử.";
                return RedirectToAction(nameof(Index));
            }

            bool coDanhMucCon = await _context.DanhMucs.AnyAsync(x => x.MaDanhMucCha == id && x.TrangThai == "Active");
            if (coDanhMucCon)
            {
                TempData["ThongBao"] = "Không thể xóa danh mục đang có chứa danh mục con.";
                return RedirectToAction(nameof(Index));
            }

            _context.DanhMucs.Remove(danhMuc);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Đã xóa danh mục thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
