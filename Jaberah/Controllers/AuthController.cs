using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Teachers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Jaberah.Models.DTOs.Login;

namespace Jaberah.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(JaberahDBContext db, IConfiguration configuration) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IConfiguration _configuration = configuration;

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

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["TokenKey"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                new Claim(ClaimTypes.Name, teacher.TeacherName),
                new Claim("PhoneNumber", teacher.PhoneNumber),
                new Claim(ClaimTypes.Role, teacher.Role.ToString())
            ]),
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            teacher.FCMToken = model.FCMToken;
            await _db.SaveChangesAsync();

            var userData = new AuthTeacher
            {
                Id = teacher.Id,
                TeacherName = teacher.TeacherName,
                PhoneNumber = teacher.PhoneNumber,
                Role = teacher.Role,
            };

            return Ok(new { user = userData, token = tokenHandler.WriteToken(token) });
        }

    }
}
