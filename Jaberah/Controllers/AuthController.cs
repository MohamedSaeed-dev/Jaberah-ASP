using Jaberah.Helpers;
using Jaberah.Middlewares;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Teachers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Jaberah.Models.DTOs.Login;

namespace Jaberah.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(JaberahDBContext db, TokenHelper token) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly TokenHelper _token = token;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            var teacher = await _db.Teachers.Include(x => x.Groups)
                .FirstOrDefaultAsync(t => t.Name == model.Username.Trim());

            if (teacher == null)
            {
                return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });
            }


            var isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, teacher.Password);

            if (!isPasswordValid)
            {
                return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });
            }

            var accessToken = _token.GenerateToken(teacher.Id.ToString(), teacher.Name, 7);
            var refreshToken = _token.GenerateToken(teacher.Id.ToString(), teacher.Name, 30);

            teacher.FCMToken = model.FCMToken;
            teacher.LastLogin = DateTime.UtcNow.AddHours(3);
            await _db.SaveChangesAsync();

            var userData = new AuthTeacher
            {
                Id = teacher.Id,
                TeacherName = teacher.Name,
                PhoneNumber = teacher.PhoneNumber,
                Role = teacher.Role,
            };
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/",
                Expires = DateTime.UtcNow.AddHours(3).AddDays(30)
            });
            return Ok(new { user = userData, accessToken });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized();
            }
            var user = await _token.VerifyToken(refreshToken);
            if (user == default)
            {
                return Forbid();
            }
            var newAccessToken = _token.GenerateToken(user.Id.ToString(), user.Name, 7);
            var newRefreshToken = _token.GenerateToken(user.Id.ToString(), user.Name, 30);
            Response.Cookies.Append("refreshToken", newRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/",
                Expires = DateTime.UtcNow.AddHours(3).AddDays(30)
            });
            return Ok(new { accessToken = newAccessToken });
        }

        // كان الـ UserId يُؤخذ من جسم الطلب، فأي معلم مصادَق يستطيع تمرير معرّف معلم آخر
        // ويوجّه إشعاراته إلى جهازه هو. الهوية تُؤخذ من التوكن لا من المستدعي.
        [ServiceFilter(typeof(VerifyTokenAttribute))]
        [HttpPatch("fcm-token")]
        public async Task<IActionResult> UpdateFCMToken([FromBody] UpdateFCMTokenDTO model)
        {
            if (model == default || string.IsNullOrWhiteSpace(model.Token))
            {
                return BadRequest(new { message = "البيانات خاطئة" });
            }

            var callerId = this.CurrentUser()!.Id;
            var teacher = await _db.Teachers.FirstOrDefaultAsync(x => x.Id == callerId);
            if (teacher == null)
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }

            teacher.FCMToken = model.Token;

            await _db.SaveChangesAsync();

            return Ok(new { message = "تم التحديث بنجاح" });

        }


    }
}
