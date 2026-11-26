using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class ThanhVienGiaDinh
    {
        [Key]
        public int MaThanhVien { get; set; }

        [Required]
        public int MaGiaDinh { get; set; }

        [Required]
        public int MaNguoiDung { get; set; }

        // Vai trò trong hộ: "ChuHo", "ThanhVien"
        [Required]
        [StringLength(30)]
        [Display(Name = "Vai trò")]
        public string VaiTro { get; set; } = "ThanhVien";

        // Trạng thái tham gia: "ChoXacNhan", "DaXacNhan", "TuChoi"
        [Required]
        [StringLength(30)]
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "ChoXacNhan";

        [Display(Name = "Ngày tham gia")]
        public DateTime NgayThamGia { get; set; } = DateTime.Now;

        // Ngày chính thức rời hộ gia đình (nếu có)
        [Display(Name = "Ngày rời hộ")]
        public DateTime? NgayRoiHo { get; set; }

        // =====================================================
        // Navigation Properties
        // =====================================================

        // Quan hệ với hộ gia đình
        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        // Quan hệ với người dùng
        [ForeignKey(nameof(MaNguoiDung))]
        public virtual NguoiDung? NguoiDung { get; set; }
    }
}