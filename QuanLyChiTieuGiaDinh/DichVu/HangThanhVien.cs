namespace Shopson.DichVu;

/// <summary>
/// HỆ THỐNG TÍCH ĐIỂM & HẠNG KHÁCH HÀNG (chuyển từ ticdiem_ham.php).
/// Công thức: cứ 10.000đ = 10 điểm.
/// Hạng Thường &lt; 2.500 | Bạc ≥ 2.500 | Vàng ≥ 5.000 | Kim Cương ≥ 10.000
/// </summary>
public static class HangThanhVien
{
    public const string Thuong = "THUONG";
    public const string Bac = "BAC";
    public const string Vang = "VANG";
    public const string KimCuong = "KIM_CUONG";

    private static readonly string[] ThuTu = { Thuong, Bac, Vang, KimCuong };

    /// <summary>Tính điểm từ tổng tiền sản phẩm: mỗi 10.000đ được 10 điểm.</summary>
    public static int TinhDiem(decimal tongTienSanPham)
    {
        return (int)(Math.Floor(tongTienSanPham / 10000) * 10);
    }

    /// <summary>Xếp hạng theo tổng điểm.</summary>
    public static string XepHang(int tongDiem)
    {
        if (tongDiem >= 10000) return KimCuong;
        if (tongDiem >= 5000) return Vang;
        if (tongDiem >= 2500) return Bac;
        return Thuong;
    }

    public static string TenHang(string? hang) => hang switch
    {
        Bac => "Bạc",
        Vang => "Vàng",
        KimCuong => "Kim Cương",
        _ => "Thường"
    };

    public static string IconHang(string? hang) => hang switch
    {
        Bac => "🥈",
        Vang => "🥇",
        KimCuong => "💎",
        _ => "⭐"
    };

    public static string MauHang(string? hang) => hang switch
    {
        Thuong => "#d0cbcb",
        Bac => "#8d8686",
        Vang => "#F9A825",
        KimCuong => "#29B6F6",
        _ => "#888888"
    };

    /// <summary>Số điểm còn thiếu để lên hạng kế tiếp (0 nếu đã cao nhất).</summary>
    public static int DiemConThieu(int tongDiem)
    {
        if (tongDiem < 2500) return 2500 - tongDiem;
        if (tongDiem < 5000) return 5000 - tongDiem;
        if (tongDiem < 10000) return 10000 - tongDiem;
        return 0;
    }

    /// <summary>Mốc điểm cao nhất của hạng hiện tại (để vẽ thanh tiến trình).</summary>
    public static int DiemToiDaHang(int tongDiem)
    {
        if (tongDiem < 2500) return 2500;
        if (tongDiem < 5000) return 5000;
        return 10000;
    }

    /// <summary>Mốc điểm bắt đầu của hạng hiện tại.</summary>
    public static int DiemBatDauHang(int tongDiem)
    {
        if (tongDiem < 2500) return 0;
        if (tongDiem < 5000) return 2500;
        if (tongDiem < 10000) return 5000;
        return 10000;
    }

    /// <summary>% thanh tiến trình trong hạng hiện tại.</summary>
    public static int PhanTramTienTrinh(int tongDiem)
    {
        int batDau = DiemBatDauHang(tongDiem);
        int toiDa = DiemToiDaHang(tongDiem);
        if (toiDa <= batDau) return 100;
        return (int)Math.Min(100, Math.Round((double)(tongDiem - batDau) / (toiDa - batDau) * 100));
    }

    /// <summary>Thứ tự hạng: Thường=1, Bạc=2, Vàng=3, Kim Cương=4 (so sánh quyền dùng voucher).</summary>
    public static int ThuTuHang(string? hang)
    {
        int idx = Array.IndexOf(ThuTu, hang);
        return idx < 0 ? 1 : idx + 1;
    }

    /// <summary>Danh sách hạng voucher mà khách được dùng. Ví dụ VÀNG -> [THUONG, BAC, VANG].</summary>
    public static List<string> LayDanhSachHangDuocDung(string? hangHienTai)
    {
        int idx = Array.IndexOf(ThuTu, hangHienTai);
        if (idx < 0) idx = 0;
        return ThuTu.Take(idx + 1).ToList();
    }
}
