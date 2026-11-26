using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public class DanhMucsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DanhMucsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private const string Active = "Active";
        private const string Inactive = "Inactive";

        private static readonly string[] CacLoaiHopLe = { "ThuNhap", "ChiTieu" };

        private enum KetQuaTao { Moi, DaCoSan, KhoiPhuc }

        #region Helpers & Phân quyền

        private static bool LoaiHopLe(string? loai) => loai != null && CacLoaiHopLe.Contains(loai);

        // BR34: Làm sạch chuỗi: bỏ dấu (kể cả đ/Đ), chữ thường, gộp khoảng trắng
        private static string LamSachChuoi(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 'đ' không bị tách dấu bởi FormD nên phải thay tay, nếu không "Đi chợ" != "di cho"
            string s = text.Trim().ToLowerInvariant().Replace('đ', 'd');
            string normalized = s.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            string result = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(result, @"\s+", " ").Trim();
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

        private static bool LaChuHo(NguoiDung nguoiDung, HoGiaDinh hoGiaDinh)
            => hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung;

        // Chủ hộ sửa/xóa mọi danh mục của hộ; thành viên chỉ sửa/xóa danh mục mình tạo. Danh mục hệ thống: không ai được sửa.
        private static bool CoQuyenSuaXoa(DanhMuc danhMuc, NguoiDung nguoiDung, HoGiaDinh hoGiaDinh)
        {
            if (danhMuc.MaGiaDinh == null) return false;
            if (LaChuHo(nguoiDung, hoGiaDinh)) return true;
            return danhMuc.MaNguoiTao == nguoiDung.MaNguoiDung;
        }

        private async Task<bool> CoGiaoDichAsync(int maDanhMuc)
        {
            return await _context.GiaoDichs.AnyAsync(x => x.MaDanhMuc == maDanhMuc) ||
                   await _context.GiaoDichDinhKys.AnyAsync(x => x.MaDanhMuc == maDanhMuc);
        }

        /// <summary>
        /// Tìm danh mục trùng tên (không phân biệt hoa/thường/dấu) trong cùng loại, gồm danh mục của hộ
        /// và danh mục hệ thống đang hoạt động. So sánh tên ở bộ nhớ (danh sách nhỏ) nên không phụ thuộc
        /// cột TenDanhMucClean cũ (dữ liệu seed có thể để trống hoặc làm sạch theo công thức cũ).
        /// Ưu tiên bản đang hoạt động, rồi danh mục hệ thống.
        /// </summary>
        private async Task<DanhMuc?> TimDanhMucTrungAsync(int maGiaDinh, string loai, string tenDaLamSach, int? boQuaId = null)
        {
            var ungVien = await _context.DanhMucs
                .Where(x => x.LoaiDanhMuc == loai &&
                            (x.MaGiaDinh == maGiaDinh || (x.MaGiaDinh == null && x.TrangThai == Active)) &&
                            (boQuaId == null || x.MaDanhMuc != boQuaId))
                .ToListAsync();

            return ungVien
                .Where(x => LamSachChuoi(x.TenDanhMuc) == tenDaLamSach)
                .OrderBy(x => x.TrangThai == Active ? 0 : 1)
                .ThenBy(x => x.MaGiaDinh == null ? 0 : 1)
                .ThenBy(x => x.MaDanhMuc)
                .FirstOrDefault();
        }

        /// <summary>
        /// Điểm vào DUY NHẤT để tạo danh mục (form chính và tạo nhanh đều dùng).
        /// Trùng => dùng lại bản có sẵn (không tạo thêm, không coi là lỗi). Bản của hộ đã ẩn => khôi phục.
        /// </summary>
        private async Task<(DanhMuc DanhMuc, KetQuaTao KetQua)> TaoHoacLayDanhMucAsync(
            NguoiDung nguoiDung, int maGiaDinh, string ten, string loai,
            string? bieuTuong, string? mauSac)
        {
            ten = ten.Trim();
            string tenClean = LamSachChuoi(ten);

            var trung = await TimDanhMucTrungAsync(maGiaDinh, loai, tenClean);
            if (trung != null)
                return await DungDanhMucCoSanAsync(trung, maGiaDinh);

            var moi = new DanhMuc
            {
                TenDanhMuc = ten,
                TenDanhMucClean = tenClean,
                LoaiDanhMuc = loai,
                MaGiaDinh = maGiaDinh,
                MaNguoiTao = nguoiDung.MaNguoiDung,
                BieuTuong = bieuTuong,
                MauSac = mauSac,
                TrangThai = Active
            };

            _context.DanhMucs.Add(moi);
            try
            {
                await _context.SaveChangesAsync();
                return (moi, KetQuaTao.Moi);
            }
            catch (DbUpdateException)
            {
                // Hai yêu cầu tạo cùng lúc: nếu có unique index thì bản thứ 2 bị từ chối, ta lấy bản đã lưu.
                _context.Entry(moi).State = EntityState.Detached;
                var daCo = await TimDanhMucTrungAsync(maGiaDinh, loai, tenClean);
                if (daCo == null) throw;
                return (daCo, KetQuaTao.DaCoSan);
            }
        }

        private async Task<(DanhMuc, KetQuaTao)> DungDanhMucCoSanAsync(DanhMuc trung, int maGiaDinh)
        {
            if (trung.TrangThai != Active && trung.MaGiaDinh == maGiaDinh)
            {
                trung.TrangThai = Active;
                await _context.SaveChangesAsync();
                return (trung, KetQuaTao.KhoiPhuc);
            }
            return (trung, KetQuaTao.DaCoSan);
        }

        #endregion

        // INDEX - Danh sách, tìm kiếm, lọc (BR33)
        public async Task<IActionResult> Index(string? tuKhoa, string? loai, bool hienDaAn = false)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
                return View(new List<DanhMuc>());

            int maHo = hoGiaDinh.MaGiaDinh;

            var query = _context.DanhMucs
                .Include(x => x.NguoiTao)
                .Where(x => (x.MaGiaDinh == null && x.TrangThai == Active) ||
                            (x.MaGiaDinh == maHo && (hienDaAn || x.TrangThai == Active)));

            if (LoaiHopLe(loai))
                query = query.Where(x => x.LoaiDanhMuc == loai);

            var ds = await query
                .OrderBy(x => x.LoaiDanhMuc)
                .ThenBy(x => x.TenDanhMuc)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                string k = LamSachChuoi(tuKhoa);
                ds = ds.Where(x => LamSachChuoi(x.TenDanhMuc).Contains(k)).ToList();
            }

            // Số giao dịch của từng danh mục thuộc hộ (view dùng để hiện nút Xóa hay Ẩn)
            var dem = await _context.GiaoDichs
                .Where(g => g.DanhMuc != null && g.DanhMuc.MaGiaDinh == maHo)
                .GroupBy(g => g.MaDanhMuc)
                .Select(g => new { Ma = g.Key, So = g.Count() })
                .ToListAsync();

            ViewBag.SoGiaoDich = dem.ToDictionary(a => Convert.ToInt32(a.Ma), a => a.So);
            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = LaChuHo(nguoiDung, hoGiaDinh);
            ViewBag.HoGiaDinh = hoGiaDinh;
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.Loai = loai;
            ViewBag.HienDaAn = hienDaAn;

            return View(ds);
        }

        // DETAILS
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var danhMuc = await _context.DanhMucs
                .Include(x => x.NguoiTao)
                .FirstOrDefaultAsync(x => x.MaDanhMuc == id &&
                    (x.MaGiaDinh == null || x.MaGiaDinh == hoGiaDinh.MaGiaDinh));

            if (danhMuc == null) return NotFound();

            ViewBag.CoQuyenSuaXoa = CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh);

            if (danhMuc.MaGiaDinh != null)
            {
                ViewBag.SoGiaoDich = await _context.GiaoDichs.CountAsync(x => x.MaDanhMuc == id);
            }

            return View(danhMuc);
        }

        // CREATE
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

            return View(new DanhMuc { LoaiDanhMuc = "ChiTieu", MauSac = "#0d6efd" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenDanhMuc,LoaiDanhMuc,BieuTuong,MauSac")] DanhMuc danhMuc)
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

            if (!LoaiHopLe(danhMuc.LoaiDanhMuc))
                ModelState.AddModelError(nameof(danhMuc.LoaiDanhMuc), "Loại danh mục không hợp lệ.");

            if (!ModelState.IsValid)
                return View(danhMuc);

            // BR34/BR35: trùng => dùng lại danh mục có sẵn, chỉ lưu MỘT bản và không báo lỗi
            var (kq, ketQua) = await TaoHoacLayDanhMucAsync(nguoiDung, hoGiaDinh.MaGiaDinh,
                danhMuc.TenDanhMuc, danhMuc.LoaiDanhMuc, danhMuc.BieuTuong, danhMuc.MauSac);

            TempData["ThongBao"] = ketQua switch
            {
                KetQuaTao.Moi => $"Thêm danh mục \"{kq.TenDanhMuc}\" thành công.",
                KetQuaTao.KhoiPhuc => $"Danh mục \"{kq.TenDanhMuc}\" đã từng có nên được khôi phục, không tạo thêm bản mới.",
                _ => $"Danh mục \"{kq.TenDanhMuc}\" đã có sẵn nên không tạo thêm."
            };
            return RedirectToAction(nameof(Index));
        }

        // BR36: TẠO NHANH TỪ FORM GIAO DỊCH (AJAX)
        // Luôn trả success=true kèm danh mục để form chọn luôn. daTonTai=true nếu dùng lại bản có sẵn.
        [HttpPost]
        public async Task<IActionResult> TaoNhanhInline(string? tenDanhMuc, string? loaiDanhMuc)
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
            if (tenDanhMuc.Length > 100)
                return Json(new { success = false, message = "Tên danh mục không được vượt quá 100 ký tự." });

            // Bắt buộc client gửi loại; không tự đoán "ChiTieu" để tránh lưu sai khi người dùng chọn Thu nhập
            string loai = loaiDanhMuc?.Trim() ?? string.Empty;
            if (!LoaiHopLe(loai))
                return Json(new { success = false, message = "Vui lòng chọn loại danh mục (Thu nhập / Chi tiêu)." });

            var (kq, ketQua) = await TaoHoacLayDanhMucAsync(nguoiDung, hoGiaDinh.MaGiaDinh,
                tenDanhMuc, loai, "🏷️", "#0d6efd");

            return Json(new
            {
                success = true,
                daTonTai = ketQua != KetQuaTao.Moi,
                maDanhMuc = kq.MaDanhMuc,
                tenDanhMuc = kq.TenDanhMuc,
                loaiDanhMuc = kq.LoaiDanhMuc,
                bieuTuong = kq.BieuTuong,
                mauSac = kq.MauSac,
                message = ketQua switch
                {
                    KetQuaTao.Moi => "Đã tạo danh mục mới.",
                    KetQuaTao.KhoiPhuc => "Danh mục đã từng có, đã được khôi phục và chọn tự động.",
                    _ => "Danh mục đã tồn tại và được chọn tự động."
                }
            });
        }

        // KIỂM TRA TRÙNG khi đang gõ (AJAX GET). Trùng không phải lỗi, chỉ gợi ý dùng danh mục có sẵn.
        [HttpGet]
        public async Task<IActionResult> KiemTraTrung(string? ten, string? loai, int? maDangSua)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Json(new { trung = false });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null || string.IsNullOrWhiteSpace(ten)) return Json(new { trung = false });

            string loaiChuan = loai?.Trim() ?? string.Empty;
            if (!LoaiHopLe(loaiChuan)) return Json(new { trung = false });

            var trung = await TimDanhMucTrungAsync(hoGiaDinh.MaGiaDinh, loaiChuan, LamSachChuoi(ten), maDangSua);
            if (trung == null) return Json(new { trung = false });

            return Json(new
            {
                trung = true,
                maDanhMuc = trung.MaDanhMuc,
                tenDanhMuc = trung.TenDanhMuc,
                laHeThong = trung.MaGiaDinh == null,
                daAn = trung.TrangThai != Active
            });
        }

        // EDIT
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

            if (!CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền chỉnh sửa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.KhoaLoai = await CoGiaoDichAsync(danhMuc.MaDanhMuc);

            return View(danhMuc);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaDanhMuc,TenDanhMuc,LoaiDanhMuc,BieuTuong,MauSac")] DanhMuc danhMuc)
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

            bool khoaLoai = await CoGiaoDichAsync(id);

            if (!LoaiHopLe(danhMuc.LoaiDanhMuc))
            {
                ModelState.AddModelError(nameof(danhMuc.LoaiDanhMuc), "Loại danh mục không hợp lệ.");
            }
            else
            {
                // Đổi loại sẽ làm sai lệch báo cáo của các giao dịch cũ
                if (danhMuc.LoaiDanhMuc != danhMucCu.LoaiDanhMuc && khoaLoai)
                    ModelState.AddModelError(nameof(danhMuc.LoaiDanhMuc),
                        "Danh mục đã phát sinh giao dịch nên không thể đổi loại.");

                // Đổi tên thành tên của danh mục khác thì không thể lưu 2 bản giống nhau.
                var trung = await TimDanhMucTrungAsync(hoGiaDinh.MaGiaDinh, danhMuc.LoaiDanhMuc, tenClean, id);
                if (trung != null && trung.TrangThai == Active)
                    ModelState.AddModelError(nameof(danhMuc.TenDanhMuc),
                        $"Đã có danh mục \"{trung.TenDanhMuc}\" cùng loại. Hãy dùng danh mục đó hoặc đặt tên khác.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.KhoaLoai = khoaLoai;
                return View(danhMuc);
            }

            danhMucCu.TenDanhMuc = danhMuc.TenDanhMuc;
            danhMucCu.TenDanhMucClean = tenClean;
            danhMucCu.LoaiDanhMuc = danhMuc.LoaiDanhMuc;
            danhMucCu.BieuTuong = danhMuc.BieuTuong;
            danhMucCu.MauSac = danhMuc.MauSac;

            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật danh mục thành công.";
            return RedirectToAction(nameof(Index));
        }

        // DELETE (BR37: đã có giao dịch thì chặn xóa, cho Ẩn)
        public async Task<IActionResult> Delete(int? id)
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

            if (!CoQuyenSuaXoa(danhMuc, nguoiDung, hoGiaDinh))
            {
                TempData["ThongBao"] = "Bạn không có quyền xóa danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            int soGiaoDich = await _context.GiaoDichs.CountAsync(x => x.MaDanhMuc == id);
            int soGiaoDichDinhKy = await _context.GiaoDichDinhKys.CountAsync(x => x.MaDanhMuc == id);
            int tongSoGiaoDich = soGiaoDich + soGiaoDichDinhKy;
            ViewBag.CoThaoTacXoa = tongSoGiaoDich == 0;
            // View nên hiện nút "Ẩn danh mục" (POST AnDanhMuc) khi không xóa được
            ViewBag.CoTheAn = danhMuc.TrangThai == Active && tongSoGiaoDich > 0;

            if (tongSoGiaoDich > 0)
                ViewBag.CanhBaoKhoe = $"Không thể xóa danh mục [{danhMuc.TenDanhMuc}] vì đã có {tongSoGiaoDich} giao dịch phát sinh. " +
                                      "Bạn có thể Ẩn danh mục (không dùng cho giao dịch mới, báo cáo cũ vẫn đúng).";

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

            if (await CoGiaoDichAsync(id))
            {
                TempData["ThongBao"] = $"Không thể xóa danh mục [{danhMuc.TenDanhMuc}] vì đã có giao dịch phát sinh. Hãy dùng chức năng Ẩn.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.DanhMucs.Remove(danhMuc);
                await _context.SaveChangesAsync();
                TempData["ThongBao"] = "Đã xóa danh mục thành công.";
            }
            catch (DbUpdateException)
            {
                // Còn bảng khác (ví dụ ngân sách) đang tham chiếu danh mục này
                _context.Entry(danhMuc).State = EntityState.Unchanged;
                TempData["ThongBao"] = "Danh mục đang được dùng ở chức năng khác (ví dụ ngân sách) nên không thể xóa. Hãy dùng chức năng Ẩn.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ẨN / KHÔI PHỤC (xóa mềm, giữ nguyên dữ liệu giao dịch cũ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnDanhMuc(int id)
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
                TempData["ThongBao"] = "Bạn không có quyền ẩn danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            danhMuc.TrangThai = Inactive;
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = $"Đã ẩn danh mục \"{danhMuc.TenDanhMuc}\". Các giao dịch cũ vẫn giữ nguyên.";
            return RedirectToAction(nameof(Index), new { hienDaAn = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiPhuc(int id)
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
                TempData["ThongBao"] = "Bạn không có quyền khôi phục danh mục này.";
                return RedirectToAction(nameof(Index));
            }

            // Không để có 2 danh mục hoạt động trùng tên
            var trung = await TimDanhMucTrungAsync(hoGiaDinh.MaGiaDinh, danhMuc.LoaiDanhMuc, LamSachChuoi(danhMuc.TenDanhMuc), id);
            if (trung != null && trung.TrangThai == Active)
            {
                TempData["ThongBao"] = $"Đã có danh mục \"{trung.TenDanhMuc}\" đang hoạt động nên không thể khôi phục bản này. Hãy dùng danh mục đó.";
                return RedirectToAction(nameof(Index), new { hienDaAn = true });
            }

            danhMuc.TrangThai = Active;
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = $"Đã khôi phục danh mục \"{danhMuc.TenDanhMuc}\".";
            return RedirectToAction(nameof(Index));
        }
    }
}