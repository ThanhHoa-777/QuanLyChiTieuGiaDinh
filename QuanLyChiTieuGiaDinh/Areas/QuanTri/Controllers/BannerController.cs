using Microsoft.AspNetCore.Mvc;
using Shopson.DichVu;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>
/// CHỌN BANNER TRANG CHỦ (tính năng mới).
/// Tải ảnh banner lên, bật/tắt banner nào đang hiển thị, chỉ dùng 1 banner, đổi thứ tự, gắn đường dẫn khi bấm vào banner, xóa.
/// Có từ 2 banner đang dùng trở lên thì trang chủ tự chạy trình chiếu.
/// </summary>
public class BannerController : QuanTriController
{
    private readonly DichVuBanner _banner;

    public BannerController(DichVuBanner banner)
    {
        _banner = banner;
    }

    public IActionResult Index() => View(_banner.LayTatCa());

    [HttpPost]
    public async Task<IActionResult> TaiLen(List<IFormFile>? anh)
    {
        int ok = 0;
        foreach (var f in anh ?? new List<IFormFile>())
        {
            if (await _banner.TaiLenAsync(f) != null) ok++;
        }
        if (ok > 0) BaoThanhCong($"✅ Đã tải lên {ok} banner.");
        else BaoLoi("Không có ảnh hợp lệ (chỉ nhận jpg, png, gif, webp, tối đa 8MB mỗi ảnh)!");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult BatTat(string tenFile, bool dangDung)
    {
        _banner.BatTat(tenFile, dangDung);
        BaoThanhCong(dangDung ? "Đã bật banner." : "Đã tắt banner (luôn giữ lại ít nhất 1 banner đang dùng).");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ChiDungMot(string tenFile)
    {
        _banner.ChiDungMotBanner(tenFile);
        BaoThanhCong("✅ Trang chủ sẽ chỉ hiển thị banner này.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult LienKet(string tenFile, string? lienKet)
    {
        _banner.DatLienKet(tenFile, lienKet);
        BaoThanhCong("Đã lưu đường dẫn của banner.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult DoiThuTu(string tenFile, int huong)
    {
        _banner.DoiThuTu(tenFile, huong);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Xoa(string tenFile)
    {
        if (_banner.Xoa(tenFile)) BaoThanhCong("Đã xóa banner.");
        else BaoLoi("Phải giữ lại ít nhất 1 banner!");
        return RedirectToAction(nameof(Index));
    }
}
