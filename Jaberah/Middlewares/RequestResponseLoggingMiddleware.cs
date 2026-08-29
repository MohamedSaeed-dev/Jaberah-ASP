using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Logger _requestLogger;

    public RequestResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;

        _requestLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                formatter: new JsonFormatter(),
                path: "Logs/error-requests.log",
                shared: true)
            .CreateLogger();
    }

    public async Task Invoke(HttpContext context)
    {
        context.Request.EnableBuffering();

        string requestBody = string.Empty;
        if (context.Request.ContentLength > 0 && context.Request.Body.CanSeek && IsLoggableBody(context.Request))
        {
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            _requestLogger.Information("{@HttpLog}", new
            {
                timestamp = DateTime.UtcNow.AddHours(3).AddHours(3),
                method = context.Request.Method,
                url = context.Request.Path.Value,
                body = Redact(requestBody),
                statusCode = context.Response.StatusCode,
                response = Redact(Truncate(responseText))
            });
        }

        await responseBody.CopyToAsync(originalBodyStream);
    }

    // تسجيل دخول فاشل يرجع 400، فكان جسم الطلب — وفيه كلمة المرور نصًا صريحًا —
    // يُكتب في ملف السجل ثم يُقرأ عبر /api/Logs. نُخفي الحقول الحساسة قبل الكتابة.
    private static readonly string[] SensitiveKeys =
    [
        "password", "newPassword", "oldPassword", "confirmPassword",
        "token", "accessToken", "refreshToken", "fcmToken", "apiKey", "deployKey"
    ];

    private static string Redact(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return payload;

        // استبدال نصي لا تحليل JSON: الجسم قد يكون مشوَّهًا أو غير JSON أصلًا،
        // والهدف ألا تُكتب القيمة مهما كان شكل الحمولة.
        foreach (var key in SensitiveKeys)
        {
            payload = Regex.Replace(
                payload,
                $"(\"{Regex.Escape(key)}\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
                "$1\"***\"",
                RegexOptions.IgnoreCase);
        }

        return payload;
    }

    private const int MaxLoggedBodyChars = 4096;

    // رفع الـ APK يمرّ من هنا: تسجيل حمولة multipart يكتب الملف الثنائي كاملًا
    // (عشرات الميغابايتات) في ملف السجل عند أي فشل.
    private static bool IsLoggableBody(HttpRequest request)
    {
        var contentType = request.ContentType;
        if (string.IsNullOrEmpty(contentType)) return false;

        if (contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return request.ContentLength <= MaxLoggedBodyChars;
    }

    private static string Truncate(string value) =>
        string.IsNullOrEmpty(value) || value.Length <= MaxLoggedBodyChars
            ? value
            : value[..MaxLoggedBodyChars] + "…[truncated]";
}
