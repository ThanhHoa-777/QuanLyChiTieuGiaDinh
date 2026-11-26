namespace Shopson.DichVu;

/// <summary>
/// Lưu ảnh người dùng tải lên (ảnh tin nhắn, ảnh khiếu nại) vào wwwroot/hinhanh/{thuMucCon}.
/// Chỉ nhận jpg, jpeg, png, gif, webp và tối đa 5MB.
/// </summary>
public class DichVuAnh
{
    private static readonly string[] DuoiAnhHopLe = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    public const string ThongBaoAnhSai = "Ảnh không hợp lệ (chỉ nhận jpg, png, gif, webp tối đa 5MB)!";

    private readonly IWebHostEnvironment _moiTruong;

    public DichVuAnh(IWebHostEnvironment moiTruong)
    {
        _moiTruong = moiTruong;
    }

    /// <summary>Có chọn file không (để phân biệt "không gửi ảnh" với "gửi ảnh sai").</summary>
    public static bool CoChonAnh(IFormFile? anh) => anh != null && anh.Length > 0;

    /// <summary>Lưu ảnh và trả về đường dẫn dạng "hinhanh/tinnhan/abc.jpg". Ảnh sai định dạng hoặc quá lớn thì trả về null.</summary>
    public async Task<string?> LuuAnhAsync(IFormFile? anh, string thuMucCon)
    {
        if (!CoChonAnh(anh)) return null;
        string duoi = Path.GetExtension(anh!.FileName).ToLower();
        if (!DuoiAnhHopLe.Contains(duoi) || anh.Length > 5 * 1024 * 1024) return null;

        string ten = $"{thuMucCon}_{DateTimeOffset.Now.ToUnixTimeSeconds()}_{Random.Shared.Next(1000, 9999)}{duoi}";
        string thuMuc = Path.Combine(_moiTruong.WebRootPath, "hinhanh", thuMucCon);
        Directory.CreateDirectory(thuMuc);
        using (var luong = new FileStream(Path.Combine(thuMuc, ten), FileMode.Create))
        {
            await anh.CopyToAsync(luong);
        }
        return $"hinhanh/{thuMucCon}/{ten}";
    }
}
