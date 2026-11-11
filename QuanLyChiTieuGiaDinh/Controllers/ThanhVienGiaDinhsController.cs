//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using QuanLyChiTieuGiaDinh.Data;
//using QuanLyChiTieuGiaDinh.Models;
//using QuanLyChiTieuGiaDinh.Services;
//using System.Security.Claims;

//namespace QuanLyChiTieuGiaDinh.Controllers
//{
//    public class ThanhVienGiaDinhsController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly ThongBaoService _thongBaoService;
//        private readonly NhatKyHeThongService _nhatKyService;

//        public ThanhVienGiaDinhsController(
//            ApplicationDbContext context,
//            ThongBaoService thongBaoService,
//            NhatKyHeThongService nhatKyService)
//        {
//            _context = context;
//            _thongBaoService = thongBaoService;
//            _nhatKyService = nhatKyService;
//        }

//        #region Private Helpers

//        private async Task<NguoiDung?> LayNguoiDungDangNhap()
//        {
//            var email = User.FindFirstValue(ClaimTypes.Email);

//            if (string.IsNullOrWhiteSpace(email))
//                return null;

//            return await _context.NguoiDungs
//                .FirstOrDefaultAsync(x => x.Email == email);
//        }

//        // Lấy hộ gia đình đang hoạt động mà người dùng làm Chủ hộ hoặc Thành viên đã xác nhận.
//        private async Task<HoGiaDinh?> LayHoGiaDinhCuaNguoiDung(int maNguoiDung)
//        {
//            var hoChu = await _context.HoGiaDinhs
//                .FirstOrDefaultAsync(x => x.MaChuHo == maNguoiDung && x.TrangThai == "HoatDong");

//            if (hoChu != null)
//                return hoChu;

//            return await _context.ThanhVienGiaDinhs
//                .Where(x => x.MaNguoiDung == maNguoiDung && x.TrangThai == "DaXacNhan")
//                .Select(x => x.HoGiaDinh)
//                .FirstOrDefaultAsync(x => x != null && x.TrangThai == "HoatDong");
//        }

//        private async Task<bool> LaChuHo(int maNguoiDung, int maGiaDinh)
//        {
//            return await _context.HoGiaDinhs.AnyAsync(x =>
//                x.MaGiaDinh == maGiaDinh &&
//                x.MaChuHo == maNguoiDung &&
//                x.TrangThai == "HoatDong");
//        }

//        // BR03: Kiểm tra người dùng có đang thuộc một hộ gia đình khác đang hoạt động hay không.
//        private async Task<bool> ThuocHoKhac(int maNguoiDung, int maGiaDinhHienTai)
//        {
//            bool laChuHoHoKhac = await _context.HoGiaDinhs
//                .AnyAsync(x => x.MaChuHo == maNguoiDung &&
//                               x.MaGiaDinh != maGiaDinhHienTai &&
//                               x.TrangThai == "HoatDong");

//            if (laChuHoHoKhac)
//                return true;

//            return await _context.ThanhVienGiaDinhs
//                .AnyAsync(x => x.MaNguoiDung == maNguoiDung &&
//                               x.MaGiaDinh != maGiaDinhHienTai &&
//                               x.TrangThai == "DaXacNhan" &&
//                               _context.HoGiaDinhs.Any(h => h.MaGiaDinh == x.MaGiaDinh && h.TrangThai == "HoatDong"));
//        }

//        // BR14: Kiểm tra xem thành viên còn giao dịch/chi phí chưa quyết toán không
//        private async Task<bool> KiemTraCongNoChuaQuyetToan(int maGiaDinh, int maNguoiDung)
//        {
//            bool coGiaoDichChuaQuyetToan = await _context.GiaoDichs.AnyAsync(g =>
//                g.MaNguoiThucHien == maNguoiDung &&
//                _context.TaiKhoanTiens.Any(tk => tk.MaTaiKhoan == g.MaTaiKhoan && tk.MaGiaDinh == maGiaDinh) &&
//                g.TrangThaiQuyetToan == "ChuaQuyetToan");

//            return coGiaoDichChuaQuyetToan;
//        }

