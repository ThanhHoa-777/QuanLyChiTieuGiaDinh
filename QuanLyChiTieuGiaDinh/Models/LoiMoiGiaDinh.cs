using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class LoiMoiGiaDinh
    {
        [Key]
        public int MaLoiMoi { get; set; }

        [Required]
        public int MaGiaDinh { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string EmailNguoiNhan { get; set; } = string.Empty;

        [Required]
        public int MaNguoiGui { get; set; }

        // Trạng thái lời mời: "ChoXacNhan", "DaXacNhan", "TuChoi", "HetHan"
        [Required]
        [StringLength(30)]
        public string TrangThai { get; set; } = "ChoXacNhan";

        public DateTime NgayGui { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey(nameof(MaNguoiGui))]
        public virtual NguoiDung? NguoiGui { get; set; }
    }
}