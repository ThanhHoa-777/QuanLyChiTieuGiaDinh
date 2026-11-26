using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class CongNoNccVM
{
    public List<DongNoPhieu> NoTheoPhieu { get; set; } = new();   // các phiếu còn nợ (quá hạn / cũ nhất trước)
    public List<CongNo> LichSuThanhToan { get; set; } = new();     // mọi lần trả tiền NCC (THANH_TOAN), mới nhất trước
    public decimal TongDoanhThu { get; set; }
    public decimal DaChiNcc { get; set; }
    public decimal SoDuKhaDung => TongDoanhThu - DaChiNcc;
    public decimal TongCongNo => NoTheoPhieu.Sum(d => d.ConNo);

    /// <summary>Gộp nợ theo nhà cung cấp (để trả một lần cho cả NCC).</summary>
    public List<(int MaNcc, string TenNcc, int SoPhieu, int SoQuaHan, decimal ConNo, decimal MucToiThieu)> TheoNcc =>
        NoTheoPhieu.GroupBy(d => new { d.MaNcc, d.TenNcc })
            .Select(g => (g.Key.MaNcc, g.Key.TenNcc, g.Count(), g.Count(d => d.QuaHan), g.Sum(d => d.ConNo), DichVuCongNo.MucTraToiThieu(g.First())))
            .OrderByDescending(x => x.Item4).ThenByDescending(x => x.Item5).ToList();
}

public class CongNoKhachVM
{
    public decimal TongDangGiu { get; set; }     // tiền CK đã nhận nhưng đơn chưa kết thúc (khớp ô trên trang tổng quan)
    public decimal TongDaNhan { get; set; }      // tổng các dòng DA_NHAN_TIEN_CK
    public decimal TongDaHoan { get; set; }      // tổng các dòng HOAN_TIEN_CK (dạng số dương)
    public List<DonHang> DonDangGiu { get; set; } = new();
    public List<CongNo> SoGhi { get; set; } = new();
    public string Loc { get; set; } = "";
}

/// <summary>
/// Công nợ: (1) nhà cung cấp - các khoản còn nợ và nút thanh toán (chỉ trả được khi doanh thu khả dụng đủ);
/// (2) khách hàng - tiền chuyển khoản shop đang giữ và sổ ghi nhận/hoàn tiền.
/// </summary>
public class CongNoController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuCongNo _congNo;

    public CongNoController(ShopSonContext db, DichVuCongNo congNo)
    {
        _db = db;
        _congNo = congNo;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new CongNoNccVM
        {
            NoTheoPhieu = await _congNo.LayNoTheoPhieuAsync(chiPhieuConNo: true),
            LichSuThanhToan = await _db.CongNos.AsNoTracking().Include(c => c.MaNccNavigation)
                .Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan)
                .OrderByDescending(c => c.NgayGhi).ThenByDescending(c => c.MaCongNo).ToListAsync(),
            TongDoanhThu = await _congNo.TongDoanhThuAsync(),
            DaChiNcc = await _congNo.DaChiNccAsync()
        };
        return View(vm);
    }

    /// <summary>
    /// CÔNG NỢ KHÁCH HÀNG: tiền chuyển khoản shop đã nhận nhưng đơn chưa kết thúc (shop còn "nợ" hàng hoặc tiền hoàn).
    /// Gồm danh sách đơn đang giữ tiền và sổ ghi các lần nhận tiền / hoàn tiền.
    /// </summary>
    public async Task<IActionResult> KhachHang(string? loc)
    {
        var dsTrangThai = HangSo.TrangThaiGiuTienKhach;
        var soGhi = _db.CongNos.AsNoTracking().Include(c => c.MaNguoiDungNavigation)
            .Where(c => c.LoaiDoiTuong == HangSo.CongNoKhachHang);
        if (loc == HangSo.DaNhanTienCK || loc == HangSo.HoanTienCK) soGhi = soGhi.Where(c => c.LoaiGhiChu == loc);

        var vm = new CongNoKhachVM
        {
            Loc = loc ?? "",
            TongDangGiu = await _congNo.CongNoKhachHangAsync(),
            TongDaNhan = await _db.CongNos.Where(c => c.LoaiDoiTuong == HangSo.CongNoKhachHang && c.LoaiGhiChu == HangSo.DaNhanTienCK).SumAsync(c => c.SoTien),
            TongDaHoan = -await _db.CongNos.Where(c => c.LoaiDoiTuong == HangSo.CongNoKhachHang && c.LoaiGhiChu == HangSo.HoanTienCK).SumAsync(c => c.SoTien),
            DonDangGiu = await _db.DonHangs.AsNoTracking()
                .Where(d => d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && d.DaNhanTien && !d.DaHoanTien && dsTrangThai.Contains(d.TrangThai))
                .OrderByDescending(d => d.NgayCapNhat).ToListAsync(),
            SoGhi = await soGhi.OrderByDescending(c => c.NgayGhi).ToListAsync()
        };
        return View(vm);
    }

    /// <summary>Trả nợ MỘT phiếu nhập với số tiền admin nhập. maPhieuQuayVe > 0: bấm từ trang chi tiết phiếu thì quay lại phiếu đó.</summary>
    [HttpPost]
    public async Task<IActionResult> TraNoPhieu(int maNhapKho, decimal soTien, int maPhieuQuayVe = 0)
    {
        var (thanhCong, thongBao) = await _congNo.TraNoPhieuAsync(maNhapKho, soTien);
        if (thanhCong) BaoThanhCong(thongBao); else BaoLoi(thongBao);

        if (maPhieuQuayVe > 0) return RedirectToAction("ChiTiet", "Kho", new { id = maPhieuQuayVe });
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Trả nợ một lần cho cả nhà cung cấp: hệ thống tự chia vào phiếu quá hạn / cũ nhất trước.</summary>
    [HttpPost]
    public async Task<IActionResult> TraNoTheoNcc(int maNcc, decimal soTien)
    {
        var (thanhCong, thongBao) = await _congNo.TraNoTheoNccAsync(maNcc, soTien);
        if (thanhCong) BaoThanhCong(thongBao); else BaoLoi(thongBao);
        return RedirectToAction(nameof(Index));
    }
}