//        private string TaoNoiDungThanhVien(ThanhVienGiaDinh tv)
//        {
//            return $"Mã thành viên: {tv.MaThanhVien}; " +
//                   $"Mã hộ: {tv.MaGiaDinh}; " +
//                   $"Mã người dùng: {tv.MaNguoiDung}; " +
//                   $"Vai trò: {tv.VaiTro}; " +
//                   $"Trạng thái: {tv.TrangThai}";
//        }

//        #endregion

//        // =====================================================
//        // 1. DANH SÁCH THÀNH VIÊN TRONG HỘ
//        // =====================================================
//        public async Task<IActionResult> Index()
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
//            if (ho == null)
//            {
//                TempData["Error"] = "Bạn chưa tham gia hộ gia đình nào.";
//                return RedirectToAction("Index", "HoGiaDinhs");
//            }

//            var danhSach = await _context.ThanhVienGiaDinhs
//                .Include(x => x.NguoiDung)
//                .Where(x => x.MaGiaDinh == ho.MaGiaDinh)
//                .OrderBy(x => x.TrangThai)
//                .ThenBy(x => x.NgayThamGia)
//                .ToListAsync();

//            ViewBag.HoGiaDinh = ho;
//            ViewBag.LaChuHo = ho.MaChuHo == nguoiDung.MaNguoiDung;
//            ViewBag.MaNguoiDungHienTai = nguoiDung.MaNguoiDung;

//            return View(danhSach);
//        }

//        // =====================================================
//        // 2. MỜI THÀNH VIÊN VÀO HỘ (HGĐ-02)
//        // =====================================================
//        [HttpGet]
//        public async Task<IActionResult> ThemThanhVien()
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await _context.HoGiaDinhs
//                .FirstOrDefaultAsync(x => x.MaChuHo == nguoiDung.MaNguoiDung && x.TrangThai == "HoatDong");

//            if (ho == null)
//            {
//                TempData["Error"] = "Chỉ chủ hộ mới có thể mời thành viên.";
//                return RedirectToAction(nameof(Index));
//            }

//            ViewBag.HoGiaDinh = ho;
//            return View();
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> ThemThanhVien(string email)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await _context.HoGiaDinhs
//                .FirstOrDefaultAsync(x => x.MaChuHo == nguoiDung.MaNguoiDung && x.TrangThai == "HoatDong");

//            if (ho == null)
//            {
//                TempData["Error"] = "Không tìm thấy hộ gia đình mà bạn làm chủ hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            if (string.IsNullOrWhiteSpace(email))
//            {
//                TempData["Error"] = "Vui lòng nhập email.";
//                ViewBag.HoGiaDinh = ho;
//                return View();
//            }

//            email = email.Trim();
//            var nguoiDuocMoi = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);

//            if (nguoiDuocMoi == null)
//            {
//                TempData["Error"] = "Không tìm thấy tài khoản nào đăng ký với email này.";
//                ViewBag.HoGiaDinh = ho;
//                return View();
//            }

//            if (nguoiDuocMoi.MaNguoiDung == nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Bạn không thể tự mời chính mình.";
//                ViewBag.HoGiaDinh = ho;
//                return View();
//            }

//            // BR06: Kiểm tra nếu tài khoản đang thuộc hộ khác
//            if (await ThuocHoKhac(nguoiDuocMoi.MaNguoiDung, ho.MaGiaDinh))
//            {
//                TempData["Error"] = "Tài khoản này đang thuộc một hộ gia đình khác. Họ cần rời hộ cũ trước khi tham gia.";
//                ViewBag.HoGiaDinh = ho;
//                return View();
//            }

//            var thanhVienCu = await _context.ThanhVienGiaDinhs
//                .FirstOrDefaultAsync(x => x.MaGiaDinh == ho.MaGiaDinh && x.MaNguoiDung == nguoiDuocMoi.MaNguoiDung);

//            if (thanhVienCu != null)
//            {
//                if (thanhVienCu.TrangThai == "DaXacNhan")
//                {
//                    TempData["Error"] = "Tài khoản này đã là thành viên của hộ gia đình.";
//                    return RedirectToAction(nameof(Index));
//                }

//                if (thanhVienCu.TrangThai == "ChoXacNhan")
//                {
//                    TempData["Error"] = "Lời mời đến tài khoản này đang chờ xác nhận.";
//                    return RedirectToAction(nameof(Index));
//                }

