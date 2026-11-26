using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Helpers;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Services;
using System.Security.Claims;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
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

        // Dòng đối ứng ghi vào lịch sử khi xóa giao dịch (hoàn / trừ lại số dư)
        private const string DauHoanTien = "|HOAN_TIEN|";
        private const string TrangThaiHoanTien = "Hoa";

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

        // Lấy danh sách mã mục tiêu mà người dùng đã tham gia
        private async Task<List<int>> LayDanhSachMucTieuDaThamGiaAsync(int maNguoiDung)
        {
            return await _context.ThanhVienMucTieus
                .Where(t => t.MaNguoiDung == maNguoiDung && t.TrangThai == TrangThaiThamGia.DaThamGia)
                .Select(t => t.MaMucTieu)
                .ToListAsync();
        }

        // Kiểm tra người dùng có quyền sử dụng tài khoản (Ví hộ gia đình / Ví nhóm / Ví mục tiêu cá nhân)
        private async Task<bool> CoQuyenDungTaiKhoanAsync(TaiKhoanTien tk, NguoiDung nguoiDung, HoGiaDinh? hoGiaDinh)
        {
            if (!tk.TrangThai) return false;

            // 1. Ví nhóm: mọi thành viên đã tham gia mục tiêu đều được dùng
            if (tk.LoaiVi == "ViNhom" && tk.MaMucTieuNguon.HasValue)
            {
                return await _context.ThanhVienMucTieus.AnyAsync(t =>
                    t.MaMucTieu == tk.MaMucTieuNguon.Value &&
                    t.MaNguoiDung == nguoiDung.MaNguoiDung &&
                    t.TrangThai == TrangThaiThamGia.DaThamGia);
            }

            // 2. Ví mục tiêu cá nhân (MaGiaDinh == null)
            if (tk.MaGiaDinh == null)
            {
                return tk.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
            }

            // 3. Ví thuộc hộ gia đình
            if (hoGiaDinh == null || tk.MaGiaDinh != hoGiaDinh.MaGiaDinh) return false;

            bool isChuHo = LaChuHo(nguoiDung, hoGiaDinh);
            return isChuHo
                ? (tk.LaTaiKhoanGiaDinh || tk.MaNguoiSoHuu == nguoiDung.MaNguoiDung)
                : (!tk.LaTaiKhoanGiaDinh && tk.MaNguoiSoHuu == nguoiDung.MaNguoiDung);
        }

        // Quyền thao tác trên MỘT GIAO DỊCH (xem / sửa / xóa).
        // Mở rộng CoQuyenDungTaiKhoanAsync để:
        //  - chủ hộ thao tác được giao dịch trong ví của hộ (kể cả ví cá nhân của thành viên mà chủ hộ đang thấy ở Index)
        //  - thành viên thao tác được giao dịch do chính mình nhập vào ví gia đình
        private async Task<bool> CoQuyenTrenGiaoDichAsync(GiaoDich g, NguoiDung nguoiDung, HoGiaDinh? ho)
        {
            var tk = g.TaiKhoanTien;
            if (tk == null) return false;

            // Quyền dùng ví thông thường
            if (await CoQuyenDungTaiKhoanAsync(tk, nguoiDung, ho)) return true;

            // Ví thuộc hộ của mình
            if (ho != null && tk.MaGiaDinh == ho.MaGiaDinh)
            {
                if (LaChuHo(nguoiDung, ho)) return true;

                if (tk.LaTaiKhoanGiaDinh && g.MaNguoiThucHien == nguoiDung.MaNguoiDung) return true;
            }

            return false;
        }

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

        private static bool GiaoDichBiKhoa(GiaoDich g) =>
            (g.GhiChu != null && (g.GhiChu.Contains(DauNapHanMuc) || g.GhiChu.Contains(DauChuyenTien)))
            || (g.DanhMuc != null && TenDanhMucHeThong.Contains(g.DanhMuc.TenDanhMuc));

        // Dấu hiệu hệ thống nằm trong GhiChu: |CHUYEN_TIEN|, |NAP_HAN_MUC|, |HM:5|, |HOAN_TIEN|
        private static readonly Regex DauHieuHeThong = new(@"\|[A-Z_]+(?::\d+)?\|", RegexOptions.Compiled);

        private static (string DauHieu, string NoiDung) TachGhiChu(string? ghiChu)
        {
            ghiChu ??= "";
            string dau = string.Concat(DauHieuHeThong.Matches(ghiChu).Select(m => m.Value));
            string noiDung = DauHieuHeThong.Replace(ghiChu, "").Trim();
            return (dau, noiDung);
        }
        private async Task<DanhMuc> LayHoacTaoDanhMucAsync(int? maGiaDinh, int maNguoiTao, string ten, string? loaiGiaoDich)
        {
            ten = ten.Trim();
            string tenClean = ChuoiHelper.LamSach(ten);

            // Giao dịch Thu => danh mục ThuNhap, còn lại (Chi) => ChiTieu
            string loaiDanhMuc = loaiGiaoDich == "Thu" ? "ThuNhap" : "ChiTieu";

            // Chỉ dùng lại danh mục CÙNG LOẠI (tránh dùng nhầm "Lương" của chi tiêu cho khoản thu)
            var dm = await _context.DanhMucs.FirstOrDefaultAsync(x =>
                x.TrangThai == "Active" &&
                x.LoaiDanhMuc == loaiDanhMuc &&
                ((maGiaDinh.HasValue && x.MaGiaDinh == maGiaDinh.Value) || x.MaGiaDinh == null) &&
                (x.TenDanhMucClean == tenClean || x.TenDanhMuc.ToLower() == ten.ToLower()));

            if (dm == null)
            {
                dm = new DanhMuc
                {
                    TenDanhMuc = ten,
                    TenDanhMucClean = tenClean,
                    LoaiDanhMuc = loaiDanhMuc,
                    BieuTuong = "🏷️",
                    MauSac = "#0d6efd",
                    MaGiaDinh = maGiaDinh,
                    MaNguoiTao = maNguoiTao,
                    TrangThai = "Active"
                };

                _context.DanhMucs.Add(dm);
                await _context.SaveChangesAsync();
            }

            return dm;
        }

        private static bool LaTenDanhMucHeThong(string? ten) =>
            !string.IsNullOrWhiteSpace(ten) &&
            TenDanhMucHeThong.Any(x => string.Equals(x, ten.Trim(), StringComparison.OrdinalIgnoreCase));

        private async Task NapDuLieuForm(int? maGiaDinh, int maNguoiDung, string? tenDanhMuc = null)
        {
            bool isChuHo = false;
            if (maGiaDinh.HasValue)
            {
                var hoGiaDinh = await _context.HoGiaDinhs.FirstOrDefaultAsync(x => x.MaGiaDinh == maGiaDinh.Value);
                isChuHo = hoGiaDinh != null && hoGiaDinh.MaChuHo == maNguoiDung;
            }

            // Lấy thêm các mục tiêu nhóm đã tham gia
            var mucTieuNhomIds = await LayDanhSachMucTieuDaThamGiaAsync(maNguoiDung);

            var taiKhoanQuery = _context.TaiKhoanTiens.Where(x => x.TrangThai && (
                // 1. Ví thuộc hộ gia đình
                (maGiaDinh.HasValue && x.MaGiaDinh == maGiaDinh.Value &&
                    (isChuHo ? (x.LaTaiKhoanGiaDinh || x.MaNguoiSoHuu == maNguoiDung)
                             : (!x.LaTaiKhoanGiaDinh && x.MaNguoiSoHuu == maNguoiDung)))
                // 2. Ví nhóm của các mục tiêu đã tham gia
                || (x.LoaiVi == "ViNhom" && x.MaMucTieuNguon.HasValue && mucTieuNhomIds.Contains(x.MaMucTieuNguon.Value))
                // 3. Ví mục tiêu cá nhân (không thuộc hộ)
                || (x.MaGiaDinh == null && x.LoaiVi == "ViCaNhan" && x.MaNguoiSoHuu == maNguoiDung)
            ));

            ViewBag.TaiKhoans = await taiKhoanQuery
                .OrderByDescending(x => x.LaTaiKhoanGiaDinh)
                .ThenBy(x => x.LoaiVi == "ViNhom" ? 0 : 1)
                .ThenBy(x => x.TenTaiKhoan)
                .ToListAsync();

            ViewBag.DanhMucs = await _context.DanhMucs
                .Where(x => (x.MaGiaDinh == null || (maGiaDinh.HasValue && x.MaGiaDinh == maGiaDinh.Value)) && x.TrangThai == "Active")
                .OrderBy(x => x.TenDanhMuc)
                .ToListAsync();

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
        private async Task<bool> CoQuyenQuanLyDinhKyAsync(GiaoDichDinhKy dinhKy, NguoiDung nguoiDung, HoGiaDinh? hoGiaDinh)
        {
            if (dinhKy.TaiKhoanTien == null) return false;

            // Nếu định kỳ thuộc Ví nhóm hoặc Ví mục tiêu cá nhân
            if (dinhKy.TaiKhoanTien.MaGiaDinh == null)
            {
                return await CoQuyenDungTaiKhoanAsync(dinhKy.TaiKhoanTien, nguoiDung, hoGiaDinh);
            }

            if (hoGiaDinh == null || dinhKy.TaiKhoanTien.MaGiaDinh != hoGiaDinh.MaGiaDinh) return false;

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

        // ---------- CẢNH BÁO NGÂN SÁCH ----------
        // Tính GIỐNG HỆT NganSachesController.Index: theo người tạo ngân sách, chỉ ví cá nhân,
        // đúng tháng/năm, đúng danh mục (hoặc "Tất cả danh mục").
        // Gọi SAU khi đã SaveChanges giao dịch (tổng đã chi đã bao gồm giao dịch vừa tạo).
        // Trả về nội dung cảnh báo mới nhất (nếu có tạo thông báo), ngược lại trả về null.
        private async Task<string?> KiemTraVaTaoCanhBaoNganSachAsync(NguoiDung nguoiDung, GiaoDich gd, TaiKhoanTien taiKhoan)
        {
            // Ngân sách hiện chỉ áp dụng cho chi tiêu từ ví cá nhân
            if (gd.LoaiGiaoDich != "Chi" || taiKhoan.LaTaiKhoanGiaDinh) return null;

            var nganSachs = await _context.NganSachs.Where(ns =>
                ns.MaNguoiTao == nguoiDung.MaNguoiDung &&
                !ns.IsGiaDinh &&
                ns.TrangThai == "Active" &&
                ns.Thang == gd.NgayGiaoDich.Month &&
                ns.Nam == gd.NgayGiaoDich.Year &&
                (ns.MaDanhMuc == null || ns.MaDanhMuc == gd.MaDanhMuc)).ToListAsync();

            string? thongDiep = null;

            foreach (var ns in nganSachs)
            {
                if (ns.SoTienHanMuc <= 0) continue;

                var q = _context.GiaoDichs.Where(x =>
                    x.LoaiGiaoDich == "Chi" &&
                    x.TrangThai == "HoatDong" &&
                    x.TaiKhoanTien != null &&
                    !x.TaiKhoanTien.LaTaiKhoanGiaDinh &&
                    x.MaNguoiThucHien == nguoiDung.MaNguoiDung &&
                    x.NgayGiaoDich.Month == ns.Thang &&
                    x.NgayGiaoDich.Year == ns.Nam);

                if (ns.MaDanhMuc.HasValue)
                    q = q.Where(x => x.MaDanhMuc == ns.MaDanhMuc.Value);

                decimal daChi = await q.SumAsync(x => (decimal?)x.SoTien) ?? 0;

                string? muc = daChi > ns.SoTienHanMuc ? "VUOT"
                            : daChi >= ns.SoTienHanMuc * ns.NguongCanhBaoPhanTram / 100m ? "NGUONG"
                            : null;
                if (muc == null) continue;

                // Mỗi ngân sách + mỗi mức chỉ báo 1 lần (tránh spam thông báo)
                string duLieu = JsonSerializer.Serialize(new { MaNganSach = ns.MaNganSach, Muc = muc });
                bool daCo = await _context.ThongBaos.AnyAsync(t =>
                    t.MaNguoiDung == nguoiDung.MaNguoiDung &&
                    t.LoaiThongBao == LoaiThongBao.CanhBaoNganSach &&
                    t.DuLieuLienKet == duLieu);
                if (daCo) continue;

                string noiDung = muc == "VUOT"
                    ? $"Ngân sách '{ns.TenNganSach}' đã VƯỢT hạn mức: đã chi {daChi:N0}đ / {ns.SoTienHanMuc:N0}đ."
                    : $"Ngân sách '{ns.TenNganSach}' đã dùng {daChi / ns.SoTienHanMuc * 100:N0}% hạn mức ({daChi:N0}đ / {ns.SoTienHanMuc:N0}đ).";

                _context.ThongBaos.Add(new ThongBao
                {
                    MaNguoiDung = nguoiDung.MaNguoiDung,
                    PhamVi = PhamViThongBao.CaNhan,
                    LoaiThongBao = LoaiThongBao.CanhBaoNganSach,
                    TieuDe = muc == "VUOT" ? "Ngân sách đã vượt hạn mức" : "Ngân sách sắp chạm hạn mức",
                    NoiDung = noiDung,
                    DuLieuLienKet = duLieu,
                    DaDoc = false,
                    IsThaoTacXong = true,
                    NgayTao = DateTime.Now
                });

                thongDiep = noiDung;
            }

            if (thongDiep != null)
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception)
                {
                    // Lỗi lưu thông báo không được làm hỏng giao dịch đã lưu
                    return null;
                }
            }

            return thongDiep;
        }

        #endregion

        // QUÉT HÓA ĐƠN OCR BẰNG GEMINI AI
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuetHoaDonGemini(IFormFile anhHoaDon, [FromServices] GeminiOcrService geminiService)
        {
            if (anhHoaDon == null || anhHoaDon.Length == 0)
                return Json(new { success = false, message = "Chưa chọn file ảnh hóa đơn!" });

            // giới hạn dung lượng và loại file trước khi gửi sang dịch vụ AI bên ngoài
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
                // không trả ex.Message ra trình duyệt (có thể lộ URL/khóa API của dịch vụ)
                _ = ex;
                return Json(new { success = false, message = "Không đọc được hóa đơn lúc này. Vui lòng thử lại hoặc nhập tay." });
            }
        }

        // INDEX
        // loai: "CaNhan" | "GiaDinh" | rỗng (tất cả)
        public async Task<IActionResult> Index(int? thang, int? nam, int? ngay, string? loai)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            var mucTieuNhomIds = await LayDanhSachMucTieuDaThamGiaAsync(nguoiDung.MaNguoiDung);

            // Chỉ trả về rỗng khi vừa không thuộc hộ vừa không có ví nhóm/ví mục tiêu cá nhân
            bool coViNgoaiHo = await _context.TaiKhoanTiens.AnyAsync(x => x.TrangThai && (
                (x.LoaiVi == "ViNhom" && x.MaMucTieuNguon.HasValue && mucTieuNhomIds.Contains(x.MaMucTieuNguon.Value)) ||
                (x.MaGiaDinh == null && x.LoaiVi == "ViCaNhan" && x.MaNguoiSoHuu == nguoiDung.MaNguoiDung)
            ));

            if (hoGiaDinh == null && !coViNgoaiHo) return View(new List<GiaoDich>());

            int selectedThang = thang ?? DateTime.Now.Month;
            int selectedNam = nam ?? DateTime.Now.Year;
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            var query = _context.GiaoDichs
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien).Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien != null
                         // Hiện cả giao dịch đã xóa (DaXoa) để view làm xám/khóa lại; dòng hoàn tiền (HoanTien) vẫn bị ẩn
                         && (x.TrangThai == "HoatDong" || x.TrangThai == "DaXoa")
                         && x.NgayGiaoDich.Month == selectedThang
                         && x.NgayGiaoDich.Year == selectedNam
                         && (
                                // 1. Giao dịch trong hộ gia đình
                                (hoGiaDinh != null && x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                                (x.TaiKhoanTien.LaTaiKhoanGiaDinh || x.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung))                             // 2. Giao dịch của Ví nhóm mình tham gia
                             || (x.TaiKhoanTien.LoaiVi == "ViNhom" && x.TaiKhoanTien.MaMucTieuNguon.HasValue
                                 && mucTieuNhomIds.Contains(x.TaiKhoanTien.MaMucTieuNguon.Value))
                             // 3. Giao dịch ví mục tiêu cá nhân (MaGiaDinh == null)
                             || (x.TaiKhoanTien.MaGiaDinh == null && x.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung)
                         ));

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

            // Tổng thu / chi chỉ tính giao dịch còn hoạt động (không tính giao dịch đã xóa)
            var dangHoatDong = giaoDichs.Where(x => x.TrangThai == "HoatDong").ToList();
            ViewBag.TongThu = dangHoatDong.Where(x => x.LoaiGiaoDich == "Thu").Sum(x => x.SoTien);
            ViewBag.TongChi = dangHoatDong.Where(x => x.LoaiGiaoDich == "Chi").Sum(x => x.SoTien);

            var queryDinhKy = _context.GiaoDichDinhKys
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien).Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien != null
                         && x.TrangThai != TrangThaiDaXoa
                         && (
                             (hoGiaDinh != null && x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                                 (isChuHo || x.TaiKhoanTien.LaTaiKhoanGiaDinh || x.MaNguoiThucHien == nguoiDung.MaNguoiDung))
                             || (x.TaiKhoanTien.LoaiVi == "ViNhom" && x.TaiKhoanTien.MaMucTieuNguon.HasValue
                                 && mucTieuNhomIds.Contains(x.TaiKhoanTien.MaMucTieuNguon.Value))
                             || (x.TaiKhoanTien.MaGiaDinh == null && x.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung)
                         ));

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

        // DANH SÁCH GIAO DỊCH ĐỊNH KỲ (GD-03 & TC29)
        public async Task<IActionResult> DanhSachDinhKy()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            var mucTieuNhomIds = await LayDanhSachMucTieuDaThamGiaAsync(nguoiDung.MaNguoiDung);

            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            // TC29: Thành viên chỉ hiển thị các cấu hình lặp do chính mình cài đặt
            var query = _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .Where(x => x.TaiKhoanTien != null &&
                            x.TrangThai != TrangThaiDaXoa &&
                            (
                                (hoGiaDinh != null && x.TaiKhoanTien.MaGiaDinh == hoGiaDinh.MaGiaDinh &&
                                    (isChuHo || x.MaNguoiThucHien == nguoiDung.MaNguoiDung))
                                || (x.TaiKhoanTien.LoaiVi == "ViNhom" && x.TaiKhoanTien.MaMucTieuNguon.HasValue
                                    && mucTieuNhomIds.Contains(x.TaiKhoanTien.MaMucTieuNguon.Value))
                                || (x.TaiKhoanTien.MaGiaDinh == null && x.MaNguoiThucHien == nguoiDung.MaNguoiDung)
                            ));

            var danhSach = await query.OrderBy(x => x.NgayChayTiepTheo).ToListAsync();

            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.LaChuHo = isChuHo;

            return View(danhSach);
        }

        // NẠP TIỀN TỪ CÁ NHÂN VÀO GIA ĐÌNH (KÈM XỬ LÝ THÔNG BÁO)
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
                        LoaiDanhMuc = "ChiTieu",
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

        // CHUYỂN TIỀN CHO THÀNH VIÊN (VÍ CÁ NHÂN -> VÍ CÁ NHÂN NGƯỜI KHÁC)
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
                        LoaiDanhMuc = "ChiTieu",
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
                    GhiChu = ghiChuThu
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // báo cho người nhận. Lỗi gửi thông báo không được làm hỏng giao dịch đã commit.
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

        // CREATE
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung);

            // Chỉ chặn khi vừa không thuộc hộ vừa không có bất kỳ ví nào khả dụng
            var danhSachVi = ViewBag.TaiKhoans as List<TaiKhoanTien>;
            if (hoGiaDinh == null && (danhSachVi == null || danhSachVi.Count == 0))
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình hoặc có Ví nhóm nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

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
                var dm = await LayHoacTaoDanhMucAsync(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc, giaoDich.LoaiGiaoDich);
                giaoDich.MaDanhMuc = dm.MaDanhMuc;
            }

            // số tiền phải > 0 (số âm sẽ làm đảo chiều thu/chi và số dư)
            if (giaoDich.SoTien <= 0)
                ModelState.AddModelError("SoTien", "Số tiền phải lớn hơn 0.");

            // Xóa cảnh báo cũ còn sót trong TempData để không hiển thị nhầm cảnh báo của lần trước
            TempData.Remove("CanhBaoNganSach");

            giaoDich.MaNguoiThucHien = nguoiDung.MaNguoiDung;
            giaoDich.TrangThai = "HoatDong";

            // Kiểm tra quyền sử dụng ví (bao gồm cả Ví hộ gia đình, Ví nhóm và Ví mục tiêu cá nhân)
            var taiKhoan = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x => x.MaTaiKhoan == giaoDich.MaTaiKhoan);
            if (taiKhoan == null || !await CoQuyenDungTaiKhoanAsync(taiKhoan, nguoiDung, hoGiaDinh))
                ModelState.AddModelError("MaTaiKhoan", "Tài khoản tiền không hợp lệ hoặc bạn không có quyền.");

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
                return View(giaoDich);
            }

            if (giaoDich.LoaiGiaoDich == "Chi" && giaoDich.SoTien > taiKhoan!.SoDuHienTai)
            {
                ModelState.AddModelError("SoTien", "Số dư tài khoản không đủ để thực hiện giao dịch.");
                await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
                return View(giaoDich);
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

            // KIỂM TRA NGÂN SÁCH SAU KHI ĐÃ LƯU GIAO DỊCH:
            // tổng đã chi lúc này đã gồm giao dịch vừa tạo; nếu chạm ngưỡng / vượt hạn mức
            // thì tạo thông báo (tab Cá nhân) và hiện cảnh báo ngay trên màn hình.
            var canhBaoNganSach = await KiemTraVaTaoCanhBaoNganSachAsync(nguoiDung, giaoDich, taiKhoan!);
            if (!string.IsNullOrWhiteSpace(canhBaoNganSach))
                TempData["CanhBaoNganSach"] = canhBaoNganSach;

            TempData["ThongBao"] = "Thêm giao dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        // EDIT (chỉ sửa thông tin, KHÔNG sửa số tiền / ví / loại thu-chi)
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .AsNoTracking()
                .Include(g => g.TaiKhoanTien).Include(g => g.DanhMuc)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && m.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                !await CoQuyenTrenGiaoDichAsync(giaoDich, nguoiDung, hoGiaDinh))
                return NotFound();

            // Trong ví nhóm: trưởng nhóm hoặc người tạo giao dịch được sửa
            bool laTruongNhomVi = giaoDich.TaiKhoanTien.LoaiVi == "ViNhom" && giaoDich.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
            if (!isChuHo && !laTruongNhomVi && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền sửa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            // Chuyển tiền / nạp quỹ: chỉ sửa được ghi chú. Ẩn dấu hiệu hệ thống khỏi ô nhập.
            bool chiSuaGhiChu = GiaoDichBiKhoa(giaoDich);
            if (chiSuaGhiChu)
                giaoDich.GhiChu = TachGhiChu(giaoDich.GhiChu).NoiDung;

            ViewBag.ChiSuaGhiChu = chiSuaGhiChu;
            ViewBag.KhoaTien = true;

            await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung);
            return View(giaoDich);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("MaGiaoDich,MaDanhMuc,NgayGiaoDich,GhiChu,AnhHoaDon")] GiaoDich input)
        {
            if (id != input.MaGiaoDich) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            // Lấy bản gốc (có tracking) rồi chỉ ghi đè các trường thông tin
            var giaoDich = await _context.GiaoDichs
                .Include(x => x.DanhMuc).Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaGiaoDich == id && x.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                !await CoQuyenTrenGiaoDichAsync(giaoDich, nguoiDung, hoGiaDinh))
                return NotFound();

            bool laTruongNhomVi = giaoDich.TaiKhoanTien.LoaiVi == "ViNhom" && giaoDich.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
            if (!isChuHo && !laTruongNhomVi && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
                return Forbid();

            bool chiSuaGhiChu = GiaoDichBiKhoa(giaoDich);

            // Các trường này không nhận từ form nên không validate
            foreach (var key in new[] { "TaiKhoanTien", "DanhMuc", "NguoiThucHien", "MaTaiKhoan", "SoTien", "LoaiGiaoDich" })
                ModelState.Remove(key);

            if (!chiSuaGhiChu)
            {
                if (input.NgayGiaoDich == default)
                    ModelState.AddModelError("NgayGiaoDich", "Vui lòng chọn ngày giao dịch.");

                // Danh mục: của hộ hoặc hệ thống, cùng loại với giao dịch, không phải danh mục dành riêng
                string loaiDanhMuc = giaoDich.LoaiGiaoDich == "Thu" ? "ThuNhap" : "ChiTieu";
                var danhMuc = await _context.DanhMucs.FirstOrDefaultAsync(d =>
                    d.MaDanhMuc == input.MaDanhMuc &&
                    (d.MaGiaDinh == null || (hoGiaDinh != null && d.MaGiaDinh == hoGiaDinh.MaGiaDinh)));

                if (danhMuc == null)
                    ModelState.AddModelError("MaDanhMuc", "Danh mục không hợp lệ.");
                else if (LaTenDanhMucHeThong(danhMuc.TenDanhMuc))
                    ModelState.AddModelError("MaDanhMuc", "Danh mục này dành riêng cho chức năng chuyển tiền / nạp quỹ.");
                else if (danhMuc.LoaiDanhMuc != loaiDanhMuc)
                    ModelState.AddModelError("MaDanhMuc", "Loại danh mục không khớp với loại giao dịch.");
                else if (danhMuc.TrangThai != "Active" && danhMuc.MaDanhMuc != giaoDich.MaDanhMuc)
                    ModelState.AddModelError("MaDanhMuc", "Danh mục này đã bị ẩn.");
            }

            // Ghi chú mới (giao dịch chuyển tiền / nạp quỹ: giữ dấu hiệu hệ thống cũ, bỏ dấu người dùng tự gõ)
            string ghiChuMoi;
            if (chiSuaGhiChu)
            {
                var (dauCu, _) = TachGhiChu(giaoDich.GhiChu);
                string noiDungSach = DauHieuHeThong.Replace(input.GhiChu ?? "", "").Trim();
                ghiChuMoi = string.IsNullOrEmpty(dauCu) ? noiDungSach : $"{dauCu} {noiDungSach}".Trim();
            }
            else
            {
                ghiChuMoi = input.GhiChu?.Trim() ?? "";
            }

            if (ghiChuMoi.Length > 500)
                ModelState.AddModelError("GhiChu", "Ghi chú quá dài (tối đa 500 ký tự).");

            if (!ModelState.IsValid)
            {
                // Hiển thị lại form với giá trị người dùng vừa nhập (không lưu vào DB)
                if (!chiSuaGhiChu)
                {
                    giaoDich.MaDanhMuc = input.MaDanhMuc;
                    giaoDich.NgayGiaoDich = input.NgayGiaoDich;
                }
                giaoDich.GhiChu = chiSuaGhiChu ? TachGhiChu(ghiChuMoi).NoiDung : ghiChuMoi;

                ViewBag.ChiSuaGhiChu = chiSuaGhiChu;
                ViewBag.KhoaTien = true;
                await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung);
                return View(giaoDich);
            }

            // Chỉ cập nhật thông tin. KHÔNG đụng tới SoTien, MaTaiKhoan, LoaiGiaoDich, số dư ví.
            giaoDich.GhiChu = ghiChuMoi;
            if (!chiSuaGhiChu)
            {
                giaoDich.MaDanhMuc = input.MaDanhMuc;
                giaoDich.NgayGiaoDich = input.NgayGiaoDich;
                giaoDich.AnhHoaDon = input.AnhHoaDon;
            }

            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật thông tin giao dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        // DELETE
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(g => g.DanhMuc).Include(g => g.TaiKhoanTien).Include(g => g.NguoiThucHien)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && m.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                !await CoQuyenTrenGiaoDichAsync(giaoDich, nguoiDung, hoGiaDinh))
                return NotFound();

            if (GiaoDichBiKhoa(giaoDich))
            {
                TempData["Loi"] = "Không thể xóa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            bool laTruongNhomVi = giaoDich.TaiKhoanTien.LoaiVi == "ViNhom" && giaoDich.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
            if (!isChuHo && !laTruongNhomVi && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền xóa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            return View(giaoDich);
        }

        // Xóa mềm giao dịch + hoàn số dư + ghi dòng đối ứng vào lịch sử biến động số dư
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(t => t.TaiKhoanTien).Include(t => t.DanhMuc)
                .FirstOrDefaultAsync(x => x.MaGiaoDich == id && x.TrangThai == "HoatDong");

            if (giaoDich == null || giaoDich.TaiKhoanTien == null)
                return NotFound();

            // Kiểm tra quyền trên giao dịch (hộ gia đình hoặc ví nhóm)
            if (!await CoQuyenTrenGiaoDichAsync(giaoDich, nguoiDung, hoGiaDinh))
                return Forbid();

            bool laTruongNhomVi = giaoDich.TaiKhoanTien.LoaiVi == "ViNhom" && giaoDich.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung;
            if (!isChuHo && !laTruongNhomVi && giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
            {
                TempData["Loi"] = "Bạn không có quyền xóa giao dịch này.";
                return RedirectToAction(nameof(Index));
            }

            if (GiaoDichBiKhoa(giaoDich))
            {
                TempData["Loi"] = "Không thể xóa giao dịch nạp quỹ hoặc chuyển tiền.";
                return RedirectToAction(nameof(Index));
            }

            // Xóa khoản Thu mà ví không đủ tiền để trừ lại => chặn, tránh số dư âm
            if (giaoDich.LoaiGiaoDich == "Thu" && giaoDich.SoTien > giaoDich.TaiKhoanTien.SoDuHienTai)
            {
                TempData["Loi"] = "Số dư ví hiện không đủ để hoàn tác khoản thu này, nên không thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Hoàn lại số dư ví
                HoanTacSoDu(giaoDich.TaiKhoanTien, giaoDich.LoaiGiaoDich, giaoDich.SoTien);

                // 2. Xóa mềm giao dịch gốc
                giaoDich.TrangThai = "DaXoa";

                // 3. Ghi dòng đối ứng vào lịch sử biến động số dư
                //    (số dư đã hoàn ở bước 1, KHÔNG gọi CapNhatSoDu thêm lần nữa).
                //    TrangThai = "HoanTien" để các màn hình lọc "HoatDong"
                //    (danh sách giao dịch, ngân sách, hạn mức) không tính nhầm dòng này.
                bool laHoanTien = giaoDich.LoaiGiaoDich == "Chi";
                _context.GiaoDichs.Add(new GiaoDich
                {
                    MaTaiKhoan = giaoDich.MaTaiKhoan,
                    MaDanhMuc = giaoDich.MaDanhMuc,
                    MaNguoiThucHien = nguoiDung.MaNguoiDung,
                    SoTien = giaoDich.SoTien,
                    LoaiGiaoDich = laHoanTien ? "Thu" : "Chi",
                    NgayGiaoDich = DateTime.Now,
                    TrangThai = TrangThaiHoanTien,
                    GhiChu = $"{DauHoanTien} {(laHoanTien ? "Hoàn tiền" : "Trừ lại")} do xóa giao dịch #{giaoDich.MaGiaoDich}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["ThongBao"] = laHoanTien
                    ? $"Xóa giao dịch thành công. Đã hoàn lại {giaoDich.SoTien:N0}đ vào ví '{giaoDich.TaiKhoanTien.TenTaiKhoan}'."
                    : "Xóa giao dịch thành công.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Loi"] = "Có lỗi xảy ra khi xóa giao dịch. Chưa có thay đổi nào được thực hiện.";
            }

            return RedirectToAction(nameof(Index));
        }

        // DETAILS
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            bool isChuHo = hoGiaDinh != null && LaChuHo(nguoiDung, hoGiaDinh);

            var giaoDich = await _context.GiaoDichs
                .Include(g => g.DanhMuc).Include(g => g.TaiKhoanTien).Include(g => g.NguoiThucHien)
                .FirstOrDefaultAsync(m => m.MaGiaoDich == id && (m.TrangThai == "HoatDong" || m.TrangThai == "DaXoa"));

            if (giaoDich == null || giaoDich.TaiKhoanTien == null ||
                !await CoQuyenTrenGiaoDichAsync(giaoDich, nguoiDung, hoGiaDinh))
                return NotFound();

            // Thành viên thường chỉ xem được giao dịch ví gia đình, ví nhóm đã tham gia, hoặc của chính mình
            if (!isChuHo &&
                !giaoDich.TaiKhoanTien.LaTaiKhoanGiaDinh &&
                giaoDich.TaiKhoanTien.LoaiVi != "ViNhom" &&
                giaoDich.MaNguoiThucHien != nguoiDung.MaNguoiDung)
                return NotFound();

            return View(giaoDich);
        }

        // THÊM GIAO DỊCH ĐỊNH KỲ (THÊM GDDK)
        [HttpGet]
        public async Task<IActionResult> ThemGDDK()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);
            await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung);

            var danhSachVi = ViewBag.TaiKhoans as List<TaiKhoanTien>;
            if (hoGiaDinh == null && (danhSachVi == null || danhSachVi.Count == 0))
            {
                TempData["ThongBao"] = "Bạn chưa tham gia Hộ gia đình hoặc có Ví nhóm nào.";
                return RedirectToAction("Index", "HoGiaDinhs");
            }

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
                var dm = await LayHoacTaoDanhMucAsync(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc, giaoDichDinhKy.LoaiGiaoDich);
                giaoDichDinhKy.MaDanhMuc = dm.MaDanhMuc;
            }

            giaoDichDinhKy.MaNguoiThucHien = nguoiDung.MaNguoiDung;
            giaoDichDinhKy.TrangThai = "HoatDong";

            // Kiểm tra quyền sở hữu ví (bao gồm cả Ví hộ gia đình, Ví nhóm và Ví mục tiêu cá nhân)
            var taiKhoan = await _context.TaiKhoanTiens.FirstOrDefaultAsync(x => x.MaTaiKhoan == giaoDichDinhKy.MaTaiKhoan);
            if (taiKhoan == null || !await CoQuyenDungTaiKhoanAsync(taiKhoan, nguoiDung, hoGiaDinh))
                ModelState.AddModelError("MaTaiKhoan", "Tài khoản tiền không hợp lệ hoặc bạn không có quyền.");

            if (giaoDichDinhKy.SoTien <= 0)
                ModelState.AddModelError("SoTien", "Số tiền phải lớn hơn 0.");

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(hoGiaDinh?.MaGiaDinh, nguoiDung.MaNguoiDung, TenDanhMuc);
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

        // GD-02: XÁC NHẬN THANH TOÁN CHO KHOẢN ĐỊNH KỲ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanThanhToan(int id, decimal soTienThucTe)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id);

            // Phải thuộc đúng hộ / ví nhóm + đúng quyền
            if (dinhKy == null || !await CoQuyenQuanLyDinhKyAsync(dinhKy, nguoiDung, hoGiaDinh))
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

            // Tạo giao dịch thực tế phát sinh
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

            // Khoản chi định kỳ cũng phải được tính vào ngân sách và cảnh báo nếu chạm ngưỡng / vượt hạn mức
            await KiemTraVaTaoCanhBaoNganSachAsync(nguoiDung, giaoDichThucTe, taiKhoan);

            TempData["ThongBao"] = "Đã xác nhận thanh toán thành công và ghi nhận vào lịch sử.";
            return RedirectToAction(nameof(DanhSachDinhKy));
        }

        // GD-03 & TC34: TẠM DỪNG / KÍCH HOẠT LẠI (đảo trạng thái)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiTrangThai(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id && x.TrangThai != TrangThaiDaXoa);

            if (dinhKy == null || !await CoQuyenQuanLyDinhKyAsync(dinhKy, nguoiDung, hoGiaDinh))
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

        // THAY ĐỔI TRẠNG THÁI ĐỊNH KỲ (TẠM DỪNG / KÍCH HOẠT / XÓA)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChuyenTrangThaiDinhKy(int id, string hanhDong)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDung(nguoiDung.MaNguoiDung);

            var dinhKy = await _context.GiaoDichDinhKys
                .Include(x => x.TaiKhoanTien)
                .FirstOrDefaultAsync(x => x.MaDinhKy == id && x.TrangThai != TrangThaiDaXoa);

            if (dinhKy == null || dinhKy.TaiKhoanTien == null)
                return NotFound();

            if (!await CoQuyenQuanLyDinhKyAsync(dinhKy, nguoiDung, hoGiaDinh))
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