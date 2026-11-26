using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;
using Shopson.ViewModels;

namespace Shopson.DichVu;

/// <summary>Chuẩn bị dữ liệu thẻ sản phẩm (kèm giá đã tính flash sale / giảm chậm bán) cho các trang hiển thị.</summary>
public class DichVuSanPham
{
    /// <summary>
    /// Điều kiện "khách nhìn thấy và mua được": còn hàng, sản phẩm không bị ẩn, thương hiệu không bị ẩn.
    /// Dùng trong truy vấn: _db.SanPhams.Where(DichVuSanPham.DangBan)
    /// </summary>
    public static readonly Expression<Func<SanPham, bool>> DangBan =
        s => s.TonKho > 0 && !s.NgungKinhDoanh && !s.MaThuongHieuNavigation.NgungKinhDoanh;

    /// <summary>Sản phẩm (đã Include thương hiệu) có bị ẩn không - bị ẩn thì khách không mua được.</summary>
    public static bool BiAn(SanPham sp) => sp.NgungKinhDoanh || (sp.MaThuongHieuNavigation?.NgungKinhDoanh ?? false);

    /// <summary>
    /// SỐ ĐANG GIỮ của từng sản phẩm = tổng số lượng trong các đơn "Chờ giao" (kho chỉ trừ thật khi admin bấm Giao).
    /// SỐ CÒN BÁN ĐƯỢC = tồn kho - số đang giữ. Khách luôn thấy và chỉ mua được theo số còn bán được,
    /// nên không có chuyện "trang ghi còn 5 mà đặt 2 bị chặn". Admin vẫn thấy tồn kho thật (hàng trên kệ).
    /// </summary>
    public static async Task<Dictionary<int, int>> LaySoDangGiuAsync(ShopSonContext db, IEnumerable<int> dsMaSanPham)
    {
        var dsMa = dsMaSanPham.Distinct().ToList();
        return await db.ChiTietDonHangs.AsNoTracking()
            .Where(c => dsMa.Contains(c.MaSanPham) && HangSo.TrangThaiChuaGiao.Contains(c.MaDonHangNavigation.TrangThai))
            .GroupBy(c => c.MaSanPham)
            .Select(g => new { g.Key, SoLuong = g.Sum(c => c.SoLuong) })
            .ToDictionaryAsync(x => x.Key, x => x.SoLuong);
    }

    /// <summary>Số còn bán được của MỘT sản phẩm (sản phẩm bị ẩn = 0).</summary>
    public static async Task<int> SoConBanAsync(ShopSonContext db, SanPham sp)
    {
        if (BiAn(sp)) return 0;
        var dangGiu = await LaySoDangGiuAsync(db, new[] { sp.MaSanPham });
        return Math.Max(0, sp.TonKho - dangGiu.GetValueOrDefault(sp.MaSanPham));
    }

    private readonly DichVuGia _gia;
    private readonly ShopSonContext _db;

    public DichVuSanPham(DichVuGia gia, ShopSonContext db)
    {
        _gia = gia;
        _db = db;
    }

    /// <summary>Biến danh sách sản phẩm (đã Include thương hiệu) thành danh sách thẻ hiển thị.</summary>
    public async Task<List<TheSanPhamVM>> TaoTheAsync(IEnumerable<SanPham> dsSanPham)
    {
        var ds = dsSanPham.ToList();
        var dsGia = await _gia.TinhNhieuAsync(ds);
        var dangGiu = await LaySoDangGiuAsync(_db, ds.Select(s => s.MaSanPham));

        var ketQua = new List<TheSanPhamVM>();
        foreach (var sp in ds)
        {
            ketQua.Add(new TheSanPhamVM
            {
                SanPham = sp,
                TenThuongHieu = sp.MaThuongHieuNavigation?.TenThuongHieu ?? "",
                Gia = dsGia[sp.MaSanPham],
                SoConBan = BiAn(sp) ? 0 : Math.Max(0, sp.TonKho - dangGiu.GetValueOrDefault(sp.MaSanPham))
            });
        }
        return ketQua;
    }

    public async Task<TheSanPhamVM?> TaoTheMotSanPhamAsync(SanPham sp)
    {
        var ds = await TaoTheAsync(new[] { sp });
        return ds.FirstOrDefault();
    }
}