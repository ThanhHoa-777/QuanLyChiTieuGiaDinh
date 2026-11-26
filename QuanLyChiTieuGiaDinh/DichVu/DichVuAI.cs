using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;

namespace Shopson.DichVu;

/// <summary>
/// TƯ VẤN LÀM ĐẸP BẰNG AI (chuyển từ ai_chat_xuly.php).
/// 1) Lấy dữ liệu THẬT từ DB: top bán chạy, đang flash sale, sản phẩm theo giá, sản phẩm khớp câu hỏi.
/// 2) Gửi dữ liệu đó + câu hỏi cho Google Gemini.
/// 3) Nếu Gemini lỗi / chưa có API key thì trả lời bằng các câu mẫu (fallback) dựa trên dữ liệu thật.
/// </summary>
public class DichVuAI
{
    private readonly ShopSonContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _cauHinh;
    private readonly DichVuFlashSale _flashSale;
    private readonly DichVuGia _gia;

    private static readonly string[] TuBoQua = { "son", "cho", "màu", "nào", "không", "có", "bao", "nhiêu", "shop", "cây", "loại", "sale", "flash" };

    public DichVuAI(ShopSonContext db, IHttpClientFactory httpClientFactory, IConfiguration cauHinh, DichVuFlashSale flashSale, DichVuGia gia)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _cauHinh = cauHinh;
        _flashSale = flashSale;
        _gia = gia;
    }

    /// <summary>Danh sách sản phẩm bán chạy hiển thị cho trang tư vấn AI (14 sản phẩm).</summary>
    public async Task<string> LayDanhSachSanPhamChoAIAsync()
    {
        var ds = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation)
            .Where(s => !s.NgungKinhDoanh && !s.MaThuongHieuNavigation.NgungKinhDoanh)
            .OrderByDescending(s => s.DaBan).Take(14).ToListAsync();
        return string.Join("; ", ds.Select(s => $"{s.TenSanPham} ({s.MaThuongHieuNavigation.TenThuongHieu}, {TienIch.DinhDangSo(s.GiaBan)}đ)"));
    }

    public async Task<string> TraLoiAsync(string cauHoi, string? loiNhacHeThong, List<(string Vai, string NoiDung)>? lichSu = null)
    {
        cauHoi = (cauHoi ?? "").Trim();
        if (cauHoi == "") return "Nàng ơi, nàng chưa nhập câu hỏi kìa! 😊";

        // ============ PHẦN 1: LẤY DỮ LIỆU THẬT TỪ DB ============

        // Top 3 bán chạy
        var topSanPham = await _db.SanPhams.AsNoTracking().Where(DichVuSanPham.DangBan).OrderByDescending(s => s.DaBan).Take(3).ToListAsync();
        string topStr = string.Join("\n", topSanPham.Select(s => $"• {s.TenSanPham} ({TienIch.DinhDangSo(s.GiaBan)}đ)"));

        // Sản phẩm đang flash sale hôm nay
        var dsFlash = await _flashSale.LayDanhSachAsync(TienIch.HomNay());
        var dsMaFlash = dsFlash.Keys.ToList();
        var spFlash = await _db.SanPhams.AsNoTracking().Where(DichVuSanPham.DangBan).Where(s => dsMaFlash.Contains(s.MaSanPham)).ToListAsync();
        var giaFlash = await _gia.TinhNhieuAsync(spFlash);
        var flashSb = new StringBuilder();
        foreach (var sp in spFlash.Take(4))
        {
            var g = giaFlash[sp.MaSanPham];
            flashSb.Append($"• {sp.TenSanPham} - Giá thường: {TienIch.DinhDangSo(g.GiaThuong)}đ ➡ 🔥 SALE CÒN: {TienIch.DinhDangSo(g.GiaCuoi)}đ\n");
        }
        string flashStr = flashSb.ToString();

        // Gợi ý theo giá nếu câu hỏi có số (vd "dưới 300k" -> 300.000đ)
        string goiYGia = "";
        var khopSo = Regex.Match(cauHoi.Replace(".", ""), @"\d+");
        if (khopSo.Success && long.TryParse(khopSo.Value, out long so))
        {
            long mucGia = so < 1000 ? so * 1000 : so;
            var dsGia = await _db.SanPhams.AsNoTracking().Where(s => !s.NgungKinhDoanh && !s.MaThuongHieuNavigation.NgungKinhDoanh).Where(s => s.GiaBan <= mucGia).OrderByDescending(s => s.GiaBan).Take(3).ToListAsync();
            if (dsGia.Count > 0)
            {
                goiYGia = $"💰 Trong tầm giá {TienIch.DinhDangSo(mucGia)}đ, ShopSon có sẵn các mẫu cực hot:\n" +
                          string.Concat(dsGia.Select(s => $"• {s.TenSanPham} - {TienIch.DinhDangSo(s.GiaBan)}đ\n"));
            }
        }

        // Tìm sản phẩm có tên khớp từ khóa trong câu hỏi
        string lienQuan = "";
        var tuKhoa = cauHoi.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !TuBoQua.Contains(t)).Distinct().ToList();
        if (tuKhoa.Count > 0)
        {
            var tatCa = await _db.SanPhams.AsNoTracking().Where(s => !s.NgungKinhDoanh && !s.MaThuongHieuNavigation.NgungKinhDoanh).ToListAsync();
            var khop = tatCa.Where(s => tuKhoa.Any(t => s.TenSanPham.ToLower().Contains(t))).Take(5).ToList();
            if (khop.Count > 0)
            {
                var sb = new StringBuilder("🔍 THÔNG TIN SẢN PHẨM KHÁCH ĐANG TÌM:\n");
                var giaKhop = await _gia.TinhNhieuAsync(khop);   // giá thật đang áp dụng (flash, ưu đãi chậm bán)
                foreach (var sp in khop)
                {
                    var g = giaKhop[sp.MaSanPham];
                    if (g.CoGiam)
                        sb.Append($"• {sp.TenSanPham} - Giá thường: {TienIch.DinhDangSo(g.GiaThuong)}đ (🔥 ĐANG GIẢM {g.PhanTramGiamTong}% CHỈ CÒN: {TienIch.DinhDangSo(g.GiaCuoi)}đ)\n");
                    else
                        sb.Append($"• {sp.TenSanPham} - Giá: {TienIch.DinhDangSo(g.GiaCuoi)}đ\n");
                }
                lienQuan = sb.ToString();
            }
        }

        // ============ PHẦN 2: GỌI GOOGLE GEMINI ============
        var heThong = new StringBuilder();
        // Lời nhắc hệ thống cố định ở máy chủ (không lấy từ trình duyệt, để người dùng không sửa được vai trò của AI)
        heThong.Append("Bạn là Beauty AI Advisor của ShopSon, chỉ tư vấn về son môi và mỹ phẩm của shop. " +
                       "Trả lời bằng tiếng Việt, thân thiện, có emoji, NGẮN GỌN: tối đa khoảng 150 từ, gợi ý 2-3 sản phẩm, " +
                       "mỗi sản phẩm 1-2 câu kèm giá. Không dùng bảng, không dùng tiêu đề #.");
        heThong.Append("\n\n=== DỮ LIỆU KHO HÀNG THỰC TẾ CỦA SHOPSON ===\n");
        heThong.Append("LỆNH TỐI CAO: Chỉ tư vấn dựa trên dữ liệu dưới đây. Nếu sản phẩm đang có Flash Sale, HÃY NHẤN MẠNH ĐIỀU NÀY để giục khách mua ngay.\n\n");
        if (lienQuan != "") heThong.Append(lienQuan + "\n");
        if (flashStr != "") heThong.Append("⚡ DANH SÁCH ĐANG FLASH SALE CỰC SỐC:\n" + flashStr + "\n");
        if (goiYGia != "") heThong.Append(goiYGia + "\n");
        heThong.Append("🏆 Danh sách Top bán chạy nhất:\n" + topStr);

        string? traLoiAI = await GoiGeminiAsync(cauHoi, heThong.ToString(), lichSu);
        if (!string.IsNullOrWhiteSpace(traLoiAI)) return traLoiAI.Trim();

        // ============ PHẦN 3: TRẢ LỜI DỰ PHÒNG (khi AI không dùng được) ============
        string q = cauHoi.ToLower();
        if (Regex.IsMatch(q, "(flash|sale|khuyến mãi|giảm giá|rẻ nhất)"))
        {
            if (flashStr != "")
                return "⚡ THÔNG BÁO KHẨN! ShopSon đang có chương trình Flash Sale cực sốc với các siêu phẩm sau:\n\n" + flashStr + "\nNàng nhanh tay chốt đơn kẻo hết hàng nhé! 🏃‍♀️💨";
            return "Hiện tại chương trình Flash Sale vừa kết thúc ạ 😭. Nhưng nàng xem thử các mẫu đang bán chạy nhất shop bù lại nha:\n" + topStr;
        }
        if (Regex.IsMatch(q, "(đẹp nhất|hot nhất|bán chạy|mua nhiều|tốt nhất|đẹp|biển)"))
            return "✨ Đây là Top 3 'siêu phẩm' đang làm mưa làm gió tại ShopSon dựa trên lượt mua:\n" + topStr + "\nĐảm bảo nàng dùng là xinh lung linh luôn! 😍";
        if (goiYGia != "" && Regex.IsMatch(q, "(dưới|khoảng|tầm|giá)"))
            return goiYGia + "Nàng có ưng mẫu nào trong số này không nè? 😊";
        if (Regex.IsMatch(q, "(da ngăm|da tối|da đen)"))
            return "💄 Nàng da ngăm đừng tự ti nhé, hãy chọn ngay các tông **Đỏ Cam, Đỏ Đất hoặc Cam Cháy**. Những màu này sẽ giúp gương mặt bừng sáng và răng trắng hơn hẳn đó!";
        if (Regex.IsMatch(q, "(da trắng|da sáng|trắng)"))
            return "🌸 Da trắng là chân ái của mọi màu son! Nhưng để nổi bật nhất, nàng hãy thử các tông **Hồng Đào, Cam San Hô hoặc Đỏ Tươi** nha. Trông nàng sẽ như thiên thần luôn!";
        if (Regex.IsMatch(q, "(đi tiệc|đám cưới|lâu trôi|lì|bền màu|ăn uống)"))
            return "💋 Đi tiệc thì phải dùng **Son Kem Lì** nàng ơi! Loại này bám cực tốt, ăn uống thả ga cũng không sợ trôi hết đâu ạ. Nàng xem mấy dòng Matte của 3CE hay MAC nha!";

        if (lienQuan != "")
            return "Dạ ShopSon gửi nàng thông tin sản phẩm nàng đang tìm ạ:\n\n" + lienQuan + "\nNàng chốt đơn luôn trên web để giữ hàng nha! 😊";
        return "Cảm ơn nàng đã nhắn tin cho ShopSon! Nàng có thể hỏi về: Flash sale đang diễn ra, son dưới 300k, hoặc tìm tên một hãng son cụ thể nhé! 😊💄";
    }

    /// <summary>
    /// Cấu hình sinh câu trả lời. Dòng gemini-2.5 có chế độ "suy nghĩ" (thinking): số token suy nghĩ bị tính chung
    /// vào maxOutputTokens nên câu trả lời dễ bị cắt giữa chừng. Với bản flash, tắt suy nghĩ (thinkingBudget = 0)
    /// để toàn bộ giới hạn dành cho câu trả lời; bản pro không tắt được nên chỉ nâng giới hạn.
    /// </summary>
    private static object TaoCauHinhSinh(string model)
    {
        if (model.Contains("flash"))
            return new { maxOutputTokens = 4096, thinkingConfig = new { thinkingBudget = 0 } };
        return new { maxOutputTokens = 8192 };
    }

    /// <summary>Gọi API Google Gemini. Trả về null nếu chưa có key hoặc bị lỗi.</summary>
    private async Task<string?> GoiGeminiAsync(string cauHoi, string heThong, List<(string Vai, string NoiDung)>? lichSu)
    {
        string apiKey = _cauHinh["Gemini:ApiKey"] ?? "";
        string model = _cauHinh["Gemini:Model"] ?? "gemini-2.5-flash";
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            // Lịch sử hội thoại (tối đa 10 lượt gần nhất) + câu hỏi hiện tại
            var cacLuot = new List<object>();
            foreach (var luot in (lichSu ?? new()).TakeLast(10))
            {
                string vai = luot.Vai == "model" ? "model" : "user";
                cacLuot.Add(new { role = vai, parts = new[] { new { text = luot.NoiDung } } });
            }
            cacLuot.Add(new { role = "user", parts = new[] { new { text = cauHoi } } });

            var noiDung = new
            {
                contents = cacLuot,
                systemInstruction = new { parts = new[] { new { text = heThong } } },
                generationConfig = TaoCauHinhSinh(model)
            };
            var yeuCau = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(noiDung), Encoding.UTF8, "application/json")
            };
            yeuCau.Headers.Add("x-goog-api-key", apiKey); // gửi key qua header, không để lộ trong URL

            var phanHoi = await client.SendAsync(yeuCau);
            if (!phanHoi.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await phanHoi.Content.ReadAsStringAsync());
            var ungVien = doc.RootElement.GetProperty("candidates");
            if (ungVien.GetArrayLength() == 0) return null;

            // Ghép TẤT CẢ các phần chữ của câu trả lời (Gemini có thể chia làm nhiều phần), bỏ phần "suy nghĩ" nội bộ
            var traLoi = new StringBuilder();
            if (ungVien[0].TryGetProperty("content", out var noiDungTraVe) && noiDungTraVe.TryGetProperty("parts", out var cacPhan))
            {
                foreach (var phan in cacPhan.EnumerateArray())
                {
                    bool laSuyNghi = phan.TryGetProperty("thought", out var tg) && tg.ValueKind == JsonValueKind.True;
                    if (!laSuyNghi && phan.TryGetProperty("text", out var chu)) traLoi.Append(chu.GetString());
                }
            }
            if (traLoi.Length == 0) return null;

            // Hết giới hạn độ dài -> báo cho khách biết câu trả lời bị cắt
            if (ungVien[0].TryGetProperty("finishReason", out var lyDo) && lyDo.GetString() == "MAX_TOKENS")
                traLoi.Append("…\n\n(Câu trả lời hơi dài, nàng hỏi cụ thể hơn để mình tư vấn kỹ nha 💕)");
            return traLoi.ToString();
        }
        catch
        {
            return null; // lỗi mạng, hết hạn mức... -> dùng câu trả lời dự phòng
        }
    }
}