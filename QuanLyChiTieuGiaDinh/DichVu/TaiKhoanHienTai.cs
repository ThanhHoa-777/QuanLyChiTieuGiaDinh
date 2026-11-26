using System.Security.Claims;

namespace Shopson.DichVu;

/// <summary>Đọc thông tin người đăng nhập từ cookie (thay cho $_SESSION['MaND'], ['HoTen'], ['QuyenHan'] của PHP).</summary>
public static class TaiKhoanHienTai
{
    public static bool DaDangNhap(this ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    /// <summary>Mã người dùng (0 nếu chưa đăng nhập).</summary>
    public static int MaNguoiDung(this ClaimsPrincipal user)
    {
        var giaTri = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(giaTri, out var ma) ? ma : 0;
    }

    public static string HoTen(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name) ?? "";

    public static bool LaQuanTri(this ClaimsPrincipal user) => user.IsInRole("QuanTri");
}
