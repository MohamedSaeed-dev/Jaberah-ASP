using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
namespace Jaberah.Middlewares
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
    public class VerifyTokenAttribute : ActionFilterAttribute
    {
        private readonly IConfiguration _config;

        public VerifyTokenAttribute(IConfiguration config)
        {
            _config = config;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var token = authHeader.Split(" ")[1];
            var user = VerifyToken(token);

            if (user == null)
            {
                context.Result = new ForbidResult();
                return;
            }
            context.HttpContext.Items["User"] = user;

            base.OnActionExecuting(context);
        }

        private UserViewModel? VerifyToken(string token)
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

                foreach (var claim in jwtToken.Claims)
                {
                    Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                }

                var username = jwtToken.Claims.FirstOrDefault(x => x.Type == "unique_name")?.Value;
                var phone = jwtToken.Claims.FirstOrDefault(x => x.Type == "PhoneNumber")?.Value;
                var role = jwtToken.Claims.FirstOrDefault(x => x.Type == "role")?.Value;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(role))
                {
                    return null;
                }

                return new UserViewModel
                {
                    Name = username,
                    PhoneNumber = phone,
                    Role = role
                };
            }
            catch
            {
                return null;
            }
        }

    }

}
