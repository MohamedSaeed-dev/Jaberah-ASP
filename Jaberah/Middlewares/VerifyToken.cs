using Jaberah.Models.JaberahModels;
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
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var scheme = authHeader.Split(" ")[0];
            var token = scheme.ToLower() == "bearer" ? authHeader.Split(" ")[1] : null;
            if (token == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            var user = _token.VerifyToken(token);

            if (user == null)
            {
                context.Result = new ForbidResult();
                return;
            }
            context.HttpContext.Items["User"] = user;

            base.OnActionExecuting(context);
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
                Expires = DateTime.UtcNow.AddDays(days),
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
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }
                return await _db.Teachers.Select(x => new UserViewModel { Id = x.Id, Name = x.Name, PhoneNumber = x.PhoneNumber, Role = x.Role.ToString() }).FirstOrDefaultAsync(x => x.Id == int.Parse(id));
            }
            catch
            {
                return null;
            }
        }
    }

}
