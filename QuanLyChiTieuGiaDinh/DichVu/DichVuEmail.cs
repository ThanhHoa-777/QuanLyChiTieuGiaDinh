using System.Net;
using System.Net.Mail;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// GỬI EMAIL THẬT qua Gmail (SMTP smtp.gmail.com:587) bằng thư viện có sẵn System.Net.Mail - không cài thêm gói.
/// Cấu hình trong appsettings.json mục "Email": DiaChiGui, MatKhauUngDung (mật khẩu ứng dụng 16 ký tự của Gmail đã bật xác minh 2 lớp), TenHienThi.
/// Chưa điền MatKhauUngDung thì không gửi (ghi nhật ký "Chưa cấu hình").
/// Mỗi lần gửi đều ghi 1 dòng NhatKyEmail (không lưu nội dung thư).
/// Đăng ký SINGLETON: tự mở scope riêng để ghi nhật ký, nên gửi ngầm (không chờ) được mà không đụng DbContext của trang.
/// </summary>
public class DichVuEmail
{
    private readonly IConfiguration _cauHinh;
    private readonly IServiceScopeFactory _taoPhamVi;
    private readonly ILogger<DichVuEmail> _nhatKy;

    public DichVuEmail(IConfiguration cauHinh, IServiceScopeFactory taoPhamVi, ILogger<DichVuEmail> nhatKy)
    {
        _cauHinh = cauHinh;
        _taoPhamVi = taoPhamVi;
        _nhatKy = nhatKy;
    }

    /// <summary>Email có đúng định dạng không (rỗng = không hợp lệ).</summary>
    public static bool EmailHopLe(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { return new MailAddress(email.Trim()).Address == email.Trim(); }
        catch { return false; }
    }

    /// <summary>Che bớt email để hiện trên màn hình: lan@gmail.com -> l***@gmail.com</summary>
    public static string CheEmail(string email)
    {
        int viTri = email.IndexOf('@');
        if (viTri <= 0) return "***";
        return email[0] + "***" + email[viTri..];
    }

    /// <summary>
    /// Gửi 1 email và CHỜ kết quả (dùng cho mã khôi phục mật khẩu: cần biết gửi được hay không).
    /// Trả về (thành công, câu lỗi).
    /// </summary>
    public async Task<(bool ThanhCong, string Loi)> GuiAsync(string emailNhan, string tieuDe, string noiDungHtml,
        string loai, int? maDonHang = null, int? maNguoiDung = null)
    {
        string diaChiGui = _cauHinh["Email:DiaChiGui"] ?? "";
        string matKhau = (_cauHinh["Email:MatKhauUngDung"] ?? "").Replace(" ", "");
        string tenHienThi = _cauHinh["Email:TenHienThi"] ?? "ShopSon";

        bool thanhCong = false;
        string loi = "";
        if (!EmailHopLe(emailNhan)) loi = "Email người nhận không hợp lệ";
        else if (diaChiGui == "" || matKhau == "") loi = "Chưa cấu hình email gửi (appsettings.json → Email)";
        else
        {
            try
            {
                using var thu = new MailMessage
                {
                    From = new MailAddress(diaChiGui, tenHienThi),
                    Subject = tieuDe,
                    Body = noiDungHtml,
                    IsBodyHtml = true
                };
                thu.To.Add(emailNhan.Trim());
                using var mayChu = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(diaChiGui, matKhau)
                };
                await mayChu.SendMailAsync(thu);
                thanhCong = true;
            }
            catch (Exception ex)
            {
                loi = ex.Message;
                _nhatKy.LogWarning(ex, "Gửi email thất bại tới {Email}", emailNhan);
            }
        }

        await GhiNhatKyAsync(emailNhan, tieuDe, loai, maDonHang, maNguoiDung, thanhCong, loi);
        return (thanhCong, loi);
    }

    /// <summary>
    /// Gửi NGẦM (không chờ): dùng cho thông báo đơn hàng để trang không bị chậm 1-3 giây.
    /// Lỗi gửi chỉ ghi vào nhật ký, không làm hỏng việc đặt / xử lý đơn.
    /// </summary>
    public void GuiNgam(string emailNhan, string tieuDe, string noiDungHtml, string loai, int? maDonHang = null, int? maNguoiDung = null)
    {
        if (!EmailHopLe(emailNhan)) return;   // khách không nhập email thì thôi, không ghi nhật ký
        _ = Task.Run(async () =>
        {
            try { await GuiAsync(emailNhan, tieuDe, noiDungHtml, loai, maDonHang, maNguoiDung); }
            catch (Exception ex) { _nhatKy.LogWarning(ex, "Lỗi gửi email ngầm"); }
        });
    }

    private async Task GhiNhatKyAsync(string emailNhan, string tieuDe, string loai, int? maDonHang, int? maNguoiDung, bool thanhCong, string loi)
    {
        try
        {
            using var phamVi = _taoPhamVi.CreateScope();
            var db = phamVi.ServiceProvider.GetRequiredService<ShopSonContext>();
            db.NhatKyEmails.Add(new NhatKyEmail
            {
                EmailNhan = Cat(emailNhan, 150),
                TieuDe = Cat(tieuDe, 200),
                LoaiEmail = loai,
                MaDonHang = maDonHang,
                MaNguoiDung = maNguoiDung,
                ThanhCong = thanhCong,
                Loi = Cat(loi, 500),
                NgayGui = DateTime.Now
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _nhatKy.LogWarning(ex, "Không ghi được nhật ký email"); }
    }

    private static string Cat(string? chuoi, int soKy) =>
        string.IsNullOrEmpty(chuoi) ? "" : (chuoi.Length <= soKy ? chuoi : chuoi.Substring(0, soKy));

    /// <summary>Khung HTML chung cho mọi email (đầu thư hồng ShopSon + nội dung + chân thư).</summary>
    public static string KhungThu(string tieuDe, string noiDungHtml) => $@"
<div style=""font-family:Arial,sans-serif;max-width:600px;margin:auto;border:1px solid #f8d7da;border-radius:12px;overflow:hidden;"">
  <div style=""background:#d63384;color:#fff;padding:16px 20px;font-size:20px;font-weight:bold;"">💄 ShopSon</div>
  <div style=""padding:20px;color:#222;font-size:14px;line-height:1.6;"">
    <h3 style=""margin-top:0;color:#d63384;"">{WebUtility.HtmlEncode(tieuDe)}</h3>
    {noiDungHtml}
  </div>
  <div style=""background:#fdf6f8;padding:12px 20px;font-size:12px;color:#888;"">Email tự động từ ShopSon, vui lòng không trả lời thư này. Cần hỗ trợ, bạn nhắn tin cho shop trên website.</div>
</div>";

    /// <summary>Đổi văn bản thường (có xuống dòng) thành HTML an toàn.</summary>
    public static string VanBanSangHtml(string vanBan) =>
        "<p>" + WebUtility.HtmlEncode(vanBan).Replace("\n", "<br>") + "</p>";
}
