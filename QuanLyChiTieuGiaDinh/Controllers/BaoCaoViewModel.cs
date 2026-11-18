using System;
using System.Collections.Generic;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class BaoCaoViewModel
    {
        // ============================================
        // THÔNG TIN CHUNG
        // ============================================

        public NguoiDung? NguoiDung { get; set; }

        public HoGiaDinh? HoGiaDinh { get; set; }

        public int ThangBaoCao { get; set; }

        public int NamBaoCao { get; set; }


        // ============================================
        // THÔNG TIN QUYỀN
        // ============================================

        // Mã người dùng đang xem báo cáo
        public int? MaNguoiDung { get; set; }

        // Mã gia đình của báo cáo
        public int? MaGiaDinh { get; set; }

        // Chủ hộ có quyền xem toàn bộ giao dịch
        public bool LaChuHo { get; set; }


        // ============================================
        // PHẠM VI BÁO CÁO
        // ============================================

        // Chủ hộ:
        //   Toàn bộ giao dịch gia đình
        //
        // Thành viên:
        //   Giao dịch của mình + giao dịch dùng chung
        public string PhamViBaoCao { get; set; }
            = string.Empty;


        // ============================================
        // TỔNG QUAN
        // ============================================

        public decimal TongThu { get; set; }

        public decimal TongChi { get; set; }

        public decimal ChenhLech { get; set; }

        public int SoGiaoDich { get; set; }


        // ============================================
        // CHI THEO DANH MỤC
        // ============================================

        public List<BaoCaoDanhMucViewModel> ChiTheoDanhMuc
        {
            get;
            set;
        }
            = new List<BaoCaoDanhMucViewModel>();


        // ============================================
        // THU THEO DANH MỤC
        // ============================================

        public List<BaoCaoDanhMucViewModel> ThuTheoDanhMuc
        {
            get;
            set;
        }
            = new List<BaoCaoDanhMucViewModel>();


        // ============================================
        // CHI THEO NGÀY
        // ============================================

        public List<BaoCaoNgayViewModel> ChiTheoNgay
        {
            get;
            set;
        }
            = new List<BaoCaoNgayViewModel>();


        // ============================================
        // DANH SÁCH GIAO DỊCH
        // ============================================

        public List<GiaoDich> GiaoDichs
        {
            get;
            set;
        }
            = new List<GiaoDich>();
    }


    // ================================================
    // THỐNG KÊ THEO DANH MỤC
    // ================================================

    public class BaoCaoDanhMucViewModel
    {
        public string TenDanhMuc { get; set; }
            = string.Empty;

        public decimal SoTien { get; set; }
    }


    // ================================================
    // THỐNG KÊ THEO NGÀY
    // ================================================

    public class BaoCaoNgayViewModel
    {
        public DateTime Ngay { get; set; }

        public decimal SoTien { get; set; }
    }
}