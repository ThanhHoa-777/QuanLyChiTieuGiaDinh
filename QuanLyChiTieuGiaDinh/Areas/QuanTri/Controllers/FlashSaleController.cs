using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class DongFlashSaleAdmin
{
    public SanPham SanPham { get; set; } = null!;
    public KetQuaGia Gia { get; set; } = null!;   // giá hiện tại của sản phẩm (chỉ có ý nghĩa khi xem ngày hôm nay)
    public int PhanTram { get; set; }
    public int LuotBan { get; set; }          // số lượt flash đã đặt (đã trừ khi đơn hủy)
    public int DaHoanThanh { get; set; }      // số lượng thuộc các đơn đã hoàn thành
    public int DangXuLy { get; set; }         // số lượng thuộc các đơn còn đang xử lý
}

public class FlashSaleAdminVM
{
    public DateOnly Ngay { get; set; }
    public DateOnly HomNay { get; set; }
    public List<DateOnly> DanhSachNgay { get; set; } = new();
    public List<DongFlashSaleAdmin> Dong { get; set; } = new();
}

/// <summary>Lịch sử Flash Sale theo ngày: sản phẩm nào được chọn, bán được bao nhiêu lượt, bao nhiêu đã hoàn thành.</summary>
public class FlashSaleController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuFlashSale _flashSale;
    private readonly DichVuGia _gia;

    public FlashSaleController(ShopSonContext db, DichVuFlashSale flashSale, DichVuGia gia)
    {
        _db = db;
        _flashSale = flashSale;
        _gia = gia;
    }

    public async Task<IActionResult> Index(DateTime? ngay)
    {
        // Chỉ xem được tới hôm nay: danh sách flash của mỗi ngày chỉ được bốc vào đúng ngày đó (theo tồn kho lúc đó),
        // nên không xem trước ngày mai - tránh việc "chốt" sản phẩm quá sớm rồi hôm sau kho đã thay đổi.
        var homNay = TienIch.HomNay();
        var ngayChon = DateOnly.FromDateTime(ngay ?? DateTime.Now);
        if (ngayChon > homNay) ngayChon = homNay;
        var dsFlash = await _flashSale.LayDanhSachAsync(ngayChon);   // hôm nay: tự bốc nếu chưa có; ngày cũ: chỉ đọc dữ liệu đã lưu

        var vm = new FlashSaleAdminVM
        {
            Ngay = ngayChon,
            HomNay = homNay,
            DanhSachNgay = await _db.FlashSaleNgays.AsNoTracking().Select(f => f.NgaySale).Distinct().OrderByDescending(n => n).ToListAsync()
        };

        var dsMa = dsFlash.Keys.ToList();
        var sanPham = await _db.SanPhams.AsNoTracking().Where(s => dsMa.Contains(s.MaSanPham)).ToListAsync();
        var giaHienTai = await _gia.TinhNhieuAsync(sanPham);
        var luot = await _flashSale.LuotDaBanNhieuAsync(ngayChon);

        // Các dòng chi tiết đơn hàng mua flash sale trong ngày đó, kèm trạng thái đơn
        var dongMua = await _db.ChiTietDonHangs.AsNoTracking()
            .Where(c => c.LaFlashSale && c.FlashSaleNgay == ngayChon && dsMa.Contains(c.MaSanPham))
            .Select(c => new { c.MaSanPham, c.SoLuong, TrangThai = c.MaDonHangNavigation.TrangThai }).ToListAsync();
        var dangXuLy = new[] { HangSo.ChoXacNhan, HangSo.XacNhan, HangSo.DangGiao, HangSo.DaGiaoChoXacNhan };

        foreach (var sp in sanPham.OrderBy(s => s.MaSanPham))
        {
            vm.Dong.Add(new DongFlashSaleAdmin
            {
                SanPham = sp,
                Gia = giaHienTai[sp.MaSanPham],
                PhanTram = dsFlash[sp.MaSanPham],
                LuotBan = luot.GetValueOrDefault(sp.MaSanPham, 0),
                DaHoanThanh = dongMua.Where(d => d.MaSanPham == sp.MaSanPham && d.TrangThai == HangSo.HoanThanh).Sum(d => d.SoLuong),
                DangXuLy = dongMua.Where(d => d.MaSanPham == sp.MaSanPham && dangXuLy.Contains(d.TrangThai)).Sum(d => d.SoLuong)
            });
        }
        return View(vm);
    }
}
