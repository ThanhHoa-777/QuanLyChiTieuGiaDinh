using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>Kết quả tính giá của MỘT sản phẩm tại thời điểm hiện tại.</summary>
public class KetQuaGia
{
    public int MaSanPham { get; set; }

    /// <summary>Giá bán thường = đúng số admin đã nhập (cột GiaBan). Cũng là giá gạch ngang khi đang được giảm.</summary>
    public decimal GiaThuong { get; set; }

    /// <summary>Giá khách thật sự phải trả (sau khi giảm).</summary>
    public decimal GiaCuoi { get; set; }

    // ----- Flash sale -----
    public bool LaFlash { get; set; }          // hôm nay có flash, còn lượt và thật sự làm giá rẻ hơn
    public bool HetLuotFlash { get; set; }     // hôm nay có flash nhưng đã bán đủ lượt
    public int PhanTramFlash { get; set; }     // % giảm của flash sale (danh nghĩa, ví dụ 10)
    public int LuotFlashDaBan { get; set; }

    // ----- Giảm giá chậm bán -----
    public bool LaGiamChamBan { get; set; }    // đang giảm vì bán chậm
    public int PhanTramChamBan { get; set; }   // % giảm chậm bán (danh nghĩa, ví dụ 10)
    public int TyLeDaBan { get; set; }         // đã bán được bao nhiêu % trong kỳ xét

    /// <summary>true = tổng mức giảm bị chặn lại ở "giảm tối đa" của sản phẩm.</summary>
    public bool DaChamTran { get; set; }

    public bool CoGiam => GiaCuoi < GiaThuong;

    /// <summary>% giảm thật so với giá thường (giá gạch ngang). Luôn khớp với hai con số giá hiển thị.</summary>
    public int PhanTramGiamTong => GiaThuong > 0 ? (int)Math.Round((GiaThuong - GiaCuoi) / GiaThuong * 100m) : 0;

    /// <summary>Mô tả ngắn lý do đang giảm, ví dụ "Flash Sale + Ưu đãi đặc biệt".</summary>
    public string LyDoGiam
    {
        get
        {
            if (LaFlash && LaGiamChamBan) return "Flash Sale + Ưu đãi đặc biệt";
            if (LaFlash) return "Flash Sale";
            if (LaGiamChamBan) return "Ưu đãi đặc biệt";
            return "";
        }
    }
}

/// <summary>
/// TÍNH GIÁ BÁN - mọi nơi (trang chủ, danh sách, chi tiết, giỏ hàng, thanh toán) đều gọi dịch vụ này
/// nên một sản phẩm luôn chỉ có MỘT mức giá duy nhất, không bao giờ lệch nhau giữa các trang.
///
/// Quy tắc:
///  1. Giá thường = đúng số admin nhập (GiaBan). Đây cũng là giá bị gạch ngang khi sản phẩm đang được giảm.
///  2. Có hai loại giảm thêm: Flash Sale (hôm nay, còn lượt) và Giảm chậm bán (chủ shop bật).
///     Hai loại CỘNG DỒN theo kiểu nhân: giá x (1 - flash) x (1 - chậm bán). Ví dụ 10% và 10% -> giảm 19%, không phải 20%.
///  3. Tổng mức giảm so với giá thường không bao giờ vượt "GiamToiDa" (%) của sản phẩm.
///     Giá gợi ý cho admin (GoiYGiaBan) cũng tính theo mức tối đa này nên dù giảm hết cỡ vẫn còn lãi.
/// </summary>
public class DichVuGia
{
    private readonly ShopSonContext _db;
    private readonly DichVuFlashSale _flashSale;

    public DichVuGia(ShopSonContext db, DichVuFlashSale flashSale)
    {
        _db = db;
        _flashSale = flashSale;
    }

