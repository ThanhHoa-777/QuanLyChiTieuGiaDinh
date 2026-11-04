using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class YeuCauHoanTien
    {
        [Key]
        public int MaYeuCau { get; set; }

        [Required]
        public int MaTaiKhoan { get; set; } // Liên kết tới quỹ chung

        [Required]
        public int MaNguoiYeuCau { get; set; } // Thành viên yêu cầu hoàn tiền

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal SoTien { get; set; }

        [StringLength(500)]
        public string? LyDo { get; set; }

        [Required]
        public string TrangThai { get; set; } = "ChoDuyet"; // Các trạng thái: ChoDuyet, DaDuyet, TuChoi

        public DateTime NgayYeuCau { get; set; } = DateTime.Now;

        public DateTime? NgayXuLy { get; set; } // Ngày Chủ hộ bấm Duyệt hoặc Từ chối

        // Navigation Properties
        [ForeignKey("MaTaiKhoan")]
        public virtual TaiKhoanTien? TaiKhoanTien { get; set; }

        [ForeignKey("MaNguoiYeuCau")]
        public virtual NguoiDung? NguoiYeuCau { get; set; }
    }
}