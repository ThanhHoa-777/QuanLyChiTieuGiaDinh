using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Danh sách khiếu nại của khách. Bấm vào từng đơn để xử lý (hoàn tiền / hoàn thành / giao thất bại).</summary>
public class KhieuNaiController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuDonHang _donHang;

    public KhieuNaiController(ShopSonContext db, DichVuDonHang donHang)
    {
        _db = db;
        _donHang = donHang;
    }

    /// <summary>xuLy: "chua" = chưa xử lý xong, "xong" = đã xử lý xong, rỗng = tất cả.</summary>
    public async Task<IActionResult> Index(string? xuLy)
    {
        await _donHang.TuDongCapNhatAsync();
        var truyVan = _db.DonHangs.AsNoTracking().Where(d => d.DaKhieuNai);
        if (xuLy == "chua") truyVan = truyVan.Where(d => d.TrangThai != HangSo.HoanThanh && d.TrangThai != HangSo.GiaoThatBai);
        else if (xuLy == "xong") truyVan = truyVan.Where(d => d.TrangThai == HangSo.HoanThanh || d.TrangThai == HangSo.GiaoThatBai);
        ViewBag.XuLy = xuLy ?? "";
        return View(await truyVan.OrderByDescending(d => d.NgayKhieuNai).ToListAsync());
    }
}