//                // Gửi lại lời mời đối với thành viên từng từ chối/rời hộ
//                var noiDungCu = TaoNoiDungThanhVien(thanhVienCu);
//                thanhVienCu.TrangThai = "ChoXacNhan";
//                thanhVienCu.VaiTro = "ThanhVien";
//                thanhVienCu.NgayThamGia = DateTime.Now;

//                await _context.SaveChangesAsync();

//                await _nhatKyService.GhiSuaAsync(nguoiDung.MaNguoiDung, "ThanhVienGiaDinh", thanhVienCu.MaThanhVien, noiDungCu, TaoNoiDungThanhVien(thanhVienCu));
//                await _thongBaoService.TaoThongBaoAsync(nguoiDuocMoi.MaNguoiDung, "📩 Lời mời gia đình", $"Bạn nhận được lời mời tham gia gia đình \"{ho.TenGiaDinh}\".", "ThanhVien");

//                TempData["Success"] = $"Đã gửi lại lời mời đến {email}.";
//                return RedirectToAction(nameof(Index));
//            }

//            var thanhVienMoi = new ThanhVienGiaDinh
//            {
//                MaGiaDinh = ho.MaGiaDinh,
//                MaNguoiDung = nguoiDuocMoi.MaNguoiDung,
//                VaiTro = "ThanhVien",
//                NgayThamGia = DateTime.Now,
//                TrangThai = "ChoXacNhan"
//            };

//            _context.ThanhVienGiaDinhs.Add(thanhVienMoi);
//            await _context.SaveChangesAsync();

//            await _nhatKyService.GhiThemAsync(nguoiDung.MaNguoiDung, "ThanhVienGiaDinh", thanhVienMoi.MaThanhVien, $"Gửi lời mời đến {nguoiDuocMoi.Email} cho hộ {ho.TenGiaDinh}");
//            await _thongBaoService.TaoThongBaoAsync(nguoiDuocMoi.MaNguoiDung, "📩 Lời mời gia đình", $"Bạn nhận được lời mời tham gia gia đình \"{ho.TenGiaDinh}\".", "ThanhVien");

//            TempData["Success"] = $"Đã gửi lời mời tham gia hộ đến {email}.";
//            return RedirectToAction(nameof(Index));
//        }

//        // =====================================================
//        // 3. XỬ LÝ LỜI MỜI (CHO NGƯỜI ĐƯỢC MỜI)
//        // =====================================================
//        public async Task<IActionResult> LoiMoi()
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var danhSach = await _context.ThanhVienGiaDinhs
//                .Include(x => x.HoGiaDinh)
//                .Where(x => x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "ChoXacNhan")
//                .OrderByDescending(x => x.NgayThamGia)
//                .ToListAsync();

//            return View(danhSach);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> ChapNhan(int id)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            await using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                var loiMoi = await _context.ThanhVienGiaDinhs
//                    .Include(x => x.HoGiaDinh)
//                    .FirstOrDefaultAsync(x => x.MaThanhVien == id && x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "ChoXacNhan");

//                if (loiMoi == null)
//                {
//                    await transaction.RollbackAsync();
//                    TempData["Error"] = "Lời mời không tồn tại hoặc đã được xử lý.";
//                    return RedirectToAction(nameof(LoiMoi));
//                }

//                // BR03, BR10: Bắt buộc không thuộc hộ nào trước khi chấp nhận
//                if (await ThuocHoKhac(nguoiDung.MaNguoiDung, loiMoi.MaGiaDinh))
//                {
//                    await transaction.RollbackAsync();
//                    TempData["Error"] = "Bạn đang thuộc một hộ gia đình khác. Vui lòng gửi yêu cầu rời hộ cũ trước.";
//                    return RedirectToAction(nameof(LoiMoi));
//                }

//                var noiDungCu = TaoNoiDungThanhVien(loiMoi);
//                loiMoi.TrangThai = "DaXacNhan";
//                loiMoi.NgayThamGia = DateTime.Now;

//                await _context.SaveChangesAsync();
//                await _nhatKyService.GhiSuaAsync(nguoiDung.MaNguoiDung, "ThanhVienGiaDinh", loiMoi.MaThanhVien, noiDungCu, TaoNoiDungThanhVien(loiMoi));

//                await transaction.CommitAsync();

