using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class TaiKhoanTiensController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaiKhoanTiensController(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Helpers

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

        // BR20: Kiểm tra quyền xem tài khoản (Tài khoản gia đình xem chung, Ví cá nhân xem riêng)
        private bool CoQuyenXem(TaiKhoanTien taiKhoan, NguoiDung nguoiDung)
        {
            if (taiKhoan.LaTaiKhoanGiaDinh || taiKhoan.LoaiVi == "QuyChung")
                return true;

            return taiKhoan.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
        }

        // Quyền sửa / xóa ví cá nhân: Chỉ người sở hữu
        private bool CoQuyenSuaXoa(TaiKhoanTien taiKhoan, NguoiDung nguoiDung)
        {
            if (taiKhoan.LaTaiKhoanGiaDinh || taiKhoan.LoaiVi == "QuyChung" || taiKhoan.LoaiVi == "ViNhom")
                return false; // Quỹ gia đình (BR24) và Ví nhóm do Mục tiêu tiết kiệm quản lý

            return taiKhoan.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
        }

        // Ví nhóm: mọi thành viên ĐÃ THAM GIA mục tiêu (kể cả người ngoài hộ) đều được xem
        private async Task<bool> LaThanhVienViNhomAsync(TaiKhoanTien taiKhoan, int maNguoiDung)
        {
            if (taiKhoan.LoaiVi != "ViNhom" || !taiKhoan.MaMucTieuNguon.HasValue) return false;

            return await _context.ThanhVienMucTieus.AnyAsync(t =>
                t.MaMucTieu == taiKhoan.MaMucTieuNguon.Value &&
                t.MaNguoiDung == maNguoiDung &&
                t.TrangThai == TrangThaiThamGia.DaThamGia);
        }

        // Số tiền thành viên ĐÃ NẠP vào ví trong kỳ hạn mức (từ NgayBatDau đến hết ngày HanChot)
        // Chỉ tính giao dịch đang hoạt động (bỏ qua giao dịch đã xóa và dòng hoàn tiền).
        private async Task<decimal> TinhSoTienDaNapAsync(HanMucChiTieu hanMuc)
        {
            var tuNgay = hanMuc.NgayBatDau.Date;
            var denNgay = hanMuc.HanChot.Date.AddDays(1);

            return await _context.GiaoDichs
                .Where(g => g.MaTaiKhoan == hanMuc.MaTaiKhoan
                         && g.MaNguoiThucHien == hanMuc.MaThanhVien
                         && g.LoaiGiaoDich == "Thu"
                         && g.TrangThai == "HoatDong"
                         && g.NgayGiaoDich >= tuNgay
                         && g.NgayGiaoDich < denNgay)
                .SumAsync(g => (decimal?)g.SoTien) ?? 0;
        }

        #endregion

        // INDEX - Danh sách Ví
        public async Task<IActionResult> Index()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            // Người ngoài hộ (được mời vào nhóm bằng email) có thể chưa thuộc hộ gia đình nào,
            // nhưng vẫn phải thấy Ví nhóm của mình
            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            int? maHo = hoGiaDinh?.MaGiaDinh;

            var mucTieuNhomIds = await _context.ThanhVienMucTieus
                .Where(t => t.MaNguoiDung == nguoiDung.MaNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia)
                .Select(t => t.MaMucTieu)
                .ToListAsync();

            var danhSachTaiKhoan = await _context.TaiKhoanTiens
                .Where(x =>
                    // 1. Quỹ chung của hộ + ví cá nhân của chính mình trong hộ
                    (maHo != null && x.MaGiaDinh == maHo &&
                        (x.LaTaiKhoanGiaDinh || x.LoaiVi == "QuyChung" || x.MaNguoiSoHuu == nguoiDung.MaNguoiDung))
                    // 2. Ví nhóm của các mục tiêu mình đã tham gia
                    || (x.LoaiVi == "ViNhom" && x.MaMucTieuNguon != null && mucTieuNhomIds.Contains(x.MaMucTieuNguon.Value))
                    // 3. Ví mục tiêu cá nhân (không thuộc hộ)
                    || (x.MaGiaDinh == null && x.LoaiVi == "ViCaNhan" && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung))
                .OrderByDescending(x => x.LaTaiKhoanGiaDinh)
                .ThenBy(x => x.LoaiVi == "ViNhom" ? 0 : 1)
                .ThenBy(x => x.TenTaiKhoan)
                .ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            return View(danhSachTaiKhoan);
        }

        // DETAILS - Xem chi tiết tài khoản và lịch sử giao dịch
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            var taiKhoan = await _context.TaiKhoanTiens
                .Include(x => x.NguoiSoHuu)
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id);

            if (taiKhoan == null) return NotFound();

            bool laViNhom = taiKhoan.LoaiVi == "ViNhom";

            if (laViNhom)
            {
                // Ví nhóm: kiểm tra theo tư cách thành viên của mục tiêu, không phụ thuộc hộ gia đình
                if (!await LaThanhVienViNhomAsync(taiKhoan, nguoiDung.MaNguoiDung))
                {
                    TempData["Error"] = "Bạn không phải thành viên của nhóm sở hữu ví này.";
                    return RedirectToAction(nameof(Index));
                }
            }
            else
            {
                bool viCaNhanKhongHo = taiKhoan.MaGiaDinh == null && taiKhoan.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
                if (!viCaNhanKhongHo && (hoGiaDinh == null || taiKhoan.MaGiaDinh != hoGiaDinh.MaGiaDinh))
                    return NotFound();

                // TC21: Bảo mật 100% Ví cá nhân của thành viên khác
                if (!CoQuyenXem(taiKhoan, nguoiDung))
                {
                    TempData["Error"] = "Bạn không có quyền truy cập Ví cá nhân của thành viên khác.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // 1. LẤY DANH SÁCH GIAO DỊCH THUỘC TÀI KHOẢN NÀY
            //    (gồm cả dòng hoàn tiền "HoanTien" và giao dịch đã xóa "DaXoa" để lịch sử biến động số dư đầy đủ)
            var danhSachGiaoDich = await _context.GiaoDichs
                .Include(g => g.NguoiThucHien)
                .Include(g => g.DanhMuc)
                .Where(g => g.MaTaiKhoan == id)
                .OrderByDescending(g => g.NgayGiaoDich)
                .ToListAsync();

            // 2. TÍNH NĂNG HẠN MỨC (chỉ Quỹ chung của hộ)
            if (taiKhoan.LaTaiKhoanGiaDinh || taiKhoan.LoaiVi == "QuyChung")
            {
                var dsHanMuc = await _context.HanMucChiTieus
                    .Include(h => h.ThanhVien)
                    .Where(h => h.MaTaiKhoan == id && h.TrangThai == true)
                    .ToListAsync();

                // Số tiền mỗi thành viên đã nạp / hạn mức (để view hiển thị "Đã nạp đủ" hoặc "Còn thiếu")
                var daNapTheoHanMuc = new Dictionary<int, decimal>();
                foreach (var h in dsHanMuc)
                    daNapTheoHanMuc[h.MaHanMuc] = await TinhSoTienDaNapAsync(h);

                ViewBag.DanhSachHanMuc = dsHanMuc;
                ViewBag.DaNapTheoHanMuc = daNapTheoHanMuc;

                // Cảnh báo (trước 3 ngày): CHỈ cho hạn mức của chính mình, tại ĐÚNG ví này, và CHƯA nạp đủ.
                // Đã nạp đủ thì không cảnh báo nữa.
                var canhBao = new List<string>();
                bool coThayDoi = false;

                foreach (var h in dsHanMuc.Where(h => h.MaThanhVien == nguoiDung.MaNguoiDung
                                                   && h.HanChot.Date <= DateTime.Now.AddDays(3).Date))
                {
                    decimal daNap = daNapTheoHanMuc[h.MaHanMuc];

                    if (daNap >= h.SoTienHanMuc)
                    {
                        if (!h.DaThongBao) { h.DaThongBao = true; coThayDoi = true; }
                        continue;
                    }

                    canhBao.Add($"Ví '{taiKhoan.TenTaiKhoan}': bạn còn thiếu {(h.SoTienHanMuc - daNap):N0}đ " +
                                $"(đã nạp {daNap:N0}/{h.SoTienHanMuc:N0}đ), hạn chót {h.HanChot:dd/MM/yyyy}.");
                }

                if (coThayDoi) await _context.SaveChangesAsync();

                if (canhBao.Any())
                    ViewBag.ThongBaoHanMuc = "CẢNH BÁO: " + string.Join(" ", canhBao);

                // Danh sách thành viên để Chủ hộ chọn khi Cài Hạn Mức
                ViewBag.DanhSachThanhVien = await _context.ThanhVienGiaDinhs
                    .Include(tv => tv.NguoiDung)
                    .Where(tv => tv.MaGiaDinh == hoGiaDinh!.MaGiaDinh && tv.TrangThai == "DaXacNhan")
                    .Select(tv => tv.NguoiDung)
                    .ToListAsync();
            }

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = hoGiaDinh != null && hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung;
            ViewBag.LaViNhom = laViNhom;

            if (laViNhom)
            {
                // Danh sách thành viên nhóm (gồm cả người ngoài hộ) để hiển thị trên trang chi tiết ví
                ViewBag.DanhSachThanhVienNhom = await _context.ThanhVienMucTieus
                    .Include(t => t.NguoiDung)
                    .Where(t => t.MaMucTieu == taiKhoan.MaMucTieuNguon && t.TrangThai == TrangThaiThamGia.DaThamGia)
                    .Select(t => t.NguoiDung)
                    .ToListAsync();
            }

            ViewBag.CoQuyenSuaXoa = CoQuyenSuaXoa(taiKhoan, nguoiDung);
            ViewBag.CoQuyenSuDung = laViNhom || CoQuyenXem(taiKhoan, nguoiDung);
            ViewBag.DanhSachGiaoDich = danhSachGiaoDich;

            return View(taiKhoan);
        }

        // CREATE - Thêm Ví cá nhân mới (TK-05A)

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();

            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(
                nguoiDung.MaNguoiDung);

            if (hoGiaDinh == null)
            {
                TempData["Error"] = "Bạn chưa thuộc hộ gia đình nào.";
                return RedirectToAction(nameof(Index));
            }

            var taiKhoan = new TaiKhoanTien
            {
                LoaiTaiKhoan = "TienMat",
                LoaiVi = "ViCaNhan",
                LaTaiKhoanGiaDinh = false,
                SoDuBanDau = 0,
                SoDuHienTai = 0,
                TrangThai = true
            };

            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("TenTaiKhoan,LoaiTaiKhoan,GhiChu")]
    TaiKhoanTien taiKhoanInput)
        {
            var nguoiDung = await LayNguoiDungDangNhap();

            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(
                nguoiDung.MaNguoiDung);

            if (hoGiaDinh == null)
            {
                TempData["Error"] = "Bạn chưa thuộc hộ gia đình nào.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
                return View(taiKhoanInput);

            var taiKhoanMoi = new TaiKhoanTien
            {
                MaGiaDinh = hoGiaDinh.MaGiaDinh,

                MaNguoiSoHuu = nguoiDung.MaNguoiDung,

                TenTaiKhoan = taiKhoanInput.TenTaiKhoan.Trim(),

                LoaiTaiKhoan = taiKhoanInput.LoaiTaiKhoan,

                // Luôn tạo ví cá nhân
                LoaiVi = "ViCaNhan",
                LaTaiKhoanGiaDinh = false,

                // Số dư luôn mặc định bằng 0
                SoDuBanDau = 0,
                SoDuHienTai = 0,

                GhiChu = taiKhoanInput.GhiChu,

                TrangThai = true
            };

            _context.TaiKhoanTiens.Add(taiKhoanMoi);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo Ví cá nhân thành công.";

            return RedirectToAction(nameof(Index));
        }


        // EDIT - Chỉnh sửa Ví cá nhân (TK-05B)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var taiKhoan = await _context.TaiKhoanTiens
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (taiKhoan == null) return NotFound();

            if (!CoQuyenSuaXoa(taiKhoan, nguoiDung))
            {
                TempData["Error"] = "Tài khoản gia đình chỉ được xem, không được phép sửa hoặc xóa.";
                return RedirectToAction(nameof(Index));
            }

            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaTaiKhoan,TenTaiKhoan,LoaiTaiKhoan,GhiChu,TrangThai")] TaiKhoanTien input)
        {
            if (id != input.MaTaiKhoan) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var taiKhoanCu = await _context.TaiKhoanTiens
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (taiKhoanCu == null) return NotFound();

            if (!CoQuyenSuaXoa(taiKhoanCu, nguoiDung))
            {
                TempData["Error"] = "Tài khoản gia đình chỉ được xem, không được phép sửa hoặc xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                taiKhoanCu.TenTaiKhoan = input.TenTaiKhoan;
                taiKhoanCu.LoaiTaiKhoan = input.LoaiTaiKhoan;
                taiKhoanCu.GhiChu = input.GhiChu;
                taiKhoanCu.TrangThai = input.TrangThai;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật thông tin Ví cá nhân thành công.";
                return RedirectToAction(nameof(Index));
            }

            return View(input);
        }

        // DELETE - Xóa / Khóa / Lưu trữ Ví cá nhân (TK-05C)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var taiKhoan = await _context.TaiKhoanTiens
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (taiKhoan == null) return NotFound();

            if (!CoQuyenSuaXoa(taiKhoan, nguoiDung))
            {
                TempData["Error"] = "Tài khoản gia đình chỉ được xem, không được phép sửa hoặc xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (taiKhoan.SoDuHienTai > 0)
            {
                ViewBag.CanhBaoSoDu = "Ví cá nhân vẫn còn số dư > 0. Bạn cần chuyển toàn bộ số tiền sang ví khác trước khi thực hiện Xóa/Khóa.";
            }

            bool coGiaoDich = await _context.GiaoDichs.AnyAsync(x => x.MaTaiKhoan == taiKhoan.MaTaiKhoan);
            ViewBag.CoGiaoDich = coGiaoDich;

            return View(taiKhoan);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var taiKhoan = await _context.TaiKhoanTiens
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (taiKhoan == null) return NotFound();

            if (!CoQuyenSuaXoa(taiKhoan, nguoiDung))
            {
                TempData["Error"] = "Tài khoản gia đình chỉ được xem, không được phép sửa hoặc xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (taiKhoan.SoDuHienTai > 0)
            {
                TempData["Error"] = "Không thể xóa/khóa ví vì vẫn còn số dư > 0. Vui lòng chuyển hết tiền trước.";
                return RedirectToAction(nameof(Index));
            }

            bool coGiaoDich = await _context.GiaoDichs.AnyAsync(x => x.MaTaiKhoan == taiKhoan.MaTaiKhoan);

            if (!coGiaoDich && taiKhoan.SoDuHienTai == 0)
            {
                _context.TaiKhoanTiens.Remove(taiKhoan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa vĩnh viễn Ví cá nhân.";
            }
            else
            {
                taiKhoan.TrangThai = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ví đã từng phát sinh giao dịch nên đã được chuyển sang trạng thái Lưu trữ/Đã khóa.";
            }

            return RedirectToAction(nameof(Index));
        }

        #region Tính năng mở rộng: Hạn mức (Quỹ chung)

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CaiDatHanMuc(
            int maTaiKhoan,
            int maThanhVien,
            decimal soTienHanMuc,
            DateTime hanChot)
        {
            var nguoiDung = await LayNguoiDungDangNhap();

            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(
                nguoiDung.MaNguoiDung);

            if (hoGiaDinh == null ||
                hoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Chỉ chủ hộ mới có quyền cài đặt hạn mức.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            // Kiểm tra ví có thuộc hộ gia đình và là ví chung không
            var taiKhoan = await _context.TaiKhoanTiens
                .FirstOrDefaultAsync(t =>
                    t.MaTaiKhoan == maTaiKhoan &&
                    t.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                    (t.LaTaiKhoanGiaDinh || t.LoaiVi == "QuyChung"));

            if (taiKhoan == null)
            {
                TempData["Error"] = "Không tìm thấy ví gia đình hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra thành viên thuộc hộ gia đình
            bool laThanhVien = await _context.ThanhVienGiaDinhs
                .AnyAsync(tv =>
                    tv.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                    tv.MaNguoiDung == maThanhVien &&
                    tv.TrangThai == "DaXacNhan");

            if (!laThanhVien)
            {
                TempData["Error"] = "Thành viên không thuộc hộ gia đình này.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            if (soTienHanMuc <= 0)
            {
                TempData["Error"] = "Hạn mức phải lớn hơn 0.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            if (hanChot.Date < DateTime.Today)
            {
                TempData["Error"] = "Hạn chót không được nhỏ hơn ngày hiện tại.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            // Mỗi thành viên chỉ có 1 hạn mức đang hiệu lực trong 1 tháng (tính theo tháng của hạn chót)
            bool daCoHanMucTrongThang = await _context.HanMucChiTieus.AnyAsync(h =>
                h.MaTaiKhoan == maTaiKhoan &&
                h.MaThanhVien == maThanhVien &&
                h.TrangThai == true &&
                h.HanChot.Year == hanChot.Year &&
                h.HanChot.Month == hanChot.Month);

            if (daCoHanMucTrongThang)
            {
                TempData["Error"] = $"Thành viên này đã có hạn mức trong tháng {hanChot:MM/yyyy}. " +
                                    "Mỗi thành viên chỉ được cài 1 hạn mức mỗi tháng (hãy vô hiệu hóa hạn mức cũ nếu muốn cài lại).";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            var hanMuc = new HanMucChiTieu
            {
                MaTaiKhoan = maTaiKhoan,
                MaThanhVien = maThanhVien,
                SoTienHanMuc = soTienHanMuc,
                NgayBatDau = DateTime.Now,
                HanChot = hanChot,
                TrangThai = true,
                DaThongBao = false
            };

            _context.HanMucChiTieus.Add(hanMuc);

            var thongBao = new ThongBao
            {
                MaNguoiDung = maThanhVien,
                PhamVi = PhamViThongBao.GiaDinh,
                LoaiThongBao = LoaiThongBao.HeThong,
                TieuDe = "Cấp hạn mức nộp tiền mới",
                NoiDung = $"Chủ hộ yêu cầu bạn nộp tối đa {soTienHanMuc:N0}đ vào ví gia đình '{taiKhoan.TenTaiKhoan}'. Hạn chót: {hanChot:dd/MM/yyyy}.",
                DuLieuLienKet = $"{{\"MaTaiKhoan\":{maTaiKhoan}}}",
                DaDoc = false,
                IsThaoTacXong = true,
                NgayTao = DateTime.Now
            };

            _context.ThongBaos.Add(thongBao);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cài đặt hạn mức và gửi thông báo cho thành viên.";

            return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
        }


        // POST: Chủ hộ Vô hiệu hóa (Hủy) hạn mức của thành viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoHieuHoaHanMuc(int maHanMuc, int maTaiKhoan)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung!.MaNguoiDung);

            if (hoGiaDinh == null || hoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Chỉ chủ hộ mới có quyền vô hiệu hóa hạn mức.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            var hanMuc = await _context.HanMucChiTieus.FindAsync(maHanMuc);
            if (hanMuc == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin hạn mức.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            // 1. Cập nhật trạng thái thành false (Vô hiệu hóa) thay vì xóa
            hanMuc.TrangThai = false;

            // 2. Tự động gửi thông báo báo cho thành viên biết hạn mức đã bị hủy
            var taiKhoan = await _context.TaiKhoanTiens.FindAsync(maTaiKhoan);
            string tenTaiKhoan = taiKhoan?.TenTaiKhoan ?? "Quỹ chung";

            var thongBao = new ThongBao
            {
                MaNguoiDung = hanMuc.MaThanhVien,
                PhamVi = PhamViThongBao.GiaDinh,
                LoaiThongBao = LoaiThongBao.HeThong,
                TieuDe = "Hủy hạn mức chi tiêu",
                NoiDung = $"Chủ hộ đã hủy giới hạn chi tiêu của bạn tại quỹ '{tenTaiKhoan}'.",
                DuLieuLienKet = $"{{\"MaTaiKhoan\": {maTaiKhoan}}}",
                DaDoc = false,
                IsThaoTacXong = true,
                NgayTao = DateTime.Now
            };
            _context.ThongBaos.Add(thongBao);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã vô hiệu hóa hạn mức thành công.";
            return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
        }
        #endregion
    }
}
