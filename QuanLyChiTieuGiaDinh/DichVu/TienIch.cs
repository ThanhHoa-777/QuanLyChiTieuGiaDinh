using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Shopson.DichVu;

/// <summary>Các hàm tiện ích nhỏ dùng khắp nơi (định dạng tiền, cắt chuỗi, MD5...).</summary>
public static class TienIch
{
    /// <summary>
    /// 1234567 -> "1.234.567" (dấu chấm ngăn cách hàng nghìn kiểu Việt Nam).
    /// Tự đổi dấu thay vì dựa vào culture của máy, để máy nào chạy cũng ra cùng một kết quả.
    /// </summary>
    public static string DinhDangSo(decimal so)
    {
        return Math.Round(so, 0).ToString("#,##0", CultureInfo.InvariantCulture).Replace(",", ".");
    }

    /// <summary>1234567 -> "1.234.567đ" (giống DinhDangTien của PHP).</summary>
    public static string DinhDangTien(decimal so)
    {
        return DinhDangSo(so) + "đ";
    }

    /// <summary>Cắt chuỗi dài thêm "..." (giống CatChuoi của PHP).</summary>
    public static string CatChuoi(string? chuoi, int soKy = 80)
    {
        if (string.IsNullOrEmpty(chuoi)) return "";
        return chuoi.Length <= soKy ? chuoi : chuoi.Substring(0, soKy) + "...";
    }

    /// <summary>Mật khẩu được lưu dạng MD5 (giữ nguyên như bản PHP để đăng nhập được với dữ liệu cũ).</summary>
    public static string MaHoaMd5(string matKhau)
    {
        var byteGoc = Encoding.UTF8.GetBytes(matKhau);
        var bam = MD5.HashData(byteGoc);
        var sb = new StringBuilder();
        foreach (var b in bam) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>Đường dẫn ảnh sản phẩm: DB lưu "hinhanh/xxx.jpg" hoặc link http đầy đủ.</summary>
    public static string DuongDanAnh(string? hinhAnh)
    {
        if (string.IsNullOrWhiteSpace(hinhAnh)) return "";
        if (hinhAnh.StartsWith("http://") || hinhAnh.StartsWith("https://") || hinhAnh.StartsWith("/")) return hinhAnh;
        return "/" + hinhAnh;
    }

    /// <summary>Đổi "2026-10-30" (dạng DateOnly) sang chuỗi ngày/tháng/năm.</summary>
    public static string NgayThangNam(DateTime? ngay) => ngay.HasValue ? ngay.Value.ToString("dd/MM/yyyy") : "";
    public static string NgayGio(DateTime? ngay) => ngay.HasValue ? ngay.Value.ToString("dd/MM/yyyy HH:mm") : "";

    /// <summary>Số giây còn lại đến hết ngày hôm nay (23:59:59) - dùng cho đồng hồ đếm ngược flash sale.</summary>
    public static int GiayConLaiTrongNgay()
    {
        var bay_gio = DateTime.Now;
        var hetNgay = bay_gio.Date.AddDays(1).AddSeconds(-1);
        return Math.Max(0, (int)(hetNgay - bay_gio).TotalSeconds);
    }

    public static DateOnly HomNay() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Đổi 3661 giây thành "01", "01", "01" (giờ, phút, giây) cho đồng hồ đếm ngược.</summary>
    public static (string Gio, string Phut, string Giay) TachGioPhutGiay(int tongGiay)
    {
        return ((tongGiay / 3600).ToString("00"), ((tongGiay % 3600) / 60).ToString("00"), (tongGiay % 60).ToString("00"));
    }

    // ---------------------------------------------------------------------
    // ĐỌC SỐ TIỀN THÀNH CHỮ (dùng cho hóa đơn). Ví dụ 1234567 -> "Một triệu hai trăm ba mươi bốn nghìn năm trăm sáu mươi bảy đồng chẵn."
    // ---------------------------------------------------------------------
    private static readonly string[] ChuSo = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    // Đọc một nhóm 3 chữ số (0-999). dayDu = true khi phía trước đã có nhóm lớn hơn nên phải đọc cả "không trăm", "lẻ"
    private static string DocBaSo(int n, bool dayDu)
    {
        int tram = n / 100, chuc = (n % 100) / 10, donVi = n % 10;
        var kq = new List<string>();
        if (tram > 0 || dayDu) kq.Add(ChuSo[tram] + " trăm");
        if (chuc > 1) kq.Add(ChuSo[chuc] + " mươi");
        else if (chuc == 1) kq.Add("mười");
        else if (donVi > 0 && (tram > 0 || dayDu)) kq.Add("lẻ");
        if (donVi > 0)
        {
            if (donVi == 1 && chuc > 1) kq.Add("mốt");          // 21 -> hai mươi mốt
            else if (donVi == 5 && chuc > 0) kq.Add("lăm");     // 15 -> mười lăm, 25 -> hai mươi lăm
            else kq.Add(ChuSo[donVi]);
        }
        return string.Join(" ", kq);
    }

    public static string DocSoThanhChu(decimal soTien)
    {
        long so = (long)Math.Round(Math.Abs(soTien));
        if (so == 0) return "Không đồng.";
        var nhom = new List<int>();                  // tách thành các nhóm 3 chữ số, từ nhỏ đến lớn
        for (long t = so; t > 0; t /= 1000) nhom.Add((int)(t % 1000));
        if (nhom.Count > 4) return DinhDangTien(soTien);   // lớn hơn 999 tỷ: không đọc, in số
        string[] hau = { "", " nghìn", " triệu", " tỷ" };
        var cacPhan = new List<string>();
        for (int i = nhom.Count - 1; i >= 0; i--)
        {
            if (nhom[i] == 0) continue;
            cacPhan.Add(DocBaSo(nhom[i], cacPhan.Count > 0) + hau[i]);
        }
        string chu = string.Join(" ", cacPhan) + " đồng chẵn.";
        return char.ToUpper(chu[0]) + chu.Substring(1);
    }
}