//                TempData["Success"] = $"Chấp nhận thành công! Bạn đã trở thành thành viên của \"{loiMoi.HoGiaDinh?.TenGiaDinh}\".";
//                return RedirectToAction(nameof(Index));
//            }
//            catch
//            {
//                await transaction.RollbackAsync();
//                TempData["Error"] = "Có lỗi xảy ra khi xử lý chấp nhận lời mời.";
//                return RedirectToAction(nameof(LoiMoi));
//            }
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> TuChoi(int id)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var loiMoi = await _context.ThanhVienGiaDinhs
//                .FirstOrDefaultAsync(x => x.MaThanhVien == id && x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "ChoXacNhan");

//            if (loiMoi == null)
//            {
//                TempData["Error"] = "Lời mời không tồn tại hoặc đã được xử lý.";
//                return RedirectToAction(nameof(LoiMoi));
//            }

//            var noiDungCu = TaoNoiDungThanhVien(loiMoi);
//            loiMoi.TrangThai = "TuChoi";

//            await _context.SaveChangesAsync();
//            await _nhatKyService.GhiSuaAsync(nguoiDung.MaNguoiDung, "ThanhVienGiaDinh", loiMoi.MaThanhVien, noiDungCu, TaoNoiDungThanhVien(loiMoi));

//            TempData["Success"] = "Bạn đã từ chối lời mời tham gia gia đình.";
//            return RedirectToAction(nameof(LoiMoi));
//        }

//        // =====================================================
//        // 4. XÓA THÀNH VIÊN HOẶC HỦY LỜI MỜI (HGĐ-03)
//        // =====================================================
//        [HttpGet]
//        public async Task<IActionResult> XacNhanXoa(int? id)
//        {
//            if (id == null)
//                return NotFound();

//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var thanhVien = await _context.ThanhVienGiaDinhs
//                .Include(x => x.HoGiaDinh)
//                .Include(x => x.NguoiDung)
//                .FirstOrDefaultAsync(x => x.MaThanhVien == id);

//            if (thanhVien == null)
//                return NotFound();

//            if (thanhVien.HoGiaDinh == null || thanhVien.HoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Chỉ chủ hộ mới được xóa thành viên.";
//                return RedirectToAction(nameof(Index));
//            }

//            if (thanhVien.MaNguoiDung == nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Chủ hộ không thể tự xóa chính mình.";
//                return RedirectToAction(nameof(Index));
//            }

//            return View(thanhVien);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> XoaThanhVien(int id)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var thanhVien = await _context.ThanhVienGiaDinhs
//                .Include(x => x.HoGiaDinh)
//                .Include(x => x.NguoiDung)
//                .FirstOrDefaultAsync(x => x.MaThanhVien == id);

//            if (thanhVien == null)
//            {
//                TempData["Error"] = "Không tìm thấy thành viên.";
//                return RedirectToAction(nameof(Index));
//            }

//            if (thanhVien.HoGiaDinh == null || thanhVien.HoGiaDinh.MaChuHo != nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Chỉ chủ hộ mới có quyền gỡ thành viên khỏi hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            if (thanhVien.MaNguoiDung == nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Chủ hộ không thể tự xóa chính mình.";
//                return RedirectToAction(nameof(Index));
//            }

//            var noiDungCu = TaoNoiDungThanhVien(thanhVien);

//            // Gỡ liên kết thành viên (Bảo toàn lịch sử dữ liệu tài chính cũ theo quy tắc)
//            _context.ThanhVienGiaDinhs.Remove(thanhVien);
//            await _context.SaveChangesAsync();

//            await _nhatKyService.GhiXoaAsync(nguoiDung.MaNguoiDung, "ThanhVienGiaDinh", id, noiDungCu);

//            TempData["Success"] = thanhVien.TrangThai == "ChoXacNhan"
//                ? "Đã hủy lời mời tham gia hộ."
//                : "Đã gỡ thành viên khỏi hộ gia đình. Tài khoản này đã trở về trạng thái tự do.";

//            return RedirectToAction(nameof(Index));
//        }

//        // =====================================================
//        // 5. YÊU CẦU RỜI HỘ & DUYỆT RỜI HỘ (HGĐ-04)
//        // =====================================================
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> GuiYeuCauRoiHo(int maGiaDinh)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.TrangThai == "HoatDong");
//            if (ho == null)
//                return NotFound();

