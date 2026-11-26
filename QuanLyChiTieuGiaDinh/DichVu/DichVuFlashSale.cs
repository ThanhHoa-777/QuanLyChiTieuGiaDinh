using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// FLASH SALE (chuyển từ flashsale_ham.php).
/// - Mỗi ngày bốc ngẫu nhiên 6 sản phẩm giảm 10%, kết quả được lưu vào bảng FlashSaleNgay.
/// - CHỈ bốc sản phẩm CÒN HÀNG (và còn chỗ để giảm theo mức "giảm tối đa" của sản phẩm).
/// - Danh sách chỉ được bốc vào ĐÚNG NGÀY HÔM NAY, dựa trên tồn kho lúc đó. Ngày khác không tự bốc:
///   ngày đã qua chỉ xem lại được dữ liệu đã lưu, ngày chưa tới thì chưa có (vì kho còn thay đổi).
/// - Nếu trong ngày có sản phẩm hết hàng mà chưa bán lượt flash nào thì tự thay bằng sản phẩm khác.
/// - Cách chọn dùng "hạt giống" theo ngày nên ngày nào cũng cho cùng một thứ tự bốc.
/// - Mỗi sản phẩm chỉ bán tối đa 20 lượt flash sale trong ngày.
/// </summary>
public class DichVuFlashSale
{
    private readonly ShopSonContext _db;

