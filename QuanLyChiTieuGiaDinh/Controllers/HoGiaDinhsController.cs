using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Services;
using System.Data;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class HoGiaDinhsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ThongBaoService _thongBaoService;

        public HoGiaDinhsController(
            ApplicationDbContext context,
            ThongBaoService thongBaoService)
        {
            _context = context;
            _thongBaoService = thongBaoService;
        }

        #region Helper Methods

        private async Task<NguoiDung?> LayNguoiDungDangNhap()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);
        }

        private async Task<bool> DaThuocHoGiaDinh(int maNguoiDung, int? boQuaMaGiaDinh = null)
        {
            bool laChuHo = await _context.HoGiaDinhs.AnyAsync(x =>
                x.MaChuHo == maNguoiDung && x.TrangThai == "HoatDong" &&
                (!boQuaMaGiaDinh.HasValue || x.MaGiaDinh != boQuaMaGiaDinh.Value));
            if (laChuHo) return true;

            return await _context.ThanhVienGiaDinhs.AnyAsync(x =>
                x.MaNguoiDung == maNguoiDung && x.TrangThai == "DaXacNhan" &&
                (!boQuaMaGiaDinh.HasValue || x.MaGiaDinh != boQuaMaGiaDinh.Value) &&
                _context.HoGiaDinhs.Any(h => h.MaGiaDinh == x.MaGiaDinh && h.TrangThai == "HoatDong"));
        }

        private async Task<bool> KiemTraCongNoChuaQuyetToan(int maGiaDinh, int maNguoiDung)
        {
            return await _context.GiaoDichs.AnyAsync(g =>
                g.MaNguoiThucHien == maNguoiDung &&
                _context.TaiKhoanTiens.Any(tk => tk.MaTaiKhoan == g.MaTaiKhoan && tk.MaGiaDinh == maGiaDinh) &&
                g.TrangThaiQuyetToan == "ChuaQuyetToan");
        }

        #endregion

        #region 1. Xem & Danh Sách Hộ Gia Đình (HGĐ Index & Details)

        public async Task<IActionResult> Index()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var danhSach = await _context.HoGiaDinhs
                .Include(h => h.ThanhVienGiaDinhs.Where(tv => tv.TrangThai == "DaXacNhan"))
                    .ThenInclude(tv => tv.NguoiDung)
                .Include(h => h.LoiMoiGiaDinhs)
                .Where(h => h.TrangThai == "HoatDong" &&
                    (h.MaChuHo == nguoiDung.MaNguoiDung ||
                     _context.ThanhVienGiaDinhs.Any(tv =>
                         tv.MaGiaDinh == h.MaGiaDinh && tv.MaNguoiDung == nguoiDung.MaNguoiDung && tv.TrangThai == "DaXacNhan")))
                .OrderByDescending(h => h.NgayTao)
                .ToListAsync();

            var loiMoiCuaToi = await _context.LoiMoiGiaDinhs
                .Include(l => l.HoGiaDinh)
                .Include(l => l.NguoiGui)
                .Where(l => l.EmailNguoiNhan == nguoiDung.Email && l.TrangThai == "ChoXacNhan")
                .ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.DaCoHoGiaDinh = await DaThuocHoGiaDinh(nguoiDung.MaNguoiDung);
            ViewBag.LoiMoiCuaToi = loiMoiCuaToi;

            return View(danhSach);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == id && h.TrangThai == "HoatDong" &&
                    (h.MaChuHo == nguoiDung.MaNguoiDung || _context.ThanhVienGiaDinhs.Any(tv => tv.MaGiaDinh == h.MaGiaDinh && tv.MaNguoiDung == nguoiDung.MaNguoiDung && tv.TrangThai == "DaXacNhan")));

            if (ho == null) return NotFound();

            ViewBag.LaChuHo = ho.MaChuHo == nguoiDung.MaNguoiDung;
            ViewBag.ThanhViens = await _context.ThanhVienGiaDinhs.Include(tv => tv.NguoiDung).Where(tv => tv.MaGiaDinh == id && tv.TrangThai == "DaXacNhan").ToListAsync();
            ViewBag.YeuCauRoiHos = await _context.YeuCauRoiHos.Include(y => y.NguoiDung).Where(y => y.MaGiaDinh == id && y.TrangThai == "ChoDuyet").ToListAsync();
            ViewBag.LoiMoisDaGui = await _context.LoiMoiGiaDinhs.Where(l => l.MaGiaDinh == id && l.TrangThai == "ChoXacNhan").ToListAsync();

            return View(ho);
        }

        #endregion

        #region 2. HGĐ-01 — Tạo Hộ Gia Đình

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();
            if (await DaThuocHoGiaDinh(nguoiDung.MaNguoiDung))
            {
                TempData["Error"] = "Bạn đã thuộc một hộ gia đình. Cần rời hộ hiện tại trước khi tạo hộ mới.";
                return RedirectToAction(nameof(Index));
            }
            return View(new HoGiaDinh());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenGiaDinh")] HoGiaDinh input)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            input.TenGiaDinh = input.TenGiaDinh?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input.TenGiaDinh)) ModelState.AddModelError(nameof(input.TenGiaDinh), "Vui lòng nhập tên gia đình.");
            else if (input.TenGiaDinh.Length > 150) ModelState.AddModelError(nameof(input.TenGiaDinh), "Tên gia đình không được vượt quá 150 ký tự.");

            if (!ModelState.IsValid) return View(input);

            // Bắt đầu Transaction để đảm bảo tính toàn vẹn dữ liệu
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (await DaThuocHoGiaDinh(nguoiDung.MaNguoiDung))
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Bạn đã thuộc một hộ gia đình. Không thể tạo thêm hộ mới.";
                    return RedirectToAction(nameof(Index));
                }

                var thoiGianTao = DateTime.Now;
                var ho = new HoGiaDinh { TenGiaDinh = input.TenGiaDinh, MaChuHo = nguoiDung.MaNguoiDung, NgayTao = thoiGianTao, TrangThai = "HoatDong" };

                // Lưu lần 1 để Identity tạo ra MaGiaDinh
                _context.HoGiaDinhs.Add(ho);
                await _context.SaveChangesAsync();

                // Thêm Chủ hộ vào danh sách thành viên
                _context.ThanhVienGiaDinhs.Add(new ThanhVienGiaDinh { MaGiaDinh = ho.MaGiaDinh, MaNguoiDung = nguoiDung.MaNguoiDung, VaiTro = "ChuHo", NgayThamGia = thoiGianTao, TrangThai = "DaXacNhan" });

                // Thêm danh mục mặc định
                _context.DanhMucs.Add(new DanhMuc { MaGiaDinh = ho.MaGiaDinh, TenDanhMuc = "Tiết kiệm", LoaiDanhMuc = "TietKiem", BieuTuong = "💰" });

                // Tự động sinh Tài khoản Quỹ chung gia đình mặc định
                var quyChungGiaDinh = new TaiKhoanTien
                {
                    MaGiaDinh = ho.MaGiaDinh,
                    MaNguoiSoHuu = nguoiDung.MaNguoiDung,
                    TenTaiKhoan = "Quỹ chung gia đình",
                    LoaiTaiKhoan = "NganHang",
                    LoaiVi = "QuyChung",
                    LaTaiKhoanGiaDinh = true,
                    SoDuBanDau = 0,
                    SoDuHienTai = 0,
                    GhiChu = "Quỹ chung mặc định của gia đình",
                    TrangThai = true
                };
                _context.TaiKhoanTiens.Add(quyChungGiaDinh);

                // Lưu lần 2 cho toàn bộ các thực thể phụ thuộc
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] = "Tạo hộ gia đình và Quỹ chung thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi tạo hộ gia đình. Vui lòng thử lại.");
                return View(input);
            }
        }

        #endregion

        #region 3. HGĐ-02 — Quy Trình Lời Mời

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiLoiMoi(int maGiaDinh, string emailNguoiNhan)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
            if (ho == null)
            {
                TempData["Error"] = "Bạn không có quyền quản lý hộ gia đình này.";
                return RedirectToAction(nameof(Index));
            }

            emailNguoiNhan = emailNguoiNhan?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(emailNguoiNhan))
            {
                TempData["Error"] = "Vui lòng nhập email hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (emailNguoiNhan.Equals(nguoiDung.Email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Bạn không thể tự gửi lời mời cho chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var nguoiDuocMoi = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == emailNguoiNhan);
            if (nguoiDuocMoi == null)
            {
                TempData["Error"] = $"Email {emailNguoiNhan} chưa đăng ký tài khoản trên hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            bool daLaThanhVien = await _context.ThanhVienGiaDinhs.AnyAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == nguoiDuocMoi.MaNguoiDung && tv.TrangThai == "DaXacNhan");
            if (daLaThanhVien)
            {
                TempData["Error"] = "Người dùng này đã là thành viên trong hộ gia đình.";
                return RedirectToAction(nameof(Index));
            }

            if (await DaThuocHoGiaDinh(nguoiDuocMoi.MaNguoiDung, maGiaDinh))
            {
                TempData["Error"] = "Người dùng này đang thuộc một hộ gia đình khác. Họ cần rời hộ cũ trước khi tham gia.";
                return RedirectToAction(nameof(Index));
            }

            bool coLoiMoiCho = await _context.LoiMoiGiaDinhs.AnyAsync(l => l.MaGiaDinh == maGiaDinh && l.EmailNguoiNhan == emailNguoiNhan && l.TrangThai == "ChoXacNhan");
            if (coLoiMoiCho)
            {
                TempData["Error"] = "Đã có lời mời đang chờ xác nhận được gửi tới email này.";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var loiMoi = new LoiMoiGiaDinh
                {
                    MaGiaDinh = maGiaDinh,
                    EmailNguoiNhan = emailNguoiNhan,
                    MaNguoiGui = nguoiDung.MaNguoiDung,
                    NgayGui = DateTime.Now,
                    TrangThai = "ChoXacNhan"
                };
                _context.LoiMoiGiaDinhs.Add(loiMoi);
                await _context.SaveChangesAsync();

                await _thongBaoService.TaoThongBaoAsync(
                    nguoiDuocMoi.MaNguoiDung,
                    "📩 Lời mời gia nhập hộ gia đình",
                    $"Chủ hộ {nguoiDung.HoTen} đã mời bạn gia nhập hộ gia đình '{ho.TenGiaDinh}'.",
                    "LoiMoi",
                    PhamViThongBao.GiaDinh);

                await transaction.CommitAsync();
                TempData["Success"] = $"Đã gửi lời mời tham gia hộ gia đình tới email {emailNguoiNhan}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi hệ thống khi gửi lời mời: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChapNhanLoiMoi(int maLoiMoi)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var loiMoi = await _context.LoiMoiGiaDinhs.Include(l => l.HoGiaDinh).FirstOrDefaultAsync(x => x.MaLoiMoi == maLoiMoi && x.EmailNguoiNhan == nguoiDung.Email && x.TrangThai == "ChoXacNhan");
            if (loiMoi == null)
            {
                TempData["Error"] = "Lời mời không tồn tại hoặc đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            if (await DaThuocHoGiaDinh(nguoiDung.MaNguoiDung))
            {
                TempData["Error"] = "Bạn đang thuộc một hộ gia đình khác. Vui lòng gửi yêu cầu rời hộ cũ trước khi gia nhập.";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                loiMoi.TrangThai = "DaXacNhan";
                _context.ThanhVienGiaDinhs.Add(new ThanhVienGiaDinh { MaGiaDinh = loiMoi.MaGiaDinh, MaNguoiDung = nguoiDung.MaNguoiDung, VaiTro = "ThanhVien", NgayThamGia = DateTime.Now, TrangThai = "DaXacNhan" });
                await _context.SaveChangesAsync();

                if (loiMoi.HoGiaDinh != null)
                {
                    await _thongBaoService.TaoThongBaoAsync(loiMoi.HoGiaDinh.MaChuHo, "🎉 Thành viên mới", $"Thành viên {nguoiDung.HoTen} đã chấp nhận lời mời.", "ThanhVien", PhamViThongBao.GiaDinh);
                }

                await transaction.CommitAsync();

                TempData["Success"] = "Gia nhập hộ gia đình thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi hệ thống khi chấp nhận lời mời.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoiLoiMoi(int maLoiMoi)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var loiMoi = await _context.LoiMoiGiaDinhs.Include(l => l.HoGiaDinh).FirstOrDefaultAsync(x => x.MaLoiMoi == maLoiMoi && x.EmailNguoiNhan == nguoiDung.Email && x.TrangThai == "ChoXacNhan");
            if (loiMoi == null)
            {
                TempData["Error"] = "Lời mời không tồn tại hoặc đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            loiMoi.TrangThai = "TuChoi";
            await _context.SaveChangesAsync();

            if (loiMoi.HoGiaDinh != null)
                await _thongBaoService.TaoThongBaoAsync(loiMoi.HoGiaDinh.MaChuHo, "❌ Lời mời bị từ chối", $"Người dùng {nguoiDung.Email} đã từ chối lời mời.", "ThanhVien", PhamViThongBao.GiaDinh);

            TempData["Success"] = "Đã từ chối lời mời gia nhập hộ gia đình.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HuyLoiMoi(int maLoiMoi)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var loiMoi = await _context.LoiMoiGiaDinhs.Include(l => l.HoGiaDinh).FirstOrDefaultAsync(l => l.MaLoiMoi == maLoiMoi && l.TrangThai == "ChoXacNhan");
            if (loiMoi == null || loiMoi.HoGiaDinh?.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Bạn không có quyền hủy lời mời này hoặc lời mời không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            _context.LoiMoiGiaDinhs.Remove(loiMoi);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã hủy lời mời tham gia hộ gia đình.";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region 4. HGĐ-03 — Gỡ Thành Viên

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GoThanhVien(int maGiaDinh, int maNguoiDungCanGo)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
            if (ho == null)
            {
                TempData["Error"] = "Chỉ Chủ hộ mới có quyền gỡ thành viên.";
                return RedirectToAction(nameof(Index));
            }

            if (maNguoiDungCanGo == nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Chủ hộ không thể tự gỡ chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var thanhVien = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == maNguoiDungCanGo && tv.TrangThai == "DaXacNhan");
            if (thanhVien == null) return RedirectToAction(nameof(Index));

            _context.ThanhVienGiaDinhs.Remove(thanhVien);
            await _context.SaveChangesAsync();

            await _thongBaoService.TaoThongBaoAsync(maNguoiDungCanGo, "⚠️ Bạn đã bị gỡ khỏi hộ", $"Chủ hộ đã gỡ bạn khỏi hộ gia đình '{ho.TenGiaDinh}'.", "ThanhVien", PhamViThongBao.GiaDinh);
            TempData["Success"] = "Đã gỡ thành viên khỏi hộ gia đình.";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region 5. Rời Hộ Gia Đình & Xử Lý Yêu Cầu

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiYeuCauRoiHo(int maGiaDinh, string? lyDo = null)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.TrangThai == "HoatDong");
            if (ho == null) return NotFound();

            if (ho.MaChuHo == nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Chủ hộ không thể gửi yêu cầu rời hộ. Vui lòng chuyển quyền hoặc giải tán.";
                return RedirectToAction(nameof(Index));
            }

            bool coCongNo = await KiemTraCongNoChuaQuyetToan(maGiaDinh, nguoiDung.MaNguoiDung);
            var yeuCau = new YeuCauRoiHo
            {
                MaGiaDinh = maGiaDinh,
                MaNguoiDung = nguoiDung.MaNguoiDung,
                ThoiDiemGui = DateTime.Now,
                TrangThai = coCongNo ? "ChoQuyetToan" : "ChoDuyet"
            };

            _context.YeuCauRoiHos.Add(yeuCau);
            await _context.SaveChangesAsync();

            string noiDungThongBao = $"Thành viên {nguoiDung.HoTen} gửi yêu cầu rời hộ.";
            if (!string.IsNullOrWhiteSpace(lyDo))
            {
                noiDungThongBao += $" Lý do: {lyDo.Trim()}";
            }

            if (coCongNo)
            {
                TempData["Error"] = "Bạn còn khoản thu/chi chưa quyết toán với hộ. Yêu cầu chuyển trạng thái chờ quyết toán.";
            }
            else
            {
                await _thongBaoService.TaoThongBaoAsync(
                    ho.MaChuHo,
                    "🚪 Yêu cầu rời hộ",
                    noiDungThongBao,
                    PhamViThongBao.GiaDinh,
                    LoaiThongBao.YeuCauThoatHo,
                    yeuCau.MaYeuCau.ToString()
                );
                TempData["Success"] = "Đã gửi yêu cầu rời hộ tới Chủ hộ.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> XuLyYeuCau(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var yeuCau = await _context.YeuCauRoiHos
                .Include(y => y.NguoiDung)
                .Include(y => y.HoGiaDinh)
                .FirstOrDefaultAsync(y => y.MaYeuCau == id);

            if (yeuCau == null) return NotFound();

            if (yeuCau.HoGiaDinh?.MaChuHo != nguoiDung.MaNguoiDung)
            {
                TempData["Error"] = "Bạn không có quyền xử lý yêu cầu này.";
                return RedirectToAction("Index", "ThongBaos");
            }

            if (yeuCau.TrangThai == "DongY" || yeuCau.TrangThai == "TuChoi")
            {
                TempData["Error"] = "Yêu cầu rời hộ này đã được xử lý hoàn tất trước đó.";
                return RedirectToAction("Index", "ThongBaos", new { tab = "GiaDinh" });
            }

            bool coCongNo = await KiemTraCongNoChuaQuyetToan(yeuCau.MaGiaDinh, yeuCau.MaNguoiDung);

            var danhSachGiaoDichChuaQuyetToan = await _context.GiaoDichs
                .Where(g => g.MaNguoiThucHien == yeuCau.MaNguoiDung &&
                            _context.TaiKhoanTiens.Any(tk => tk.MaTaiKhoan == g.MaTaiKhoan && tk.MaGiaDinh == yeuCau.MaGiaDinh) &&
                            g.TrangThaiQuyetToan == "ChuaQuyetToan")
                .ToListAsync();

            ViewBag.CoCongNo = coCongNo;
            ViewBag.DanhSachGiaoDich = danhSachGiaoDichChuaQuyetToan;

            return View(yeuCau);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyYeuCauRoiHo(int maYeuCau, bool dongY, string? lyDoXuLy = null)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var yeuCau = await _context.YeuCauRoiHos.Include(y => y.HoGiaDinh).FirstOrDefaultAsync(x => x.MaYeuCau == maYeuCau && x.TrangThai == "ChoDuyet");
            if (yeuCau == null || yeuCau.HoGiaDinh?.MaChuHo != nguoiDung.MaNguoiDung) return RedirectToAction(nameof(Index));

            yeuCau.NguoiDuyet = nguoiDung.MaNguoiDung;
            yeuCau.ThoiDiemXuLy = DateTime.Now;

            string thongBaoPhanHoi = !string.IsNullOrWhiteSpace(lyDoXuLy) ? $" Lý do: {lyDoXuLy.Trim()}" : "";

            if (dongY)
            {
                yeuCau.TrangThai = "DongY";
                var thanhVien = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(tv => tv.MaGiaDinh == yeuCau.MaGiaDinh && tv.MaNguoiDung == yeuCau.MaNguoiDung);
                if (thanhVien != null) _context.ThanhVienGiaDinhs.Remove(thanhVien);

                await _thongBaoService.TaoThongBaoAsync(yeuCau.MaNguoiDung, "✅ Đã duyệt yêu cầu rời hộ", $"Chủ hộ đã chấp nhận yêu cầu của bạn.{thongBaoPhanHoi}", PhamViThongBao.GiaDinh, LoaiThongBao.PheDuyet, null);
                TempData["Success"] = "Đã duyệt yêu cầu rời hộ.";
            }
            else
            {
                yeuCau.TrangThai = "TuChoi";
                await _thongBaoService.TaoThongBaoAsync(yeuCau.MaNguoiDung, "❌ Từ chối rời hộ", $"Chủ hộ đã từ chối yêu cầu của bạn.{thongBaoPhanHoi}", PhamViThongBao.GiaDinh, LoaiThongBao.PheDuyet, null);
                TempData["Success"] = "Đã từ chối yêu cầu rời hộ.";
            }

            var thongBaoLienQuan = await _context.ThongBaos
                .FirstOrDefaultAsync(t => t.MaNguoiDung == nguoiDung.MaNguoiDung &&
                                           t.LoaiThongBao == LoaiThongBao.YeuCauThoatHo &&
                                           t.DuLieuLienKet == maYeuCau.ToString());
            if (thongBaoLienQuan != null)
            {
                thongBaoLienQuan.DaDoc = true;
                thongBaoLienQuan.IsThaoTacXong = true;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "ThongBaos", new { tab = "GiaDinh" });
        }
        #endregion

        #region 6. HGĐ-05 — Chuyển Vai Trò Chủ Hộ

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChuyenVaiTroChuHo(int maGiaDinh, int maChuHoMoi)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
            var thanhVienMoi = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == maChuHoMoi && tv.TrangThai == "DaXacNhan");

            if (ho == null || thanhVienMoi == null) return RedirectToAction(nameof(Index));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var chuHoCu = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(tv => tv.MaGiaDinh == maGiaDinh && tv.MaNguoiDung == nguoiDung.MaNguoiDung);
                if (chuHoCu != null) chuHoCu.VaiTro = "ThanhVien";

                thanhVienMoi.VaiTro = "ChuHo";
                ho.MaChuHo = maChuHoMoi;
                _context.LichSuChuyenChuHos.Add(new LichSuChuyenChuHo { MaGiaDinh = maGiaDinh, MaChuHoCu = nguoiDung.MaNguoiDung, MaChuHoMoi = maChuHoMoi, ThoiGianChuyenGiao = DateTime.Now });
                await _context.SaveChangesAsync();

                await _thongBaoService.TaoThongBaoAsync(maChuHoMoi, "👑 Bạn đã trở thành Chủ hộ", $"Bạn đã được chuyển giao quyền.", "ThanhVien", PhamViThongBao.GiaDinh);
                await transaction.CommitAsync();

                TempData["Success"] = "Chuyển quyền Chủ hộ thành công.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Xảy ra lỗi trong quá trình chuyển quyền.";
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region 7. HGĐ-06 & HGĐ-07 — Chỉnh Sửa Tên Hộ & Giải Tán Hộ

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("MaGiaDinh,TenGiaDinh")] HoGiaDinh input)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            input.TenGiaDinh = input.TenGiaDinh?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(input.TenGiaDinh))
            {
                TempData["Error"] = "Tên hộ gia đình không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h =>
                h.MaGiaDinh == input.MaGiaDinh &&
                h.TrangThai == "HoatDong" &&
                h.MaChuHo == nguoiDung.MaNguoiDung);

            if (ho != null)
            {
                ho.TenGiaDinh = input.TenGiaDinh;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật tên hộ gia đình thành công.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy hộ gia đình hoặc bạn không có quyền đổi tên.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GiaiTanHo(int maGiaDinh)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return Challenge();

            var ho = await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaGiaDinh == maGiaDinh && h.MaChuHo == nguoiDung.MaNguoiDung && h.TrangThai == "HoatDong");
            if (ho == null) return RedirectToAction(nameof(Index));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ho.TrangThai = "DaGiaiTan";
                ho.NgayGiaiTan = DateTime.Now;
                var thanhViens = await _context.ThanhVienGiaDinhs.Where(tv => tv.MaGiaDinh == maGiaDinh).ToListAsync();

                foreach (var tv in thanhViens.Where(t => t.MaNguoiDung != nguoiDung.MaNguoiDung))
                {
                    await _thongBaoService.TaoThongBaoAsync(tv.MaNguoiDung, "📢 Hộ gia đình đã giải tán", $"Chủ hộ đã giải tán '{ho.TenGiaDinh}'.", "ThanhVien", PhamViThongBao.GiaDinh);
                }

                _context.ThanhVienGiaDinhs.RemoveRange(thanhViens);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Giải tán hộ gia đình thành công.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi hệ thống khi giải tán hộ.";
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}