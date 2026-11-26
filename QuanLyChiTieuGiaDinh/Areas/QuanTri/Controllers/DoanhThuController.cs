using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class DongBaoCao
{
    public string Nhan { get; set; } = "";
    public DateTime TuNgay { get; set; }      // đầu kỳ (để bấm xem chi tiết)
    public DateTime DenNgay { get; set; }     // cuối kỳ (không gồm ngày này)
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal ChiNcc { get; set; }
    public decimal LoiNhuan => DoanhThu - ChiNcc;
}

public class DongDonDoanhThu
{
    public DonHang Don { get; set; } = null!;
    public DateTime NgayTinh { get; set; }
}

public class DongSanPhamDoanhThu
{
    public string TenSanPham { get; set; } = "";
    public int SoLuong { get; set; }
    public decimal TienHang { get; set; }
}

public class ChiTietDoanhThuVM
{
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }       // không gồm ngày này
    public string TieuDe { get; set; } = "";
    public List<DongDonDoanhThu> DonHang { get; set; } = new();
    public List<DongSanPhamDoanhThu> SanPham { get; set; } = new();
    public List<CongNo> KhoanChi { get; set; } = new();
    public List<DieuChinhKho> HaoHut { get; set; } = new();   // phiếu điều chỉnh kho trong kỳ (chỉ để biết, không trừ vào tiền)
    public decimal TongHaoHut => HaoHut.Sum(d => d.GiaTriHaoHut);
    public decimal TongDoanhThu => DonHang.Sum(d => d.Don.TongTien);
    public decimal TongChi => KhoanChi.Sum(c => c.SoTien);
}

public class DoanhThuVM
{
    public string Loai { get; set; } = "thang";
    public int Nam { get; set; }
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public decimal TongDoanhThu, DoanhThuThang, ChiNccThang, TongChiNcc;
    public decimal TongHaoHut;   // giá trị hàng hỏng / hết hạn / mất (theo giá vốn) - không trừ vào doanh thu khả dụng
    public int DonHoanThanh, DonCho;
    public decimal LoiNhuanThang => DoanhThuThang - ChiNccThang;
    public decimal TongLoiNhuan => TongDoanhThu - TongChiNcc;
    public List<DongBaoCao> BaoCao { get; set; } = new();
}