    public DichVuFlashSale(ShopSonContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy danh sách sản phẩm flash sale của một ngày: [MaSanPham] = PhầnTrămGiảm.
    /// Hôm nay: chưa có thì bốc mới, có sản phẩm hết hàng thì thay thế. Ngày khác: chỉ đọc dữ liệu đã lưu.
    /// </summary>
    public async Task<Dictionary<int, int>> LayDanhSachAsync(DateOnly ngay)
    {
        var dsDong = await _db.FlashSaleNgays.AsNoTracking().Include(f => f.MaSanPhamNavigation)
            .Where(f => f.NgaySale == ngay).ToListAsync();

        if (ngay == TienIch.HomNay())
        {
            bool canBocMoi = dsDong.Count == 0;
            bool coHetHang = dsDong.Any(f => f.MaSanPhamNavigation.TonKho <= 0 && f.LuotBan == 0);
            if (canBocMoi || coHetHang)
            {
                try
                {
                    await CapNhatDanhSachHomNayAsync(ngay);
                }
                catch (DbUpdateException)
                {
                    // Hai người truy cập cùng lúc: người kia đã lưu rồi -> bỏ phần mình, đọc lại từ DB
                    _db.ChangeTracker.Clear();
                }
                dsDong = await _db.FlashSaleNgays.AsNoTracking().Include(f => f.MaSanPhamNavigation)
                    .Where(f => f.NgaySale == ngay).ToListAsync();
            }
        }
        return dsDong.ToDictionary(f => f.MaSanPham, f => f.PhanTramGiam);
    }

    /// <summary>
    /// Bốc mới (nếu chưa có) hoặc thay các sản phẩm đã hết hàng và chưa bán lượt nào bằng sản phẩm còn hàng.
    /// </summary>
    private async Task CapNhatDanhSachHomNayAsync(DateOnly ngay)
    {
        var dsHienCo = await _db.FlashSaleNgays.Include(f => f.MaSanPhamNavigation).Where(f => f.NgaySale == ngay).ToListAsync();

        // Bỏ các sản phẩm đã hết hàng mà chưa bán lượt flash nào
        var canThay = dsHienCo.Where(f => f.MaSanPhamNavigation.TonKho <= 0 && f.LuotBan == 0).ToList();
        _db.FlashSaleNgays.RemoveRange(canThay);
        var conLai = dsHienCo.Except(canThay).Select(f => f.MaSanPham).ToList();

        int canThem = HangSo.SoSanPhamFlashMoiNgay - conLai.Count;
        if (canThem <= 0 && canThay.Count == 0) return;

        // Ứng viên = còn hàng + còn chỗ giảm, chưa nằm trong danh sách. Xáo trộn theo hạt giống của ngày.
        var ungVien = (await LayUngVienAsync()).Where(m => !conLai.Contains(m)).ToList();
        var daXao = XaoTronTheoNgay(ngay, ungVien);

        foreach (int ma in daXao.Take(Math.Max(0, canThem)))
        {
            _db.FlashSaleNgays.Add(new FlashSaleNgay
            {
                NgaySale = ngay,
                MaSanPham = ma,
                PhanTramGiam = HangSo.PhanTramFlashMacDinh,
                LuotBan = 0
            });
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Mã các sản phẩm đủ điều kiện vào flash sale: còn hàng và cho phép giảm (giảm tối đa lớn hơn 0).</summary>
    private async Task<List<int>> LayUngVienAsync()
    {
        var ds = await _db.SanPhams.AsNoTracking().Where(DichVuSanPham.DangBan).OrderBy(s => s.MaSanPham)
            .Select(s => new { s.MaSanPham, s.GiamToiDa }).ToListAsync();
        // Sản phẩm để giảm tối đa = 0% nghĩa là chủ shop không muốn giảm giá món này -> không đưa vào flash
        return ds.Where(s => s.GiamToiDa > 0).Select(s => s.MaSanPham).ToList();
    }

    /// <summary>Xáo trộn danh sách bằng thuật toán Fisher-Yates với hạt giống = ngày dạng yyyyMMdd (ví dụ 20261030).</summary>
    private static List<int> XaoTronTheoNgay(DateOnly ngay, List<int> dsMa)
    {
        var rnd = new Random(ngay.Year * 10000 + ngay.Month * 100 + ngay.Day);
        var ketQua = new List<int>(dsMa);
        for (int i = ketQua.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (ketQua[i], ketQua[j]) = (ketQua[j], ketQua[i]);
        }
        return ketQua;
    }

    /// <summary>Số lượt flash sale đã bán của sản phẩm trong ngày.</summary>
    public async Task<int> LuotDaBanAsync(DateOnly ngay, int maSanPham)
    {
        var dong = await _db.FlashSaleNgays.AsNoTracking()
            .FirstOrDefaultAsync(f => f.NgaySale == ngay && f.MaSanPham == maSanPham);
        return dong == null ? 0 : dong.LuotBan;
    }

    /// <summary>Lấy số lượt đã bán của nhiều sản phẩm cùng lúc.</summary>
    public async Task<Dictionary<int, int>> LuotDaBanNhieuAsync(DateOnly ngay)
    {
        var ds = await _db.FlashSaleNgays.AsNoTracking().Where(f => f.NgaySale == ngay).ToListAsync();
        return ds.ToDictionary(f => f.MaSanPham, f => f.LuotBan);
    }

    /// <summary>
    /// Khi đơn bị hủy: trả lại lượt flash sale (chỉ trả nếu ngày sale là HÔM NAY, vì ngày cũ đã kết thúc).
    /// Ví dụ giới hạn 20 lượt, đã bán 17, khách hủy đơn mua 1 sản phẩm -> còn 16 đã bán (thêm 1 lượt).
    /// Chỉ thay đổi trong bộ nhớ, người gọi chịu trách nhiệm SaveChanges.
    /// </summary>
    public async Task HoanLuotAsync(int maDonHang)
    {
        var homNay = TienIch.HomNay();
        var dsFlash = await _db.ChiTietDonHangs.Where(c => c.MaDonHang == maDonHang && c.LaFlashSale).ToListAsync();
        foreach (var ct in dsFlash)
        {
            if (ct.FlashSaleNgay != homNay) continue;
            var dong = await _db.FlashSaleNgays.FirstOrDefaultAsync(f => f.NgaySale == homNay && f.MaSanPham == ct.MaSanPham);
            if (dong != null)
            {
                dong.LuotBan = Math.Max(dong.LuotBan - ct.SoLuong, 0); // không để số âm
            }
        }
    }

    /// <summary>Bỏ khoảng trắng, dấu chấm, dấu gạch để so sánh số điện thoại ("0911 222.333" = "0911222333").</summary>
    public static string ChuanHoaSdt(string? soDienThoai) =>
        new string((soDienThoai ?? "").Where(char.IsDigit).ToArray());

    /// <summary>
    /// Số sản phẩm flash (của một mã sản phẩm) khách ĐÃ mua hôm nay, tính chung theo TÀI KHOẢN và SỐ ĐIỆN THOẠI:
    /// đơn nào cùng tài khoản HOẶC cùng số điện thoại đều cộng vào. Nhờ vậy đổi tài khoản nhưng giữ SĐT,
    /// hay giữ tài khoản nhưng đổi SĐT, đều không mua thêm được. Đơn đã hủy không tính (lượt đã được trả lại).
    /// </summary>
    public async Task<int> SoFlashDaMuaAsync(int maSanPham, int maNguoiDung, string soDienThoai)
    {
        var homNay = TienIch.HomNay();
        // Lấy các dòng flash hôm nay của sản phẩm này (ít dữ liệu), rồi so SĐT đã chuẩn hóa trong bộ nhớ
        var dsDong = await _db.ChiTietDonHangs.AsNoTracking()
            .Where(c => c.MaSanPham == maSanPham && c.LaFlashSale && c.FlashSaleNgay == homNay
                     && c.MaDonHangNavigation.TrangThai != HangSo.Huy)
            .Select(c => new { c.SoLuong, c.MaDonHangNavigation.MaNguoiDung, c.MaDonHangNavigation.SoDienThoai })
            .ToListAsync();

        string sdt = ChuanHoaSdt(soDienThoai);
        return dsDong.Where(d => (maNguoiDung > 0 && d.MaNguoiDung == maNguoiDung)
                              || (sdt != "" && ChuanHoaSdt(d.SoDienThoai) == sdt))
                     .Sum(d => d.SoLuong);
    }
}
