using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class MucTieuTietKiemController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MucTieuTietKiemController(ApplicationDbContext context)
        {
            _context = context;
        }

        private enum KetQuaMoi { DaMoi, DaCoTrongNhom, KhongCoTaiKhoan }

        // =====================================================
        // Helpers
        // =====================================================

        // MaNguoiDung (int) của bảng NguoiDung, được gán 1 lần cho mỗi request
        private int _currentUserId;
        private int CurrentUserId => _currentUserId;

        /// <summary>
        /// Identity chỉ dùng để đăng nhập (ID là GUID). Ở đây ánh xạ tài khoản đăng nhập
        /// sang bảng NguoiDung thông qua Email để lấy MaNguoiDung (int).
        /// </summary>
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
            {
                context.Result = Forbid();
                return;
            }

            var emailThuong = email.Trim().ToLower();

            var maNguoiDung = await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.Email.ToLower() == emailThuong)
                .Select(u => (int?)u.MaNguoiDung)
                .FirstOrDefaultAsync();

            if (maNguoiDung == null)
            {
                // Tài khoản đăng nhập chưa có hồ sơ trong bảng NguoiDung
                context.Result = Forbid();
                return;
            }

            _currentUserId = maNguoiDung.Value;
            await next();
        }

        /// <summary>
        /// Chia đều SoTienMucTieu cho các thành viên ĐÃ THAM GIA (DaThamGia).
        /// Người đang chờ xác nhận / đã từ chối có ChiTieuCanDong = 0 cho tới khi họ đồng ý.
        /// Phần dư do làm tròn cộng vào người cuối để tổng luôn đúng bằng SoTienMucTieu.
        /// Đồng thời cập nhật cờ DaHoanThanhChiTieu (BR47).
        /// </summary>
        private static void ChiaLaiChiTieu(MucTieuTietKiem mt)
        {
            var dangThamGia = mt.ThanhVienMucTieus
                .Where(t => t.TrangThai == TrangThaiThamGia.DaThamGia)
                .OrderBy(t => t.MaNguoiDung == mt.MaTruongNhom ? 0 : 1)
                .ThenBy(t => t.NgayThamGia)
                .ToList();

            foreach (var t in mt.ThanhVienMucTieus.Where(t => t.TrangThai != TrangThaiThamGia.DaThamGia))
            {
                t.ChiTieuCanDong = 0;
                t.DaHoanThanhChiTieu = false;
            }

            if (dangThamGia.Count == 0) return;

            decimal phanMoiNguoi = Math.Floor(mt.SoTienMucTieu / dangThamGia.Count * 100) / 100m;
            decimal daChia = 0;

            for (int i = 0; i < dangThamGia.Count; i++)
            {
                var tv = dangThamGia[i];
                if (i == dangThamGia.Count - 1)
                    tv.ChiTieuCanDong = mt.SoTienMucTieu - daChia;
                else
                {
                    tv.ChiTieuCanDong = phanMoiNguoi;
                    daChia += phanMoiNguoi;
                }
                tv.DaHoanThanhChiTieu = tv.ChiTieuCanDong > 0 && tv.SoTienDaDong >= tv.ChiTieuCanDong;
            }
        }

        private static decimal TinhTienDo(MucTieuTietKiem mt) =>
            mt.SoTienMucTieu <= 0 ? 0 : Math.Min(100, Math.Round(mt.SoTienHienCo / mt.SoTienMucTieu * 100, 1));

        private static decimal TinhTienDoCaNhan(ThanhVienMucTieu tv) =>
            tv.ChiTieuCanDong <= 0 ? 0 : Math.Min(100, Math.Round(tv.SoTienDaDong / tv.ChiTieuCanDong * 100, 1));

        private static List<string> TachEmail(string? chuoi)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return new();
            var validator = new EmailAddressAttribute();
            return chuoi.Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .Where(e => validator.IsValid(e))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Mời 1 người theo email (chưa SaveChanges). Vì MaNguoiDung là int bắt buộc nên
        /// email phải thuộc về người dùng đã có trong bảng NguoiDung.
        /// Các MaNguoiDung được mời thành công sẽ được thêm vào daMoiIds để gửi thông báo sau.
        /// </summary>
        private async Task<KetQuaMoi> MoiTheoEmailAsync(MucTieuTietKiem mt, string email, List<int> daMoiIds)
        {
            var nguoiDung = await _context.NguoiDungs
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (nguoiDung == null) return KetQuaMoi.KhongCoTaiKhoan;

            var tonTai = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == nguoiDung.MaNguoiDung);
            if (tonTai != null)
            {
                if (tonTai.TrangThai == TrangThaiThamGia.TuChoi)
                {
                    tonTai.TrangThai = TrangThaiThamGia.ChoXacNhan; // mời lại người đã từ chối
                    daMoiIds.Add(nguoiDung.MaNguoiDung);
                    return KetQuaMoi.DaMoi;
                }
                return KetQuaMoi.DaCoTrongNhom;
            }

            mt.ThanhVienMucTieus.Add(new ThanhVienMucTieu
            {
                MaNguoiDung = nguoiDung.MaNguoiDung,
                TrangThai = TrangThaiThamGia.ChoXacNhan,
                NgayThamGia = DateTime.Now
            });
            daMoiIds.Add(nguoiDung.MaNguoiDung);
            return KetQuaMoi.DaMoi;
        }

        // ---------- Thông báo lời mời ----------

        /// <summary>Chuỗi JSON lưu trong ThongBao.DuLieuLienKet, ví dụ {"MaMucTieu":5}.</summary>
        private static string TaoDuLieuLienKet(int maMucTieu) =>
            JsonSerializer.Serialize(new { MaMucTieu = maMucTieu });

        /// <summary>
        /// Gửi thông báo lời mời tới những người vừa được mời (có SaveChanges).
        /// Phải gọi SAU KHI mục tiêu đã có MaMucTieu.
        /// </summary>
        private async Task GuiThongBaoLoiMoiAsync(MucTieuTietKiem mt, IEnumerable<int> maNguoiNhans)
        {
            var ds = maNguoiNhans.Distinct().ToList();
            if (ds.Count == 0) return;

            var tenTruongNhom = await _context.NguoiDungs
                .AsNoTracking()
                .Where(n => n.MaNguoiDung == mt.MaTruongNhom)
                .Select(n => n.HoTen)
                .FirstOrDefaultAsync() ?? "Trưởng nhóm";

            var duLieu = TaoDuLieuLienKet(mt.MaMucTieu);

            foreach (var maNd in ds)
            {
                // Tránh trùng: nếu còn thông báo lời mời chưa xử lý cho mục tiêu này thì không tạo thêm
                var key = $"\"MaMucTieu\":{mt.MaMucTieu}}}";
                bool daCo = await _context.ThongBaos.AnyAsync(t =>
                    t.MaNguoiDung == maNd &&
                    t.LoaiThongBao == LoaiThongBao.LoiMoi &&
                    !t.IsThaoTacXong &&
                    t.DuLieuLienKet != null && t.DuLieuLienKet.Contains(key));
                if (daCo) continue;

                _context.ThongBaos.Add(new ThongBao
                {
                    MaNguoiDung = maNd,
                    PhamVi = PhamViThongBao.CaNhan,
                    LoaiThongBao = LoaiThongBao.LoiMoi,
                    TieuDe = "Lời mời tham gia mục tiêu tiết kiệm",
                    NoiDung = $"{tenTruongNhom} mời bạn tham gia mục tiêu \"{mt.TenMucTieu}\" " +
                              $"(mục tiêu {mt.SoTienMucTieu:N0} đ, hạn {mt.NgayHan:dd/MM/yyyy}).",
                    DuLieuLienKet = duLieu,
                    DaDoc = false,
                    IsThaoTacXong = false,
                    NgayTao = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Đánh dấu thông báo lời mời của mục tiêu là đã xử lý (ẩn nút Chấp nhận / Từ chối). Chưa SaveChanges.
        /// maNguoiDung = null => áp dụng cho mọi người được mời (dùng khi xóa mục tiêu).
        /// </summary>
        private async Task DongThongBaoLoiMoiAsync(int? maNguoiDung, int maMucTieu)
        {
            var key = $"\"MaMucTieu\":{maMucTieu}}}";

            var query = _context.ThongBaos.Where(t =>
                t.LoaiThongBao == LoaiThongBao.LoiMoi &&
                !t.IsThaoTacXong &&
                t.DuLieuLienKet != null && t.DuLieuLienKet.Contains(key));

            if (maNguoiDung.HasValue)
                query = query.Where(t => t.MaNguoiDung == maNguoiDung.Value);

            var ds = await query.ToListAsync();
            foreach (var t in ds)
            {
                t.IsThaoTacXong = true;
                t.DaDoc = true;
            }
        }

        // ---------- Helper thông báo mục tiêu (luôn phạm vi Cá nhân) ----------

        private static string Cat(string s, int max) => s.Length <= max ? s : s[..max];

        private static List<int> DangThamGiaIds(MucTieuTietKiem mt) =>
            mt.ThanhVienMucTieus.Where(t => t.TrangThai == TrangThaiThamGia.DaThamGia)
                                .Select(t => t.MaNguoiDung).ToList();

        private async Task<string> LayTenAsync(int maNd) =>
            await _context.NguoiDungs.AsNoTracking()
                .Where(n => n.MaNguoiDung == maNd)
                .Select(n => n.HoTen)
                .FirstOrDefaultAsync() ?? "Một thành viên";

        /// <summary>
        /// Thêm thông báo (CHƯA SaveChanges nên đi cùng transaction của nghiệp vụ).
        /// Mặc định bỏ qua người đang thao tác. kemLienKet=false khi người nhận không còn xem được mục tiêu.
        /// </summary>
        private void ThemThongBao(MucTieuTietKiem mt, IEnumerable<int> maNguoiNhans, LoaiThongBao loai,
            string tieuDe, string noiDung, bool kemLienKet = true, bool baoCaNguoiThucHien = false)
        {
            var duLieu = kemLienKet ? TaoDuLieuLienKet(mt.MaMucTieu) : null;

            foreach (var maNd in maNguoiNhans.Distinct())
            {
                if (!baoCaNguoiThucHien && maNd == CurrentUserId) continue;

                _context.ThongBaos.Add(new ThongBao
                {
                    MaNguoiDung = maNd,
                    PhamVi = QuanLyChiTieuGiaDinh.Services.ThongBaoService.PhamViCuaMucTieu(mt.LoaiMucTieu),   // mục tiêu của hộ -> tab Gia đình, còn lại -> Cá nhân
                    LoaiThongBao = loai,
                    TieuDe = Cat(tieuDe, 200),
                    NoiDung = Cat(noiDung, 1000),
                    DuLieuLienKet = duLieu,
                    DaDoc = false,
                    IsThaoTacXong = false,
                    NgayTao = DateTime.Now
                });
            }
        }

        /// <summary>Thông báo kèm chỉ tiêu hiện tại của từng người (sau khi đã ChiaLaiChiTieu).</summary>
        private void ThongBaoChiTieu(MucTieuTietKiem mt, IEnumerable<int> maNguoiNhans, string tieuDe, string lyDo)
        {
            foreach (var maNd in maNguoiNhans.Distinct())
            {
                var tv = mt.ThanhVienMucTieus.FirstOrDefault(t =>
                    t.MaNguoiDung == maNd && t.TrangThai == TrangThaiThamGia.DaThamGia);
                if (tv == null) continue;

                ThemThongBao(mt, new[] { maNd }, LoaiThongBao.HeThong, tieuDe,
                    $"{lyDo} Chỉ tiêu của bạn hiện là {tv.ChiTieuCanDong:N0} đ (đã đóng {tv.SoTienDaDong:N0} đ).");
            }
        }

        /// <summary>
        /// Mục tiêu đạt đủ tiền: đóng lời mời treo, chuyển thành ví và báo cho mọi người.
        /// Dùng chung cho NapTien và Edit. Chưa SaveChanges.
        /// </summary>
        private async Task<TaiKhoanTien> HoanThanhVaTaoViAsync(MucTieuTietKiem mt)
        {
            mt.TrangThai = TrangThaiMucTieu.DaHoanThanh;
            mt.IsActiveWallet = true;

            await DongThongBaoLoiMoiAsync(null, mt.MaMucTieu);

            bool laGiaDinh = mt.MaGiaDinh.HasValue && mt.MaGiaDinh > 0;
            var vi = new TaiKhoanTien
            {
                // Hộ gia đình gắn MaGiaDinh, nhóm tự do tách hẳn (= 0)
                MaGiaDinh = laGiaDinh ? mt.MaGiaDinh!.Value : 0,
                MaNguoiSoHuu = mt.MaTruongNhom,
                TenTaiKhoan = (laGiaDinh ? "Quỹ chung: " : "Quỹ nhóm (Trưởng nhóm giữ): ") + mt.TenMucTieu,
                LoaiTaiKhoan = "TienMat",
                LoaiVi = laGiaDinh ? "QuyChung" : "CaNhan",
                LaTaiKhoanGiaDinh = laGiaDinh,
                SoDuBanDau = mt.SoTienHienCo,
                SoDuHienTai = mt.SoTienHienCo,
                TrangThai = true
            };
            _context.Add(vi);

            var tenTruong = await LayTenAsync(mt.MaTruongNhom);
            var thanhVien = DangThamGiaIds(mt);

            // Trưởng nhóm (kể cả khi chính họ là người nạp)
            ThemThongBao(mt, new[] { mt.MaTruongNhom }, LoaiThongBao.BienDongSoDu,
                "Mục tiêu hoàn thành, đã tạo ví",
                $"Mục tiêu \"{mt.TenMucTieu}\" đã đạt {mt.SoTienMucTieu:N0} đ. Hệ thống đã tạo ví \"{vi.TenTaiKhoan}\" " +
                $"với số dư {vi.SoDuHienTai:N0} đ, đứng tên bạn. Việc nạp tiền vào mục tiêu đã được khóa.",
                baoCaNguoiThucHien: true);

            // Các thành viên còn lại
            ThemThongBao(mt, thanhVien.Where(x => x != mt.MaTruongNhom), LoaiThongBao.BienDongSoDu,
                "Mục tiêu đã hoàn thành",
                $"Chúc mừng! Mục tiêu \"{mt.TenMucTieu}\" đã đạt {mt.SoTienMucTieu:N0} đ. " +
                $"Quỹ được chuyển thành ví \"{vi.TenTaiKhoan}\" do {tenTruong} đứng tên" +
                (laGiaDinh ? " (ví chung của hộ gia đình)." : "."),
                baoCaNguoiThucHien: true);

            // Người còn đang chờ xác nhận
            ThemThongBao(mt,
                mt.ThanhVienMucTieus.Where(t => t.TrangThai == TrangThaiThamGia.ChoXacNhan).Select(t => t.MaNguoiDung),
                LoaiThongBao.HeThong, "Lời mời không còn hiệu lực",
                $"Mục tiêu \"{mt.TenMucTieu}\" đã hoàn thành nên lời mời tham gia không còn hiệu lực.");

            return vi;
        }

        private async Task<MucTieuTietKiem?> LayMucTieuDayDuAsync(int id, bool tracking = true)
        {
            var query = _context.MucTieuTietKiems
                .Include(m => m.TruongNhom)
                .Include(m => m.HoGiaDinh)
                .Include(m => m.ThanhVienMucTieus).ThenInclude(t => t.NguoiDung)
                .AsQueryable();
            if (!tracking) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync(m => m.MaMucTieu == id);
        }

        // Trưởng nhóm, thành viên đã tham gia, hoặc người đang được mời đều được xem
        private bool CoQuyenXem(MucTieuTietKiem mt) =>
            mt.MaTruongNhom == CurrentUserId ||
            mt.ThanhVienMucTieus.Any(t => t.MaNguoiDung == CurrentUserId &&
                                          t.TrangThai != TrangThaiThamGia.TuChoi);

        private bool LaTruongNhom(MucTieuTietKiem mt) => mt.MaTruongNhom == CurrentUserId;

        private static bool DaKetThuc(MucTieuTietKiem mt) =>
            mt.TrangThai == TrangThaiMucTieu.DaHoanThanh || mt.TrangThai == TrangThaiMucTieu.DaHuy;

        /// <summary>Nạp thông tin hộ gia đình hiện tại của người dùng cho view Create.</summary>
        private async Task NapThongTinHoAsync(int userId)
        {
            var tvHo = await LayHoGiaDinhHienTaiAsync(userId);
            ViewBag.CoHoGiaDinh = tvHo != null;
            ViewBag.TenHoGiaDinh = tvHo?.HoGiaDinh?.TenGiaDinh;

            if (tvHo == null)
            {
                ViewBag.ThanhVienHo = new List<string>();
                return;
            }

            var ids = await _context.ThanhVienGiaDinhs
                .Where(t => t.MaGiaDinh == tvHo.MaGiaDinh &&
                            t.TrangThai == "DaXacNhan" &&
                            t.NgayRoiHo == null)
                .Select(t => t.MaNguoiDung)
                .ToListAsync();

            ViewBag.ThanhVienHo = await _context.NguoiDungs
                .AsNoTracking()
                .Where(n => ids.Contains(n.MaNguoiDung))
                .OrderBy(n => n.HoTen)
                .Select(n => n.HoTen)
                .ToListAsync();
        }

        /// <summary>Lấy hộ gia đình đang hoạt động mà người dùng là thành viên đã xác nhận.</summary>
        private async Task<ThanhVienGiaDinh?> LayHoGiaDinhHienTaiAsync(int maNguoiDung)
        {
            return await _context.ThanhVienGiaDinhs
                .Include(t => t.HoGiaDinh)
                .FirstOrDefaultAsync(t =>
                    t.MaNguoiDung == maNguoiDung &&
                    t.TrangThai == "DaXacNhan" &&
                    t.NgayRoiHo == null &&
                    t.HoGiaDinh!.TrangThai == "HoatDong");
        }

        // =====================================================
        // INDEX
        // =====================================================
        public async Task<IActionResult> Index(string? tuKhoa, TrangThaiMucTieu? trangThai)
        {
            int userId = CurrentUserId;

            var query = _context.MucTieuTietKiems
                .AsNoTracking()
                .Include(m => m.ThanhVienMucTieus)
                .Where(m => m.MaTruongNhom == userId ||
                            m.ThanhVienMucTieus.Any(t => t.MaNguoiDung == userId &&
                                                         t.TrangThai == TrangThaiThamGia.DaThamGia));

            if (!string.IsNullOrWhiteSpace(tuKhoa))
                query = query.Where(m => m.TenMucTieu.Contains(tuKhoa));

            if (trangThai.HasValue)
                query = query.Where(m => m.TrangThai == trangThai.Value);

            var ds = await query.OrderByDescending(m => m.NgayBatDau).ToListAsync();

            // Các lời mời đang chờ người dùng hiện tại xác nhận
            ViewBag.LoiMoi = await _context.ThanhVienMucTieus
                .AsNoTracking()
                .Include(t => t.MucTieuTietKiem).ThenInclude(m => m!.TruongNhom)
                .Where(t => t.MaNguoiDung == userId &&
                            t.TrangThai == TrangThaiThamGia.ChoXacNhan &&
                            t.MucTieuTietKiem!.TrangThai != TrangThaiMucTieu.DaHuy &&
                            t.MucTieuTietKiem.TrangThai != TrangThaiMucTieu.DaHoanThanh)
                .ToListAsync();

            // View dùng ViewBag.TienDo[MaMucTieu] để vẽ progress bar
            ViewBag.TienDo = ds.ToDictionary(m => m.MaMucTieu, m => TinhTienDo(m));
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.TrangThai = trangThai;
            return View(ds);
        }

        // =====================================================
        // DETAILS
        // =====================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var mt = await LayMucTieuDayDuAsync(id.Value, tracking: false);
            if (mt == null) return NotFound();
            if (!CoQuyenXem(mt)) return Forbid();

            var cuaToi = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == CurrentUserId);

            ViewBag.TienDo = TinhTienDo(mt);
            ViewBag.TienDoCaNhan = mt.ThanhVienMucTieus.ToDictionary(t => t.MaThanhVienMucTieu, t => TinhTienDoCaNhan(t));
            ViewBag.LaTruongNhom = LaTruongNhom(mt);
            ViewBag.DangChoXacNhan = cuaToi?.TrangThai == TrangThaiThamGia.ChoXacNhan; // hiện nút Đồng ý / Từ chối
            ViewBag.SoThanhVien = mt.ThanhVienMucTieus.Count(t => t.TrangThai == TrangThaiThamGia.DaThamGia);
            ViewBag.MaNguoiDungHienTai = CurrentUserId;

            // Lịch sử nạp tiền của mục tiêu (50 giao dịch gần nhất)
            ViewBag.LichSu = await _context.LichSuTietKiems
                .AsNoTracking()
                .Include(l => l.NguoiThucHien)
                .Include(l => l.TaiKhoanNguon)
                .Where(l => l.MaMucTieu == mt.MaMucTieu)
                .OrderByDescending(l => l.NgayThucHien)
                .Take(50)
                .ToListAsync();

            ViewBag.TongSoGiaoDich = await _context.LichSuTietKiems
                .CountAsync(l => l.MaMucTieu == mt.MaMucTieu);
            var chuaVao = new List<string>();
            if (mt.LoaiMucTieu == LoaiMucTieuTietKiem.HoGiaDinh && mt.MaGiaDinh != null)
            {
                var idsHo = await _context.ThanhVienGiaDinhs
                    .Where(t => t.MaGiaDinh == mt.MaGiaDinh &&
                                t.TrangThai == "DaXacNhan" &&
                                t.NgayRoiHo == null)
                    .Select(t => t.MaNguoiDung)
                    .ToListAsync();

                var idsTrongMucTieu = mt.ThanhVienMucTieus.Select(t => t.MaNguoiDung).ToList();

                chuaVao = await _context.NguoiDungs
                    .AsNoTracking()
                    .Where(n => idsHo.Contains(n.MaNguoiDung) && !idsTrongMucTieu.Contains(n.MaNguoiDung))
                    .OrderBy(n => n.HoTen)
                    .Select(n => n.HoTen)
                    .ToListAsync();
            }
            ViewBag.ChuaVaoMucTieu = chuaVao;
            return View(mt);
        }

        // =====================================================
        // CREATE
        // =====================================================
        public async Task<IActionResult> Create()
        {
            await NapThongTinHoAsync(CurrentUserId);
            return View(new MucTieuTietKiem
            {
                NgayBatDau = DateTime.Today,
                NgayHan = DateTime.Today.AddMonths(6)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("TenMucTieu,LoaiMucTieu,SoTienMucTieu,NgayBatDau,NgayHan,AnhDaiDienUrl")] MucTieuTietKiem model,
            string? danhSachEmail)
        {
            int userId = CurrentUserId;
            await NapThongTinHoAsync(userId);
            ViewBag.DanhSachEmail = danhSachEmail;

            ModelState.Remove(nameof(MucTieuTietKiem.TruongNhom));
            ModelState.Remove(nameof(MucTieuTietKiem.HoGiaDinh));

            if (model.NgayHan.Date <= model.NgayBatDau.Date)
                ModelState.AddModelError(nameof(model.NgayHan), "Hạn hoàn thành phải sau ngày bắt đầu.");

            if (!ModelState.IsValid) return View(model);

            model.MaTruongNhom = userId;
            model.SoTienHienCo = 0;
            model.TrangThai = TrangThaiMucTieu.DangTichLuy;
            model.IsActiveWallet = false;
            model.MaGiaDinh = null;

            // Trưởng nhóm luôn là thành viên đầu tiên và tự động tham gia
            model.ThanhVienMucTieus.Add(new ThanhVienMucTieu
            {
                MaNguoiDung = userId,
                TrangThai = TrangThaiThamGia.DaThamGia,
                NgayThamGia = DateTime.Now
            });

            var emailKhongCoTaiKhoan = new List<string>();
            var daMoiIds = new List<int>();          // những người sẽ nhận thông báo lời mời
            var cacThanhVienHoIds = new List<int>(); // thành viên hộ tự động tham gia

            switch (model.LoaiMucTieu)
            {
                case LoaiMucTieuTietKiem.HoGiaDinh:
                    {
                        var tvHo = await LayHoGiaDinhHienTaiAsync(userId);
                        if (tvHo == null)
                        {
                            ModelState.AddModelError(nameof(model.LoaiMucTieu),
                                "Bạn chưa thuộc hộ gia đình nào đang hoạt động nên không thể tạo mục tiêu hộ gia đình.");
                            return View(model);
                        }

                        model.MaGiaDinh = tvHo.MaGiaDinh;

                        // Tất cả thành viên đã xác nhận, còn trong hộ (trừ người tạo)
                        var cacThanhVienHo = await _context.ThanhVienGiaDinhs
                            .Where(t => t.MaGiaDinh == tvHo.MaGiaDinh &&
                                        t.TrangThai == "DaXacNhan" &&
                                        t.NgayRoiHo == null &&
                                        t.MaNguoiDung != userId)
                            .Select(t => t.MaNguoiDung)
                            .ToListAsync();

                        cacThanhVienHoIds.AddRange(cacThanhVienHo);

                        foreach (var maNd in cacThanhVienHo)
                        {
                            model.ThanhVienMucTieus.Add(new ThanhVienMucTieu
                            {
                                MaNguoiDung = maNd,
                                TrangThai = TrangThaiThamGia.DaThamGia, // người cùng hộ tự động tham gia
                                NgayThamGia = DateTime.Now
                            });
                        }
                        break;
                    }

                case LoaiMucTieuTietKiem.NhomTuDo:
                    {
                        foreach (var email in TachEmail(danhSachEmail))
                        {
                            if (await MoiTheoEmailAsync(model, email, daMoiIds) == KetQuaMoi.KhongCoTaiKhoan)
                                emailKhongCoTaiKhoan.Add(email);
                        }
                        break;
                    }

                default: // Cá nhân
                    break;
            }

            ChiaLaiChiTieu(model);

            _context.MucTieuTietKiems.Add(model);
            await _context.SaveChangesAsync();

            // Có MaMucTieu rồi mới gửi thông báo
            await GuiThongBaoLoiMoiAsync(model, daMoiIds); // Nhóm tự do: lời mời

            if (cacThanhVienHoIds.Count > 0)               // Hộ gia đình: tự động tham gia
            {
                var tenNguoiTao = await LayTenAsync(userId);
                ThongBaoChiTieu(model, cacThanhVienHoIds, "Mục tiêu tiết kiệm mới của hộ gia đình",
                    $"{tenNguoiTao} vừa tạo mục tiêu \"{model.TenMucTieu}\" ({model.SoTienMucTieu:N0} đ, hạn {model.NgayHan:dd/MM/yyyy}).");
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Đã tạo mục tiêu \"{model.TenMucTieu}\".";
            if (emailKhongCoTaiKhoan.Count > 0)
                TempData["Warning"] = "Các email sau chưa có tài khoản nên chưa mời được: " +
                                      string.Join(", ", emailKhongCoTaiKhoan);

            return RedirectToAction(nameof(Details), new { id = model.MaMucTieu });
        }

        // =====================================================
        // EDIT
        // =====================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var mt = await LayMucTieuDayDuAsync(id.Value, tracking: false);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();

            if (DaKetThuc(mt))
            {
                TempData["Error"] = "Mục tiêu đã hoàn thành hoặc đã hủy nên không thể chỉnh sửa.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return View(mt);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("MaMucTieu,TenMucTieu,SoTienMucTieu,NgayHan,AnhDaiDienUrl")] MucTieuTietKiem model)
        {
            if (id != model.MaMucTieu) return NotFound();

            var mt = await LayMucTieuDayDuAsync(id);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();
            if (DaKetThuc(mt))
            {
                TempData["Error"] = "Mục tiêu đã hoàn thành hoặc đã hủy nên không thể chỉnh sửa.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.Remove(nameof(MucTieuTietKiem.TruongNhom));
            ModelState.Remove(nameof(MucTieuTietKiem.HoGiaDinh));
            ModelState.Remove(nameof(MucTieuTietKiem.LoaiMucTieu));

            if (model.NgayHan.Date <= mt.NgayBatDau.Date)
                ModelState.AddModelError(nameof(model.NgayHan), "Hạn hoàn thành phải sau ngày bắt đầu.");

            if (model.SoTienMucTieu < mt.SoTienHienCo)
                ModelState.AddModelError(nameof(model.SoTienMucTieu),
                    $"Số tiền mục tiêu không được nhỏ hơn số tiền đã tích lũy ({mt.SoTienHienCo:N0}).");

            if (!ModelState.IsValid)
            {
                model.LoaiMucTieu = mt.LoaiMucTieu;
                model.NgayBatDau = mt.NgayBatDau;
                model.SoTienHienCo = mt.SoTienHienCo;
                model.TrangThai = mt.TrangThai;
                model.IsActiveWallet = mt.IsActiveWallet;
                model.ThanhVienMucTieus = mt.ThanhVienMucTieus;
                return View(model);
            }

            bool doiSoTien = mt.SoTienMucTieu != model.SoTienMucTieu;
            bool doiHan = mt.NgayHan.Date != model.NgayHan.Date;

            // Chỉ cập nhật các field được phép sửa (IsActiveWallet do hệ thống quản lý)
            mt.TenMucTieu = model.TenMucTieu;
            mt.SoTienMucTieu = model.SoTienMucTieu;
            mt.NgayHan = model.NgayHan;
            mt.AnhDaiDienUrl = model.AnhDaiDienUrl;

            if (doiSoTien) ChiaLaiChiTieu(mt);

            if (doiSoTien || doiHan)
            {
                var ly = new List<string>();
                if (doiSoTien) ly.Add($"số tiền mục tiêu đổi thành {mt.SoTienMucTieu:N0} đ");
                if (doiHan) ly.Add($"hạn hoàn thành đổi thành {mt.NgayHan:dd/MM/yyyy}");

                ThongBaoChiTieu(mt, DangThamGiaIds(mt), "Mục tiêu được cập nhật",
                    $"Trưởng nhóm đã cập nhật \"{mt.TenMucTieu}\": {string.Join(", ", ly)}.");
            }

            // Đủ tiền sau khi sửa (hạ mục tiêu xuống bằng số đã tích lũy): tạo ví giống NapTien
            if (mt.SoTienHienCo > 0 && mt.SoTienHienCo >= mt.SoTienMucTieu && !mt.IsActiveWallet)
                await HoanThanhVaTaoViAsync(mt);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.MucTieuTietKiems.AnyAsync(m => m.MaMucTieu == id)) return NotFound();
                throw;
            }

            TempData["Success"] = doiSoTien
                ? "Đã cập nhật mục tiêu và chia lại chỉ tiêu cho các thành viên."
                : "Đã cập nhật mục tiêu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =====================================================
        // MỜI / XÓA THÀNH VIÊN (Nhóm tự do)
        // =====================================================

        /// <summary>Trưởng nhóm mời thêm người bằng email (nhiều email cách nhau bởi dấu phẩy).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemThanhVien(int id, string email)
        {
            var mt = await LayMucTieuDayDuAsync(id);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();

            if (mt.LoaiMucTieu != LoaiMucTieuTietKiem.NhomTuDo)
            {
                TempData["Error"] = "Chỉ Nhóm tự do mới được mời người bên ngoài bằng email.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (DaKetThuc(mt))
            {
                TempData["Error"] = "Mục tiêu đã kết thúc, không thể mời thêm thành viên.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var dsEmail = TachEmail(email);
            if (dsEmail.Count == 0)
            {
                TempData["Error"] = "Email không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            int daMoi = 0;
            var khongCoTaiKhoan = new List<string>();
            var daCo = new List<string>();
            var daMoiIds = new List<int>();

            foreach (var e in dsEmail)
            {
                switch (await MoiTheoEmailAsync(mt, e, daMoiIds))
                {
                    case KetQuaMoi.DaMoi: daMoi++; break;
                    case KetQuaMoi.KhongCoTaiKhoan: khongCoTaiKhoan.Add(e); break;
                    default: daCo.Add(e); break;
                }
            }

            if (daMoi > 0)
            {
                await _context.SaveChangesAsync();
                await GuiThongBaoLoiMoiAsync(mt, daMoiIds);
                TempData["Success"] = $"Đã gửi lời mời cho {daMoi} người. Khi họ đồng ý, chỉ tiêu mỗi người sẽ được chia lại.";
            }
            if (khongCoTaiKhoan.Count > 0)
                TempData["Warning"] = "Chưa có tài khoản: " + string.Join(", ", khongCoTaiKhoan);
            if (daMoi == 0 && khongCoTaiKhoan.Count == 0)
                TempData["Error"] = "Các email này đã có trong nhóm.";

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>Người được mời đồng ý tham gia => chia lại chỉ tiêu cho tất cả, tiến độ cập nhật theo.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DongYThamGia(int id, string? returnUrl = null)
        {
            var mt = await LayMucTieuDayDuAsync(id);
            if (mt == null) return NotFound();

            var tv = mt.ThanhVienMucTieus.FirstOrDefault(t =>
                t.MaNguoiDung == CurrentUserId && t.TrangThai == TrangThaiThamGia.ChoXacNhan);
            if (tv == null) return Forbid();

            if (DaKetThuc(mt))
            {
                // Mục tiêu đã kết thúc: lời mời không còn hiệu lực nên ẩn nút trong thông báo
                await DongThongBaoLoiMoiAsync(CurrentUserId, id);
                await _context.SaveChangesAsync();

                TempData["Error"] = "Mục tiêu đã kết thúc nên không thể tham gia.";
                return QuayLai(returnUrl, RedirectToAction(nameof(Index)));
            }

            tv.TrangThai = TrangThaiThamGia.DaThamGia;
            tv.NgayThamGia = DateTime.Now;

            ChiaLaiChiTieu(mt);
            await DongThongBaoLoiMoiAsync(CurrentUserId, id);

            var ten = await LayTenAsync(CurrentUserId);
            ThemThongBao(mt, new[] { mt.MaTruongNhom }, LoaiThongBao.HeThong,
                "Thành viên đã chấp nhận lời mời",
                $"{ten} đã tham gia mục tiêu \"{mt.TenMucTieu}\". Chỉ tiêu của mọi người đã được chia lại.");

            ThongBaoChiTieu(mt, DangThamGiaIds(mt).Where(x => x != mt.MaTruongNhom),
                "Chỉ tiêu được chia lại", $"{ten} vừa tham gia mục tiêu \"{mt.TenMucTieu}\".");

            await _context.SaveChangesAsync();

            TempData["Success"] = "Bạn đã tham gia mục tiêu. Chỉ tiêu của các thành viên đã được chia lại.";
            return QuayLai(returnUrl, RedirectToAction(nameof(Details), new { id }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoiThamGia(int id, string? returnUrl = null)
        {
            int userId = CurrentUserId;

            var mt = await _context.MucTieuTietKiems.FirstOrDefaultAsync(m => m.MaMucTieu == id);
            if (mt == null) return NotFound();

            var tv = await _context.ThanhVienMucTieus.FirstOrDefaultAsync(t =>
                t.MaMucTieu == id &&
                t.MaNguoiDung == userId &&
                t.TrangThai == TrangThaiThamGia.ChoXacNhan);
            if (tv == null) return Forbid();

            tv.TrangThai = TrangThaiThamGia.TuChoi;
            tv.ChiTieuCanDong = 0;

            await DongThongBaoLoiMoiAsync(userId, id);

            var ten = await LayTenAsync(userId);
            ThemThongBao(mt, new[] { mt.MaTruongNhom }, LoaiThongBao.HeThong,
                "Lời mời bị từ chối", $"{ten} đã từ chối tham gia mục tiêu \"{mt.TenMucTieu}\".");

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã từ chối lời mời.";
            return QuayLai(returnUrl, RedirectToAction(nameof(Index)));
        }

        /// <summary>Quay lại trang được chỉ định (vd trang thông báo) nếu là URL nội bộ, ngược lại dùng đích mặc định.</summary>
        private IActionResult QuayLai(string? returnUrl, IActionResult macDinh) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : macDinh;

        /// <summary>Xóa thành viên (hoặc hủy lời mời) khỏi Nhóm tự do rồi chia lại chỉ tiêu.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaThanhVien(int id, int maThanhVienMucTieu)
        {
            var mt = await LayMucTieuDayDuAsync(id);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();

            if (mt.LoaiMucTieu != LoaiMucTieuTietKiem.NhomTuDo)
            {
                TempData["Error"] = "Chỉ được xóa thành viên khỏi Nhóm tự do.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var tv = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaThanhVienMucTieu == maThanhVienMucTieu);
            if (tv == null) return NotFound();

            if (tv.MaNguoiDung == mt.MaTruongNhom)
            {
                TempData["Error"] = "Không thể xóa trưởng nhóm.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (tv.SoTienDaDong > 0)
            {
                TempData["Error"] = "Thành viên này đã đóng tiền vào quỹ nên không thể xóa.";
                return RedirectToAction(nameof(Details), new { id });
            }

            int maNguoiBiXoa = tv.MaNguoiDung;
            bool dangCho = tv.TrangThai == TrangThaiThamGia.ChoXacNhan;
            string tenNguoiBiXoa = tv.NguoiDung?.HoTen ?? "Một thành viên";

            mt.ThanhVienMucTieus.Remove(tv);
            _context.ThanhVienMucTieus.Remove(tv);

            // Nếu đây là hủy lời mời đang chờ thì người kia không còn thấy nút Chấp nhận / Từ chối
            await DongThongBaoLoiMoiAsync(maNguoiBiXoa, id);

            ChiaLaiChiTieu(mt);

            // Người bị xóa không còn quyền xem nên không gắn liên kết
            ThemThongBao(mt, new[] { maNguoiBiXoa }, LoaiThongBao.HeThong,
                dangCho ? "Lời mời đã bị thu hồi" : "Bạn đã bị xóa khỏi mục tiêu",
                dangCho ? $"Trưởng nhóm đã thu hồi lời mời tham gia mục tiêu \"{mt.TenMucTieu}\"."
                        : $"Bạn đã bị trưởng nhóm xóa khỏi mục tiêu \"{mt.TenMucTieu}\".",
                kemLienKet: false);

            if (!dangCho)
                ThongBaoChiTieu(mt, DangThamGiaIds(mt), "Chỉ tiêu được chia lại",
                    $"{tenNguoiBiXoa} không còn trong mục tiêu \"{mt.TenMucTieu}\".");

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa thành viên và chia lại chỉ tiêu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Mục tiêu Hộ gia đình: thêm người mới vào hộ, bỏ người đã rời hộ (nếu chưa đóng tiền), rồi chia lại chỉ tiêu.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DongBoThanhVienHo(int id)
        {
            var mt = await LayMucTieuDayDuAsync(id);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();

            if (mt.LoaiMucTieu != LoaiMucTieuTietKiem.HoGiaDinh || mt.MaGiaDinh == null || DaKetThuc(mt))
            {
                TempData["Error"] = "Chỉ đồng bộ được với mục tiêu Hộ gia đình đang hoạt động.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var thanhVienHienTai = await _context.ThanhVienGiaDinhs
                .Where(t => t.MaGiaDinh == mt.MaGiaDinh && t.TrangThai == "DaXacNhan" && t.NgayRoiHo == null)
                .Select(t => t.MaNguoiDung)
                .ToListAsync();

            var daThemIds = new List<int>();
            var daBoIds = new List<int>();

            foreach (var maNd in thanhVienHienTai)
            {
                var tonTai = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == maNd);
                if (tonTai == null)
                {
                    mt.ThanhVienMucTieus.Add(new ThanhVienMucTieu
                    {
                        MaNguoiDung = maNd,
                        TrangThai = TrangThaiThamGia.DaThamGia,
                        NgayThamGia = DateTime.Now
                    });
                    daThemIds.Add(maNd);
                }
                else if (tonTai.TrangThai != TrangThaiThamGia.DaThamGia)
                {
                    tonTai.TrangThai = TrangThaiThamGia.DaThamGia;
                    daThemIds.Add(maNd);
                }
            }

            bool conNguoiDaDongTien = false;
            var daRoiHo = mt.ThanhVienMucTieus
                .Where(t => t.MaNguoiDung != mt.MaTruongNhom && !thanhVienHienTai.Contains(t.MaNguoiDung))
                .ToList();

            foreach (var tv in daRoiHo)
            {
                if (tv.SoTienDaDong > 0) { conNguoiDaDongTien = true; continue; } // giữ lại để không mất dữ liệu đóng góp
                daBoIds.Add(tv.MaNguoiDung);
                mt.ThanhVienMucTieus.Remove(tv);
                _context.ThanhVienMucTieus.Remove(tv);
            }

            ChiaLaiChiTieu(mt);

            if (daThemIds.Count + daBoIds.Count > 0)
            {
                var dangThamGia = DangThamGiaIds(mt);

                ThongBaoChiTieu(mt, dangThamGia.Where(x => daThemIds.Contains(x)),
                    "Bạn được thêm vào mục tiêu của hộ",
                    $"Mục tiêu \"{mt.TenMucTieu}\" cần {mt.SoTienMucTieu:N0} đ, hạn {mt.NgayHan:dd/MM/yyyy}.");

                ThongBaoChiTieu(mt, dangThamGia.Where(x => !daThemIds.Contains(x)),
                    "Chỉ tiêu được chia lại", "Danh sách thành viên của mục tiêu đã thay đổi.");

                ThemThongBao(mt, daBoIds, LoaiThongBao.HeThong, "Bạn không còn trong mục tiêu của hộ",
                    $"Bạn đã rời hộ nên không còn tham gia mục tiêu \"{mt.TenMucTieu}\".", kemLienKet: false);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã đồng bộ thành viên theo hộ gia đình và chia lại chỉ tiêu.";
            if (conNguoiDaDongTien)
                TempData["Warning"] = "Có thành viên đã rời hộ nhưng đã đóng tiền vào quỹ nên vẫn được giữ lại trong mục tiêu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =====================================================
        // DELETE
        // =====================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var mt = await LayMucTieuDayDuAsync(id.Value, tracking: false);
            if (mt == null) return NotFound();
            if (!LaTruongNhom(mt)) return Forbid();

            ViewBag.TienDo = TinhTienDo(mt);
            return View(mt);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var mt = await _context.MucTieuTietKiems
                    .Include(m => m.ThanhVienMucTieus)
                    .Include(m => m.LichSuTietKiems)
                    .FirstOrDefaultAsync(m => m.MaMucTieu == id);

                if (mt == null) return NotFound();
                if (mt.MaTruongNhom != CurrentUserId) return Forbid();

                decimal tienHoanVe = mt.SoTienHienCo;
                TaiKhoanTien? viChungLienQuan = null;

                // Nếu mục tiêu đã biến thành ví, ta lấy số dư thực tế CÒN LẠI của ví đó
                if (mt.IsActiveWallet)
                {
                    viChungLienQuan = await _context.Set<TaiKhoanTien>()
                        .FirstOrDefaultAsync(tk => tk.TenTaiKhoan.Contains(mt.TenMucTieu) && tk.MaNguoiSoHuu == mt.MaTruongNhom && tk.TrangThai == true);

                    if (viChungLienQuan != null)
                    {
                        tienHoanVe = viChungLienQuan.SoDuHienTai;
                    }
                }

                // Nếu có tiền cần hoàn, tìm ví cá nhân của Trưởng nhóm
                if (tienHoanVe > 0)
                {
                    var viCaNhan = await _context.Set<TaiKhoanTien>()
                        .FirstOrDefaultAsync(tk => tk.MaNguoiSoHuu == mt.MaTruongNhom && tk.LaTaiKhoanGiaDinh == false && tk.TrangThai == true);

                    // Chặn xóa nếu Trưởng nhóm không có ví cá nhân để nhận lại tiền
                    if (viCaNhan == null)
                    {
                        TempData["Error"] = "Bạn cần có ít nhất 1 ví cá nhân đang hoạt động để nhận lại tiền hoàn trả trước khi xóa mục tiêu này.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Hoàn tiền về ví cá nhân
                    viCaNhan.SoDuHienTai += tienHoanVe;
                    _context.Update(viCaNhan);

                    TempData["Success"] = $"Đã xóa mục tiêu và hoàn trả {tienHoanVe:N0} đ về ví cá nhân \"{viCaNhan.TenTaiKhoan}\".";
                }
                else
                {
                    TempData["Success"] = "Đã xóa mục tiêu tiết kiệm.";
                }

                // Vô hiệu hóa Ví dùng chung nếu có (Rút sạch số dư về 0 và chuyển trạng thái)
                if (viChungLienQuan != null)
                {
                    viChungLienQuan.SoDuHienTai = 0;
                    viChungLienQuan.TrangThai = false;
                    _context.Update(viChungLienQuan);
                }

                // Báo cho thành viên (không gắn liên kết vì mục tiêu sẽ không còn)
                var tenTruongXoa = await LayTenAsync(CurrentUserId);
                ThemThongBao(mt,
                    mt.ThanhVienMucTieus.Where(t => t.TrangThai != TrangThaiThamGia.TuChoi).Select(t => t.MaNguoiDung),
                    LoaiThongBao.HeThong, "Mục tiêu đã bị xóa",
                    $"{tenTruongXoa} đã xóa mục tiêu \"{mt.TenMucTieu}\". Số tiền còn lại được hoàn về ví cá nhân của trưởng nhóm.",
                    kemLienKet: false);

                // Các lời mời chưa xử lý của mục tiêu này không còn hiệu lực
                await DongThongBaoLoiMoiAsync(null, id);

                // Dọn dẹp dữ liệu rác
                _context.LichSuTietKiems.RemoveRange(mt.LichSuTietKiems);
                _context.ThanhVienMucTieus.RemoveRange(mt.ThanhVienMucTieus);
                _context.MucTieuTietKiems.Remove(mt);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Có lỗi hệ thống xảy ra khi xóa mục tiêu.";
                return RedirectToAction(nameof(Index));
            }
        }

        // =====================================================
        // NẠP TIỀN VÀO MỤC TIÊU (Lấy từ ví cá nhân)
        // =====================================================

        /// <summary>GET: Nạp tiền</summary>
        public async Task<IActionResult> NapTien(int? id)
        {
            if (id == null) return NotFound();

            var mt = await _context.MucTieuTietKiems
                .Include(m => m.ThanhVienMucTieus)
                .FirstOrDefaultAsync(m => m.MaMucTieu == id);

            if (mt == null) return NotFound();
            if (!CoQuyenXem(mt)) return Forbid();

            // CHẶN: Nếu mục tiêu đã kết thúc (Hoàn thành hoặc Hủy), không cho truy cập trang Nạp tiền
            if (DaKetThuc(mt))
            {
                TempData["Error"] = "Mục tiêu đã hoàn thành hoặc đã hủy, không thể nạp thêm tiền.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Kiểm tra người này có thực sự tham gia hay không
            var tv = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == CurrentUserId && t.TrangThai == TrangThaiThamGia.DaThamGia);
            if (tv == null)
            {
                TempData["Error"] = "Bạn chưa tham gia mục tiêu này (hoặc đang chờ xác nhận) nên không thể nạp tiền.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (tv.DaHoanThanhChiTieu)
            {
                TempData["Warning"] = "Bạn đã hoàn thành đủ phần chỉ tiêu của mình. Nhưng bạn vẫn có thể nạp thêm nếu muốn!";
            }

            // Lấy danh sách ví cá nhân đang hoạt động của User này để làm Nguồn nạp
            var dsViCaNhan = await _context.Set<TaiKhoanTien>()
                .AsNoTracking()
                .Where(tk => tk.MaNguoiSoHuu == CurrentUserId && tk.LaTaiKhoanGiaDinh == false && tk.TrangThai == true)
                .ToListAsync();

            ViewBag.MucTieu = mt;
            ViewBag.ThanhVien = tv;
            ViewData["MaTaiKhoanNguon"] = new SelectList(dsViCaNhan, "MaTaiKhoan", "TenTaiKhoan");

            return View(new LichSuTietKiem
            {
                MaMucTieu = mt.MaMucTieu,
                LoaiGiaoDich = LoaiGiaoDichQuy.NapTien,
                NgayThucHien = DateTime.Now
            });
        }

        /// <summary>POST: Xử lý trừ tiền từ ví và cộng tiền vào tiến độ cá nhân + quỹ chung</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NapTien(LichSuTietKiem lichSu)
        {
            // Xóa các ràng buộc không cần thiết để tránh Validate fail
            ModelState.Remove(nameof(LichSuTietKiem.MucTieuTietKiem));
            ModelState.Remove(nameof(LichSuTietKiem.NguoiThucHien));
            ModelState.Remove(nameof(LichSuTietKiem.TaiKhoanNguon));
            ModelState.Remove(nameof(LichSuTietKiem.DanhMuc));

            int userId = CurrentUserId;
            lichSu.MaNguoiThucHien = userId;
            lichSu.LoaiGiaoDich = LoaiGiaoDichQuy.NapTien;

            var mt = await _context.MucTieuTietKiems
                .Include(m => m.ThanhVienMucTieus)
                .FirstOrDefaultAsync(m => m.MaMucTieu == lichSu.MaMucTieu);

            if (mt == null) return NotFound();
            if (!CoQuyenXem(mt)) return Forbid();

            // CHẶN BẢO MẬT: Bắt lỗi nếu người dùng vượt rào submit bằng Inspect/Postman khi quỹ đã hoàn thành
            if (DaKetThuc(mt))
            {
                TempData["Error"] = "Mục tiêu đã hoàn thành hoặc đã hủy, không thể nạp thêm tiền.";
                return RedirectToAction(nameof(Details), new { id = lichSu.MaMucTieu });
            }

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Trừ tiền ở ví cá nhân (Nếu có chọn ví)
                    if (lichSu.MaTaiKhoanNguon.HasValue)
                    {
                        var viCaNhan = await _context.Set<TaiKhoanTien>().FindAsync(lichSu.MaTaiKhoanNguon.Value);
                        if (viCaNhan == null || viCaNhan.MaNguoiSoHuu != userId)
                        {
                            ModelState.AddModelError("MaTaiKhoanNguon", "Ví nguồn không hợp lệ.");
                            throw new Exception("Ví nguồn không hợp lệ.");
                        }

                        if (viCaNhan.SoDuHienTai < lichSu.SoTien)
                        {
                            ModelState.AddModelError("SoTien", $"Số dư ví \"{viCaNhan.TenTaiKhoan}\" không đủ (Còn {viCaNhan.SoDuHienTai:N0} đ).");
                            throw new Exception("Số dư không đủ.");
                        }

                        viCaNhan.SoDuHienTai -= lichSu.SoTien;
                        _context.Update(viCaNhan);
                    }

                    // 2. Cập nhật tiến độ của người nạp
                    decimal tienDoTruoc = TinhTienDo(mt);
                    bool truocDoDaXong = false;

                    var tv = mt.ThanhVienMucTieus.FirstOrDefault(t => t.MaNguoiDung == userId && t.TrangThai == TrangThaiThamGia.DaThamGia);
                    if (tv != null)
                    {
                        truocDoDaXong = tv.DaHoanThanhChiTieu;
                        tv.SoTienDaDong += lichSu.SoTien;
                        if (tv.SoTienDaDong >= tv.ChiTieuCanDong && tv.ChiTieuCanDong > 0)
                            tv.DaHoanThanhChiTieu = true;
                        _context.Update(tv);
                    }

                    // 3. Cộng vào tổng mục tiêu
                    mt.SoTienHienCo += lichSu.SoTien;
                    var tenNguoiNap = await LayTenAsync(userId);

                    // 4. Đủ tiền thì hoàn thành, tạo ví và báo cho mọi người
                    if (mt.SoTienHienCo >= mt.SoTienMucTieu && !mt.IsActiveWallet)
                    {
                        await HoanThanhVaTaoViAsync(mt);
                        TempData["Success"] = $"Tuyệt vời! Mục tiêu \"{mt.TenMucTieu}\" đã hoàn thành và tự động chuyển thành Ví. (Chức năng nạp tiền đã được khóa).";
                    }
                    else
                    {
                        TempData["Success"] = $"Nạp thành công {lichSu.SoTien:N0} đ vào mục tiêu.";
                        decimal tienDoSau = TinhTienDo(mt);

                        if (userId != mt.MaTruongNhom)
                            ThemThongBao(mt, new[] { mt.MaTruongNhom }, LoaiThongBao.BienDongSoDu, "Có khoản nạp mới",
                                $"{tenNguoiNap} vừa nạp {lichSu.SoTien:N0} đ vào \"{mt.TenMucTieu}\". Tiến độ: {tienDoSau}%.");

                        if (tv != null && !truocDoDaXong && tv.DaHoanThanhChiTieu)
                            ThemThongBao(mt, new[] { userId }, LoaiThongBao.BienDongSoDu, "Bạn đã hoàn thành chỉ tiêu",
                                $"Bạn đã đóng đủ phần chỉ tiêu ({tv.ChiTieuCanDong:N0} đ) cho mục tiêu \"{mt.TenMucTieu}\".",
                                baoCaNguoiThucHien: true);

                        foreach (var moc in new[] { 50, 80 })
                            if (tienDoTruoc < moc && tienDoSau >= moc)
                                ThemThongBao(mt, DangThamGiaIds(mt), LoaiThongBao.BienDongSoDu, $"Mục tiêu đạt {moc}%",
                                    $"Mục tiêu \"{mt.TenMucTieu}\" đã đạt {tienDoSau}% ({mt.SoTienHienCo:N0}/{mt.SoTienMucTieu:N0} đ).");
                    }

                    _context.Update(mt);

                    // 5. Lưu lịch sử nạp
                    _context.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Details), new { id = lichSu.MaMucTieu });
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    if (ModelState.ErrorCount == 0)
                    {
                        ModelState.AddModelError("", "Đã có lỗi hệ thống xảy ra khi thực hiện giao dịch.");
                    }
                }
            }

            // Nếu Validate lỗi (ví dụ không nhập số tiền), nạp lại dữ liệu cho Dropdown
            var dsViLoadLai = await _context.Set<TaiKhoanTien>()
                .AsNoTracking()
                .Where(tk => tk.MaNguoiSoHuu == userId && tk.LaTaiKhoanGiaDinh == false && tk.TrangThai == true)
                .ToListAsync();

            ViewBag.MucTieu = mt;
            ViewData["MaTaiKhoanNguon"] = new SelectList(dsViLoadLai, "MaTaiKhoan", "TenTaiKhoan", lichSu.MaTaiKhoanNguon);

            return View(lichSu);
        }
    }
}