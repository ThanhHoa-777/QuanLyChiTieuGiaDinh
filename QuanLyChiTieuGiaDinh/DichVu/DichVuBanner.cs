using System.Text.Json;

namespace Shopson.DichVu;

/// <summary>Thông tin một banner.</summary>
public class ThongTinBanner
{
    public string TenFile { get; set; } = "";
    public bool DangDung { get; set; }     // true = đang hiển thị trên trang chủ
    public string LienKet { get; set; } = ""; // bấm vào banner sẽ mở link này (có thể để trống)
    public int ThuTu { get; set; }
}

/// <summary>
/// QUẢN LÝ BANNER TRANG CHỦ (tính năng MỚI so với bản PHP).
/// - Admin tải ảnh banner lên, chọn banner nào đang dùng, đổi thứ tự, gắn link, xóa.
/// - Có nhiều banner đang dùng -> trang chủ tự chạy trình chiếu (đổi ảnh sau 4 giây).
/// - Ảnh lưu ở wwwroot/banner, danh sách cấu hình lưu ở wwwroot/banner/banner.json
///   (không cần thêm bảng vào CSDL nên không phải scaffold lại).
/// </summary>
public class DichVuBanner
{
    private static readonly object KhoaGhiFile = new();
    private static readonly string[] DuoiHopLe = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly string _thuMuc;
    private string DuongDanJson => Path.Combine(_thuMuc, "banner.json");

    public DichVuBanner(IWebHostEnvironment moiTruong)
    {
        _thuMuc = Path.Combine(moiTruong.WebRootPath, "banner");
        Directory.CreateDirectory(_thuMuc);
    }

    /// <summary>Danh sách tất cả banner (kèm cả ảnh mới chép vào thư mục mà chưa có trong cấu hình).</summary>
    public List<ThongTinBanner> LayTatCa()
    {
        List<ThongTinBanner> ds;
        lock (KhoaGhiFile)
        {
            ds = DocCauHinh();
            var cacFileThat = Directory.GetFiles(_thuMuc)
                .Where(f => DuoiHopLe.Contains(Path.GetExtension(f).ToLower()))
                .Select(f => Path.GetFileName(f)).ToList();

            // Bỏ cấu hình của file đã bị xóa mất
            ds = ds.Where(b => cacFileThat.Contains(b.TenFile)).ToList();

            // Thêm banner mới phát hiện trong thư mục
            foreach (var ten in cacFileThat)
            {
                if (!ds.Any(b => b.TenFile == ten))
                {
                    ds.Add(new ThongTinBanner { TenFile = ten, DangDung = ds.Count(b => b.DangDung) == 0, ThuTu = ds.Count + 1 });
                }
            }
            // Luôn phải có ít nhất 1 banner đang dùng
            if (ds.Count > 0 && !ds.Any(b => b.DangDung)) ds[0].DangDung = true;
        }
        return ds.OrderBy(b => b.ThuTu).ToList();
    }

    /// <summary>Banner đang hiển thị trên trang chủ.</summary>
    public List<ThongTinBanner> LayBannerDangDung() => LayTatCa().Where(b => b.DangDung).ToList();

    public void Luu(List<ThongTinBanner> ds)
    {
        lock (KhoaGhiFile)
        {
            File.WriteAllText(DuongDanJson, JsonSerializer.Serialize(ds, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>Lưu file ảnh banner mới. Trả về tên file, hoặc null nếu đuôi file không hợp lệ.</summary>
    public async Task<string?> TaiLenAsync(IFormFile file)
    {
        string duoi = Path.GetExtension(file.FileName).ToLower();
        if (!DuoiHopLe.Contains(duoi) || file.Length == 0 || file.Length > 8 * 1024 * 1024) return null;

        string tenMoi = $"banner_{DateTime.Now:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}{duoi}";
        using (var luong = new FileStream(Path.Combine(_thuMuc, tenMoi), FileMode.Create))
        {
            await file.CopyToAsync(luong);
        }
        var ds = LayTatCa();
        if (!ds.Any(b => b.TenFile == tenMoi)) ds.Add(new ThongTinBanner { TenFile = tenMoi, ThuTu = ds.Count + 1 });
        Luu(ds);
        return tenMoi;
    }

    public void BatTat(string tenFile, bool dangDung)
    {
        var ds = LayTatCa();
        var b = ds.FirstOrDefault(x => x.TenFile == tenFile);
        if (b == null) return;
        b.DangDung = dangDung;
        if (!ds.Any(x => x.DangDung)) b.DangDung = true; // không cho tắt hết
        Luu(ds);
    }

    /// <summary>Chỉ dùng đúng 1 banner này, tắt các banner khác.</summary>
    public void ChiDungMotBanner(string tenFile)
    {
        var ds = LayTatCa();
        foreach (var b in ds) b.DangDung = b.TenFile == tenFile;
        Luu(ds);
    }

    public void DatLienKet(string tenFile, string? lienKet)
    {
        var ds = LayTatCa();
        var b = ds.FirstOrDefault(x => x.TenFile == tenFile);
        if (b == null) return;
        b.LienKet = (lienKet ?? "").Trim();
        Luu(ds);
    }

    public void DoiThuTu(string tenFile, int huong) // -1 = lên, +1 = xuống
    {
        var ds = LayTatCa().OrderBy(b => b.ThuTu).ToList();
        int i = ds.FindIndex(b => b.TenFile == tenFile);
        int j = i + huong;
        if (i < 0 || j < 0 || j >= ds.Count) return;
        (ds[i], ds[j]) = (ds[j], ds[i]);
        for (int k = 0; k < ds.Count; k++) ds[k].ThuTu = k + 1;
        Luu(ds);
    }

    public bool Xoa(string tenFile)
    {
        var ds = LayTatCa();
        if (ds.Count <= 1) return false; // phải còn ít nhất 1 banner
        string duongDan = Path.Combine(_thuMuc, Path.GetFileName(tenFile));
        if (File.Exists(duongDan)) File.Delete(duongDan);
        ds = ds.Where(b => b.TenFile != tenFile).ToList();
        for (int k = 0; k < ds.Count; k++) ds[k].ThuTu = k + 1;
        if (!ds.Any(b => b.DangDung)) ds[0].DangDung = true;
        Luu(ds);
        return true;
    }

    private List<ThongTinBanner> DocCauHinh()
    {
        try
        {
            if (!File.Exists(DuongDanJson)) return new List<ThongTinBanner>();
            return JsonSerializer.Deserialize<List<ThongTinBanner>>(File.ReadAllText(DuongDanJson)) ?? new List<ThongTinBanner>();
        }
        catch
        {
            return new List<ThongTinBanner>();
        }
    }
}
