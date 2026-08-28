using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace Jaberah.Middlewares
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
    public class VerifyTokenAttribute(TokenHelper token) : ActionFilterAttribute
    {
        private readonly TokenHelper _token = token;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var token = parts.Length == 2 && parts[0].Equals("bearer", StringComparison.OrdinalIgnoreCase)
                ? parts[1]
                : null;

            if (token == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var user = await _token.VerifyToken(token);

            if (user == null)
            {
                context.Result = new ForbidResult();
                return;
            }

            context.HttpContext.Items["User"] = user;

            await next();
        }

    }

    public class TokenHelper(IConfiguration config, JaberahDBContext db)
    {
        private readonly IConfiguration _config = config;
        private readonly JaberahDBContext _db = db;

        public string GenerateToken(string id, string name, int days)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["TokenKey"]!);
            var refreshTokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, id),
                    new Claim(ClaimTypes.Name, name)
                ]),
                Expires = DateTime.UtcNow.AddHours(3).AddDays(days),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(refreshTokenDescriptor));
        }
        public async Task<UserViewModel?> VerifyToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.ASCII.GetBytes(_config["TokenKey"]!);
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                }, out SecurityToken verifiedToken);

                var jwtToken = (JwtSecurityToken)verifiedToken;
                var id = jwtToken.Claims.FirstOrDefault(x => x.Type == "nameid")?.Value; ;
                if (string.IsNullOrEmpty(id) || !int.TryParse(id, out var teacherId))
                {
                    return null;
                }
                return await _db.Teachers
                    .AsNoTracking()
                    .Where(x => x.Id == teacherId)
                    .Select(x => new UserViewModel { Id = x.Id, Name = x.Name, PhoneNumber = x.PhoneNumber, Role = x.Role.ToString() })
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }
    }

}
