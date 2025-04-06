using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Logger _requestLogger;

    public RequestResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;

        // Logger that writes logs in JSON format to a file
        _requestLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(new JsonFormatter(),
                          "Logs/http-requests.json",
                          shared: true)
            .CreateLogger();
    }

    public async Task Invoke(HttpContext context)
    {
        context.Request.EnableBuffering();

        string requestBody = "";
        if (context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
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
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        _requestLogger.Information("{@HttpLog}", new
        {
            timestamp = DateTime.UtcNow,
            method = context.Request.Method,
            url = context.Request.Path.Value,
            body = requestBody,
            statusCode = context.Response.StatusCode,
            response = responseText
        });

        await responseBody.CopyToAsync(originalBodyStream);
    }
}
