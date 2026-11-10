using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class GeminiOcrService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiOcrService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiApi:ApiKey"] ?? string.Empty;
            _model = configuration["GeminiApi:Model"] ?? "gemini-1.5-flash";
        }

        public async Task<GeminiOcrResult?> DocHoaDonAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // 1. Chuyển file ảnh sang dạng Base64
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] imageBytes = ms.ToArray();
            string base64Image = Convert.ToBase64String(imageBytes);

            // 2. Chuẩn bị Request Payload cho Gemini REST API
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Hãy đọc hóa đơn này và trích xuất thông tin dưới dạng JSON chuẩn. Dữ liệu bao gồm các trường: soTien (số nguyên, tổng tiền thanh toán), ngayGiaoDich (định dạng YYYY-MM-DD), ghiChu (tên cửa hàng/đơn vị hoặc danh sách sản phẩm ngắn gọn)." },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = file.ContentType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    response_schema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            soTien = new { type = "NUMBER" },
                            ngayGiaoDich = new { type = "STRING" },
                            ghiChu = new { type = "STRING" }
                        },
                        required = new[] { "soTien", "ngayGiaoDich", "ghiChu" }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            string requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var response = await _httpClient.PostAsync(requestUri, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error: {response.StatusCode} - {errorResponse}");
            }

            // 3. Đọc và Parse kết quả JSON trả về từ Gemini
            string jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            string structuredJsonText = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            var ocrResult = JsonSerializer.Deserialize<GeminiOcrResult>(structuredJsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return ocrResult;
        }
    }

    public class GeminiOcrResult
    {
        [JsonPropertyName("soTien")]
        public decimal SoTien { get; set; }

        [JsonPropertyName("ngayGiaoDich")]
        public string NgayGiaoDich { get; set; } = string.Empty;

        [JsonPropertyName("ghiChu")]
        public string GhiChu { get; set; } = string.Empty;
    }
}