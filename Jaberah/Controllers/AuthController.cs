using Jaberah.Middlewares;
using Jaberah.Models.JaberahModels;
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
                .FirstOrDefaultAsync(t => t.TeacherName == model.Username.Trim());

            if (teacher == null)
            {
                return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });
            }


            var isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, teacher.Password);

            if (!isPasswordValid)
            {
                return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });
            }

            var accessToken = _token.GenerateToken(teacher.Id.ToString(), 7);
            var refreshToken = _token.GenerateToken(teacher.Id.ToString(), 30);

            teacher.FCMToken = model.FCMToken;
            await _db.SaveChangesAsync();

            var userData = new AuthTeacher
            {
                Id = teacher.Id,
                TeacherName = teacher.TeacherName,
                PhoneNumber = teacher.PhoneNumber,
                Role = teacher.Role,
            };

            return Ok(new { user = userData, accessToken, refreshToken });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDTO refreshDTO)
        {
            if(string.IsNullOrWhiteSpace(refreshDTO.RefreshToken))
            {
                return BadRequest(new { message = "البيانات خاطئة" });
            }
            var user = await _token.VerifyToken(refreshDTO.RefreshToken);
            if(user == default)
            {
                return Forbid();
            }

            return Ok(new { accessToken = _token.GenerateToken(user.Id.ToString(), 7) });
        }

        [HttpPatch("update-fcm-token")]
        public async Task<IActionResult> UpdateFCMToken([FromBody] UpdateFCMTokenDTO model)
        {
            if(model == default || model.UserId <= 0 || string.IsNullOrWhiteSpace( model.Token))
            {
                return BadRequest(new { message = "البيانات خاطئة" });
            }
            var teacher = await _db.Teachers.FirstOrDefaultAsync(x => x.Id == model.UserId);
            if (teacher == null)
            {
                return BadRequest(new {message = "لايوجد معلم"});
            }

            teacher.FCMToken = model.Token;

            await _db.SaveChangesAsync();

            return Ok(new {message = "تم التحديث بنجاح"});

        }


    }
}
