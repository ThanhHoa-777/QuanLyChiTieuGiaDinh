using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class ThongBaoService
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // 1. TẠO THÔNG BÁO CHUẨN (DÙNG ENUM)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            PhamViThongBao phamVi = PhamViThongBao.CaNhan,
            LoaiThongBao loaiThongBao = LoaiThongBao.HeThong,
            string? duLieuLienKet = null)
        {
            var thongBao = new ThongBao
            {
                MaNguoiDung = maNguoiDung,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                PhamVi = phamVi,
                LoaiThongBao = loaiThongBao,
                DuLieuLienKet = duLieuLienKet,
                DaDoc = false,
                IsThaoTacXong = false,
                NgayTao = DateTime.Now
            };

            _context.ThongBaos.Add(thongBao);
            await _context.SaveChangesAsync();
        }

        // =====================================================
        // 2. OVERLOAD CHO CODE CỦ (TRUYỀN STRING)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            string loaiThongBao = "HeThong")
        {
            var loaiEnum = Enum.TryParse<LoaiThongBao>(loaiThongBao, true, out var result)
                ? result
                : LoaiThongBao.HeThong;

            await TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, PhamViThongBao.CaNhan, loaiEnum, null);
        }

        // =====================================================
        // 2.1 OVERLOAD MỚI (TRUYỀN STRING + PHẠM VI)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            string loaiThongBao,
            PhamViThongBao phamVi)
        {
            var loaiEnum = Enum.TryParse<LoaiThongBao>(loaiThongBao, true, out var result)
                ? result
                : LoaiThongBao.HeThong;

            await TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, phamVi, loaiEnum, null);
        }

        // =====================================================
        // 3. THÔNG BÁO CHO TOÀN HỘ GIA ĐÌNH
        // =====================================================
        public async Task TaoThongBaoGiaDinhAsync(
            int maGiaDinh,
            string tieuDe,
            string noiDung,
            LoaiThongBao loaiThongBao,
            string? duLieuLienKet = null,
            int? ngoaiTruMaNguoiDung = null)
        {
            var danhSachThanhVien = await _context.ThanhVienGiaDinhs
                .Where(x => x.MaGiaDinh == maGiaDinh)
                .Select(x => x.MaNguoiDung)
                .ToListAsync();

            foreach (var maNguoiDung in danhSachThanhVien)
            {
                if (ngoaiTruMaNguoiDung.HasValue && maNguoiDung == ngoaiTruMaNguoiDung.Value)
                    continue;

                await TaoThongBaoAsync(
                    maNguoiDung,
                    tieuDe,
                    noiDung,
                    PhamViThongBao.GiaDinh,
                    loaiThongBao,
                    duLieuLienKet);
            }
        }

        // =====================================================
        // 4. QUY TẮC PHẠM VI THÔNG BÁO (HỢP NHẤT TẠI MỘT CHỖ)
        // =====================================================
        // Nguyên tắc: thông báo thuộc tab "Gia đình" khi nó liên quan đến hộ mà người nhận
        // ĐANG LÀ THÀNH VIÊN. Thông báo thuộc tab "Cá nhân" khi chỉ liên quan riêng người nhận,
        // hoặc khi người nhận CHƯA / KHÔNG CÒN thuộc hộ đó.
        //
        //   Sự kiện                                          Người nhận        Phạm vi
        //   Lời mời gia nhập hộ                              người được mời    Cá nhân  (chưa thuộc hộ)
        //   Lời mời tham gia mục tiêu nhóm tự do             người được mời    Cá nhân
        //   Thành viên mới / từ chối lời mời                 chủ hộ            Gia đình
        //   Yêu cầu rời hộ                                   chủ hộ            Gia đình
        //   Kết quả: chủ hộ ĐỒNG Ý cho rời hộ                người rời hộ      Cá nhân  (đã rời hộ)
        //   Kết quả: chủ hộ TỪ CHỐI cho rời hộ               người xin rời     Gia đình (vẫn trong hộ)
        //   Bị gỡ khỏi hộ / hộ bị giải tán                   người bị ảnh hưởng Cá nhân (không còn hộ)
        //   Chuyển quyền chủ hộ                              chủ hộ mới        Gia đình
        //   Hạn mức nộp tiền / chi tiêu ví gia đình          thành viên        Gia đình
        //   Mục tiêu tiết kiệm của hộ gia đình               thành viên hộ     Gia đình
        //   Mục tiêu tiết kiệm cá nhân / nhóm tự do          người tham gia    Cá nhân
        //
        /// <summary>Phạm vi thông báo của một mục tiêu tiết kiệm: mục tiêu của hộ -> Gia đình, còn lại -> Cá nhân.</summary>
        public static PhamViThongBao PhamViCuaMucTieu(LoaiMucTieuTietKiem loaiMucTieu)
        {
            return loaiMucTieu == LoaiMucTieuTietKiem.HoGiaDinh
                ? PhamViThongBao.GiaDinh
                : PhamViThongBao.CaNhan;
        }

        /// <summary>Gửi thông báo vào tab Cá nhân của một người.</summary>
        public Task TaoThongBaoCaNhanAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            LoaiThongBao loaiThongBao = LoaiThongBao.HeThong,
            string? duLieuLienKet = null)
        {
            return TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, PhamViThongBao.CaNhan, loaiThongBao, duLieuLienKet);
        }

        /// <summary>Gửi thông báo vào tab Gia đình của một thành viên hộ.</summary>
        public Task TaoThongBaoGiaDinhChoThanhVienAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            LoaiThongBao loaiThongBao = LoaiThongBao.HeThong,
            string? duLieuLienKet = null)
        {
            return TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, PhamViThongBao.GiaDinh, loaiThongBao, duLieuLienKet);
        }
    }
}