//            if (ho.MaChuHo == nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Chủ hộ không thể tự rời hộ. Hãy chuyển vai trò Chủ hộ hoặc Giải tán hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            // BR14: Ràng buộc kiểm tra công nợ tài chính
//            bool coCongNo = await KiemTraCongNoChuaQuyetToan(maGiaDinh, nguoiDung.MaNguoiDung);
//            string trangThaiYeuCau = coCongNo ? "ChoQuyetToan" : "ChoDuyet";

//            var yeuCau = new YeuCauRoiHo
//            {
//                MaGiaDinh = maGiaDinh,
//                MaNguoiDung = nguoiDung.MaNguoiDung,
//                ThoiDiemGui = DateTime.Now,
//                TrangThai = trangThaiYeuCau
//            };

//            _context.YeuCauRoiHos.Add(yeuCau);
//            await _context.SaveChangesAsync();

//            if (coCongNo)
//            {
//                TempData["Error"] = "Bạn còn khoản công nợ/chi tiêu chưa quyết toán. Yêu cầu rời hộ của bạn đang ở trạng thái 'Chờ quyết toán'.";
//            }
//            else
//            {
//                // Gửi thông báo đến Chủ hộ
//                await _thongBaoService.TaoThongBaoAsync(ho.MaChuHo, "🚪 Yêu cầu rời hộ", $"Thành viên {nguoiDung.HoTen} đã gửi yêu cầu rời hộ gia đình.", "YeuCauRoiHo");
//                TempData["Success"] = "Đã gửi yêu cầu rời hộ tới Chủ hộ thành công. Vui lòng chờ xét duyệt.";
//            }

//            return RedirectToAction(nameof(Index));
//        }

//        // Danh sách các yêu cầu rời hộ gửi tới Chủ hộ
//        public async Task<IActionResult> DanhSachYeuCauRoiHo()
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
//            if (ho == null)
//            {
//                TempData["Error"] = "Chỉ Chủ hộ mới có quyền duyệt yêu cầu rời hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            var yeuCaus = await _context.YeuCauRoiHos
//                .Include(y => y.NguoiDung)
//                .Where(y => y.MaGiaDinh == ho.MaGiaDinh)
//                .OrderByDescending(y => y.ThoiDiemGui)
//                .ToListAsync();

//            return View(yeuCaus);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> XuLyYeuCauRoiHo(int maYeuCau, bool dongY)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var yeuCau = await _context.YeuCauRoiHos
//                .Include(y => y.HoGiaDinh)
//                .FirstOrDefaultAsync(y => y.MaYeuCau == maYeuCau);

//            if (yeuCau == null || yeuCau.HoGiaDinh?.MaChuHo != nguoiDung.MaNguoiDung)
//            {
//                TempData["Error"] = "Bạn không có quyền xử lý yêu cầu này.";
//                return RedirectToAction(nameof(Index));
//            }

//            if (yeuCau.TrangThai == "ChoQuyetToan")
//            {
//                TempData["Error"] = "Thành viên chưa hoàn tất quyết toán công nợ. Không thể duyệt rời hộ.";
//                return RedirectToAction(nameof(DanhSachYeuCauRoiHo));
//            }

//            yeuCau.NguoiDuyet = nguoiDung.MaNguoiDung;
//            yeuCau.ThoiDiemXuLy = DateTime.Now;

//            if (dongY)
//            {
//                yeuCau.TrangThai = "DongY";

//                // Chấm dứt quan hệ thành viên
//                var thanhVien = await _context.ThanhVienGiaDinhs
//                    .FirstOrDefaultAsync(tv => tv.MaGiaDinh == yeuCau.MaGiaDinh && tv.MaNguoiDung == yeuCau.MaNguoiDung);

//                if (thanhVien != null)
//                {
//                    _context.ThanhVienGiaDinhs.Remove(thanhVien);
//                }

//                await _thongBaoService.TaoThongBaoAsync(yeuCau.MaNguoiDung, "✅ Kết quả rời hộ", "Chủ hộ đã chấp nhận yêu cầu rời hộ của bạn. Tài khoản của bạn đã trở về trạng thái tự do.", "KetQuaRoiHo");
//                TempData["Success"] = "Đã chấp nhận yêu cầu rời hộ.";
//            }
//            else
//            {
//                yeuCau.TrangThai = "TuChoi";
//                await _thongBaoService.TaoThongBaoAsync(yeuCau.MaNguoiDung, "❌ Kết quả rời hộ", "Chủ hộ đã từ chối yêu cầu rời hộ của bạn.", "KetQuaRoiHo");
//                TempData["Success"] = "Đã từ chối yêu cầu rời hộ.";
//            }

