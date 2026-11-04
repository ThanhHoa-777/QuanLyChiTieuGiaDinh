using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }


        // =========================================================
        // DU LIEU FORM
        // =========================================================

        [BindProperty]
        public InputModel Input { get; set; } = default!;


        public string? ReturnUrl { get; set; }


        public IList<AuthenticationScheme>? ExternalLogins { get; set; }


        // =========================================================
        // INPUT MODEL
        // =========================================================

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
            [StringLength(
                100,
                ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
            [Display(Name = "Họ tên")]
            public string HoTen { get; set; } = string.Empty;


            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            [StringLength(150)]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;


            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(
                100,
                ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.",
                MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; } = string.Empty;


            [DataType(DataType.Password)]
            [Display(Name = "Nhập lại mật khẩu")]
            [Compare(
                "Password",
                ErrorMessage = "Mật khẩu nhập lại không khớp.")]
            public string? ConfirmPassword { get; set; }
        }


        // =========================================================
        // MO TRANG DANG KY
        // =========================================================

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();
        }


        // =========================================================
        // XU LY DANG KY
        // =========================================================

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");


            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();


            // =====================================================
            // KIEM TRA DU LIEU FORM
            // =====================================================

            if (!ModelState.IsValid)
            {
                return Page();
            }


            // =====================================================
            // KIEM TRA EMAIL DA CO TRONG NGUOIDUNG
            // =====================================================

            bool daTonTaiNguoiDung =
                await _context.NguoiDungs
                    .AnyAsync(x => x.Email == Input.Email);


            if (daTonTaiNguoiDung)
            {
                ModelState.AddModelError(
                    "Input.Email",
                    "Email này đã được sử dụng.");

                return Page();
            }


            // =====================================================
            // TAO TAI KHOAN IDENTITY
            // =====================================================

            var user = new IdentityUser();


            await _userManager.SetUserNameAsync(
                user,
                Input.Email);


            await _userManager.SetEmailAsync(
                user,
                Input.Email);


            var result =
                await _userManager.CreateAsync(
                    user,
                    Input.Password);


            // =====================================================
            // TAO THANH CONG
            // =====================================================

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Người dùng đã tạo tài khoản mới.");


                // =================================================
                // TAO NGUOI DUNG CUA HE THONG
                // =================================================

                var nguoiDung = new NguoiDung
                {
                    HoTen = Input.HoTen,
                    Email = Input.Email,
                    NgayTao = DateTime.Now
                };


                _context.NguoiDungs.Add(nguoiDung);


                await _context.SaveChangesAsync();


                // =================================================
                // TAO MA XAC NHAN EMAIL
                // =================================================

                var userId =
                    await _userManager.GetUserIdAsync(user);


                var code =
                    await _userManager
                        .GenerateEmailConfirmationTokenAsync(user);


                code = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(code));


                var callbackUrl =
                    Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new
                        {
                            area = "Identity",
                            userId = userId,
                            code = code,
                            returnUrl = returnUrl
                        },
                        protocol: Request.Scheme);


                // =================================================
                // GUI EMAIL NEU CO CALLBACK URL
                // =================================================

                if (callbackUrl != null)
                {
                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Xác nhận tài khoản",
                        $"Vui lòng xác nhận tài khoản bằng cách " +
                        $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>" +
                        $"bấm vào đây</a>.");
                }


                // =================================================
                // NEU KHONG BAT BUOC XAC NHAN EMAIL
                // DANG NHAP LUON
                // =================================================

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    return RedirectToPage(
                        "RegisterConfirmation",
                        new
                        {
                            email = Input.Email,
                            returnUrl = returnUrl
                        });
                }


                await _signInManager.SignInAsync(
                    user,
                    isPersistent: false);


                return LocalRedirect(returnUrl);
            }


            // =====================================================
            // HIEN THI LOI IDENTITY
            // =====================================================

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }


            return Page();
        }
    }
}