namespace QuanLyChiTieuGiaDinh.Models
{
    /// <summary>Một chỉ số so sánh giữa tháng báo cáo và tháng trước.</summary>
    public class ChiSoSoSanhViewModel
    {
        public decimal HienTai { get; set; }
        public decimal ThangTruoc { get; set; }

        /// <summary>Chênh lệch bằng tiền (tháng này - tháng trước). Dương = tăng, âm = giảm.</summary>
        public decimal ChenhLechTien => HienTai - ThangTruoc;

        /// <summary>Tháng trước bằng 0 thì không có cơ sở tính phần trăm (tránh chia cho 0).</summary>
        public bool CoTheTinhPhanTram => ThangTruoc != 0;

        /// <summary>Tỷ lệ tăng/giảm (%) so với tháng trước; null nếu không tính được.</summary>
        public decimal? PhanTram => CoTheTinhPhanTram
            ? Math.Round(ChenhLechTien / Math.Abs(ThangTruoc) * 100, 1)
            : (decimal?)null;
    }

    /// <summary>Số liệu so sánh tháng báo cáo với tháng liền trước (cùng điều kiện lọc).</summary>
    public class BaoCaoSoSanhViewModel
    {
        public int ThangTruoc { get; set; }
        public int NamTruoc { get; set; }
        public int SoGiaoDichThangTruoc { get; set; }

        public bool CoDuLieuThangTruoc => SoGiaoDichThangTruoc > 0;

        public ChiSoSoSanhViewModel Thu { get; set; } = new();
        public ChiSoSoSanhViewModel Chi { get; set; } = new();
        public ChiSoSoSanhViewModel ChenhLech { get; set; } = new();
    }
}
