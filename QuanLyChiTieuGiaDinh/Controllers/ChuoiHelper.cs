using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyChiTieuGiaDinh.Helpers
{
    /// <summary>
    /// Hàm xử lý chuỗi dùng chung.
    /// </summary>
    public static class ChuoiHelper
    {
        // BR34: bỏ dấu, chữ thường, gộp khoảng trắng để so trùng tên
        public static string LamSach(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            string result = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(result, @"\s+", " ");
        }
    }
}
