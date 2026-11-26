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
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
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
        /// Tạo truy vấn giao dịch thực tế phân tách rõ Hộ gia đình và Cá nhân, hỗ trợ Theo tháng hoặc Cả năm (thang == null).
        /// </summary>
        private IQueryable<GiaoDich> TaoTruyVanBaoCao(
            int maGiaDinh, NguoiDung nguoiDung, bool laChuHo, bool laCaNhan, int? thang, int nam)
        {
            var query = _context.GiaoDichs
                .Where(x => x.TaiKhoanTien != null &&
                            x.NgayGiaoDich.Year == nam)
                .ThuChiThucTe();

            if (thang.HasValue)
            {
                query = query.Where(x => x.NgayGiaoDich.Month == thang.Value);
            }

            if (laCaNhan)
            {
                // Cá nhân: giao dịch dùng tài khoản cá nhân của người dùng (không phụ thuộc MaGiaDinh của ví bị null hay không)
                query = query.Where(x => !x.TaiKhoanTien!.LaTaiKhoanGiaDinh &&
                                         (x.TaiKhoanTien.MaNguoiSoHuu == nguoiDung.MaNguoiDung ||
                                          x.MaNguoiThucHien == nguoiDung.MaNguoiDung));
            }
            else
            {
                // Hộ gia đình: thuộc hộ gia đình hiện tại
                query = query.Where(x => x.TaiKhoanTien!.MaGiaDinh == maGiaDinh);

                if (!laChuHo)
                {
                    // Thành viên xem hộ gia đình (BR56): giao dịch dùng tài khoản gia đình hoặc giao dịch của chính mình
                    query = query.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh ||
                                             x.MaNguoiThucHien == nguoiDung.MaNguoiDung);
                }
            }

            return query;
        }

        /// <summary>Tổng thu / tổng chi - một cách tính duy nhất cho cả kỳ này và kỳ trước.</summary>
        private static (decimal TongThu, decimal TongChi) TinhTong(IEnumerable<(string Loai, decimal SoTien)> giaoDichs)
        {
            var ds = giaoDichs.ToList();
            return (ds.Where(x => x.Loai == "Thu").Sum(x => x.SoTien),
                    ds.Where(x => x.Loai == "Chi").Sum(x => x.SoTien));
        }

        public async Task<IActionResult> Index(int? thang, int? nam, string cheDo = "gia-dinh", string kieuBaoCao = "thang")
        {
            var nguoiDung = await LayNguoiDungDangNhapAsync();
            if (nguoiDung == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            bool laCaNam = kieuBaoCao == "nam";
            bool laCaNhan = cheDo == "ca-nhan";
            int thangBaoCao = thang ?? DateTime.Now.Month;
            int namBaoCao = nam ?? DateTime.Now.Year;

            if (thangBaoCao < 1 || thangBaoCao > 12) thangBaoCao = DateTime.Now.Month;
            if (namBaoCao < 2000 || namBaoCao > 2100) namBaoCao = DateTime.Now.Year;

            var hoGiaDinh = await LayHoGiaDinhCuaNguoiDungAsync(nguoiDung.MaNguoiDung);
            if (hoGiaDinh == null)
            {
                ViewBag.CheDoXem = laCaNhan ? "ca-nhan" : "gia-dinh";
                ViewBag.LaCaNam = laCaNam;
                ViewBag.KieuBaoCao = laCaNam ? "nam" : "thang";
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

            // ===== KỲ BÁO CÁO (1 THÁNG HOẶC CẢ NĂM) =====
            int? thangLoc = laCaNam ? null : thangBaoCao;
            var giaoDichs = await TaoTruyVanBaoCao(maGiaDinh, nguoiDung, laChuHo, laCaNhan, thangLoc, namBaoCao)
                .Include(x => x.TaiKhoanTien)
                .Include(x => x.DanhMuc)
                .Include(x => x.NguoiThucHien)
                .OrderByDescending(x => x.NgayGiaoDich)
                .ToListAsync();

            var (tongThu, tongChi) = TinhTong(giaoDichs.Select(x => (x.LoaiGiaoDich, x.SoTien)));

            // ===== KỲ TRƯỚC (THÁNG TRƯỚC HOẶC NĂM TRƯỚC) =====
            int thangKyTruoc, namKyTruoc;
            int? thangLocKyTruoc;

            if (laCaNam)
            {
                thangKyTruoc = 0;
                namKyTruoc = namBaoCao - 1;
                thangLocKyTruoc = null;
            }
            else
            {
                var thangTruocDate = new DateTime(namBaoCao, thangBaoCao, 1).AddMonths(-1);
                thangKyTruoc = thangTruocDate.Month;
                namKyTruoc = thangTruocDate.Year;
                thangLocKyTruoc = thangKyTruoc;
            }

            var giaoDichKyTruoc = (await TaoTruyVanBaoCao(maGiaDinh, nguoiDung, laChuHo, laCaNhan,
                    thangLocKyTruoc, namKyTruoc)
                .Select(x => new { x.LoaiGiaoDich, x.SoTien })
                .ToListAsync())
                .Select(x => (x.LoaiGiaoDich, x.SoTien))
                .ToList();

            var (thuTruoc, chiTruoc) = TinhTong(giaoDichKyTruoc);

            var soSanh = new BaoCaoSoSanhViewModel
            {
                ThangTruoc = thangKyTruoc,
                NamTruoc = namKyTruoc,
                SoGiaoDichThangTruoc = giaoDichKyTruoc.Count,
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
                    ? $"Chỉ gồm các giao dịch thuộc tài khoản cá nhân của {nguoiDung.HoTen}."
                    : laChuHo
                        ? "Toàn bộ giao dịch của hộ gia đình."
                        : "Giao dịch của bạn và giao dịch dùng tài khoản gia đình.",
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
            ViewBag.SoSanh = soSanh;
            ViewBag.LaCaNam = laCaNam;
            ViewBag.KieuBaoCao = laCaNam ? "nam" : "thang";

            return View(model);
        }
    }
}