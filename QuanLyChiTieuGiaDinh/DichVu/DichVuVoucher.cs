using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>Kết quả kiểm tra một mã giảm giá.</summary>
public class KetQuaVoucher
{
    public bool HopLe { get; set; }
    public string ThongBao { get; set; } = "";
    public string MaCode { get; set; } = "";
    public decimal TienGiam { get; set; }      // số tiền trừ vào tiền hàng
    public bool MienPhiShip { get; set; }      // mã miễn phí vận chuyển (GiaTri = 0)
    public string TenDong { get; set; } = "";  // dòng hiển thị: "Giảm 10% (MA)"
}

/// <summary>Một mã giảm giá được gợi ý cho khách ở ô nhập mã (kèm số tiền giảm ước tính theo giỏ hiện tại).</summary>
public class GoiYVoucher
{
    public string MaCode { get; set; } = "";
    public string MoTa { get; set; } = "";
    public string Hang { get; set; } = "";
    public string TenHang { get; set; } = "";
    public string MauHang { get; set; } = "";
    public string MoTaGiam { get; set; } = "";       // "Giảm 10% (tối đa 600.000đ)"
    public decimal TienGiamUocTinh { get; set; }
    public bool DuDieuKien { get; set; }              // đơn hiện tại đã đủ "đơn tối thiểu" chưa
    public string LyDo { get; set; } = "";            // nếu chưa đủ điều kiện: "Mua thêm 120.000đ để dùng mã"
    public string HanDung { get; set; } = "";
}

/// <summary>
/// MÃ GIẢM GIÁ & PHÍ SHIP (chuyển từ thanhtoan.php + thanhtoan_xuly.php).
/// Toàn bộ kiểm tra làm ở phía máy chủ nên khách không thể gian lận bằng cách sửa JavaScript.
/// </summary>
public class DichVuVoucher
{
    private readonly ShopSonContext _db;

    public DichVuVoucher(ShopSonContext db)
    {
        _db = db;
    }

    /// <summary>Phí ship: đơn từ 500.000đ miễn phí, dưới đó 30.000đ.</summary>
    public static decimal TinhPhiShip(decimal tamTinh)
    {
        return tamTinh >= HangSo.NguongMienPhiShip ? 0 : HangSo.PhiShipMacDinh;
    }

    /// <summary>Số tiền mã sẽ giảm (ước tính) với tiền hàng hiện tại - dùng để sắp xếp gợi ý, không dùng để tính đơn.</summary>
    private static decimal UocTinhGiam(MaGiamGium v, decimal tamTinh)
    {
        if (v.GiaTri == 0) return TinhPhiShip(tamTinh);                      // mã miễn ship: tiết kiệm đúng bằng phí ship
        if (v.LoaiGiam == "PHAN_TRAM")
        {
            decimal tien = Math.Round(tamTinh * v.GiaTri / 100m, 0, MidpointRounding.AwayFromZero);
            return v.GiamToiDa > 0 ? Math.Min(tien, v.GiamToiDa) : tien;
        }
        return v.GiaTri;
    }

    /// <summary>Tài khoản này đã dùng mã bao nhiêu lần (chỉ đếm đơn CHƯA HỦY - hủy đơn thì được trả lượt).</summary>
    public async Task<int> SoLanKhachDaDungAsync(string maCode, int maNguoiDung) =>
        await _db.DonHangs.CountAsync(d => d.MaNguoiDung == maNguoiDung && d.MaGiamGia == maCode && d.TrangThai != HangSo.Huy);

    /// <summary>
    /// Gợi ý các mã khách ĐƯỢC DÙNG (đúng hạng, còn hạn, còn lượt). Sắp xếp: hạng cao xuống thấp
    /// (Kim Cương → Vàng → Bạc → Thường); trong cùng hạng, mã đủ điều kiện lên trước rồi đến mã giảm nhiều hơn.
    /// </summary>
    public async Task<List<GoiYVoucher>> LayGoiYAsync(int maNguoiDung, decimal tamTinh)
    {
        if (maNguoiDung <= 0) return new List<GoiYVoucher>();
        var khach = await _db.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaNguoiDung == maNguoiDung);
        string hangKhach = khach?.HangKhachHang ?? HangThanhVien.Thuong;
        var hangDuocDung = HangThanhVien.LayDanhSachHangDuocDung(hangKhach);
        var homNay = TienIch.HomNay();

        var ds = await _db.MaGiamGia.AsNoTracking()
            .Where(v => hangDuocDung.Contains(v.HangVoucher) && (v.NgayHetHan == null || v.NgayHetHan >= homNay) && v.GioiHanDung - v.SoLanDung > 0)
            .ToListAsync();

