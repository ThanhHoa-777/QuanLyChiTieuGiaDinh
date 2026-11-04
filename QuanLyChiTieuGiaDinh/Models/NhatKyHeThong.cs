using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum LoaiThaoTac
    {
        [Display(Name = "Tạo mới")]
        TaoMoi = 1,

        [Display(Name = "Chỉnh sửa")]
        ChinhSua = 2,

        [Display(Name = "Xóa")]
        Xoa = 3,

        [Display(Name = "Phê duyệt")]
        PheDuyet = 4,

        [Display(Name = "Nhân sự")]
        NhanSu = 5
    }

    public enum DoiTuongTacDong
    {
        [Display(Name = "Giao dịch")]
        GiaoDich = 1,

        [Display(Name = "Ngân sách")]
        NganSach = 2,

        [Display(Name = "Mục tiêu")]
        MucTieu = 3,

        [Display(Name = "Hộ gia đình")]
        HoGiaDinh = 4,

        [Display(Name = "Ví tiền")]
        Vi = 5,

        [Display(Name = "Thông báo")]
        ThongBao = 6
    }

    [Table("NhatKyHeThongs")]
    public class NhatKyHeThong
    {
        [Key]
        public int MaNhatKy { get; set; }

        public int? MaGiaDinh { get; set; } // Nullable nếu là thao tác ví cá nhân riêng

        public int MaNguoiDung { get; set; } // Tác nhân thực hiện

        [Display(Name = "Loại thao tác")]
        public LoaiThaoTac LoaiThaoTac { get; set; } = LoaiThaoTac.TaoMoi;

        [Display(Name = "Đối tượng")]
        public DoiTuongTacDong DoiTuongTacDong { get; set; } = DoiTuongTacDong.GiaoDich;

        public int? MaBanGhi { get; set; }

        [StringLength(500)]
        [Display(Name = "Mô tả chi tiết")]
        public string MoTaChiTiet { get; set; } = string.Empty;

        public string? NoiDungCu { get; set; } // Dạng JSON/Text

        public string? NoiDungMoi { get; set; } // Dạng JSON/Text

        [Display(Name = "Thời gian")]
        public DateTime ThoiGian { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("MaNguoiDung")]
        public virtual NguoiDung? NguoiDung { get; set; }

        [ForeignKey("MaGiaDinh")]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }
    }
}