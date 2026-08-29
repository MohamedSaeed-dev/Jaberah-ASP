using System.Net;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Proceed with the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            // Handle the exception globally
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log the exception
        _logger.LogError(exception, "An unhandled exception occurred.");

        // Set the response status code
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // كان الرد الافتراضي يحمل exception.Message دائمًا، والشرط أدناه يوسّعه في التطوير فقط،
        // أي أن الإنتاج كان يسرّب نص الاستثناء (أخطاء SQL، أسماء جداول، أحيانًا أجزاء من
        // سلسلة الاتصال) لأي مستدعٍ. التفاصيل تبقى في السجل أعلاه، ولا تُرسل إلا في التطوير.
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        object errorResponse = isDevelopment
            ? new
            {
                Message = "An unexpected error occurred.",
                Details = exception.ToString()
            }
            : new
            {
                Message = "An unexpected error occurred. Please try again later."
            };

        // Write the error response
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
    }
}
