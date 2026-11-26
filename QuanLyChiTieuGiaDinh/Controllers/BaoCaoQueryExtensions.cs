using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Helpers
{
  
    public static class BaoCaoQueryExtensions
    {
        // ===== Dấu hiệu hệ thống trong GhiChu (GiaoDichesController nên dùng lại các hằng này) =====
        public const string DauNapHanMuc = "|NAP_HAN_MUC|";
        public const string DauChuyenTien = "|CHUYEN_TIEN|";
        public const string DauNapMucTieu = "|NAP_MUC_TIEU|";   // MỚI: nạp tiền vào mục tiêu tiết kiệm (quy tắc 6)
        public static string DauHanMuc(int maHanMuc) => $"|HM:{maHanMuc}|";

        public static readonly string[] TenDanhMucHeThong =
            { "Chuyển tiền cá nhân", "Chuyển tiền quỹ", "Nạp mục tiêu tiết kiệm" };

        // ===== (1) THU/CHI THỰC TẾ =====
        public static IQueryable<GiaoDich> ThuChiThucTe(this IQueryable<GiaoDich> query)
        {
            return query.Where(x =>
                x.TrangThai == "HoatDong" &&
                !(x.GhiChu != null && (x.GhiChu.Contains(DauNapHanMuc) ||
                                       x.GhiChu.Contains(DauChuyenTien) ||
                                       x.GhiChu.Contains(DauNapMucTieu))) &&
                !(x.DanhMuc != null && TenDanhMucHeThong.Contains(x.DanhMuc.TenDanhMuc)));
        }

        // ===== (2) PHẠM VI XEM GIAO DỊCH =====
        // Chủ hộ: toàn bộ hộ | Thành viên: của mình + ví gia đình | Cá nhân: chỉ của mình
        public static IQueryable<GiaoDich> TrongHo(this IQueryable<GiaoDich> query, int maGiaDinh)
            => query.Where(x => x.TaiKhoanTien != null && x.TaiKhoanTien.MaGiaDinh == maGiaDinh);

        public static IQueryable<GiaoDich> TrongPhamViXem(
            this IQueryable<GiaoDich> query, int maNguoiDung, bool laChuHo, bool laCaNhan)
        {
            if (laCaNhan)
                return query.Where(x => x.MaNguoiThucHien == maNguoiDung);

            if (laChuHo)
                return query;

            return query.Where(x => x.MaNguoiThucHien == maNguoiDung ||
                                    x.TaiKhoanTien!.LaTaiKhoanGiaDinh);
        }

        // ===== (3) "ĐÃ CHI" CỦA NGÂN SÁCH =====
        // Quy ước: chỉ tính ví thuộc HỘ (ví cá nhân + ví gia đình/quỹ chung).
        // KHÔNG tính ví nhóm (ViNhom) và ví mục tiêu cá nhân (MaGiaDinh == null).
        public static IQueryable<GiaoDich> GiaoDichTinhNganSach(this IQueryable<GiaoDich> query, NganSach ns)
        {
            var q = query
                .Where(x => x.LoaiGiaoDich == "Chi" &&
                            x.TaiKhoanTien != null &&
                            x.TaiKhoanTien.MaGiaDinh == ns.MaGiaDinh &&
                            x.TaiKhoanTien.LoaiVi != "ViNhom" &&
                            x.NgayGiaoDich.Month == ns.Thang &&
                            x.NgayGiaoDich.Year == ns.Nam)
                .ThuChiThucTe();

            if (ns.MaDanhMuc.HasValue)
                q = q.Where(x => x.MaDanhMuc == ns.MaDanhMuc.Value);

            return ns.IsGiaDinh
                ? q.Where(x => x.TaiKhoanTien!.LaTaiKhoanGiaDinh || x.TaiKhoanTien.LoaiVi == "QuyChung")
                : q.Where(x => x.MaNguoiThucHien == ns.MaNguoiTao &&
                               !x.TaiKhoanTien!.LaTaiKhoanGiaDinh &&
                               x.TaiKhoanTien.LoaiVi != "QuyChung");
        }

        /// <summary>Hàm duy nhất tính "đã chi" của một ngân sách (dùng cho Index, Details, cảnh báo, TrangChu).</summary>
        public static async Task<decimal> TinhDaChiAsync(this ApplicationDbContext context, NganSach ns)
            => await context.GiaoDichs.GiaoDichTinhNganSach(ns).SumAsync(x => (decimal?)x.SoTien) ?? 0;

        // ===== (5) "ĐÃ NẠP" CỦA HẠN MỨC =====
        // Hạn mức = mức BẮT BUỘC nạp. Đã nạp = tổng dòng Thu của chính thành viên trên ví đích có dấu |HM:id|.
        public static async Task<decimal> TinhDaNapAsync(this ApplicationDbContext context, HanMucChiTieu hanMuc)
        {
            string dau = DauHanMuc(hanMuc.MaHanMuc);

            return await context.GiaoDichs
                .Where(g => g.MaTaiKhoan == hanMuc.MaTaiKhoan &&
                            g.MaNguoiThucHien == hanMuc.MaThanhVien &&
                            g.LoaiGiaoDich == "Thu" &&
                            g.TrangThai == "HoatDong" &&
                            g.GhiChu != null &&
                            g.GhiChu.Contains(dau))
                .SumAsync(g => (decimal?)g.SoTien) ?? 0;
        }

        // ===== (6) MỤC TIÊU TIẾT KIỆM =====
        // Ví cá nhân "thường": không phải ví gia đình, không phải ví nhóm / ví mục tiêu (MaMucTieuNguon == null).
        public static IQueryable<TaiKhoanTien> ViCaNhanThuong(this IQueryable<TaiKhoanTien> query, int maNguoiDung)
            => query.Where(tk => tk.MaNguoiSoHuu == maNguoiDung &&
                                 tk.TrangThai &&
                                 !tk.LaTaiKhoanGiaDinh &&
                                 tk.LoaiVi == "ViCaNhan" &&
                                 tk.MaMucTieuNguon == null);

        public const string DanhMucNapMucTieu = "Nạp mục tiêu tiết kiệm";

        /// <summary>Lấy (hoặc tạo) danh mục hệ thống cho dòng nạp / hoàn tiền mục tiêu. loaiDanhMuc: "ChiTieu" | "ThuNhap".</summary>
        public static async Task<DanhMuc> LayHoacTaoDanhMucHeThongAsync(
            this ApplicationDbContext context, string ten, string loaiDanhMuc, int? maGiaDinh, int maNguoiTao)
        {
            var dm = await context.DanhMucs.FirstOrDefaultAsync(x =>
                x.TrangThai == "Active" &&
                x.LoaiDanhMuc == loaiDanhMuc &&
                x.TenDanhMuc == ten &&
                (x.MaGiaDinh == maGiaDinh || x.MaGiaDinh == null));

            if (dm != null) return dm;

            dm = new DanhMuc
            {
                TenDanhMuc = ten,
                TenDanhMucClean = ChuoiHelper.LamSach(ten),
                LoaiDanhMuc = loaiDanhMuc,
                BieuTuong = "🏷️",
                MauSac = "#0d6efd",
                MaGiaDinh = maGiaDinh,
                MaNguoiTao = maNguoiTao,
                TrangThai = "Active"
            };
            context.DanhMucs.Add(dm);
            await context.SaveChangesAsync();
            return dm;
        }
    }
}
