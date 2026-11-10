using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class NhatKyHeThongService
    {
        private readonly ApplicationDbContext _context;

        public NhatKyHeThongService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task GhiNhatKyAsync(
            int maNguoiDung,
            LoaiThaoTac loaiThaoTac,
            DoiTuongTacDong doiTuong,
            string moTaChiTiet,
            int? maGiaDinh = null,
            int? maBanGhi = null,
            string? noiDungCu = null,
            string? noiDungMoi = null)
        {
            if (maNguoiDung <= 0) return;

            var nhatKy = new NhatKyHeThong
            {
                MaNguoiDung = maNguoiDung,
                MaGiaDinh = maGiaDinh,
                LoaiThaoTac = loaiThaoTac,
                DoiTuongTacDong = doiTuong,
                MoTaChiTiet = moTaChiTiet,
                MaBanGhi = maBanGhi,
                NoiDungCu = noiDungCu,
                NoiDungMoi = noiDungMoi,
                ThoiGian = DateTime.Now
            };

            _context.NhatKyHeThongs.Add(nhatKy);
            await _context.SaveChangesAsync();
        }

        // Phương thức Overload hỗ trợ tương thích mã nguồn cũ
        public async Task GhiThemAsync(int maNguoiDung, string tenBang, int maBanGhi, string noiDungMoi, int? maGiaDinh = null)
        {
            await GhiNhatKyAsync(maNguoiDung, LoaiThaoTac.TaoMoi, DoiTuongTacDong.GiaoDich, $"Tạo mới bản ghi bảng {tenBang}", maGiaDinh, maBanGhi, null, noiDungMoi);
        }

        public async Task GhiSuaAsync(int maNguoiDung, string tenBang, int maBanGhi, string noiDungCu, string noiDungMoi, int? maGiaDinh = null)
        {
            await GhiNhatKyAsync(maNguoiDung, LoaiThaoTac.ChinhSua, DoiTuongTacDong.GiaoDich, $"Chỉnh sửa bản ghi bảng {tenBang}", maGiaDinh, maBanGhi, noiDungCu, noiDungMoi);
        }

        public async Task GhiXoaAsync(int maNguoiDung, string tenBang, int maBanGhi, string noiDungCu, int? maGiaDinh = null)
        {
            await GhiNhatKyAsync(maNguoiDung, LoaiThaoTac.Xoa, DoiTuongTacDong.GiaoDich, $"Xóa bản ghi bảng {tenBang}", maGiaDinh, maBanGhi, noiDungCu, null);
        }
    }
}