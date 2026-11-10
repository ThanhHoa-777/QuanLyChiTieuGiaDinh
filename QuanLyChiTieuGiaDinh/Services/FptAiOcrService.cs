using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class FptAiOcrService
    {
        private readonly HttpClient _httpClient;

        public FptAiOcrService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Dictionary<string, string>> DocHoaDonAsync(IFormFile file)
        {
            var ketQua = new Dictionary<string, string>
            {
                ["SoTien"] = "0",
                ["NgayGiaoDich"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm")
            };

            if (file == null || file.Length == 0)
                return ketQua;

            using var form = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var streamContent = new StreamContent(stream);

            form.Add(streamContent, "file", file.FileName);
            form.Add(new StringContent("K88729864288957"), "apikey");
            form.Add(new StringContent("vie"), "language");
            form.Add(new StringContent("2"), "OCREngine"); // Engine 2 nhận diện tiếng Việt chính xác hơn

            var response = await _httpClient.PostAsync("https://api.ocr.space/parse/image", form);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Lỗi kết nối tới dịch vụ OCR.");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("ParsedResults", out var parsedResults) && parsedResults.GetArrayLength() > 0)
            {
                var parsedText = parsedResults[0].GetProperty("ParsedText").GetString();

                if (!string.IsNullOrWhiteSpace(parsedText))
                {
                    ketQua["SoTien"] = TrichXuatSoTien(parsedText);
                    ketQua["NgayGiaoDich"] = TrichXuatNgay(parsedText);
                }
            }

            return ketQua;
        }

        private string TrichXuatSoTien(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "0";

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] tuKhoa = new string[] { "tong cong", "tong tien", "thanh toan", "thanh tien", "total", "amount", "sum" };

            // 1. Ưu tiên lấy số tiền trên dòng chứa từ khóa Tổng cộng / Thanh toán
            foreach (var line in lines)
            {
                string lineClean = RemoveDiacritics(line.ToLower());
                if (tuKhoa.Any(tk => lineClean.Contains(tk)))
                {
                    var matches = Regex.Matches(line, @"\d+");
                    if (matches.Count > 0)
                    {
                        // Ghép các cụm số vụn lại (ví dụ "140" và "000" thành "140000")
                        string fullNum = string.Join("", matches.Cast<Match>().Select(m => m.Value));
                        if (long.TryParse(fullNum, out long val) && val >= 1000)
                        {
                            return val.ToString();
                        }
                    }
                }
            }

            // 2. Dự phòng: Tìm số lớn nhất hợp lệ trên toàn bộ hóa đơn
            long maxVal = 0;
            var allNumMatches = Regex.Matches(text, @"\b\d{1,3}(?:[\.,]\d{3})+\b|\b\d{4,8}\b");
            foreach (Match match in allNumMatches)
            {
                string numOnly = Regex.Replace(match.Value, @"[^\d]", "");
                if (long.TryParse(numOnly, out long val))
                {
                    if (val >= 1000 && val < 1000000000 && val > maxVal)
                    {
                        maxVal = val;
                    }
                }
            }

            return maxVal > 0 ? maxVal.ToString() : "0";
        }

        private string TrichXuatNgay(string text)
        {
            // Bóc tách định dạng Ngày/Tháng/Năm (dd/MM/yyyy)
            var dateMatch = Regex.Match(text, @"\b(\d{1,2})[/\.-](\d{1,2})[/\.-](\d{2,4})\b");
            if (dateMatch.Success)
            {
                int day = int.Parse(dateMatch.Groups[1].Value);
                int month = int.Parse(dateMatch.Groups[2].Value);
                int year = int.Parse(dateMatch.Groups[3].Value);

                if (year < 100) year += 2000;

                try
                {
                    // Ép cố định kiểu dd/MM/yyyy của Việt Nam
                    DateTime dt = new DateTime(year, month, day);
                    return dt.ToString("yyyy-MM-ddTHH:mm");
                }
                catch { }
            }

            return DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
        }

        private string RemoveDiacritics(string text)
        {
            string[] vietnameseSigns = new string[]
            {
                "aAeEoOuUiIdDyY", "áàảãạăắằẳẵặâấầẩẫậ", "ÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬ",
                "éèẻẽẹêếềểễệ", "ÉÈẺẼẸÊẾỀỂỄỆ", "óòỏõọôốồổỗộơớờởỡợ", "ÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢ",
                "úùủũụưứừửữự", "ÚÙỦŨỤƯỨỪỬỮỰ", "íìỉĩị", "ÍÌỈĨỊ", "đ", "Đ", "ýỳỷỹỵ", "ÝỲỶỸỴ"
            };

            for (int i = 1; i < vietnameseSigns.Length; i++)
            {
                for (int j = 0; j < vietnameseSigns[i].Length; j++)
                    text = text.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
            }
            return text;
        }
    }
}