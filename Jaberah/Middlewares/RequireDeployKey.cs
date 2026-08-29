using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace Jaberah.Middlewares
{
    /// <summary>
    /// يحمي نقاط النهاية التي يستدعيها خط النشر (CI) لا المستخدمون.
    /// المصادقة بمفتاح مشترك في ترويسة X-Deploy-Key لأن الـ CI لا يملك JWT.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
    public class RequireDeployKeyAttribute(IConfiguration config) : ActionFilterAttribute
    {
        public const string HeaderName = "X-Deploy-Key";

        private readonly IConfiguration _config = config;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var expected = _config["DeployKey"];

            // لا مفتاح مضبوط = النقطة مغلقة. الافتراض الآمن هو الرفض لا الفتح.
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(provided) || !AreEqual(provided, expected))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            base.OnActionExecuting(context);
        }

        // تجزئة الطرفين قبل المقارنة توحّد الطول فلا يسرّب زمن المقارنة طول المفتاح
        private static bool AreEqual(string provided, string expected)
        {
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
    }
}