//            await _context.SaveChangesAsync();
//            return RedirectToAction(nameof(DanhSachYeuCauRoiHo));
//        }

//        // =====================================================
//        // 6. CHUYỂN VAI TRÒ CHỦ HỘ (HGĐ-05)
//        // =====================================================
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> ChuyenVaiTroChuHo(int maGiaDinh, int maChuHoMoi)
//        {
//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
//            if (ho == null)
//            {
//                TempData["Error"] = "Chỉ Chủ hộ hiện tại mới có quyền chuyển vai trò Chủ hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            var thanhVienMoi = await _context.ThanhVienGiaDinhs
//                .FirstOrDefaultAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == maChuHoMoi && tv.TrangThai == "DaXacNhan");

//            if (thanhVienMoi == null)
//            {
//                TempData["Error"] = "Thành viên được chọn không hợp lệ hoặc không thuộc hộ.";
//                return RedirectToAction(nameof(Index));
//            }

//            await using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                // Hạ vai trò Chủ hộ cũ xuống Thành viên
//                var chuHoCu = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == nguoiDung.MaNguoiDung);
//                if (chuHoCu != null) chuHoCu.VaiTro = "ThanhVien";

//                // Nâng vai trò Thành viên mới lên Chủ hộ
//                thanhVienMoi.VaiTro = "ChuHo";
//                ho.MaChuHo = maChuHoMoi;

//                // Ghi lại lịch sử chuyển giao
//                _context.LichSuChuyenChuHos.Add(new LichSuChuyenChuHo
//                {
//                    MaGiaDinh = maGiaDinh,
//                    MaChuHoCu = nguoiDung.MaNguoiDung,
//                    MaChuHoMoi = maChuHoMoi,
//                    ThoiGianChuyenGiao = DateTime.Now
//                });

//                await _context.SaveChangesAsync();
//                await transaction.CommitAsync();

//                await _thongBaoService.TaoThongBaoAsync(maChuHoMoi, "👑 Bàn giao quyền Chủ hộ", $"Bạn đã được bàn giao làm Chủ hộ mới của \"{ho.TenGiaDinh}\".", "ThanhVien");

//                TempData["Success"] = "Chuyển quyền Chủ hộ thành công. Bạn hiện là Thành viên của hộ.";
//                return RedirectToAction(nameof(Index));
//            }
//            catch
//            {
//                await transaction.RollbackAsync();
//                TempData["Error"] = "Có lỗi xảy ra khi thực hiện chuyển quyền Chủ hộ.";
//                return RedirectToAction(nameof(Index));
//            }
//        }

//        // =====================================================
//        // 7. CHI TIẾT THÀNH VIÊN
//        // =====================================================
//        public async Task<IActionResult> ChiTiet(int? id)
//        {
//            if (id == null)
//                return NotFound();

//            var nguoiDung = await LayNguoiDungDangNhap();
//            if (nguoiDung == null)
//                return Challenge();

//            var thanhVien = await _context.ThanhVienGiaDinhs
//                .Include(x => x.HoGiaDinh)
//                .Include(x => x.NguoiDung)
//                .FirstOrDefaultAsync(x => x.MaThanhVien == id);

//            if (thanhVien == null)
//                return NotFound();

//            bool laChuHo = await LaChuHo(nguoiDung.MaNguoiDung, thanhVien.MaGiaDinh);
//            bool laThanhVienDaXacNhan = await _context.ThanhVienGiaDinhs
//                .AnyAsync(x => x.MaGiaDinh == thanhVien.MaGiaDinh &&
//                               x.MaNguoiDung == nguoiDung.MaNguoiDung &&
//                               x.TrangThai == "DaXacNhan");

//            if (!laChuHo && !laThanhVienDaXacNhan)
//            {
//                TempData["Error"] = "Bạn không có quyền xem thông tin thành viên này.";
//                return RedirectToAction(nameof(Index));
//            }

//            ViewBag.LaChuHo = laChuHo;
//            return View(thanhVien);
//        }
//    }
//}