using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// CÔNG NỢ & DOANH THU.
/// Bảng CongNo đã gộp công nợ khách hàng và công nợ nhà cung cấp, phân biệt bằng cột LoaiDoiTuong.
/// </summary>
public class DichVuCongNo
{
    private readonly ShopSonContext _db;

    public DichVuCongNo(ShopSonContext db)
    {
        _db = db;
    }

    // =====================================================================
    // CÔNG NỢ KHÁCH HÀNG (tiền chuyển khoản)
    // =====================================================================

    /// <summary>
    /// Admin xác nhận đã nhận tiền chuyển khoản -> cộng công nợ khách (số dương).
    /// Chống ghi trùng: mỗi đơn chỉ có 1 dòng DA_NHAN_TIEN_CK.
    /// </summary>
    public async Task GhiCongNoKhachAsync(DonHang don)
    {
        if (don.MaNguoiDung == null || don.TongTien <= 0) return;
        bool daCo = await _db.CongNos.AnyAsync(c => c.MaDonHang == don.MaDonHang && c.LoaiGhiChu == HangSo.DaNhanTienCK);
        if (daCo) return;

        _db.CongNos.Add(new CongNo
        {
            LoaiDoiTuong = HangSo.CongNoKhachHang,
            MaNguoiDung = don.MaNguoiDung,
            MaDonHang = don.MaDonHang,
            LoaiGhiChu = HangSo.DaNhanTienCK,
            SoTien = don.TongTien,
            NgayGhi = DateTime.Now,
            GhiChu = $"Xác nhận nhận tiền chuyển khoản đơn #{don.MaDonHang}"
        });
    }

    /// <summary>Admin bấm "Đã hoàn tiền cho khách" -> trừ công nợ khách (số âm). Chống trừ trùng.</summary>
    public async Task TruCongNoKhachAsync(DonHang don)
    {
        if (don.MaNguoiDung == null || don.TongTien <= 0) return;
        bool daCo = await _db.CongNos.AnyAsync(c => c.MaDonHang == don.MaDonHang && c.LoaiGhiChu == HangSo.HoanTienCK);
        if (daCo) return;

        _db.CongNos.Add(new CongNo
        {
            LoaiDoiTuong = HangSo.CongNoKhachHang,
            MaNguoiDung = don.MaNguoiDung,
            MaDonHang = don.MaDonHang,
            LoaiGhiChu = HangSo.HoanTienCK,
            SoTien = -don.TongTien,
            NgayGhi = DateTime.Now,
            GhiChu = $"Hoàn tiền CK đơn #{don.MaDonHang}"
        });
    }

    /// <summary>
    /// Công nợ khách hàng hiển thị trên trang tổng quan = tiền chuyển khoản đã nhận
    /// nhưng đơn chưa kết thúc (đang xử lý, đang giao, hoặc đã hủy / giao thất bại nhưng chưa hoàn tiền).
    /// </summary>
    public async Task<decimal> CongNoKhachHangAsync()
    {
        var dsTrangThai = HangSo.TrangThaiGiuTienKhach;   // gồm cả GIAO_THAT_BAI: chưa hoàn tiền thì shop vẫn đang giữ tiền
        return await _db.DonHangs.AsNoTracking()
            .Where(d => d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && d.DaNhanTien && !d.DaHoanTien && dsTrangThai.Contains(d.TrangThai))
            .SumAsync(d => d.TongTien);
    }

    // =====================================================================
    // DOANH THU & CÔNG NỢ NHÀ CUNG CẤP
    // =====================================================================

    /// <summary>Tổng doanh thu = tổng tiền các đơn đã HOÀN THÀNH.</summary>
    public async Task<decimal> TongDoanhThuAsync()
    {
        return await _db.DonHangs.AsNoTracking().Where(d => d.TrangThai == HangSo.HoanThanh).SumAsync(d => d.TongTien);
    }

