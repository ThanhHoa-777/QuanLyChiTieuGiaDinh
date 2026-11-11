using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    public class NganSachesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NganSachesController(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Helper Methods

        private async Task<NguoiDung?> LayNguoiDungDangNhap()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);
        }

        private async Task NapDuLieuForm(int? maGiaDinh, int? selectedDanhMucId = null)
        {
            // Lấy danh mục chi tiêu của gia đình hoặc danh mục chung hệ thống
            var danhMucs = await _context.DanhMucs
                .Where(x => x.LoaiDanhMuc == "ChiTieu" &&
                            x.TrangThai == "Active" &&
                            (x.MaGiaDinh == null || (maGiaDinh.HasValue && x.MaGiaDinh == maGiaDinh.Value)))
                .OrderBy(x => x.TenDanhMuc)
                .ToListAsync();

            ViewBag.DanhMucs = danhMucs;
            ViewBag.MaDanhMuc = new SelectList(danhMucs, "MaDanhMuc", "TenDanhMuc", selectedDanhMucId);
        }

        #endregion

        // =========================================================
        // MÀN HÌNH DANH SÁCH NGÂN SÁCH CÁ NHÂN THEO THÁNG/NĂM
        // =========================================================
        public async Task<IActionResult> Index(int? thang, int? nam)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            int selectedThang = thang ?? DateTime.Now.Month;
            int selectedNam = nam ?? DateTime.Now.Year;

            // Chỉ lấy ngân sách cá nhân do chính người dùng này tạo và đang Active
            var query = _context.NganSachs
                .Include(x => x.DanhMuc)
                .Where(x => x.MaNguoiTao == nguoiDung.MaNguoiDung &&
                            !x.IsGiaDinh &&
                            x.Thang == selectedThang &&
                            x.Nam == selectedNam &&
                            x.TrangThai == "Active");

            var danhSachNganSach = await query.OrderBy(x => x.TenNganSach).ToListAsync();
            var danhSachTinhToan = new List<object>();
            int ngayDaQua = DateTime.Now.Day;

            // Lấy thông tin Hộ gia đình của người dùng để dò tài khoản ví cá nhân
            var thanhVien = await _context.ThanhVienGiaDinhs
                .Include(x => x.HoGiaDinh)
                .FirstOrDefaultAsync(x => x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "DaXacNhan");
            int? maGiaDinh = thanhVien?.MaGiaDinh ?? (await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaChuHo == nguoiDung.MaNguoiDung))?.MaGiaDinh;

            foreach (var nganSach in danhSachNganSach)
            {
                var giaoDichQuery = _context.GiaoDichs
                    .Include(g => g.TaiKhoanTien)
                    .Where(x => x.LoaiGiaoDich == "Chi" &&
                                x.TrangThaiDuyet == "DaDuyet" &&
                                x.TrangThai == "HoatDong" &&
                                x.TaiKhoanTien != null &&
                                x.MaNguoiThucHien == nguoiDung.MaNguoiDung &&
                                !x.TaiKhoanTien.LaTaiKhoanGiaDinh && // Chỉ tính ví cá nhân
                                x.NgayGiaoDich.Month == nganSach.Thang &&
                                x.NgayGiaoDich.Year == nganSach.Nam);

                // Lọc theo Danh mục nếu ngân sách có chỉ định cụ thể danh mục
                if (nganSach.MaDanhMuc.HasValue)
                {
                    giaoDichQuery = giaoDichQuery.Where(x => x.MaDanhMuc == nganSach.MaDanhMuc.Value);
                }

                decimal daChi = await giaoDichQuery.SumAsync(x => (decimal?)x.SoTien) ?? 0;
                decimal hanMuc = nganSach.SoTienHanMuc;
                decimal conLai = hanMuc - daChi;
                decimal phanTramDaDung = hanMuc > 0 ? (daChi / hanMuc) * 100 : 0;

                // Màu thanh tiến độ
                string mauTienDo = "bg-success";
                if (phanTramDaDung >= 100) mauTienDo = "bg-danger";
                else if (phanTramDaDung >= nganSach.NguongCanhBaoPhanTram) mauTienDo = "bg-warning";

                // Dự báo thông minh cuối tháng
                int soNgayTrongThang = DateTime.DaysInMonth(nganSach.Nam, nganSach.Thang);
                bool laThangHienTai = nganSach.Thang == DateTime.Now.Month && nganSach.Nam == DateTime.Now.Year;
                string loiCanhBaoDuBao = "";

                if (laThangHienTai && ngayDaQua > 0)
                {
                    decimal tocDoChiHangNgay = daChi / ngayDaQua;
                    decimal duBaoCuoiThang = daChi + (tocDoChiHangNgay * (soNgayTrongThang - ngayDaQua));

                    if (hanMuc > 0 && duBaoCuoiThang > hanMuc)
                    {
                        decimal phanTramVuot = ((duBaoCuoiThang - hanMuc) / hanMuc) * 100;
                        loiCanhBaoDuBao = $"Tốc độ chi {tocDoChiHangNgay:N0} ₫/ngày. Dự kiến cuối tháng vượt {phanTramVuot:N0}% hạn mức!";
                    }
                }

                danhSachTinhToan.Add(new
                {
                    NganSach = nganSach,
                    DaChi = daChi,
                    ConLai = conLai,
                    PhanTramDaDung = phanTramDaDung,
                    MauTienDo = mauTienDo,
                    LoiCanhBaoDuBao = loiCanhBaoDuBao
                });
            }

            ViewBag.DanhSachTinhToan = danhSachTinhToan;
            ViewBag.ThangChon = selectedThang;
            ViewBag.NamChon = selectedNam;

            return View(danhSachNganSach);
        }

        // =========================================================
        // XEM CHI TIẾT DANH SÁCH GIAO DỊCH THUỘC NGÂN SÁCH (DRILL-DOWN)
        // =========================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var nganSach = await _context.NganSachs
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiTao)
                .FirstOrDefaultAsync(x => x.MaNganSach == id && x.TrangThai == "Active" && x.MaNguoiTao == nguoiDung.MaNguoiDung && !x.IsGiaDinh);

            if (nganSach == null) return NotFound();

            var giaoDichQuery = _context.GiaoDichs
                .Include(g => g.TaiKhoanTien)
                .Include(g => g.DanhMuc)
                .Include(g => g.NguoiThucHien)
                .Where(x => x.LoaiGiaoDich == "Chi" &&
                            x.TrangThaiDuyet == "DaDuyet" &&
                            x.TrangThai == "HoatDong" &&
                            x.TaiKhoanTien != null &&
                            x.MaNguoiThucHien == nguoiDung.MaNguoiDung &&
                            !x.TaiKhoanTien.LaTaiKhoanGiaDinh &&
                            x.NgayGiaoDich.Month == nganSach.Thang &&
                            x.NgayGiaoDich.Year == nganSach.Nam);

            if (nganSach.MaDanhMuc.HasValue)
            {
                giaoDichQuery = giaoDichQuery.Where(x => x.MaDanhMuc == nganSach.MaDanhMuc.Value);
            }

            var danhSachGiaoDich = await giaoDichQuery.OrderByDescending(x => x.NgayGiaoDich).ToListAsync();

            ViewBag.DanhSachGiaoDich = danhSachGiaoDich;
            ViewBag.TongDaChi = danhSachGiaoDich.Sum(x => x.SoTien);

            return View(nganSach);
        }

        // =========================================================
        // THIẾT LẬP NGÂN SÁCH MỚI
        // =========================================================
        public async Task<IActionResult> Create()
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var thanhVien = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(x => x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "DaXacNhan");
            int? maGiaDinh = thanhVien?.MaGiaDinh ?? (await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaChuHo == nguoiDung.MaNguoiDung))?.MaGiaDinh;

            await NapDuLieuForm(maGiaDinh);

            var now = DateTime.Now;
            var model = new NganSach
            {
                Thang = now.Month,
                Nam = now.Year,
                NgayBatDau = new DateTime(now.Year, now.Month, 1),
                NgayKetThuc = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)),
                NguongCanhBaoPhanTram = 80,
                IsGiaDinh = false // Luôn là cá nhân
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NganSach nganSach)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            nganSach.IsGiaDinh = false; // Cố định cá nhân

            var thanhVien = await _context.ThanhVienGiaDinhs.FirstOrDefaultAsync(x => x.MaNguoiDung == nguoiDung.MaNguoiDung && x.TrangThai == "DaXacNhan");
            int? maGiaDinh = thanhVien?.MaGiaDinh ?? (await _context.HoGiaDinhs.FirstOrDefaultAsync(h => h.MaChuHo == nguoiDung.MaNguoiDung))?.MaGiaDinh;

            // Kiểm tra trùng lặp cấu hình ngân sách cá nhân theo danh mục trong tháng/năm
            var daTonTai = await _context.NganSachs.AnyAsync(x =>
                x.MaNguoiTao == nguoiDung.MaNguoiDung &&
                !x.IsGiaDinh &&
                x.MaDanhMuc == nganSach.MaDanhMuc &&
                x.Thang == nganSach.Thang &&
                x.Nam == nganSach.Nam &&
                x.TrangThai == "Active");

            if (daTonTai)
            {
                ModelState.AddModelError("", "Ngân sách cho danh mục này trong tháng đã chọn đã tồn tại!");
            }

            if (!ModelState.IsValid)
            {
                await NapDuLieuForm(maGiaDinh, nganSach.MaDanhMuc);
                return View(nganSach);
            }

            nganSach.MaGiaDinh = maGiaDinh ?? 0;
            nganSach.MaNguoiTao = nguoiDung.MaNguoiDung;
            nganSach.TrangThai = "Active";
            nganSach.NgayTao = DateTime.Now;

            _context.NganSachs.Add(nganSach);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Tạo ngân sách cá nhân thành công.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // CHỈNH SỬA NGÂN SÁCH CÁ NHÂN
        // =========================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var nganSach = await _context.NganSachs.FirstOrDefaultAsync(x => x.MaNganSach == id && x.TrangThai == "Active" && x.MaNguoiTao == nguoiDung.MaNguoiDung);
            if (nganSach == null) return NotFound();

            await NapDuLieuForm(nganSach.MaGiaDinh, nganSach.MaDanhMuc);
            return View(nganSach);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NganSach nganSachInput)
        {
            if (id != nganSachInput.MaNganSach) return NotFound();

            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var nganSachGoc = await _context.NganSachs.FirstOrDefaultAsync(x => x.MaNganSach == id && x.TrangThai == "Active" && x.MaNguoiTao == nguoiDung.MaNguoiDung);
            if (nganSachGoc == null) return NotFound();

            if (ModelState.IsValid)
            {
                nganSachGoc.TenNganSach = nganSachInput.TenNganSach;
                nganSachGoc.SoTienHanMuc = nganSachInput.SoTienHanMuc;
                nganSachGoc.NguongCanhBaoPhanTram = nganSachInput.NguongCanhBaoPhanTram;
                nganSachGoc.NgayBatDau = nganSachInput.NgayBatDau;
                nganSachGoc.NgayKetThuc = nganSachInput.NgayKetThuc;

                await _context.SaveChangesAsync();

                TempData["ThongBao"] = "Cập nhật ngân sách thành công.";
                return RedirectToAction(nameof(Index));
            }

            await NapDuLieuForm(nganSachGoc.MaGiaDinh, nganSachInput.MaDanhMuc);
            return View(nganSachInput);
        }

        // =========================================================
        // XÓA NGÂN SÁCH CÁ NHÂN
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var nguoiDung = await LayNguoiDungDangNhap();
            if (nguoiDung == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var nganSach = await _context.NganSachs.FirstOrDefaultAsync(x => x.MaNganSach == id && x.TrangThai == "Active" && x.MaNguoiTao == nguoiDung.MaNguoiDung);
            if (nganSach == null) return NotFound();

            nganSach.TrangThai = "Deleted";
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Xóa ngân sách thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}