using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class LichSuHanMuc
    {
        [Key]
        public int MaLichSu { get; set; }

        [Required]
        public int MaNganSach { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Hạn mức cũ")]
        public decimal HanMucCu { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Hạn mức mới")]
        public decimal HanMucMoi { get; set; }

        [Required]
        public int MaNguoiThayDoi { get; set; }

        [Display(Name = "Ngày thay đổi")]
        public DateTime NgayThayDoi { get; set; } = DateTime.Now;

        [StringLength(255, ErrorMessage = "Ghi chú không vượt quá 255 ký tự.")]
        [Display(Name = "Ghi chú")]
        public string? GhiChu { get; set; }

        // Quan hệ
        [ForeignKey("MaNganSach")]
        public virtual NganSach? NganSach { get; set; }

        [ForeignKey("MaNguoiThayDoi")]
        public virtual NguoiDung? NguoiThayDoi { get; set; }
    }
}