    /// <summary>Tổng tiền đã chi trả cho nhà cung cấp (các dòng THANH_TOAN).</summary>
    public async Task<decimal> DaChiNccAsync()
    {
        return await _db.CongNos.AsNoTracking()
            .Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan)
            .SumAsync(c => c.SoTien);
    }

    /// <summary>Doanh thu khả dụng = tổng doanh thu - đã chi NCC. Đây là số tiền được phép dùng để trả nợ/nhập hàng.</summary>
    public async Task<decimal> SoDuKhaDungAsync()
    {
        return await TongDoanhThuAsync() - await DaChiNccAsync();
    }

    // =====================================================================
    // CÔNG NỢ NHÀ CUNG CẤP - TRẢ DẦN
    // Cách ghi sổ:
    //  - Nhập hàng chưa trả: 1 dòng PHAT_SINH = nợ gốc của phiếu (giữ nguyên, không sửa).
    //  - Mỗi lần trả: thêm 1 dòng THANH_TOAN (có mã phiếu). Còn nợ = nợ gốc - tổng các lần trả.
    //  - Nhập hàng trả ngay: chỉ có 1 dòng THANH_TOAN, không có PHAT_SINH (phiếu không bao giờ nợ).
    // =====================================================================

    /// <summary>Danh sách nợ theo từng phiếu nhập (chỉ phiếu có ghi nợ). chiPhieuConNo = true: chỉ lấy phiếu còn nợ.</summary>
    public async Task<List<DongNoPhieu>> LayNoTheoPhieuAsync(bool chiPhieuConNo = false, int maNcc = 0, int maNhapKho = 0)
    {
        var truyVan = _db.CongNos.AsNoTracking().Include(c => c.MaNhapKhoNavigation).Include(c => c.MaNccNavigation)
            .Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.PhatSinh && c.MaNhapKho != null);
        if (maNcc > 0) truyVan = truyVan.Where(c => c.MaNcc == maNcc);
        if (maNhapKho > 0) truyVan = truyVan.Where(c => c.MaNhapKho == maNhapKho);
        var dsPhatSinh = await truyVan.ToListAsync();

        var dsMaPhieu = dsPhatSinh.Select(c => c.MaNhapKho!.Value).ToList();
        var dsDaTra = await _db.CongNos.AsNoTracking()
            .Where(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.LoaiGhiChu == HangSo.ThanhToan
                        && c.MaNhapKho != null && dsMaPhieu.Contains(c.MaNhapKho.Value))
            .OrderBy(c => c.NgayGhi).ToListAsync();

        var ketQua = new List<DongNoPhieu>();
        foreach (var ps in dsPhatSinh)
        {
            var dong = new DongNoPhieu
            {
                MaNhapKho = ps.MaNhapKho!.Value,
                MaNcc = ps.MaNcc ?? 0,
                TenNcc = ps.MaNccNavigation?.TenNcc ?? "—",
                NgayNhap = ps.MaNhapKhoNavigation?.NgayNhap ?? ps.NgayGhi,
                NoGoc = ps.SoTien,
                LichSuTra = dsDaTra.Where(t => t.MaNhapKho == ps.MaNhapKho).ToList()
            };
            if (!chiPhieuConNo || dong.ConNo > 0) ketQua.Add(dong);
        }
        // Quá hạn lên đầu, rồi phiếu có hạn sớm hơn (nhập trước) - đây cũng là thứ tự ưu tiên khi trả theo NCC
        return ketQua.OrderByDescending(d => d.QuaHan).ThenBy(d => d.HanThanhToan).ThenBy(d => d.MaNhapKho).ToList();
    }

    /// <summary>Tổng nợ nhà cung cấp còn phải trả = tổng nợ gốc - tổng đã trả của các phiếu ghi nợ.</summary>
    public async Task<decimal> TongCongNoNccAsync()
    {
        var ds = await LayNoTheoPhieuAsync(chiPhieuConNo: true);
        return ds.Sum(d => d.ConNo);
    }

    /// <summary>Số tiền tối thiểu cho một lần trả: 10% nợ gốc (làm tròn lên 1.000đ), nhưng không quá số còn nợ.</summary>
    public static decimal MucTraToiThieu(DongNoPhieu dong)
    {
        decimal muc = Math.Ceiling(dong.NoGoc * HangSo.PhanTramTraToiThieu / 100m / 1000m) * 1000m;
        return Math.Min(muc, dong.ConNo);
    }

    /// <summary>
    /// Trả nợ MỘT phiếu nhập với số tiền admin nhập. Kiểm tra: lớn hơn 0, không dưới mức tối thiểu,
    /// không vượt số còn nợ, không vượt doanh thu khả dụng. Trả về (thành công, thông báo).
    /// </summary>
    public async Task<(bool ThanhCong, string ThongBao)> TraNoPhieuAsync(int maNhapKho, decimal soTien)
    {
        var dong = (await LayNoTheoPhieuAsync(maNhapKho: maNhapKho)).FirstOrDefault();
        if (dong == null) return (false, "Phiếu này không có công nợ cần trả.");
        if (dong.ConNo <= 0) return (false, $"Phiếu #{maNhapKho} đã trả hết rồi.");

        string? loi = await KiemTraSoTienAsync(soTien, dong.ConNo, MucTraToiThieu(dong));
        if (loi != null) return (false, loi);

        await using var giaoDich = await _db.Database.BeginTransactionAsync();
        await GhiMotLanTraAsync(dong, soTien);
        await _db.SaveChangesAsync();
        await giaoDich.CommitAsync();

        decimal conLai = dong.ConNo - soTien;
        return (true, conLai == 0
            ? $"✅ Đã trả {TienIch.DinhDangTien(soTien)} - phiếu #{maNhapKho} đã thanh toán xong."
            : $"✅ Đã trả {TienIch.DinhDangTien(soTien)} cho phiếu #{maNhapKho}, còn nợ {TienIch.DinhDangTien(conLai)}.");
    }

    /// <summary>
    /// Trả nợ MỘT LẦN cho cả nhà cung cấp: hệ thống tự chia số tiền vào các phiếu quá hạn / cũ nhất trước,
    /// trả hết phiếu này mới sang phiếu kế tiếp (nên chỉ phiếu cuối cùng có thể bị trả một phần).
    /// </summary>
    public async Task<(bool ThanhCong, string ThongBao)> TraNoTheoNccAsync(int maNcc, decimal soTien)
    {
        var dsPhieu = await LayNoTheoPhieuAsync(chiPhieuConNo: true, maNcc: maNcc);
        if (dsPhieu.Count == 0) return (false, "Nhà cung cấp này không còn khoản nợ nào.");

        decimal tongConNo = dsPhieu.Sum(d => d.ConNo);
        string? loi = await KiemTraSoTienAsync(soTien, tongConNo, MucTraToiThieu(dsPhieu[0]));
        if (loi != null) return (false, loi);

        await using var giaoDich = await _db.Database.BeginTransactionAsync();
        decimal conPhaiChia = soTien;
        var dsDaTra = new List<string>();
        foreach (var dong in dsPhieu)
        {
            if (conPhaiChia <= 0) break;
            decimal tra = Math.Min(conPhaiChia, dong.ConNo);
            await GhiMotLanTraAsync(dong, tra);
            dsDaTra.Add($"#{dong.MaNhapKho}: {TienIch.DinhDangTien(tra)}" + (tra == dong.ConNo ? " (xong)" : ""));
            conPhaiChia -= tra;
        }
        await _db.SaveChangesAsync();
        await giaoDich.CommitAsync();
        return (true, $"✅ Đã trả {TienIch.DinhDangTien(soTien)} cho {dsPhieu[0].TenNcc}, chia vào: {string.Join("; ", dsDaTra)}.");
    }

    /// <summary>Kiểm tra số tiền trả. Trả về câu báo lỗi, hoặc null nếu hợp lệ.</summary>
    private async Task<string?> KiemTraSoTienAsync(decimal soTien, decimal conNo, decimal mucToiThieu)
    {
        if (soTien <= 0) return "Vui lòng nhập số tiền cần trả lớn hơn 0!";
        if (soTien > conNo) return $"Số tiền vượt quá số còn nợ ({TienIch.DinhDangTien(conNo)})!";
        if (soTien < mucToiThieu)
            return $"Mỗi lần phải trả ít nhất {TienIch.DinhDangTien(mucToiThieu)} ({HangSo.PhanTramTraToiThieu}% nợ gốc) để tránh trả lắt nhắt nhiều lần.";
        decimal khaDung = await SoDuKhaDungAsync();
        if (soTien > khaDung)
            return $"Doanh thu khả dụng không đủ để thanh toán. Hiện có {TienIch.DinhDangTien(khaDung)}, còn thiếu {TienIch.DinhDangTien(soTien - khaDung)}.";
        return null;
    }

    /// <summary>Ghi 1 dòng THANH_TOAN cho phiếu; trả hết thì đánh dấu phiếu "Đã thanh toán". Người gọi SaveChanges.</summary>
    private async Task GhiMotLanTraAsync(DongNoPhieu dong, decimal soTien)
    {
        _db.CongNos.Add(new CongNo
        {
            LoaiDoiTuong = HangSo.CongNoNhaCungCap,
            MaNcc = dong.MaNcc,
            MaNhapKho = dong.MaNhapKho,
            LoaiGhiChu = HangSo.ThanhToan,
            SoTien = soTien,
            NgayGhi = DateTime.Now,
            GhiChu = $"Trả nợ phiếu #{dong.MaNhapKho} (lần {dong.LichSuTra.Count + 1})"
        });
        if (soTien >= dong.ConNo)
        {
            var phieu = await _db.NhapKhos.FirstOrDefaultAsync(p => p.MaNhapKho == dong.MaNhapKho);
            if (phieu != null) phieu.DaThanhToan = true;
        }
    }
}

