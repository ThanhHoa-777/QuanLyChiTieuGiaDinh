using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>
/// Quản lý mã giảm giá. Loại giảm: SO_TIEN (trừ số tiền cố định), PHAN_TRAM (trừ %, có thể giới hạn giảm tối đa),
/// GiaTri = 0 nghĩa là mã miễn phí vận chuyển. Mỗi mã dành cho một hạng khách trở lên.
/// </summary>
public class MaGiamGiaController : QuanTriController
{
    private readonly ShopSonContext _db;

    public MaGiamGiaController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.MaGiamGia.AsNoTracking().OrderBy(v => v.MaCode).ToListAsync());

    /// <summary>Danh sách các đơn hàng đã dùng mã này (bấm vào đơn để xem chi tiết).</summary>
    public async Task<IActionResult> DonDaDung(string id)
    {
        var ma = await _db.MaGiamGia.AsNoTracking().FirstOrDefaultAsync(v => v.MaCode == id);
        if (ma == null) return RedirectToAction(nameof(Index));
        ViewBag.Ma = ma;
        var dsDon = await _db.DonHangs.AsNoTracking().Where(d => d.MaGiamGia == id)
            .OrderByDescending(d => d.NgayDat).ToListAsync();
        return View(dsDon);
    }

    [HttpGet]
    public IActionResult Them() => View("Form", new MaGiamGium { GioiHanDung = 100, HangVoucher = HangThanhVien.Thuong, LoaiGiam = "SO_TIEN" });

    [HttpPost]
    public async Task<IActionResult> Them(MaGiamGium v)
    {
        v.MaCode = (v.MaCode ?? "").Trim().ToUpper();
        if (v.MaCode == "") { BaoLoi("Mã giảm giá không được để trống!"); return View("Form", v); }
        if (await _db.MaGiamGia.AnyAsync(x => x.MaCode == v.MaCode)) { BaoLoi("Mã này đã tồn tại!"); return View("Form", v); }
        ChuanHoa(v);
        v.SoLanDung = 0;
        _db.MaGiamGia.Add(v);
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã thêm mã giảm giá.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Sua(string id)
    {
        var v = await _db.MaGiamGia.AsNoTracking().FirstOrDefaultAsync(x => x.MaCode == id);
        return v == null ? RedirectToAction(nameof(Index)) : View("Form", v);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(string id, MaGiamGium form)
    {
        var v = await _db.MaGiamGia.FirstOrDefaultAsync(x => x.MaCode == id);
        if (v == null) return RedirectToAction(nameof(Index));
        ChuanHoa(form);
        v.GiaTri = form.GiaTri; v.MoTa = form.MoTa; v.NgayHetHan = form.NgayHetHan; v.GioiHanDung = form.GioiHanDung; v.SoLanMoiKhach = form.SoLanMoiKhach;
        v.HangVoucher = form.HangVoucher; v.LoaiGiam = form.LoaiGiam; v.DonToiThieu = form.DonToiThieu; v.GiamToiDa = form.GiamToiDa;
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật mã giảm giá.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(string id)
    {
        if (await _db.DonHangs.AnyAsync(d => d.MaGiamGia == id))
        {
            BaoLoi("Không xóa được: mã này đã được dùng trong đơn hàng. Hãy đặt ngày hết hạn là hôm qua để ngừng áp dụng.");
            return RedirectToAction(nameof(Index));
        }
        var v = await _db.MaGiamGia.FirstOrDefaultAsync(x => x.MaCode == id);
        if (v != null) { _db.MaGiamGia.Remove(v); await _db.SaveChangesAsync(); BaoThanhCong("Đã xóa mã giảm giá."); }
        return RedirectToAction(nameof(Index));
    }

    private static void ChuanHoa(MaGiamGium v)
    {
        v.MoTa = (v.MoTa ?? "").Trim();
        if (v.LoaiGiam != "PHAN_TRAM") { v.LoaiGiam = "SO_TIEN"; v.GiamToiDa = 0; }
        if (v.GiaTri < 0) v.GiaTri = 0;
        if (v.GioiHanDung < 0) v.GioiHanDung = 0;
        if (v.SoLanMoiKhach < 0) v.SoLanMoiKhach = 0;
        if (v.DonToiThieu < 0) v.DonToiThieu = 0;
        if (v.GiamToiDa < 0) v.GiamToiDa = 0;
        if (!new[] { "THUONG", "BAC", "VANG", "KIM_CUONG" }.Contains(v.HangVoucher)) v.HangVoucher = HangThanhVien.Thuong;
    }
}
