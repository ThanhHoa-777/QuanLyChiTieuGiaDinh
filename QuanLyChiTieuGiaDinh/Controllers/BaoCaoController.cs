using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Helpers;
using QuanLyChiTieuGiaDinh.Models;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.Controllers
{
    [Authorize]
    public class BaoCaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BaoCaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<NguoiDung?> LayNguoiDungDangNhapAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);
        }

        // Lấy đúng hộ ĐANG HOẠT ĐỘNG, giống TrangChuController
        private async Task<HoGiaDinh?> LayHoGiaDinhCuaNguoiDungAsync(int maNguoiDung)
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

        /// <summary>
        /// TRUY VẤN DÙNG CHUNG cho tháng báo cáo và tháng so sánh (và dữ liệu xuất file, vì file xuất lấy từ cùng model hiển thị):
        /// luôn lọc đúng hộ gia đình + đúng tháng + điều kiện thu/chi thực tế (BaoCaoQueryExtensions.ThuChiThucTe)
        /// + phạm vi xem của người dùng (cá nhân / chủ hộ / thành viên BR56).
        /// </summary>
        private IQueryable<GiaoDich> TaoTruyVanBaoCao(
            int maGiaDinh, NguoiDung nguoiDung, bool laChuHo, bool laCaNhan, int thang, int nam)
        {
            var query = _context.GiaoDichs
                .Where(x => x.TaiKhoanTien != null &&
                            x.TaiKhoanTien.MaGiaDinh == maGiaDinh &&
                            x.NgayGiaoDich.Month == thang &&
                            x.NgayGiaoDich.Year == nam)
                .ThuChiThucTe();

            if (laCaNhan)
            {
                // Cá nhân: chỉ giao dịch do chính người dùng thực hiện
                query = query.Where(x => x.MaNguoiThucHien == nguoiDung.MaNguoiDung);
            }
            else if (!laChuHo)
            {
                // Gia đình - thành viên (BR56): giao dịch của mình + giao dịch dùng tài khoản gia đình
                query = query.Where(x => x.MaNguoiThucHien == nguoiDung.MaNguoiDung ||
                                         x.TaiKhoanTien!.LaTaiKhoanGiaDinh);
            }

            return query;
        }

        /// <summary>Tổng thu / tổng chi - một cách tính duy nhất cho cả tháng này và tháng trước.</summary>
        private static (decimal TongThu, decimal TongChi) TinhTong(IEnumerable<(string Loai, decimal SoTien)> giaoDichs)
        {
            var ds = giaoDichs.ToList();
            return (ds.Where(x => x.Loai == "Thu").Sum(x => x.SoTien),
                    ds.Where(x => x.Loai == "Chi").Sum(x => x.SoTien));
        }

        public async Task<IActionResult> Index(int? thang, int? nam, string cheDo = "gia-dinh")
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            int thangBaoCao = thang ?? DateTime.Now.Month;
            int namBaoCao = nam ?? DateTime.Now.Year;

            if (thangBaoCao < 1 || thangBaoCao > 12) thangBaoCao = DateTime.Now.Month;
            if (namBaoCao < 2000 || namBaoCao > 2100) namBaoCao = DateTime.Now.Year;

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDungAsync(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                return View(new BaoCaoViewModel
                {
                    NguoiDung = nguoiDung,
                    HoGiaDinh = null,
                    ThangBaoCao = thangBaoCao,
                    NamBaoCao = namBaoCao
                });
            }

            int maGiaDinh = hoGiaDinh.MaGiaDinh;
            bool laChuHo = LaChuHo(nguoiDung, hoGiaDinh);

            // Chế độ xem: "gia-dinh" (mặc định) hoặc "ca-nhan"
            bool laCaNhan = cheDo == "ca-nhan";

            // ===== THÁNG BÁO CÁO =====
            var giaoDichs = await TaoTruyVanBaoCao(maGiaDinh, nguoiDung, laChuHo, laCaNhan, thangBaoCao, namBaoCao)
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .OrderByDescending(x => x.NgayGiaoDich)
                .ToListAsync();

            var (tongThu, tongChi) = TinhTong(giaoDichs.Select(x => (x.LoaiGiaoDich, x.SoTien)));

            // ===== THÁNG TRƯỚC (cùng điều kiện lọc) =====
            var thangTruocDate = new DateTime(namBaoCao, thangBaoCao, 1).AddMonths(-1);

            var giaoDichThangTruoc = (await TaoTruyVanBaoCao(maGiaDinh, nguoiDung, laChuHo, laCaNhan,
                    thangTruocDate.Month, thangTruocDate.Year)
                .Select(x => new { x.LoaiGiaoDich, x.SoTien })
                .ToListAsync())
                .Select(x => (x.LoaiGiaoDich, x.SoTien))
                .ToList();

            var (thuTruoc, chiTruoc) = TinhTong(giaoDichThangTruoc);

            var soSanh = new BaoCaoSoSanhViewModel
            {
                ThangTruoc = thangTruocDate.Month,
                NamTruoc = thangTruocDate.Year,
                SoGiaoDichThangTruoc = giaoDichThangTruoc.Count,
                Thu = new ChiSoSoSanhViewModel { HienTai = tongThu, ThangTruoc = thuTruoc },
                Chi = new ChiSoSoSanhViewModel { HienTai = tongChi, ThangTruoc = chiTruoc },
                ChenhLech = new ChiSoSoSanhViewModel { HienTai = tongThu - tongChi, ThangTruoc = thuTruoc - chiTruoc }
            };

            List<BaoCaoDanhMucViewModel> GomTheoDanhMuc(string loai) => giaoDichs
                .Where(x => x.LoaiGiaoDich == loai)
                .GroupBy(x => x.DanhMuc != null ? x.DanhMuc.TenDanhMuc : "Không có danh mục")
                .Select(x => new BaoCaoDanhMucViewModel { TenDanhMuc = x.Key, SoTien = x.Sum(y => y.SoTien) })
                .OrderByDescending(x => x.SoTien)
                .ToList();

            var chiTheoNgay = giaoDichs
                .Where(x => x.LoaiGiaoDich == "Chi")
                .GroupBy(x => x.NgayGiaoDich.Date)
                .Select(x => new BaoCaoNgayViewModel { Ngay = x.Key, SoTien = x.Sum(y => y.SoTien) })
                .OrderBy(x => x.Ngay)
                .ToList();

            var model = new BaoCaoViewModel
            {
                NguoiDung = nguoiDung,
                HoGiaDinh = hoGiaDinh,
                MaNguoiDung = nguoiDung.MaNguoiDung,
                MaGiaDinh = maGiaDinh,
                LaChuHo = laChuHo,
                PhamViBaoCao = laCaNhan
                    ? "Chỉ gồm các giao dịch do bạn thực hiện."
                    : laChuHo
                        ? "Toàn bộ giao dịch của hộ gia đình."
                        : "Giao dịch do bạn thực hiện và giao dịch dùng tài khoản gia đình.",
                ThangBaoCao = thangBaoCao,
                NamBaoCao = namBaoCao,
                TongThu = tongThu,
                TongChi = tongChi,
                ChenhLech = tongThu - tongChi,
                SoGiaoDich = giaoDichs.Count,
                ChiTheoDanhMuc = GomTheoDanhMuc("Chi"),
                ThuTheoDanhMuc = GomTheoDanhMuc("Thu"),
                ChiTheoNgay = chiTheoNgay,
                GiaoDichs = giaoDichs
            };

            ViewBag.CheDoXem = laCaNhan ? "ca-nhan" : "gia-dinh";
            ViewBag.LaChuHo = laChuHo;
            ViewBag.MaNguoiDung = nguoiDung.MaNguoiDung;
            ViewBag.MaGiaDinh = maGiaDinh;
            ViewBag.SoSanh = soSanh; // số liệu so sánh với tháng trước (hiển thị + xuất file)

            return View(model);
        }
    }
}
