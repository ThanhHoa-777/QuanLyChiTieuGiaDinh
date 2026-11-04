using System.ComponentModel.DataAnnotations;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class NguoiDung
    {
        [Key]
        public int MaNguoiDung { get; set; }

        [Required]
        [StringLength(100)]
        public string HoTen { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? SoDienThoai { get; set; }

        [StringLength(500)]
        public string? AnhDaiDien { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.Now;

        // Quan hệ với hộ gia đình
        public virtual ICollection<ThanhVienGiaDinh> ThanhVienGiaDinhs { get; set; }
            = new List<ThanhVienGiaDinh>();

        public virtual ICollection<HoGiaDinh> HoGiaDinhs { get; set; }
            = new List<HoGiaDinh>();
    }
}