    /// <summary>Giá sàn: thấp nhất được phép bán = giá thường trừ mức giảm tối đa.</summary>
    public static decimal TinhGiaSan(decimal giaBan, int giamToiDa)
    {
        return Math.Round(giaBan * (1 - giamToiDa / 100m), 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// GỢI Ý GIÁ BÁN cho admin: giá thấp nhất để SAU KHI GIẢM TỐI ĐA vẫn còn lãi ít nhất "loiNhuanToiThieu" % so với giá nhập.
    /// Công thức: giá nhập x (1 + lãi) / (1 - giảm tối đa), rồi làm tròn LÊN bội của 1.000đ.
    /// Ví dụ nhập 100.000đ, lãi tối thiểu 10%, giảm tối đa 30% -> 100.000 x 1,1 / 0,7 = 157.143 -> gợi ý 158.000đ.
    /// Chỉ là gợi ý, admin vẫn nhập giá khác được.
    /// </summary>
    public static decimal GoiYGiaBan(decimal giaVon, int giamToiDa, int loiNhuanToiThieu = HangSo.LoiNhuanToiThieu)
    {
        if (giaVon <= 0 || giamToiDa >= 100) return 0;
        decimal gia = giaVon * (1 + loiNhuanToiThieu / 100m) / (1 - giamToiDa / 100m);
        return Math.Ceiling(gia / HangSo.BuocLamTronGia) * HangSo.BuocLamTronGia;
    }

    /// <summary>Tính giá cho nhiều sản phẩm cùng lúc (chỉ tốn vài truy vấn, không truy vấn lặp theo từng sản phẩm).</summary>
    /// boQuaFlash = true: tính như hôm nay KHÔNG có flash sale (dùng cho phần vượt giới hạn flash của mỗi khách).
    public async Task<Dictionary<int, KetQuaGia>> TinhNhieuAsync(IEnumerable<SanPham> dsSanPham, bool boQuaFlash = false)
    {
        var ds = dsSanPham.ToList();
        var ketQua = new Dictionary<int, KetQuaGia>();
        if (ds.Count == 0) return ketQua;

        var homNay = TienIch.HomNay();
        var dsFlash = await _flashSale.LayDanhSachAsync(homNay);
        var dsLuot = await _flashSale.LuotDaBanNhieuAsync(homNay);
        var daBanTrongKy = await LayDaBanTrongKyAsync(ds);

        foreach (var sp in ds)
        {
            int phanTramFlash = 0;
            bool coFlash = !boQuaFlash && dsFlash.TryGetValue(sp.MaSanPham, out phanTramFlash);
            int luot = dsLuot.TryGetValue(sp.MaSanPham, out int l) ? l : 0;
            int daBanKy = daBanTrongKy.TryGetValue(sp.MaSanPham, out int b) ? b : 0;
            ketQua[sp.MaSanPham] = TinhMot(sp, coFlash, phanTramFlash, luot, daBanKy);
        }
        return ketQua;
    }

    public async Task<KetQuaGia> TinhMotAsync(SanPham sp)
    {
        var kq = await TinhNhieuAsync(new[] { sp });
        return kq[sp.MaSanPham];
    }

    /// <summary>Gom số lượng đã bán (đơn HOÀN THÀNH) trong kỳ xét của những sản phẩm đủ điều kiện xét giảm chậm bán.</summary>
    private async Task<Dictionary<int, int>> LayDaBanTrongKyAsync(List<SanPham> ds)
    {
        var moc = DateTime.Now.AddMonths(-HangSo.SoThangXetChamBan);
        var dsMa = ds.Where(s => s.TuDongGiam && s.NgayTao <= moc && s.TonKho > 0).Select(s => s.MaSanPham).ToList();
        if (dsMa.Count == 0) return new Dictionary<int, int>();

        var dong = await _db.ChiTietDonHangs.AsNoTracking()
            .Where(c => dsMa.Contains(c.MaSanPham)
                        && c.MaDonHangNavigation.TrangThai == HangSo.HoanThanh
                        && (c.MaDonHangNavigation.NgayHoanThanh ?? c.MaDonHangNavigation.NgayCapNhat) >= moc)
            .GroupBy(c => c.MaSanPham)
            .Select(g => new { Ma = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
            .ToListAsync();
        return dong.ToDictionary(d => d.Ma, d => d.SoLuong);
    }

    /// <summary>Tính giá của một sản phẩm khi đã biết đủ thông tin flash sale và số lượng đã bán trong kỳ.</summary>
    private static KetQuaGia TinhMot(SanPham sp, bool coFlashHomNay, int phanTramFlash, int luotFlashDaBan, int daBanTrongKy)
    {
        decimal giaThuong = sp.GiaBan;
        var kq = new KetQuaGia
        {
            MaSanPham = sp.MaSanPham,
            GiaThuong = giaThuong,
            LuotFlashDaBan = luotFlashDaBan
        };

        // ----- Loại giảm 1: flash sale hôm nay -----
        int giamFlash = 0;
        if (coFlashHomNay)
        {
            if (luotFlashDaBan >= HangSo.LuotFlashToiDa) kq.HetLuotFlash = true;
            else giamFlash = phanTramFlash;
        }

        // ----- Loại giảm 2: bán chậm -----
        int giamChamBan = 0;
        var moc = DateTime.Now.AddMonths(-HangSo.SoThangXetChamBan);
        if (sp.TuDongGiam && sp.NgayTao <= moc && sp.TonKho > 0)
        {
            // Lượng hàng có đầu kỳ ~ tồn kho hiện tại + số đã bán trong kỳ
            int dauKy = sp.TonKho + daBanTrongKy;
            kq.TyLeDaBan = dauKy > 0 ? (int)Math.Round(daBanTrongKy * 100.0 / dauKy) : 100;
            if (kq.TyLeDaBan < HangSo.TyLeBanMucTieu) giamChamBan = sp.PhanTramChamBan;   // % do admin đặt riêng cho sản phẩm
        }

        // ----- Gộp: CỘNG DỒN kiểu nhân, rồi chặn bởi mức giảm tối đa -----
        decimal heSo = 1m;
        if (giamFlash > 0) heSo *= 1 - giamFlash / 100m;       // vd flash 10% -> nhân 0,9
        if (giamChamBan > 0) heSo *= 1 - giamChamBan / 100m;   // vd chậm bán 10% -> nhân thêm 0,9 (tổng 0,81 = giảm 19%)
        decimal gia = Math.Round(giaThuong * heSo, 0, MidpointRounding.AwayFromZero);

        decimal giaSan = TinhGiaSan(giaThuong, sp.GiamToiDa);
        if (gia < giaSan)          // giảm quá tay -> nâng lên đúng giá sàn
        {
            gia = giaSan;
            kq.DaChamTran = true;
        }
        kq.GiaCuoi = gia;

        // ----- Gắn nhãn: chỉ khi giá cuối thật sự thấp hơn giá thường -----
        if (gia < giaThuong)
        {
            if (giamFlash > 0) { kq.LaFlash = true; kq.PhanTramFlash = giamFlash; }
            if (giamChamBan > 0) { kq.LaGiamChamBan = true; kq.PhanTramChamBan = giamChamBan; }
        }
        return kq;
    }
} 