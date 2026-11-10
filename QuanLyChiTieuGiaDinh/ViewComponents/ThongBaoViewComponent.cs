using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using System.Security.Claims;

namespace QuanLyChiTieuGiaDinh.ViewComponents
{
    public class ThongBaoViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var email = HttpContext.User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return View(0);
            }

            var nguoiDung = await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.Email == email);

            if (nguoiDung == null)
            {
                return View(0);
            }

            var soThongBaoChuaDoc = await _context.ThongBaos
                .CountAsync(x =>
                    x.MaNguoiDung == nguoiDung.MaNguoiDung &&
                    !x.DaDoc);

            return View(soThongBaoChuaDoc);
        }
    }
}