/// <summary>Công nợ của MỘT phiếu nhập: nợ gốc, đã trả, còn nợ, hạn thanh toán.</summary>
public class DongNoPhieu
{
    public int MaNhapKho { get; set; }
    public int MaNcc { get; set; }
    public string TenNcc { get; set; } = "";
    public DateTime NgayNhap { get; set; }
    public decimal NoGoc { get; set; }
    public List<CongNo> LichSuTra { get; set; } = new();     // các lần đã trả (dòng THANH_TOAN), cũ trước

    public decimal DaTra => LichSuTra.Sum(t => t.SoTien);
    public decimal ConNo => NoGoc - DaTra;
    public int PhanTramDaTra => NoGoc > 0 ? (int)Math.Floor(DaTra / NoGoc * 100) : 0;
    public DateTime HanThanhToan => NgayNhap.Date.AddDays(HangSo.SoNgayHanNoNcc);
    public bool QuaHan => ConNo > 0 && DateTime.Today > HanThanhToan;
    public int SoNgayQuaHan => QuaHan ? (DateTime.Today - HanThanhToan).Days : 0;
    public int SoNgayConLai => (HanThanhToan - DateTime.Today).Days;
    public string TrangThai => ConNo <= 0 ? "Đã thanh toán" : DaTra > 0 ? $"Đã trả một phần ({PhanTramDaTra}%)" : "Chưa thanh toán";
}
