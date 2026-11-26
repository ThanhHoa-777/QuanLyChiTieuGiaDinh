using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Hồ sơ cá nhân và đổi mật khẩu của tài khoản admin đang đăng nhập.</summary>
public class CaNhanController : QuanTriController
{
    private readonly ShopSonContext _db;

    public CaNhanController(ShopSonContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> HoSo() => View(await _db.NguoiDungs.AsNoTracking().FirstAsync(n => n.MaNguoiDung == User.MaNguoiDung()));

    [HttpPost]
    public async Task<IActionResult> HoSo(string? tenNguoiDung, string? email, string? soDienThoai, string? diaChi)
    {
        var nd = await _db.NguoiDungs.FirstAsync(n => n.MaNguoiDung == User.MaNguoiDung());
        if (string.IsNullOrWhiteSpace(tenNguoiDung)) { BaoLoi("Họ tên không được để trống!"); return RedirectToAction(nameof(HoSo)); }
        if (!string.IsNullOrWhiteSpace(email) && !DichVuEmail.EmailHopLe(email)) { BaoLoi("Email không hợp lệ (ví dụ đúng: ten@gmail.com)!"); return RedirectToAction(nameof(HoSo)); }
        nd.TenNguoiDung = tenNguoiDung.Trim(); nd.Email = (email ?? "").Trim(); nd.SoDienThoai = (soDienThoai ?? "").Trim(); nd.DiaChi = (diaChi ?? "").Trim();
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật hồ sơ. (Tên hiển thị sẽ đổi sau khi bạn đăng nhập lại.)");
        return RedirectToAction(nameof(HoSo));
    }

    [HttpGet]
    public IActionResult DoiMatKhau() => View();

    [HttpPost]
    public async Task<IActionResult> DoiMatKhau(string? matKhauCu, string? matKhauMoi, string? matKhauMoiLai)
    {
        var nd = await _db.NguoiDungs.FirstAsync(n => n.MaNguoiDung == User.MaNguoiDung());
        if (nd.MatKhau != TienIch.MaHoaMd5(matKhauCu ?? "")) { BaoLoi("Mật khẩu hiện tại không chính xác!"); return RedirectToAction(nameof(DoiMatKhau)); }
        if (string.IsNullOrWhiteSpace(matKhauMoi)) { BaoLoi("Mật khẩu mới không được bỏ trống!"); return RedirectToAction(nameof(DoiMatKhau)); }
        if (matKhauMoi != matKhauMoiLai) { BaoLoi("Mật khẩu nhập lại không khớp!"); return RedirectToAction(nameof(DoiMatKhau)); }
        nd.MatKhau = TienIch.MaHoaMd5(matKhauMoi);
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đổi mật khẩu thành công!");
        return RedirectToAction(nameof(HoSo));
    }
}
