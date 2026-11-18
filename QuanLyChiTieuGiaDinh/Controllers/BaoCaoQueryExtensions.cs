using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Helpers
{
    /// <summary>
    /// ĐIỀU KIỆN LỌC DUY NHẤT cho mọi số liệu thu/chi thực tế (Báo cáo tháng, so sánh, xuất file, Dashboard...).
    /// Mọi nơi tính tổng thu / tổng chi phải gọi <see cref="ThuChiThucTe"/> để số liệu luôn khớp nhau.
    /// </summary>
    public static class BaoCaoQueryExtensions
    {
        // Dấu hiệu giao dịch chuyển tiền nội bộ (cùng quy ước với GiaoDichesController)
        public const string DauNapHanMuc = "|NAP_HAN_MUC|";
        public const string DauChuyenTien = "|CHUYEN_TIEN|";
        public static readonly string[] TenDanhMucHeThong = { "Chuyển tiền cá nhân", "Chuyển tiền quỹ" };

        /// <summary>
        /// Chỉ giữ các giao dịch thu/chi thực tế:
        ///  - TrangThai == "HoatDong" (bỏ giao dịch đã xóa mềm)
        ///  - bỏ giao dịch nạp quỹ / chuyển tiền (đi theo cặp Chi + Thu giữa 2 ví, không phải thu chi thực) - cả hai chiều
        ///  - bỏ giao dịch đang chờ duyệt hoặc bị từ chối
        /// Chưa lọc theo hộ / người dùng / tháng: nơi gọi tự thêm.
        /// </summary>
        public static IQueryable<GiaoDich> ThuChiThucTe(this IQueryable<GiaoDich> query)
        {
            return query.Where(x =>
                x.TrangThai == "HoatDong" &&
                !(x.GhiChu != null && (x.GhiChu.Contains(DauNapHanMuc) || x.GhiChu.Contains(DauChuyenTien))) &&
                !(x.DanhMuc != null && TenDanhMucHeThong.Contains(x.DanhMuc.TenDanhMuc)) &&
                x.TrangThaiDuyet != "TuChoi" && x.TrangThaiDuyet != "ChoDuyet");
        }
    }
}
