using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class YeuCauRoiHo
    {
        [Key]
        public int MaYeuCau { get; set; }

        [Required]
        public int MaGiaDinh { get; set; }

        [Required]
        public int MaNguoiDung { get; set; }

        public DateTime ThoiDiemGui { get; set; } = DateTime.Now;

        // Trạng thái: "ChoQuyetToan", "ChoDuyet", "DongY", "TuChoi"
        [Required]
        [StringLength(30)]
        public string TrangThai { get; set; } = "ChoDuyet";

        public int? NguoiDuyet { get; set; }

        public DateTime? ThoiDiemXuLy { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey(nameof(MaNguoiDung))]
        public virtual NguoiDung? NguoiDung { get; set; }

        [ForeignKey(nameof(NguoiDuyet))]
        public virtual NguoiDung? NguoiDuyetGiaDinh { get; set; }
    }
}