/// <summary>
/// Báo cáo doanh thu: chỉ tính đơn HOÀN THÀNH (theo ngày hoàn thành), trừ tiền đã chi trả nhà cung cấp -> lợi nhuận.
/// Xem theo từng ngày (khoảng ngày) hoặc theo từng tháng của một năm.
/// </summary>
public class DoanhThuController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuCongNo _congNo;

    public DoanhThuController(ShopSonContext db, DichVuCongNo congNo)
    {
        _db = db;
        _congNo = congNo;
    }

    public async Task<IActionResult> Index(string? loai, int? nam, DateTime? tuNgay, DateTime? denNgay)
    {
        var bayGio = DateTime.Now;
        var vm = new DoanhThuVM
        {
            Loai = loai == "ngay" ? "ngay" : "thang",
            Nam = nam ?? bayGio.Year,
            TuNgay = (tuNgay ?? new DateTime(bayGio.Year, bayGio.Month, 1)).Date,
            DenNgay = (denNgay ?? bayGio).Date
        };
        var dauThang = new DateTime(bayGio.Year, bayGio.Month, 1);

        vm.TongDoanhThu = await _congNo.TongDoanhThuAsync();
        vm.TongChiNcc = await _congNo.DaChiNccAsync();
        vm.DoanhThuThang = await _db.DonHangs.Where(d => d.TrangThai == HangSo.HoanThanh && (d.NgayHoanThanh ?? d.NgayCapNhat) >= dauThang).SumAsync(d => d.TongTien);
        vm.ChiNccThang = await _db.CongNos.Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan && c.NgayGhi >= dauThang).SumAsync(c => c.SoTien);
        vm.DonHoanThanh = await _db.DonHangs.CountAsync(d => d.TrangThai == HangSo.HoanThanh);
        vm.DonCho = await _db.DonHangs.CountAsync(d => HangSo.TrangThaiChuaGiao.Contains(d.TrangThai));   // đơn chờ giao (gồm cả trạng thái cũ CHO_XAC_NHAN)
        vm.TongHaoHut = (await _db.DieuChinhKhos.AsNoTracking().ToListAsync()).Sum(d => d.GiaTriHaoHut);

        // Lấy dữ liệu thô rồi gom nhóm trong bộ nhớ (dữ liệu của shop nhỏ nên rất nhanh và dễ hiểu)
        var donOk = await _db.DonHangs.AsNoTracking().Where(d => d.TrangThai == HangSo.HoanThanh).Select(d => new { NgayCapNhat = d.NgayHoanThanh ?? d.NgayCapNhat, d.TongTien }).ToListAsync();
        var chi = await _db.CongNos.AsNoTracking().Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan)
            .Select(c => new { c.NgayGhi, c.SoTien }).ToListAsync();

        if (vm.Loai == "ngay")
        {
            var tu = vm.TuNgay; var den = vm.DenNgay.AddDays(1);
            vm.BaoCao = donOk.Where(d => d.NgayCapNhat >= tu && d.NgayCapNhat < den)
                .GroupBy(d => d.NgayCapNhat.Date).OrderByDescending(g => g.Key)
                .Select(g => new DongBaoCao
                {
                    Nhan = g.Key.ToString("dd/MM/yyyy"),
                    TuNgay = g.Key,
                    DenNgay = g.Key.AddDays(1),
                    SoDon = g.Count(),
                    DoanhThu = g.Sum(x => x.TongTien),
                    ChiNcc = chi.Where(c => c.NgayGhi.Date == g.Key).Sum(c => c.SoTien)
                }).ToList();
        }
        else
        {
            vm.BaoCao = donOk.Where(d => d.NgayCapNhat.Year == vm.Nam)
                .GroupBy(d => d.NgayCapNhat.Month).OrderByDescending(g => g.Key)
                .Select(g => new DongBaoCao
                {
                    Nhan = "Tháng " + g.Key,
                    TuNgay = new DateTime(vm.Nam, g.Key, 1),
                    DenNgay = new DateTime(vm.Nam, g.Key, 1).AddMonths(1),
                    SoDon = g.Count(),
                    DoanhThu = g.Sum(x => x.TongTien),
                    ChiNcc = chi.Where(c => c.NgayGhi.Year == vm.Nam && c.NgayGhi.Month == g.Key).Sum(c => c.SoTien)
                }).ToList();
        }
        return View(vm);
    }

    /// <summary>
    /// Xem chi tiết doanh thu: danh sách từng đơn hoàn thành, doanh thu theo sản phẩm và các khoản đã chi cho nhà cung cấp.
    /// Không truyền ngày = xem toàn bộ (dùng để đối soát "Doanh thu khả dụng").
    /// </summary>
    private async Task<ChiTietDoanhThuVM> TaoChiTietAsync(DateTime? tuNgay, DateTime? denNgay)
    {
        var vm = new ChiTietDoanhThuVM { TuNgay = tuNgay?.Date, DenNgay = denNgay?.Date };
        vm.TieuDe = tuNgay == null ? "Toàn bộ thời gian"
            : denNgay!.Value.Date == tuNgay.Value.Date.AddDays(1) ? "Ngày " + tuNgay.Value.ToString("dd/MM/yyyy")
            : $"Từ {tuNgay.Value:dd/MM/yyyy} đến {denNgay!.Value.AddDays(-1):dd/MM/yyyy}";

        var truyVan = _db.DonHangs.AsNoTracking().Where(d => d.TrangThai == HangSo.HoanThanh);
        var dsDon = await truyVan.ToListAsync();
        vm.DonHang = dsDon
            .Select(d => new DongDonDoanhThu { Don = d, NgayTinh = d.NgayHoanThanh ?? d.NgayCapNhat })
            .Where(x => (tuNgay == null || x.NgayTinh >= tuNgay.Value.Date) && (denNgay == null || x.NgayTinh < denNgay.Value.Date))
            .OrderByDescending(x => x.NgayTinh).ToList();

        var dsMa = vm.DonHang.Select(x => x.Don.MaDonHang).ToList();
        var dong = await _db.ChiTietDonHangs.AsNoTracking().Include(c => c.MaSanPhamNavigation)
            .Where(c => dsMa.Contains(c.MaDonHang)).ToListAsync();
        vm.SanPham = dong.GroupBy(c => c.MaSanPhamNavigation.TenSanPham)
            .Select(g => new DongSanPhamDoanhThu { TenSanPham = g.Key, SoLuong = g.Sum(c => c.SoLuong), TienHang = g.Sum(c => c.GiaBan * c.SoLuong) })
            .OrderByDescending(x => x.TienHang).ToList();

        var chi = await _db.CongNos.AsNoTracking().Include(c => c.MaNccNavigation)
            .Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan).ToListAsync();
        vm.KhoanChi = chi.Where(c => (tuNgay == null || c.NgayGhi >= tuNgay.Value.Date) && (denNgay == null || c.NgayGhi < denNgay.Value.Date))
            .OrderByDescending(c => c.NgayGhi).ToList();

        var dieuChinh = await _db.DieuChinhKhos.AsNoTracking().Include(d => d.MaSanPhamNavigation).ToListAsync();
        vm.HaoHut = dieuChinh.Where(d => (tuNgay == null || d.NgayTao >= tuNgay.Value.Date) && (denNgay == null || d.NgayTao < denNgay.Value.Date))
            .OrderByDescending(d => d.NgayTao).ToList();
        return vm;
    }

    /// <summary>Xem chi tiết doanh thu: từng đơn hoàn thành, doanh thu theo sản phẩm, các khoản chi nhà cung cấp.</summary>
    public async Task<IActionResult> ChiTiet(DateTime? tuNgay, DateTime? denNgay)
    {
        return View(await TaoChiTietAsync(tuNgay, denNgay));
    }

    /// <summary>Xuất chi tiết doanh thu ra file Excel (.csv): mỗi dòng một đơn hoàn thành, có dòng tổng cuối file.</summary>
    [HttpGet]
    public async Task<IActionResult> XuatExcel(DateTime? tuNgay, DateTime? denNgay)
    {
        var vm = await TaoChiTietAsync(tuNgay, denNgay);
        var dong = vm.DonHang.Select(x => new[]
        {
            x.Don.MaDonHang.ToString(), x.NgayTinh.ToString("dd/MM/yyyy HH:mm"), x.Don.HoTen, x.Don.MaNguoiDung == null ? "Khách" : "Thành viên",
            x.Don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan ? "Chuyển khoản" : "COD", ((long)x.Don.TongTien).ToString()
        }).ToList();
        dong.Add(new[] { "", "", "TỔNG DOANH THU", "", "", ((long)vm.TongDoanhThu).ToString() });
        dong.Add(new[] { "", "", "ĐÃ CHI NHÀ CUNG CẤP", "", "", ((long)vm.TongChi).ToString() });
        dong.Add(new[] { "", "", "CHÊNH LỆCH (doanh thu - chi)", "", "", ((long)(vm.TongDoanhThu - vm.TongChi)).ToString() });
        return TaoFileCsv($"DoanhThu_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            new[] { "Mã đơn", "Ngày hoàn thành", "Khách hàng", "Loại khách", "Thanh toán", "Tổng tiền" }, dong);
    }
}