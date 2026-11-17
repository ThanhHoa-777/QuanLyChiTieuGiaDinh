using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Helpers;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Data;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize] // SỬA: trước đây thiếu
    public class GiaoDichesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ThongBaoService _thongBaoService;

        // Dấu đánh dấu lưu trong GhiChu và tên danh mục hệ thống
        private const string DauNapHanMuc = "|NAP_HAN_MUC|";
        private const string DauChuyenTien = "|CHUYEN_TIEN|";
        private const string DanhMucChuyenTienCaNhan = "Chuyển tiền cá nhân";
        private const string DanhMucChuyenTienQuy = "Chuyển tiền quỹ";
        private static readonly string[] TenDanhMucHeThong = { DanhMucChuyenTienCaNhan, DanhMucChuyenTienQuy };

        // Trạng thái "đã xóa" (xóa mềm) của cấu hình giao dịch định kỳ - dùng thống nhất một giá trị
        private const string TrangThaiDaXoa = "DaXoa";

        public GiaoDichesController(
            ApplicationDbContext context,
            ThongBaoService thongBaoService
            )
        {
            _context = context;
            _thongBaoService = thongBaoService;
        }

        #region Helpers & Permissions

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

        private bool LaChuHo(NguoiDung nguoiDung, HoGiaDinh hoGiaDinh) => hoGiaDinh.MaChuHo == nguoiDung.MaNguoiDung;

        private void CapNhatSoDu(TaiKhoanTien taiKhoan, string loaiGiaoDich, decimal soTien)
        {
            if (loaiGiaoDich == "Thu") taiKhoan.SoDuHienTai += soTien;
            else if (loaiGiaoDich == "Chi") taiKhoan.SoDuHienTai -= soTien;
        }

        private void HoanTacSoDu(TaiKhoanTien taiKhoan, string loaiGiaoDich, decimal soTien)
        {
            if (loaiGiaoDich == "Thu") taiKhoan.SoDuHienTai -= soTien;
            else if (loaiGiaoDich == "Chi") taiKhoan.SoDuHienTai += soTien;
        }

        /// <summary>
        /// Giao dịch nạp quỹ / chuyển tiền luôn đi theo cặp Chi + Thu trên 2 ví,
        /// nên không cho sửa/xóa lẻ từng chiều (tránh lệch số dư).
        /// Cần Include(DanhMuc) để nhận diện cả dữ liệu cũ chưa có dấu đánh dấu.
        /// </summary>
        private static bool GiaoDichBiKhoa(GiaoDich g) =>
            (g.GhiChu != null && (g.GhiChu.Contains(DauNapHanMuc) || g.GhiChu.Contains(DauChuyenTien)))
            || (g.DanhMuc != null && TenDanhMucHeThong.Contains(g.DanhMuc.TenDanhMuc));

        /// <summary>
        /// SỬA: gom việc tìm/tạo danh mục từ form giao dịch. Danh mục mới luôn có TenDanhMucClean và MaNguoiTao
        /// để khớp BR34 với DanhMucsController (trước đây thiếu nên có thể tạo trùng và lệch bộ lọc loại danh mục).
        /// </summary>
        private async Task<DanhMuc> LayHoacTaoDanhMucAsync(int maGiaDinh, int maNguoiTao, string ten, string? loaiGiaoDich)
        {
            ten = ten.Trim();
            string tenClean = ChuoiHelper.LamSach(ten);

            var dm = await _context.DanhMucs.FirstOrDefaultAsync(x =>
                x.TrangThai == "Active" &&
                (x.MaGiaDinh == maGiaDinh || x.MaGiaDinh == null) &&
                (x.TenDanhMucClean == tenClean || x.TenDanhMuc.ToLower() == ten.ToLower()));

            if (dm == null)
            {
                dm = new DanhMuc
                {
                    TenDanhMuc = ten,
                    TenDanhMucClean = tenClean,
                    MaGiaDinh = maGiaDinh,
                    MaNguoiTao = maNguoiTao,
                    TrangThai = "Active"
                };

                // Giao dịch Chi => danh mục chi tiêu (khớp bộ lọc ở NganSaches). Giao dịch Thu giữ giá trị mặc định của model.
                if (loaiGiaoDich == "Chi") dm.LoaiDanhMuc = "ChiTieu";

                _context.DanhMucs.Add(dm);
                await _context.SaveChangesAsync();
            }

            return dm;
        }

        private static bool LaTenDanhMucHeThong(string? ten) =>
            !string.IsNullOrWhiteSpace(ten) &&
            TenDanhMucHeThong.Any(x => string.Equals(x, ten.Trim(), StringComparison.OrdinalIgnoreCase));

        private async Task NapDuLieuForm(int maGiaDinh, int maNguoiDung, string? tenDanhMuc = null)
        {
            var hoGiaDinh = await _context.HoGiaDinhs.FirstOrDefaultAsync(x => x.MaGiaDinh == maGiaDinh);
            bool isChuHo = hoGiaDinh != null && hoGiaDinh.MaChuHo == maNguoiDung;

            var taiKhoanQuery = _context.TaiKhoanTiens.Where(x => x.MaGiaDinh == maGiaDinh && x.TrangThai);
            if (isChuHo)
            {
                taiKhoanQuery = taiKhoanQuery.Where(x => x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == maNguoiDung);
            }
            else
            {
                taiKhoanQuery = taiKhoanQuery.Where(x => !x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == maNguoiDung);
            }

            ViewBag.TaiKhoans = await taiKhoanQuery.OrderByDescending(x => x.LaTaiKhoanGiaDinh).ThenBy(x => x.TenTaiKhoan).ToListAsync();
            ViewBag.DanhMucs = await _context.DanhMucs.Where(x => (x.MaGiaDinh == null || x.MaGiaDinh == maGiaDinh) && x.TrangThai == "Active").OrderBy(x => x.TenDanhMuc).ToListAsync();
            ViewBag.TenDanhMuc = tenDanhMuc;
        }

        // Nạp dữ liệu cho form chuyển tiền
        private async Task NapDuLieuChuyenTien(HoGiaDinh hoGiaDinh, NguoiDung nguoiDung)
        {
            ViewBag.TaiKhoanNguons = await _context.TaiKhoanTiens
                .Where(x => x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                            !x.LaTaiKhoanGiaDinh &&
                            x.MaNguoiSoHuu == nguoiDung.MaNguoiDung &&
                            x.TrangThai)
                .OrderBy(x => x.TenTaiKhoan)
                .ToListAsync();

            // Danh sách thành viên (gồm chủ hộ), trừ bản thân
            var maThanhViens = await _context.ThanhVienGiaDinhs
                .Where(x => x.MaGiaDinh == hoGiaDinh.MaGiaDinh && x.TrangThai == "DaXacNhan")
                .Select(x => x.MaNguoiDung)
                .ToListAsync();

            maThanhViens.Add(hoGiaDinh.MaChuHo);

            ViewBag.NguoiNhans = await _context.NguoiDungs
                .Where(x => maThanhViens.Contains(x.MaNguoiDung) && x.MaNguoiDung != nguoiDung.MaNguoiDung)
                .OrderBy(x => x.HoTen)
                .ToListAsync();
        }

        // ---------- Helpers cho giao dịch định kỳ ----------

        /// <summary>
        /// Quyền quản lý (thanh toán / tạm dừng / kích hoạt / xóa) một cấu hình định kỳ:
        /// cấu hình phải thuộc đúng hộ của người dùng; chủ hộ, người tạo cấu hình
        /// hoặc chủ sở hữu ví mới được thao tác.
        /// </summary>
        private bool CoQuyenQuanLyDinhKy(GiaoDichDinhKy dinhKy, NguoiDung nguoiDung, HoGiaDinh hoGiaDinh)
        {
            if (dinhKy.TaiKhoanTien == null || dinhKy.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh) return false;

            if (LaChuHo(nguoiDung, hoGiaDinh)) return true;
            return dinhKy.MaNguoiThucHien == nguoiDung.MaNguoiDung
                || dinhKy.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
        }

        /// <summary>Ghép ngày; nếu tháng không có ngày được chọn (VD 31/2) thì lấy ngày cuối tháng.</summary>
        private static DateTime TaoNgayChay(int nam, int thang, int ngay, TimeSpan gio)
        {
            int ngayHopLe = Math.Min(Math.Max(ngay, 1), DateTime.DaysInMonth(nam, thang));
            return new DateTime(nam, thang, ngayHopLe).Add(gio);
        }

        /// <summary>
        /// Ngày chạy đầu tiên của cấu hình: lần xuất hiện gần nhất của "ngày (và tháng) lặp" tính từ mốc trở đi.
        /// </summary>
        private static DateTime TinhNgayChayDauTien(string chuKy, int ngayLap, int thangLap, DateTime moc)
        {
            var tuNgay = moc.Date;

            if (chuKy == "HangNam")
            {
                int thang = Math.Min(Math.Max(thangLap, 1), 12);
                var ungVien = TaoNgayChay(tuNgay.Year, thang, ngayLap, TimeSpan.Zero);
                if (ungVien < tuNgay) ungVien = TaoNgayChay(tuNgay.Year + 1, thang, ngayLap, TimeSpan.Zero);
                return ungVien;
            }

            var ungVienThang = TaoNgayChay(tuNgay.Year, tuNgay.Month, ngayLap, TimeSpan.Zero);
            if (ungVienThang < tuNgay)
            {
                var thangSau = new DateTime(tuNgay.Year, tuNgay.Month, 1).AddMonths(1);
                ungVienThang = TaoNgayChay(thangSau.Year, thangSau.Month, ngayLap, TimeSpan.Zero);
            }
            return ungVienThang;
        }

        /// <summary>
        /// Tính ngày chạy kế tiếp sau khi thanh toán. Chỉ chu kỳ HangNam được xử lý riêng (+1 năm, đúng tháng/ngày lặp);
        /// mọi chu kỳ còn lại mặc định cộng thêm 1 tháng. Luôn giữ đúng "ngày lặp trong chu kỳ"
        /// (VD đặt ngày 31: tháng 2 chạy ngày 28/29, sang tháng 3 lại về ngày 31, không bị trôi xuống 28).
        /// </summary>
        private static DateTime TinhNgayChayTiepTheo(GiaoDichDinhKy dinhKy)
        {
            var goc = dinhKy.NgayChayTiepTheo;
            int ngay = dinhKy.NgayLapTrongChuKy > 0 ? dinhKy.NgayLapTrongChuKy : goc.Day;

            if (dinhKy.ChuKy == "HangNam")
            {
                int thang = dinhKy.ThangLapTrongNam >= 1 && dinhKy.ThangLapTrongNam <= 12
                    ? dinhKy.ThangLapTrongNam
                    : goc.Month;
                return TaoNgayChay(goc.Year + 1, thang, ngay, goc.TimeOfDay);
            }

            var thangSau = new DateTime(goc.Year, goc.Month, 1).AddMonths(1);
            return TaoNgayChay(thangSau.Year, thangSau.Month, ngay, goc.TimeOfDay);
        }

        #endregion

        // =====================================================
        // QUÉT HÓA ĐƠN OCR BẰNG GEMINI AI
        // =====================================================
        // SỬA: thêm chống giả mạo yêu cầu. Khi gọi bằng fetch/AJAX cần gửi token, ví dụ:
        //   formData.append("__RequestVerificationToken", document.querySelector('input[name="__RequestVerificationToken"]').value);
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuetHoaDonGemini(IFormFile anhHoaDon, [FromServices] GeminiOcrService geminiService)
        {
            if (anhHoaDon == null || anhHoaDon.Length == 0)
                return Json(new { success = false, message = "Chưa chọn file ảnh hóa đơn!" });

            // SỬA: giới hạn dung lượng và loại file trước khi gửi sang dịch vụ AI bên ngoài
            if (anhHoaDon.Length > 5 * 1024 * 1024)
                return Json(new { success = false, message = "Ảnh quá lớn (tối đa 5 MB)." });

            if (string.IsNullOrEmpty(anhHoaDon.ContentType) ||
                !anhHoaDon.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "File phải là ảnh (JPG, PNG...)." });

            try
            {
                var result = await geminiService.DocHoaDonAsync(anhHoaDon);
                if (result == null)
                    return Json(new { success = false, message = "Không trích xuất được dữ liệu từ hóa đơn." });

                string ngayFormatted = DateTime.Now.ToString("yyyy-MM-dd");
                if (DateTime.TryParse(result.NgayGiaoDich, out var parsedDate))
                    ngayFormatted = parsedDate.ToString("yyyy-MM-dd");

                return Json(new { success = true, soTien = result.SoTien, ghiChu = result.GhiChu, ngayGiaoDich = ngayFormatted });
            }
            catch (Exception ex)
            {
                // SỬA: không trả ex.Message ra trình duyệt (có thể lộ URL/khóa API của dịch vụ)
                _ = ex;
                return Json(new { success = false, message = "Không đọc được hóa đơn lúc này. Vui lòng thử lại hoặc nhập tay." });
            }
        }

        // =====================================================
        // INDEX
        // loai: "CaNhan" | "GiaDinh" | rỗng (tất cả)
        // =====================================================
        public async Task<IActionResult> Index(int? thang, int? nam, int? ngay, string? loai)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return View(new List<GiaoDich>());

            int selectedThang = thang ?? DateTime.Now.Month;
            int selectedNam = nam ?? DateTime.Now.Year;
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var query = _context.GiaoDichs
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien).Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien != null && x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh && x.TrangThai == "HoatDong" && x.NgayGiaoDich.Month == selectedThang && x.NgayGiaoDich.Year == selectedNam);

            if (!isChuHo)
            {
                query = query.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh || x.MaNguoiThucHien == nguoiDung.MaNguoiDung);
            }

            // Lịch sử biến động số dư đã có ở Tài khoản tiền,
            // nên giao dịch chuyển tiền / nạp quỹ chỉ hiện 1 chiều (chiều Chi).
            query = query.Where(x => !(x.LoaiGiaoDich == "Thu"
                                       && x.DanhMuc != null
                                       && TenDanhMucHeThong.Contains(x.DanhMuc.TenDanhMuc)));

            // Lọc theo loại ví
            if (loai == "GiaDinh")
                query = query.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh);
            else if (loai == "CaNhan")
                query = query.Where(x => !x.TaiKhoanTien!.LaTaiKhoanGiaDinh);
            else
                loai = "";

            if (ngay.HasValue) query = query.Where(x => x.NgayGiaoDich.Day == ngay.Value);
            var giaoDichs = await query.OrderByDescending(x => x.NgayGiaoDich).ToListAsync();

            // Cần model GiaoDichDinhKy có navigation NguoiThucHien (giống GiaoDich)
            var queryDinhKy = _context.GiaoDichDinhKys
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien).Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien!.MaGiaDinh == hoGiaDinh.MaGiaDinh && x.TrangThai != TrangThaiDaXoa); // chỉ hiện HoatDong / TamDung

            if (!isChuHo)
            {
                queryDinhKy = queryDinhKy.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh || x.MaNguoiThucHien == nguoiDung.MaNguoiDung);
            }

            var danhSachDinhKy = await queryDinhKy.OrderByDescending(x => x.NgayChayTiepTheo).ToListAsync();

            ViewBag.DanhSachDinhKy = danhSachDinhKy;
            ViewBag.Thang = selectedThang;
            ViewBag.Nam = selectedNam;
            ViewBag.Ngay = ngay;
            ViewBag.Loai = loai;
            ViewBag.LaChuHo = isChuHo;
            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;

            return View(giaoDichs);
        }

        // =====================================================
        // DANH SÁCH GIAO DỊCH ĐỊNH KỲ (GD-03 & TC29) - gộp từ GiaoDichDinhKiesController.Index
        // =====================================================
        public async Task<IActionResult> DanhSachDinhKy()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return View(new List<GiaoDichDinhKy>());

            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            // TC29: Thành viên chỉ hiển thị các cấu hình lặp do chính mình cài đặt
            var query = _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien != null &&
                            x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                            x.TrangThai != TrangThaiDaXoa); // không hiện cấu hình đã xóa

            if (!isChuHo)
            {
                query = query.Where(x => x.MaNguoiThucHien == nguoiDung.MaNguoiDung);
            }

            var danhSach = await query.OrderBy(x => x.NgayChayTiepTheo).ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = isChuHo;

            return View(danhSach);
        }

        // =====================================================
        // NẠP TIỀN TỪ CÁ NHÂN VÀO GIA ĐÌNH (KÈM XỬ LÝ THÔNG BÁO)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> NapTienGiaDinh(int? maThongBao)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            ViewBag.MaThongBao = maThongBao;

            ViewBag.TaiKhoanNguons = await _context.TaiKhoanTiens
                .Where(x => x.MaGiaDinh == hoGiaDinh.MaGiaDinh && !x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung && x.TrangThai)
                .ToListAsync();

            ViewBag.TaiKhoanDichs = await _context.TaiKhoanTiens
                .Where(x => x.MaGiaDinh == hoGiaDinh.MaGiaDinh && x.LaTaiKhoanGiaDinh && x.TrangThai)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NapTienGiaDinh(
            int MaTaiKhoanNguon,
            int MaTaiKhoanDich,
            decimal SoTien,
            string? GhiChu,
            int? MaThongBao)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["Loi"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            if (SoTien <= 0)
            {
                TempData["Loi"] = "Số tiền nạp phải lớn hơn 0.";
                return RedirectToAction(nameof(NapTienGiaDinh), new { maThongBao = MaThongBao });
            }

            // Serializable để hạn chế gửi nhiều yêu cầu nạp cùng lúc làm vượt hạn mức.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // 1. KIỂM TRA TÀI KHOẢN NGUỒN VÀ ĐÍCH
                var tkNguon = await _context.TaiKhoanTiens
                    .FirstOrDefaultAsync(x =>
                        x.MaTaiKhoan == MaTaiKhoanNguon &&
                        x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                        !x.LaTaiKhoanGiaDinh &&
                        x.MaNguoiSoHuu == nguoiDung.MaNguoiDung &&
                        x.TrangThai);

                var tkDich = await _context.TaiKhoanTiens
                    .FirstOrDefaultAsync(x =>
                        x.MaTaiKhoan == MaTaiKhoanDich &&
                        x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                        x.LaTaiKhoanGiaDinh &&
                        x.TrangThai);

                if (tkNguon == null || tkDich == null)
                {
                    TempData["Loi"] = "Tài khoản nguồn hoặc tài khoản đích không hợp lệ.";
                    return RedirectToAction(nameof(NapTienGiaDinh), new { maThongBao = MaThongBao });
                }

                if (SoTien > tkNguon.SoDuHienTai)
                {
                    TempData["Loi"] = "Số dư tài khoản cá nhân không đủ để nạp.";
                    return RedirectToAction(nameof(NapTienGiaDinh), new { maThongBao = MaThongBao });
                }

                // 2. HẠN MỨC ĐANG ÁP DỤNG CHO THÀNH VIÊN
                DateTime homNay = DateTime.Today;

                var danhSachHanMuc = await _context.HanMucChiTieus
                    .Where(h =>
                        h.MaTaiKhoan == tkDich.MaTaiKhoan &&
                        h.MaThanhVien == nguoiDung.MaNguoiDung &&
                        h.TrangThai &&
                        h.NgayBatDau <= homNay.AddDays(1).AddTicks(-1) &&
                        h.HanChot >= homNay)
                    .OrderBy(h => h.MaHanMuc)
                    .ToListAsync();

                // 3. KIỂM TRA SỐ TIỀN CÒN ĐƯỢC PHÉP NẠP
                var maHanMucApDung = new List<int>();

                foreach (var hanMuc in danhSachHanMuc)
                {
                    string dauDanh = $"|HM:{hanMuc.MaHanMuc}|";

                    decimal daNap = await _context.GiaoDichs
                        .Where(g =>
                            g.MaTaiKhoan == tkDich.MaTaiKhoan &&
                            g.MaNguoiThucHien == nguoiDung.MaNguoiDung &&
                            g.LoaiGiaoDich == "Thu" &&
                            g.TrangThaiDuyet == "DaDuyet" &&
                            g.TrangThai == "HoatDong" &&
                            g.GhiChu != null &&
                            g.GhiChu.Contains(dauDanh))
                        .SumAsync(g => (decimal?)g.SoTien) ?? 0;

                    decimal conDuocNap = hanMuc.SoTienHanMuc - daNap;

                    if (SoTien > conDuocNap)
                    {
                        TempData["Loi"] =
                            $"Bạn chỉ còn được nạp tối đa {Math.Max(0, conDuocNap):N0}đ " +
                            $"vào ví '{tkDich.TenTaiKhoan}'. " +
                            $"Hạn mức được cấp là {hanMuc.SoTienHanMuc:N0}đ, " +
                            $"đã nạp {daNap:N0}đ.";

                        return RedirectToAction(nameof(NapTienGiaDinh), new { maThongBao = MaThongBao });
                    }

                    maHanMucApDung.Add(hanMuc.MaHanMuc);
                }

                // 4. DANH MỤC CHUYỂN TIỀN QUỸ
                var dmChuyenTien = await _context.DanhMucs
                    .FirstOrDefaultAsync(x =>
                        x.TenDanhMuc == DanhMucChuyenTienQuy &&
                        (x.MaGiaDinh == hoGiaDinh.MaGiaDinh || x.MaGiaDinh == null) &&
                        x.TrangThai == "Active");

                if (dmChuyenTien == null)
                {
                    dmChuyenTien = new DanhMuc
                    {
                        TenDanhMuc = DanhMucChuyenTienQuy,
                        TenDanhMucClean = ChuoiHelper.LamSach(DanhMucChuyenTienQuy),
                        MaNguoiTao = nguoiDung.MaNguoiDung,
                        MaGiaDinh = hoGiaDinh.MaGiaDinh,
                        TrangThai = "Active"
                    };

                    _context.DanhMucs.Add(dmChuyenTien);
                    await _context.SaveChangesAsync();
                }

                // 5. GHI CHÚ + DẤU HẠN MỨC
                string dauHanMuc = maHanMucApDung.Any()
                    ? DauNapHanMuc + string.Join("", maHanMucApDung.Select(id => $"|HM:{id}|"))
                    : "";

                string noiDungGhiChu = string.IsNullOrWhiteSpace(GhiChu) ? "" : GhiChu.Trim();

                string ghiChuGiaoDich = $"{dauHanMuc} Nạp tiền vào quỹ gia đình. {noiDungGhiChu}";

                // Giới hạn theo StringLength(500) của model GiaoDich.
                if (ghiChuGiaoDich.Length > 500)
                    ghiChuGiaoDich = ghiChuGiaoDich.Substring(0, 500);

                // 6. TRỪ TIỀN TÀI KHOẢN CÁ NHÂN
                tkNguon.SoDuHienTai -= SoTien;

                _context.GiaoDichs.Add(new GiaoDich
                {
                    MaTaiKhoan = tkNguon.MaTaiKhoan,
                    MaDanhMuc = dmChuyenTien.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = SoTien,
                    LoaiGiaoDich = "Chi",
                    NgayGiaoDich = DateTime.Now,
                    TrangThai = "HoatDong",
                    TrangThaiDuyet = "DaDuyet",
                    TrangThaiQuyetToan = "ChuaQuyetToan",
                    GhiChu = maHanMucApDung.Any()
                        ? ghiChuGiaoDich
                        : $"Nạp tiền vào quỹ gia đình ({tkDich.TenTaiKhoan}). {noiDungGhiChu}"
                });

                // 7. CỘNG TIỀN VÀO TÀI KHOẢN GIA ĐÌNH
                tkDich.SoDuHienTai += SoTien;

                _context.GiaoDichs.Add(new GiaoDich
                {
                    MaTaiKhoan = tkDich.MaTaiKhoan,
                    MaDanhMuc = dmChuyenTien.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = SoTien,
                    LoaiGiaoDich = "Thu",
                    NgayGiaoDich = DateTime.Now,
                    TrangThai = "HoatDong",
                    TrangThaiDuyet = "DaDuyet",
                    TrangThaiQuyetToan = "ChuaQuyetToan",
                    // Gắn mã hạn mức để lần nạp sau tính được tổng đã nạp.
                    GhiChu = maHanMucApDung.Any()
                        ? ghiChuGiaoDich
                        : $"Nhận tiền từ {nguoiDung.HoTen} ({tkNguon.TenTaiKhoan}). {noiDungGhiChu}"
                });

                // 8. ĐÁNH DẤU THÔNG BÁO ĐÃ XỬ LÝ
                if (MaThongBao.HasValue)
                {
                    var thongBao = await _context.ThongBaos
                        .FirstOrDefaultAsync(x =>
                            x.MaThongBao == MaThongBao.Value &&
                            x.MaNguoiDung == nguoiDung.MaNguoiDung);

                    if (thongBao != null)
                    {
                        thongBao.IsThaoTacXong = true;
                        thongBao.DaDoc = true;
                    }
                }

                // 9. LƯU VÀ HOÀN TẤT
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["ThongBao"] = $"Bạn đã nạp thành công {SoTien:N0}đ vào quỹ gia đình.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Loi"] = "Có lỗi xảy ra khi nạp tiền. Giao dịch chưa được thực hiện.";
                return RedirectToAction(nameof(NapTienGiaDinh), new { maThongBao = MaThongBao });
            }
        }

        // =====================================================
        // CHUYỂN TIỀN CHO THÀNH VIÊN (VÍ CÁ NHÂN -> VÍ CÁ NHÂN NGƯỜI KHÁC)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ChuyenTien()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            await NapDuLieuChuyenTien(hoGiaDinh, nguoiDung);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChuyenTien(
            int MaTaiKhoanNguon,
            int MaNguoiNhan,
            string TenViNhan,
            decimal SoTien,
            string? GhiChu)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["Loi"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            if (SoTien <= 0)
            {
                TempData["Loi"] = "Số tiền chuyển phải lớn hơn 0.";
                return RedirectToAction(nameof(ChuyenTien));
            }

            if (MaNguoiNhan == nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không thể chuyển tiền cho chính mình.";
                return RedirectToAction(nameof(ChuyenTien));
            }

            if (string.IsNullOrWhiteSpace(TenViNhan))
            {
                TempData["Loi"] = "Vui lòng nhập tên ví của người nhận.";
                return RedirectToAction(nameof(ChuyenTien));
            }

            // Người nhận phải thuộc cùng hộ gia đình
            bool nguoiNhanHopLe =
                hoGiaDinh.MaChuHo == MaNguoiNhan ||
                await _context.ThanhVienGiaDinhs.AnyAsync(x =>
                    x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                    x.MaNguoiDung == MaNguoiNhan &&
                    x.TrangThai == "DaXacNhan");

            if (!nguoiNhanHopLe)
            {
                TempData["Loi"] = "Người nhận không thuộc hộ gia đình của bạn.";
                return RedirectToAction(nameof(ChuyenTien));
            }

            var nguoiNhan = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.MaNguoiDung == MaNguoiNhan);
            if (nguoiNhan == null)
            {
                TempData["Loi"] = "Không tìm thấy người nhận.";
                return RedirectToAction(nameof(ChuyenTien));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // 1. Ví nguồn: phải là ví cá nhân của mình
                var tkNguon = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x =>
                    x.MaTaiKhoan == MaTaiKhoanNguon &&
                    x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                    !x.LaTaiKhoanGiaDinh &&
                    x.MaNguoiSoHuu == nguoiDung.MaNguoiDung &&
                    x.TrangThai);

                if (tkNguon == null)
                {
                    TempData["Loi"] = "Ví nguồn không hợp lệ.";
                    return RedirectToAction(nameof(ChuyenTien));
                }

                // 2. Ví đích: tên khớp + do người nhận sở hữu
                string tenViChuanHoa = TenViNhan.Trim().ToLower();

                var danhSachViDich = await _context.TaiKhoanTiens
                    .Where(x =>
                        x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                        !x.LaTaiKhoanGiaDinh &&
                        x.MaNguoiSoHuu == MaNguoiNhan &&
                        x.TrangThai &&
                        x.TenTaiKhoan.ToLower() == tenViChuanHoa)
                    .ToListAsync();

                if (danhSachViDich.Count == 0)
                {
                    TempData["Loi"] = $"{nguoiNhan.HoTen} không sở hữu ví nào có tên '{TenViNhan.Trim()}'.";
                    return RedirectToAction(nameof(ChuyenTien));
                }

                if (danhSachViDich.Count > 1)
                {
                    TempData["Loi"] = $"{nguoiNhan.HoTen} có nhiều ví trùng tên '{TenViNhan.Trim()}'. Vui lòng nhờ người nhận đổi tên ví để phân biệt.";
                    return RedirectToAction(nameof(ChuyenTien));
                }

                var tkDich = danhSachViDich[0];

                if (SoTien > tkNguon.SoDuHienTai)
                {
                    TempData["Loi"] = "Số dư ví nguồn không đủ để chuyển.";
                    return RedirectToAction(nameof(ChuyenTien));
                }

                // 3. Danh mục "Chuyển tiền cá nhân"
                var dmChuyen = await _context.DanhMucs.FirstOrDefaultAsync(x =>
                    x.TenDanhMuc == DanhMucChuyenTienCaNhan &&
                    (x.MaGiaDinh == hoGiaDinh.MaGiaDinh || x.MaGiaDinh == null) &&
                    x.TrangThai == "Active");

                if (dmChuyen == null)
                {
                    dmChuyen = new DanhMuc
                    {
                        TenDanhMuc = DanhMucChuyenTienCaNhan,
                        TenDanhMucClean = ChuoiHelper.LamSach(DanhMucChuyenTienCaNhan),
                        MaNguoiTao = nguoiDung.MaNguoiDung,
                        MaGiaDinh = hoGiaDinh.MaGiaDinh,
                        TrangThai = "Active"
                    };
                    _context.DanhMucs.Add(dmChuyen);
                    await _context.SaveChangesAsync();
                }

                string noiDung = string.IsNullOrWhiteSpace(GhiChu) ? "" : GhiChu.Trim();

                string ghiChuChi = $"{DauChuyenTien} Chuyển tiền cho {nguoiNhan.HoTen} (ví {tkDich.TenTaiKhoan}). {noiDung}";
                string ghiChuThu = $"{DauChuyenTien} Nhận tiền từ {nguoiDung.HoTen} (ví {tkNguon.TenTaiKhoan}). {noiDung}";
                if (ghiChuChi.Length > 500) ghiChuChi = ghiChuChi.Substring(0, 500);
                if (ghiChuThu.Length > 500) ghiChuThu = ghiChuThu.Substring(0, 500);

                // 4. Cập nhật số dư
                tkNguon.SoDuHienTai -= SoTien;
                tkDich.SoDuHienTai += SoTien;

                // 5. Ghi 2 giao dịch
                _context.GiaoDichs.Add(new GiaoDich
                {
                    MaTaiKhoan = tkNguon.MaTaiKhoan,
                    MaDanhMuc = dmChuyen.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = SoTien,
                    LoaiGiaoDich = "Chi",
                    NgayGiaoDich = DateTime.Now,
                    TrangThai = "HoatDong",
                    TrangThaiDuyet = "DaDuyet",
                    TrangThaiQuyetToan = "ChuaQuyetToan",
                    GhiChu = ghiChuChi
                });

                _context.GiaoDichs.Add(new GiaoDich
                {
                    MaTaiKhoan = tkDich.MaTaiKhoan,
                    MaDanhMuc = dmChuyen.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = SoTien,
                    LoaiGiaoDich = "Thu",
                    NgayGiaoDich = DateTime.Now,
                    TrangThai = "HoatDong",
                    TrangThaiDuyet = "DaDuyet",
                    TrangThaiQuyetToan = "ChuaQuyetToan",
                    GhiChu = ghiChuThu
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // SỬA: báo cho người nhận. Lỗi gửi thông báo không được làm hỏng giao dịch đã commit.
                try
                {
                    _context.ThongBaos.Add(new ThongBao
                    {
                        MaNguoiDung = nguoiNhan.MaNguoiDung,
                        PhamVi = PhamViThongBao.CaNhan,
                        LoaiThongBao = LoaiThongBao.BienDongSoDu,
                        TieuDe = "Bạn vừa nhận được tiền",
                        NoiDung = $"{nguoiDung.HoTen} đã chuyển {SoTien:N0}đ vào ví '{tkDich.TenTaiKhoan}' của bạn.",
                        DaDoc = false,
                        IsThaoTacXong = true,
                        NgayTao = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
                catch (Exception)
                {
                    // bỏ qua: thông báo chỉ mang tính tiện ích
                }

                TempData["ThongBao"] = $"Đã chuyển {SoTien:N0}đ cho {nguoiNhan.HoTen} (ví {tkDich.TenTaiKhoan}).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Loi"] = "Có lỗi xảy ra khi chuyển tiền. Giao dịch chưa được thực hiện.";
                return RedirectToAction(nameof(ChuyenTien));
            }
        }

        // =====================================================
        // CREATE
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung);
            return View(new GiaoDich { NgayGiaoDich = DateTime.Now, LoaiGiaoDich = "Chi" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("MaTaiKhoan,SoTien,LoaiGiaoDich,NgayGiaoDich,GhiChu,AnhHoaDon")] GiaoDich giaoDich,
            string? TenDanhMuc, bool isDinhKy, string? chuKyDinhKy, DateTime? ngayBatDauDinhKy, bool isBienDong,
            int? ngayLapDinhKy, int? thangLapDinhKy)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            ModelState.Remove("TaiKhoanTien");
            ModelState.Remove("DanhMuc");
            ModelState.Remove("NguoiThucHien");
            ModelState.Remove("MaDanhMuc");

            if (string.IsNullOrWhiteSpace(TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Vui lòng nhập hoặc chọn danh mục.");
            }
            else if (LaTenDanhMucHeThong(TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Danh mục này dành riêng cho chức năng chuyển tiền / nạp quỹ.");
            }
            else
            {
                var dm = await LayHoacTaoDanhMucAsync(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc, giaoDich.LoaiGiaoDich);
                giaoDich.MaDanhMuc = dm.MaDanhMuc;
            }

            // SỬA: số tiền phải > 0 (số âm sẽ làm đảo chiều thu/chi và số dư)
            if (giaoDich.SoTien <= 0)
                ModelState.AddModelError("SoTien", "Số tiền phải lớn hơn 0.");

            // Xóa cảnh báo cũ còn sót trong TempData để không lưu nhầm thông báo của lần trước
            TempData.Remove("CanhBaoNganSach");

            giaoDich.MaNguoiThucHien = nguoiDung.MaNguoiDung;
            giaoDich.TrangThai = "HoatDong";
            giaoDich.TrangThaiDuyet = "DaDuyet";

            var taiKhoan = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x =>
                x.MaTaiKhoan == giaoDich.MaTaiKhoan &&
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.TrangThai &&
                (isChuHo ? (x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == nguoiDung.MaNguoiDung) : (!x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung)));

            if (taiKhoan == null) ModelState.AddModelError("MaTaiKhoan", "Tài khoản tiền không hợp lệ hoặc bạn không có quyền.");

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
                return View(giaoDich);
            }

            if (giaoDich.LoaiGiaoDich == "Chi" && giaoDich.SoTien > taiKhoan!.SoDuHienTai)
            {
                ModelState.AddModelError("SoTien", "Số dư tài khoản không đủ để thực hiện giao dịch.");
                await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
                return View(giaoDich);
            }

            // =====================================================
            // CẢNH BÁO VƯỢT NGÂN SÁCH (CHỈ ÁP DỤNG CHO GIAO DỊCH CHI)
            // =====================================================
            if (giaoDich.LoaiGiaoDich == "Chi")
            {
                var nganSachLienQuan = await _context.NganSachs
                    .FirstOrDefaultAsync(ns => ns.MaGiaDinh == hoGiaDinh.MaGiaDinh
                                            && ns.TrangThai == "Active"
                                            && ns.Thang == giaoDich.NgayGiaoDich.Month
                                            && ns.Nam == giaoDich.NgayGiaoDich.Year
                                            && (ns.MaDanhMuc == null || ns.MaDanhMuc == giaoDich.MaDanhMuc)
                                            && ns.IsGiaDinh == taiKhoan!.LaTaiKhoanGiaDinh);

                if (nganSachLienQuan != null)
                {
                    var queryGiaoDichNganSach = _context.GiaoDichs
                        .Include(g => g.TaiKhoanTien)
                        .Where(x => x.LoaiGiaoDich == "Chi" &&
                                    x.TrangThaiDuyet == "DaDuyet" &&
                                    x.TrangThai == "HoatDong" &&
                                    x.TaiKhoanTien != null &&
                                    x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                                    x.NgayGiaoDich.Month == nganSachLienQuan.Thang &&
                                    x.NgayGiaoDich.Year == nganSachLienQuan.Nam);

                    if (nganSachLienQuan.MaDanhMuc.HasValue)
                        queryGiaoDichNganSach = queryGiaoDichNganSach.Where(x => x.MaDanhMuc == nganSachLienQuan.MaDanhMuc.Value);

                    if (nganSachLienQuan.IsGiaDinh)
                        queryGiaoDichNganSach = queryGiaoDichNganSach.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh || x.TaiKhoanTien.LoaiVi == "QuyChung");
                    else
                        queryGiaoDichNganSach = queryGiaoDichNganSach.Where(x => x.MaNguoiThucHien == nganSachLienQuan.MaNguoiTao && !x.TaiKhoanTien!.LaTaiKhoanGiaDinh);

                    decimal daChi = await queryGiaoDichNganSach.SumAsync(x => (decimal?)x.SoTien) ?? 0;
                    decimal conLai = nganSachLienQuan.SoTienHanMuc - daChi;

                    if (giaoDich.SoTien > conLai)
                    {
                        TempData["CanhBaoNganSach"] = $"Chú ý: Giao dịch này ({giaoDich.SoTien:N0}đ) làm bạn VƯỢT QUÁ ngân sách '{nganSachLienQuan.TenNganSach}' (ngân sách chỉ còn {conLai:N0}đ).";
                    }
                    else if ((daChi + giaoDich.SoTien) >= nganSachLienQuan.SoTienHanMuc * (nganSachLienQuan.NguongCanhBaoPhanTram / 100m))
                    {
                        TempData["CanhBaoNganSach"] = $"Chú ý: Giao dịch này đưa bạn chạm ngưỡng cảnh báo của ngân sách '{nganSachLienQuan.TenNganSach}'.";
                    }
                }
            }

            CapNhatSoDu(taiKhoan!, giaoDich.LoaiGiaoDich, giaoDich.SoTien);

            if (isDinhKy)
            {
                string chuKy = chuKyDinhKy == "HangNam" ? "HangNam" : "HangThang";

                // Mốc bắt đầu: người dùng chọn, mặc định sau 1 kỳ kể từ giao dịch hiện tại
                // (Hàng năm + đã chọn ngày/tháng: tính từ ngày hôm sau để không bỏ lỡ kỳ gần nhất trong năm)
                DateTime moc = ngayBatDauDinhKy ??
                               (chuKy == "HangNam"
                                   ? (ngayLapDinhKy.HasValue ? giaoDich.NgayGiaoDich.AddDays(1) : giaoDich.NgayGiaoDich.AddYears(1))
                                   : giaoDich.NgayGiaoDich.AddMonths(1));

                // Ngày (1-31) và tháng (1-12) thực hiện do người dùng chọn; không chọn thì lấy theo mốc như trước
                int ngayLap = ngayLapDinhKy.HasValue && ngayLapDinhKy.Value >= 1 && ngayLapDinhKy.Value <= 31
                    ? ngayLapDinhKy.Value : moc.Day;
                int thangLap = thangLapDinhKy.HasValue && thangLapDinhKy.Value >= 1 && thangLapDinhKy.Value <= 12
                    ? thangLapDinhKy.Value : moc.Month;

                // Tháng không có ngày được chọn (VD 31) => tự lấy ngày cuối tháng
                DateTime nextRun = ngayLapDinhKy.HasValue
                    ? TinhNgayChayDauTien(chuKy, ngayLap, thangLap, moc)
                    : moc;

                var cauHinhDinhKy = new GiaoDichDinhKy
                {
                    MaTaiKhoan = giaoDich.MaTaiKhoan,
                    MaDanhMuc = giaoDich.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = giaoDich.SoTien,
                    LoaiGiaoDich = giaoDich.LoaiGiaoDich,
                    ChuKy = chuKy,
                    NgayLapTrongChuKy = ngayLap,
                    ThangLapTrongNam = thangLap,
                    IsBienDong = isBienDong,
                    NgayBatDau = giaoDich.NgayGiaoDich,
                    NgayChayTiepTheo = nextRun,
                    TrangThai = "HoatDong",
                    GhiChu = giaoDich.GhiChu
                };

                _context.GiaoDichDinhKys.Add(cauHinhDinhKy);
                await _context.SaveChangesAsync();
                giaoDich.MaDinhKy = cauHinhDinhKy.MaDinhKy;
            }

            _context.GiaoDichs.Add(giaoDich);
            await _context.SaveChangesAsync();

            // SỬA: lưu cảnh báo ngân sách thành thông báo (trước đây chỉ hiện 1 lần qua TempData rồi mất,
            // nên bộ lọc "Cảnh báo" ở trung tâm thông báo không bao giờ có dữ liệu loại này)
            if (TempData.Peek("CanhBaoNganSach") is string canhBaoNganSach && !string.IsNullOrWhiteSpace(canhBaoNganSach))
            {
                _context.ThongBaos.Add(new ThongBao
                {
                    MaNguoiDung = nguoiDung.MaNguoiDung,
                    PhamVi = PhamViThongBao.CaNhan,
                    LoaiThongBao = LoaiThongBao.CanhBaoNganSach,
                    TieuDe = "Cảnh báo ngân sách",
                    NoiDung = canhBaoNganSach,
                    DaDoc = false,
                    IsThaoTacXong = true,
                    NgayTao = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            TempData["ThongBao"] = "Thêm giao dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // EDIT
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(g => g.TaiKhoanTien).Include(g => g.DanhMuc)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && m.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                giaoDich.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return NotFound();

            // Không cho sửa giao dịch nạp quỹ / chuyển tiền (đi theo cặp 2 ví)
            if (GiaoDichBiKhoa(giaoDich))
            {
                TempData["Loi"] = "Không thể chỉnh sửa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            if (!isChuHo && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền sửa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung);
            return View(giaoDich);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaGiaoDich,MaTaiKhoan,MaDanhMuc,SoTien,LoaiGiaoDich,NgayGiaoDich,GhiChu,AnhHoaDon")] GiaoDich giaoDichMoi)
        {
            if (id != giaoDichMoi.MaGiaoDich) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDichCu = await _context.GiaoDichs
                .AsNoTracking()
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaGiaoDich == id && x.TrangThai == "HoatDong");

            if (giaoDichCu == null || giaoDichCu.TaiKhoanTien == null ||
                giaoDichCu.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return NotFound();

            if (GiaoDichBiKhoa(giaoDichCu))
            {
                TempData["Loi"] = "Không thể chỉnh sửa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            if (!isChuHo && giaoDichCu.MaNguoiThucHien != nguoiDung.MaNguoiDung)
                return Forbid();

            ModelState.Remove("TaiKhoanTien");
            ModelState.Remove("DanhMuc");
            ModelState.Remove("NguoiThucHien");

            var taiKhoanMoi = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x =>
                x.MaTaiKhoan == giaoDichMoi.MaTaiKhoan &&
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.TrangThai &&
                (isChuHo ? (x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == nguoiDung.MaNguoiDung) : (!x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung)));

            if (taiKhoanMoi == null) ModelState.AddModelError("MaTaiKhoan", "Tài khoản tiền không hợp lệ.");

            if (giaoDichMoi.SoTien <= 0)
                ModelState.AddModelError("SoTien", "Số tiền phải lớn hơn 0.");

            // SỬA: danh mục phải là danh mục hệ thống hoặc của hộ mình (trước đây nhận thẳng MaDanhMuc từ form)
            bool danhMucHopLe = await _context.DanhMucs.AnyAsync(d =>
                d.MaDanhMuc == giaoDichMoi.MaDanhMuc &&
                (d.MaGiaDinh == null || d.MaGiaDinh == hoGiaDinh.MaGiaDinh));
            if (!danhMucHopLe)
                ModelState.AddModelError("MaDanhMuc", "Danh mục không hợp lệ.");

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung);
                return View(giaoDichMoi);
            }

            var taiKhoanCu = await _context.TaiKhoanTiens.FindAsync(giaoDichCu.MaTaiKhoan);

            HoanTacSoDu(taiKhoanCu!, giaoDichCu.LoaiGiaoDich, giaoDichCu.SoTien);

            if (giaoDichMoi.LoaiGiaoDich == "Chi" && giaoDichMoi.SoTien > taiKhoanMoi!.SoDuHienTai)
            {
                CapNhatSoDu(taiKhoanCu!, giaoDichCu.LoaiGiaoDich, giaoDichCu.SoTien);
                ModelState.AddModelError("SoTien", "Số dư tài khoản mới không đủ.");
                await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung);
                return View(giaoDichMoi);
            }

            CapNhatSoDu(taiKhoanMoi!, giaoDichMoi.LoaiGiaoDich, giaoDichMoi.SoTien);

            // Giữ lại các trường không nằm trong form (tránh bị ghi đè về mặc định)
            giaoDichMoi.MaNguoiThucHien = giaoDichCu.MaNguoiThucHien;
            giaoDichMoi.TrangThai = giaoDichCu.TrangThai;
            giaoDichMoi.TrangThaiDuyet = "DaDuyet";
            giaoDichMoi.TrangThaiQuyetToan = giaoDichCu.TrangThaiQuyetToan;
            giaoDichMoi.MaDinhKy = giaoDichCu.MaDinhKy;

            _context.Update(giaoDichMoi);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật giao dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // DELETE
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(g => g.DanhMuc).Include(g => g.TaiKhoanTien).Include(g => g.NguoiThucHien)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && m.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                giaoDich.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return NotFound();

            if (GiaoDichBiKhoa(giaoDich))
            {
                TempData["Loi"] = "Không thể xóa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            if (!isChuHo && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền xóa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            return View(giaoDich);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
                return NotFound();
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(t => t.TaiKhoanTien).Include(t => t.DanhMuc)
                .FirstOrDefaultAsync(x => x.MaGiaoDich == id && x.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null)
                return NotFound();

            // Giao dịch phải thuộc hộ gia đình hiện tại
            if (giaoDich.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return Forbid();

            // Chủ hộ hoặc người tạo giao dịch mới được xóa (khớp với giao diện)
            if (!isChuHo && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền xóa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            if (GiaoDichBiKhoa(giaoDich))
            {
                TempData["Loi"] = "Không thể xóa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            HoanTacSoDu(giaoDich.TaiKhoanTien, giaoDich.LoaiGiaoDich, giaoDich.SoTien);

            // Xóa mềm
            giaoDich.TrangThai = "DaXoa";
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Xóa giao dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // DETAILS
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(g => g.DanhMuc).Include(g => g.TaiKhoanTien).Include(g => g.NguoiThucHien)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && m.TrangThai != "DaXoa");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                giaoDich.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return NotFound();

            // Thành viên thường chỉ xem được giao dịch ví gia đình hoặc của chính mình
            if (!isChuHo &&
                !giaoDich.TaiKhoanTien.LaTaiKhoanGiaDinh &&
                giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
                return NotFound();

            return View(giaoDich);
        }

        // =====================================================
        // THÊM GIAO DỊCH ĐỊNH KỲ (THÊM GDDK)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ThemGDDK()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

            await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung);
            return View(new GiaoDichDinhKy
            {
                NgayBatDau = DateTime.Now,
                NgayChayTiepTheo = DateTime.Now.AddMonths(1),
                NgayLapTrongChuKy = DateTime.Now.Day,
                ThangLapTrongNam = DateTime.Now.Month,
                LoaiGiaoDich = "Chi"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemGDDK(
            [Bind("MaTaiKhoan,SoTien,LoaiGiaoDich,ChuKy,NgayLapTrongChuKy,ThangLapTrongNam,NgayBatDau,GhiChu,IsBienDong")] GiaoDichDinhKy giaoDichDinhKy,
            string? TenDanhMuc)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }
            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            ModelState.Remove("TaiKhoanTien");
            ModelState.Remove("DanhMuc");
            ModelState.Remove("NguoiThucHien");
            ModelState.Remove("MaDanhMuc");
            ModelState.Remove("NgayChayTiepTheo"); // được tính từ ngày/tháng lặp, không nhận từ form

            // Chu kỳ chỉ có HangThang / HangNam. Tháng lặp chỉ cần khi chu kỳ là HangNam.
            giaoDichDinhKy.ChuKy = giaoDichDinhKy.ChuKy == "HangNam" ? "HangNam" : "HangThang";
            if (giaoDichDinhKy.ChuKy != "HangNam") ModelState.Remove(nameof(GiaoDichDinhKy.ThangLapTrongNam));
            if (giaoDichDinhKy.NgayBatDau == default) giaoDichDinhKy.NgayBatDau = DateTime.Now;

            if (string.IsNullOrWhiteSpace(TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Vui lòng nhập hoặc chọn danh mục.");
            }
            else if (LaTenDanhMucHeThong(TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Danh mục này dành riêng cho chức năng chuyển tiền / nạp quỹ.");
            }
            else
            {
                var dm = await LayHoacTaoDanhMucAsync(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc, giaoDichDinhKy.LoaiGiaoDich);
                giaoDichDinhKy.MaDanhMuc = dm.MaDanhMuc;
            }

            giaoDichDinhKy.MaNguoiThucHien = nguoiDung.MaNguoiDung;
            giaoDichDinhKy.TrangThai = "HoatDong";

            // Kiểm tra quyền sở hữu ví giống Create:
            // chủ hộ: ví gia đình + ví của mình; thành viên: chỉ ví cá nhân của mình
            var taiKhoan = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x =>
                x.MaTaiKhoan == giaoDichDinhKy.MaTaiKhoan &&
                x.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                x.TrangThai &&
                (isChuHo ? (x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == nguoiDung.MaNguoiDung) : (!x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung)));

            if (taiKhoan == null)
                ModelState.AddModelError("MaTaiKhoan", "Tài khoản tiền không hợp lệ hoặc bạn không có quyền.");

            if (giaoDichDinhKy.SoTien <= 0)
                ModelState.AddModelError("SoTien", "Số tiền phải lớn hơn 0.");

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(hoGiaDinh.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
                return View(giaoDichDinhKy);
            }

            // Ngày chạy đầu tiên theo ngày (và tháng) người dùng chọn; tháng không có ngày đó => ngày cuối tháng
            giaoDichDinhKy.NgayChayTiepTheo = TinhNgayChayDauTien(
                giaoDichDinhKy.ChuKy,
                giaoDichDinhKy.NgayLapTrongChuKy,
                giaoDichDinhKy.ThangLapTrongNam,
                giaoDichDinhKy.NgayBatDau);

            _context.GiaoDichDinhKys.Add(giaoDichDinhKy);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Đã lưu lịch giao dịch định kỳ thành công. Hệ thống sẽ tự động trừ tiền khi đến hạn.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // GD-02: XÁC NHẬN THANH TOÁN CHO KHOẢN ĐỊNH KỲ (gộp từ GiaoDichDinhKiesController)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanThanhToan(int id, decimal soTienThucTe)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id);

            // Phải thuộc đúng hộ + đúng quyền (chủ hộ / người tạo / chủ ví)
            if (dinhKy == null || !CoQuyenQuanLyDinhKy(dinhKy, nguoiDung, hoGiaDinh))
            {
                TempData["Loi"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            // Cấu hình đã xóa thì không còn tồn tại với người dùng
            if (dinhKy.TrangThai == TrangThaiDaXoa)
            {
                TempData["Loi"] = "Cấu hình định kỳ này đã bị xóa.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            // Đang tạm dừng thì không được xác nhận thanh toán
            if (dinhKy.TrangThai == "TamDung")
            {
                TempData["Loi"] = "Cấu hình định kỳ đang tạm dừng. Hãy kích hoạt lại trước khi thanh toán.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            if (dinhKy.TrangThai != "HoatDong")
            {
                TempData["Loi"] = "Cấu hình định kỳ không ở trạng thái hoạt động.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            if (soTienThucTe < 0)
            {
                TempData["Loi"] = "Số tiền thực tế không hợp lệ.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            var taiKhoan = dinhKy.TaiKhoanTien;
            if (taiKhoan == null || !taiKhoan.TrangThai)
            {
                TempData["Loi"] = "Tài khoản thanh toán bị khóa hoặc không khả dụng.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            decimal soTienGhiNhan = soTienThucTe > 0 ? soTienThucTe : dinhKy.SoTien;

            // Khoản Chi phải đủ số dư (giống Create). Không đủ => báo lỗi, không tạo giao dịch, không đổi số dư.
            if (dinhKy.LoaiGiaoDich == "Chi" && soTienGhiNhan > taiKhoan.SoDuHienTai)
            {
                TempData["Loi"] = $"Số dư tài khoản \"{taiKhoan.TenTaiKhoan}\" không đủ để thanh toán {soTienGhiNhan:N0} ₫.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            // Tạo giao dịch thực tế phát sinh (các cột bắt buộc khác của GiaoDich dùng giá trị mặc định của model)
            var giaoDichThucTe = new GiaoDich
            {
                MaTaiKhoan = dinhKy.MaTaiKhoan,
                MaDanhMuc = dinhKy.MaDanhMuc,
                MaNguoiThucHien = nguoiDung.MaNguoiDung,
                SoTien = soTienGhiNhan,
                LoaiGiaoDich = dinhKy.LoaiGiaoDich,
                NgayGiaoDich = DateTime.Now,
                GhiChu = $"Giao dịch định kỳ: {dinhKy.GhiChu}",
                MaDinhKy = dinhKy.MaDinhKy,
                TrangThai = "HoatDong"
            };

            // Trừ hoặc cộng số dư
            CapNhatSoDu(taiKhoan, giaoDichThucTe.LoaiGiaoDich, giaoDichThucTe.SoTien);

            // Chu kỳ lặp tiếp theo: HangNam => +1 năm; còn lại => +1 tháng
            dinhKy.NgayChayTiepTheo = TinhNgayChayTiepTheo(dinhKy);

            _context.GiaoDichs.Add(giaoDichThucTe);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Đã xác nhận thanh toán thành công và ghi nhận vào lịch sử.";
            return RedirectToAction(nameof(DanhSachDinhKy));
        }

        // =====================================================
        // GD-03 & TC34: TẠM DỪNG / KÍCH HOẠT LẠI (đảo trạng thái)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiTrangThai(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id && x.TrangThai != TrangThaiDaXoa);

            if (dinhKy == null || !CoQuyenQuanLyDinhKy(dinhKy, nguoiDung, hoGiaDinh))
            {
                TempData["Loi"] = "Bạn không có quyền thay đổi trạng thái cấu hình này.";
                return RedirectToAction(nameof(DanhSachDinhKy));
            }

            dinhKy.TrangThai = dinhKy.TrangThai == "HoatDong" ? "TamDung" : "HoatDong";
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = dinhKy.TrangThai == "TamDung"
                ? "Đã tạm dừng nhắc nhở giao dịch định kỳ."
                : "Đã kích hoạt lại giao dịch định kỳ.";

            return RedirectToAction(nameof(DanhSachDinhKy));
        }

        // =====================================================
        // THAY ĐỔI TRẠNG THÁI ĐỊNH KỲ (TẠM DỪNG / KÍCH HOẠT / XÓA)
        // BR30: "Xoa" là xóa mềm (TrangThai = DaXoa) - đây là phương thức xóa DUY NHẤT của cấu hình định kỳ,
        // không xóa cứng khỏi Database để bảo toàn lịch sử các giao dịch đã phát sinh.
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChuyenTrangThaiDinhKy(int id, string hanhDong)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null) return NotFound();

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id && x.TrangThai != TrangThaiDaXoa);

            if (dinhKy == null || dinhKy.TaiKhoanTien == null ||
                dinhKy.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh)
                return NotFound();

            if (!CoQuyenQuanLyDinhKy(dinhKy, nguoiDung, hoGiaDinh))
            {
                TempData["Loi"] = "Bạn không có quyền thao tác giao dịch định kỳ này.";
                return RedirectToAction(nameof(Index));
            }

            switch (hanhDong)
            {
                case "TamDung": dinhKy.TrangThai = "TamDung"; break;
                case "KichHoat": dinhKy.TrangThai = "HoatDong"; break;
                case "Xoa": dinhKy.TrangThai = TrangThaiDaXoa; break;
                default:
                    TempData["Loi"] = "Thao tác không hợp lệ.";
                    return RedirectToAction(nameof(Index));
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = hanhDong == "Xoa"
                ? "Đã xóa cấu hình định kỳ. Lịch sử các giao dịch trong quá khứ được giữ nguyên vẹn."
                : "Cập nhật trạng thái giao dịch định kỳ thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
