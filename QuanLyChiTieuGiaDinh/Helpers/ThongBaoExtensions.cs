using System.Text.Json;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Helpers
{
    public static class ThongBaoExtensions
    {
        /// <summary>Đọc MaMucTieu từ DuLieuLienKet nếu là JSON; ngược lại null (vd YeuCauThoatHo lưu id thường).</summary>
        public static int? LayMaMucTieu(this ThongBao tb)
        {
            var s = tb.DuLieuLienKet;
            if (string.IsNullOrWhiteSpace(s) || !s.TrimStart().StartsWith("{")) return null;
            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.TryGetProperty("MaMucTieu", out var v) && v.TryGetInt32(out var id))
                    return id;
            }
            catch (JsonException) { }
            return null;
        }
    }
}