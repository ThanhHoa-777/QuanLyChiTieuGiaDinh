using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class LichSuChuyenChuHo
    {
        [Key]
        public int MaLichSu { get; set; }

        [Required]
        public int MaGiaDinh { get; set; }

        [Required]
        public int MaChuHoCu { get; set; }

        [Required]
        public int MaChuHoMoi { get; set; }

        public DateTime ThoiGianChuyenGiao { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey(nameof(MaChuHoCu))]
        public virtual NguoiDung? ChuHoCu { get; set; }

        [ForeignKey(nameof(MaChuHoMoi))]
        public virtual NguoiDung? ChuHoMoi { get; set; }
    }
}