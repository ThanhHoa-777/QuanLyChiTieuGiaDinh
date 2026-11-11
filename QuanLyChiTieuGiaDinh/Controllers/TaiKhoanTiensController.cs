using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
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
            if (taiKhoan.LaTaiKhoanGiaDinh || taiKhoan.LoaiVi == "QuyChung")
                return false; // Quỹ gia đình bảo tồn theo BR24

            return taiKhoan.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
        }

        #endregion

        // =========================================================
        // INDEX - Danh sách Ví
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
                return View(new List<TaiKhoanTien>());

            // Lấy 1. Quỹ chung của hộ và 2. Các Ví cá nhân của chính người dùng đăng nhập
            var danhSachTaiKhoan = await _context.TaiKhoanTiens
                .Where(x => x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                            (x.LaTaiKhoanGiaDinh || x.LoaiVi == "QuyChung" || x.MaNguoiSoHuu == nguoiDung.MaNguoiDung))
                .OrderByDescending(x => x.LaTaiKhoanGiaDinh)
                .ThenBy(x => x.TenTaiKhoan)
                .ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            return View(danhSachTaiKhoan);
        }

        // =========================================================
        // DETAILS - Xem chi tiết tài khoản và lịch sử giao dịch
        // =========================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var taiKhoan = await _context.TaiKhoanTiens
                .Include(x => x.NguoiSoHuu)
                .FirstOrDefaultAsync(x => x.MaTaiKhoan == id && x.MaGiaDinh == hoGiaDinh.MaGiaDinh);

            if (taiKhoan == null) return NotFound();

            // TC21: Bảo mật 100% Ví cá nhân của thành viên khác
            if (!CoQuyenXem(taiKhoan, nguoiDung))
            {
                TempData["Error"] = "Bạn không có quyền truy cập Ví cá nhân của thành viên khác.";
                return RedirectToAction(nameof(Index));
            }

            // 1. LẤY DANH SÁCH GIAO DỊCH THUỘC TÀI KHOẢN NÀY
            var danhSachGiaoDich = await _context.GiaoDichs
                .Include(g => g.NguoiThucHien)
                .Where(g => g.MaTaiKhoan == id)
                .OrderByDescending(g => g.NgayGiaoDich)
                .ToListAsync();

            // 2. TÍNH NĂNG HẠN MỨC: Lấy danh sách hạn mức và cảnh báo nếu sắp đến hạn
            if (taiKhoan.LaTaiKhoanGiaDinh || taiKhoan.LoaiVi == "QuyChung")
            {
                ViewBag.DanhSachHanMuc = await _context.HanMucChiTieus
                    .Include(h => h.ThanhVien)
                    .Where(h => h.MaTaiKhoan == id && h.TrangThai == true)
                    .ToListAsync();

                // Cảnh báo hạn mức (Passive Check) trước 3 ngày
                var hanMucGanHan = await _context.HanMucChiTieus
                    .Where(h => h.MaTaiKhoan == id
                             && h.MaThanhVien == nguoiDung.MaNguoiDung
                             && h.HanChot.Date <= DateTime.Now.AddDays(3).Date
                             && h.TrangThai == true
                             && !h.DaThongBao)
                    .ToListAsync();

                if (hanMucGanHan.Any())
                {
                    ViewBag.ThongBaoHanMuc = "CẢNH BÁO: Bạn sắp đến hạn chót của một số hạn mức chi tiêu trong quỹ chung. Vui lòng kiểm tra!";
                }

                // 3. TÍNH NĂNG HOÀN TIÊN: Lấy danh sách yêu cầu hoàn tiền
                if (hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung)
                {
                    // Chủ hộ xem tất cả yêu cầu chờ duyệt
                    ViewBag.DanhSachYeuCauHoanTien = await _context.YeuCauHoanTiens
                        .Include(y => y.NguoiYeuCau)
                        .Where(y => y.MaTaiKhoan == id && y.TrangThai == "ChoDuyet")
                        .ToListAsync();
                }
                else
                {
                    // Thành viên chỉ xem yêu cầu của chính mình
                    ViewBag.DanhSachYeuCauHoanTien = await _context.YeuCauHoanTiens
                        .Include(y => y.NguoiYeuCau)
                        .Where(y => y.MaTaiKhoan == id && y.MaNguoiYeuCau == nguoiDung.MaNguoiDung)
                        .ToListAsync();
                }

                // Danh sách thành viên để Chủ hộ chọn khi Cài Hạn Mức
                ViewBag.DanhSachThanhVien = await _context.ThanhVienGiaDinhs
                    .Include(tv => tv.NguoiDung)
                    .Where(tv => tv.MaGiaDinh == hoGiaDinh.MaGiaDinh && tv.TrangThai == "DaXacNhan")
                    .Select(tv => tv.NguoiDung)
                    .ToListAsync();
            }

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = (hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung);
            ViewBag.CoQuyenSuaXoa = CoQuyenSuaXoa(taiKhoan, nguoiDung);
            ViewBag.CoQuyenSuDung = CoQuyenXem(taiKhoan, nguoiDung);
            ViewBag.DanhSachGiaoDich = danhSachGiaoDich;

            return View(taiKhoan);
        }

        // =========================================================
        // CREATE - Thêm Ví cá nhân mới (TK-05A)
        // =========================================================
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["Error"] = "Bạn chưa thuộc hộ gia đình nào.";
                return RedirectToAction(nameof(Index));
            }

            return View(new TaiKhoanTien());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenTaiKhoan,LoaiTaiKhoan,SoDuBanDau,GhiChu")] TaiKhoanTien taiKhoanInput)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                ModelState.AddModelError("", "Bạn chưa thuộc hộ gia đình nào.");
                return View(taiKhoanInput);
            }

            if (taiKhoanInput.SoDuBanDau < 0)
            {
                ModelState.AddModelError(nameof(taiKhoanInput.SoDuBanDau), "Số dư ban đầu không được nhỏ hơn 0.");
            }

            if (ModelState.IsValid)
            {
                taiKhoanInput.MaGiaDinh = hoGiaDinh.MaGiaDinh;
                taiKhoanInput.MaNguoiSoHuu = nguoiDung.MaNguoiDung;
                taiKhoanInput.LoaiVi = "ViCaNhan";
                taiKhoanInput.LaTaiKhoanGiaDinh = false;
                taiKhoanInput.SoDuHienTai = taiKhoanInput.SoDuBanDau;
                taiKhoanInput.TrangThai = true;

                _context.TaiKhoanTiens.Add(taiKhoanInput);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tạo Ví cá nhân mới thành công.";
                return RedirectToAction(nameof(Index));
            }

            return View(taiKhoanInput);
        }

        // =========================================================
        // EDIT - Chỉnh sửa Ví cá nhân (TK-05B)
        // =========================================================
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

        // =========================================================
        // DELETE - Xóa / Khóa / Lưu trữ Ví cá nhân (TK-05C)
        // =========================================================
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

        #region Tính năng mở rộng: Hạn mức & Hoàn tiền (Quỹ chung)
        // POST: Chủ hộ Cài đặt hạn mức
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CaiDatHanMuc(int maTaiKhoan, int maThanhVien, decimal soTienHanMuc, DateTime hanChot)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung!.MaNguoiDung);

            if (hoGiaDinh == null || hoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Chỉ chủ hộ mới có quyền cài đặt hạn mức chi tiêu.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            // 1. Tạo bản ghi Hạn mức
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

            // 2. Tạo bản ghi Thông báo gửi đến thành viên (Khớp với cấu trúc Model ThongBao)
            var taiKhoan = await _context.TaiKhoanTiens.FindAsync(maTaiKhoan);
            string tenTaiKhoan = taiKhoan?.TenTaiKhoan ?? "Quỹ chung";

            var thongBao = new ThongBao
            {
                MaNguoiDung = maThanhVien,
                PhamVi = PhamViThongBao.GiaDinh, // Gửi riêng cho cá nhân bị áp hạn mức
                LoaiThongBao = LoaiThongBao.HeThong,
                TieuDe = "Cấp hạn mức chi tiêu mới",
                NoiDung = $"Chủ hộ đã cấp cho bạn hạn mức {soTienHanMuc:N0}đ từ '{tenTaiKhoan}'. Hạn chót sử dụng: {hanChot:dd/MM/yyyy}.",
                DuLieuLienKet = $"{{\"MaTaiKhoan\": {maTaiKhoan}}}", // Lưu vết ID tài khoản dạng JSON
                DaDoc = false,
                IsThaoTacXong = true, // Vì đây chỉ là thông báo xem, không cần bấm duyệt
                NgayTao = DateTime.Now
            };
            _context.ThongBaos.Add(thongBao);

            // Lưu tất cả vào Database cùng lúc
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cài đặt hạn mức và gửi thông báo cho thành viên thành công.";
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
        // POST: Thành viên tạo yêu cầu hoàn tiền khi nạp dư
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoYeuCauHoanTien(int maTaiKhoan, decimal soTien, string lyDo)
        {
            var nguoiDung = await LayNguoiDungDangNhap();

            if (soTien <= 0)
            {
                TempData["Error"] = "Số tiền yêu cầu hoàn phải lớn hơn 0.";
                return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
            }

            var yeuCau = new YeuCauHoanTien
            {
                MaTaiKhoan = maTaiKhoan,
                MaNguoiYeuCau = nguoiDung!.MaNguoiDung,
                SoTien = soTien,
                LyDo = lyDo,
                TrangThai = "ChoDuyet",
                NgayYeuCau = DateTime.Now
            };

            _context.YeuCauHoanTiens.Add(yeuCau);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu hoàn tiền đến Chủ hộ.";
            return RedirectToAction(nameof(Details), new { id = maTaiKhoan });
        }

        // POST: Chủ hộ xử lý yêu cầu hoàn tiền (Duyệt / Từ chối)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyHoanTien(int maYeuCau, string hanhDong) // "Duyet" hoặc "TuChoi"
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung!.MaNguoiDung);

            var yeuCau = await _context.YeuCauHoanTiens.FindAsync(maYeuCau);
            if (yeuCau == null) return NotFound();

            if (hoGiaDinh == null || hoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Bạn không có quyền duyệt yêu cầu hoàn tiền.";
                return RedirectToAction(nameof(Details), new { id = yeuCau.MaTaiKhoan });
            }

            if (yeuCau.TrangThai != "ChoDuyet")
            {
                TempData["Error"] = "Yêu cầu này đã được xử lý trước đó.";
                return RedirectToAction(nameof(Details), new { id = yeuCau.MaTaiKhoan });
            }

            if (hanhDong == "Duyet")
            {
                var taiKhoan = await _context.TaiKhoanTiens.FindAsync(yeuCau.MaTaiKhoan);
                if (taiKhoan == null || taiKhoan.SoDuHienTai < yeuCau.SoTien)
                {
                    TempData["Error"] = "Số dư quỹ chung không đủ để hoàn tiền.";
                    return RedirectToAction(nameof(Details), new { id = yeuCau.MaTaiKhoan });
                }

                taiKhoan.SoDuHienTai -= yeuCau.SoTien;
                yeuCau.TrangThai = "DaDuyet";
            }
            else
            {
                yeuCau.TrangThai = "TuChoi";
            }

            yeuCau.NgayXuLy = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã {(hanhDong == "Duyet" ? "duyệt" : "từ chối")} yêu cầu hoàn tiền.";
            return RedirectToAction(nameof(Details), new { id = yeuCau.MaTaiKhoan });
        }

        #endregion
    }
}