        var ketQua = new List<GoiYVoucher>();
        foreach (var v in ds)
        {
            // Khách đã dùng hết số lần cho phép thì không gợi ý mã này nữa
            if (v.SoLanMoiKhach > 0 && await SoLanKhachDaDungAsync(v.MaCode, maNguoiDung) >= v.SoLanMoiKhach) continue;
            bool duDieuKien = v.DonToiThieu <= 0 || tamTinh >= v.DonToiThieu;
            string moTaGiam = v.GiaTri == 0 ? "Miễn phí vận chuyển"
                : v.LoaiGiam == "PHAN_TRAM" ? $"Giảm {v.GiaTri}%" + (v.GiamToiDa > 0 ? $" (tối đa {TienIch.DinhDangTien(v.GiamToiDa)})" : "")
                : $"Giảm {TienIch.DinhDangTien(v.GiaTri)}";
            ketQua.Add(new GoiYVoucher
            {
                MaCode = v.MaCode,
                MoTa = v.MoTa,
                Hang = v.HangVoucher,
                TenHang = HangThanhVien.TenHang(v.HangVoucher),
                MauHang = HangThanhVien.MauHang(v.HangVoucher),
                MoTaGiam = moTaGiam + (v.DonToiThieu > 0 ? $" · đơn từ {TienIch.DinhDangTien(v.DonToiThieu)}" : ""),
                TienGiamUocTinh = duDieuKien ? UocTinhGiam(v, tamTinh) : 0,
                DuDieuKien = duDieuKien,
                LyDo = duDieuKien ? "" : $"Mua thêm {TienIch.DinhDangTien(v.DonToiThieu - tamTinh)} để dùng mã này",
                HanDung = v.NgayHetHan.HasValue ? "HSD " + v.NgayHetHan.Value.ToString("dd/MM/yyyy") : "Không giới hạn thời gian"
            });
        }
        return ketQua
            .OrderByDescending(g => HangThanhVien.ThuTuHang(g.Hang))
            .ThenByDescending(g => g.DuDieuKien)
            .ThenByDescending(g => g.TienGiamUocTinh)
            .ThenBy(g => g.MaCode)
            .ToList();
    }

    /// <summary>
    /// Kiểm tra mã giảm giá theo thứ tự: tồn tại -> đã đăng nhập -> còn hạn -> còn lượt -> đủ hạng -> đủ đơn tối thiểu.
    /// Rồi tính số tiền giảm theo loại: miễn ship / giảm % (có trần giảm tối đa) / giảm số tiền cố định.
    /// </summary>
    public async Task<KetQuaVoucher> KiemTraAsync(string? maCode, int maNguoiDung, decimal tamTinh)
    {
        var kq = new KetQuaVoucher();
        maCode = (maCode ?? "").Trim().ToUpper();
        if (maCode == "")
        {
            kq.ThongBao = "Vui lòng nhập mã!";
            return kq;
        }

        var voucher = await _db.MaGiamGia.AsNoTracking().FirstOrDefaultAsync(v => v.MaCode == maCode);
        if (voucher == null)
        {
            kq.ThongBao = "Mã không hợp lệ!";
            return kq;
        }
        if (maNguoiDung <= 0)
        {
            kq.ThongBao = "Vui lòng đăng nhập để dùng mã giảm giá!";
            return kq;
        }
        if (voucher.NgayHetHan != null && voucher.NgayHetHan < TienIch.HomNay())
        {
            kq.ThongBao = "Mã đã hết hạn!";
            return kq;
        }
        if (voucher.SoLanDung >= voucher.GioiHanDung)
        {
            kq.ThongBao = "Mã đã hết lượt sử dụng!";
            return kq;
        }

        if (voucher.SoLanMoiKhach > 0)
        {
            int daDung = await SoLanKhachDaDungAsync(voucher.MaCode, maNguoiDung);
            if (daDung >= voucher.SoLanMoiKhach)
            {
                kq.ThongBao = voucher.SoLanMoiKhach == 1
                    ? "Bạn đã dùng mã này rồi (mỗi tài khoản chỉ dùng 1 lần)!"
                    : $"Bạn đã dùng hết {voucher.SoLanMoiKhach} lượt của mã này!";
                return kq;
            }
        }

        var khach = await _db.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaNguoiDung == maNguoiDung);
        string hangKhach = khach?.HangKhachHang ?? HangThanhVien.Thuong;
        if (HangThanhVien.ThuTuHang(hangKhach) < HangThanhVien.ThuTuHang(voucher.HangVoucher))
        {
            kq.ThongBao = $"Mã dành cho hạng {HangThanhVien.TenHang(voucher.HangVoucher)} trở lên!";
            return kq;
        }
        if (voucher.DonToiThieu > 0 && tamTinh < voucher.DonToiThieu)
        {
            kq.ThongBao = $"Đơn tối thiểu {TienIch.DinhDangTien(voucher.DonToiThieu)} mới áp dụng được mã này! (Đơn hiện tại: {TienIch.DinhDangTien(tamTinh)})";
            return kq;
        }

        kq.HopLe = true;
        kq.MaCode = voucher.MaCode;

        if (voucher.GiaTri == 0)
        {
            // Mã miễn phí vận chuyển
            kq.MienPhiShip = true;
            kq.TienGiam = 0;
            kq.TenDong = $"Miễn phí vận chuyển ({voucher.MaCode})";
            kq.ThongBao = "Miễn phí vận chuyển!";
        }
        else if (voucher.LoaiGiam == "PHAN_TRAM")
        {
            decimal tien = Math.Round(tamTinh * voucher.GiaTri / 100m, 0, MidpointRounding.AwayFromZero);
            bool daChamTran = false;
            if (voucher.GiamToiDa > 0 && tien > voucher.GiamToiDa)
            {
                tien = voucher.GiamToiDa;
                daChamTran = true;
            }
            kq.TienGiam = tien;
            kq.TenDong = $"Giảm {voucher.GiaTri}% ({voucher.MaCode})";
            kq.ThongBao = $"Giảm {voucher.GiaTri}% = {TienIch.DinhDangTien(tien)}" +
                          (daChamTran ? $" (tối đa {TienIch.DinhDangTien(voucher.GiamToiDa)})" : "") + "!";
        }
        else
        {
            kq.TienGiam = voucher.GiaTri;
            kq.TenDong = $"Giảm giá ({voucher.MaCode})";
            kq.ThongBao = $"Giảm {TienIch.DinhDangTien(voucher.GiaTri)}!";
        }
        return kq;
    }
}
