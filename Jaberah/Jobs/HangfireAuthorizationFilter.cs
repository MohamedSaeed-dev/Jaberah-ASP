using Hangfire.Dashboard;
using Jaberah.Middlewares;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels;

/// <summary>
/// لوحة Hangfire تعرض المهام وتسمح بتشغيلها وحذفها، فهي للمدير وحده.
/// كانت ترجع true دائمًا، أي مفتوحة لأي زائر.
/// </summary>
/// <remarks>
/// اللوحة مُركَّبة بعد UseAuthentication/UseAuthorization، وسياسة المشروع العامة
/// (FallbackPolicy) ترفض غير المصادَق بـ 401 قبل الوصول إلى هنا. فدور هذا الفلتر
/// هو تضييق ما تبقّى إلى ADMIN فقط. ولهذا لا يصلح تمرير التوكن عبر كويري سترنغ:
/// الطلب يُرفض قبل أن يصل إلينا — يلزم ترويسة Authorization صحيحة.
/// </remarks>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var token = ExtractBearerToken(httpContext.Request.Headers.Authorization.ToString());
        if (string.IsNullOrWhiteSpace(token)) return false;

        var tokenHelper = httpContext.RequestServices.GetService<TokenHelper>();
        if (tokenHelper is null) return false;

        // IDashboardAuthorizationFilter متزامن ولا يوفّر نسخة async، فلا مفرّ من
        // الانتظار هنا. اللوحة صفحة إدارية نادرة الاستخدام فالأثر مقبول.
        UserViewModel? user = tokenHelper.VerifyToken(token).GetAwaiter().GetResult();

        return user is not null && user.Role == nameof(Role.ADMIN);
    }

    private static string? ExtractBearerToken(string authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader)) return null;

        var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0].Equals("bearer", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }
}
