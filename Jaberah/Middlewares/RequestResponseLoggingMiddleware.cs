using Serilog;
using Serilog.Core;
using Serilog.Events;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Logger _requestLogger;

    public RequestResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;

        // Configure a local logger ONLY for request logs
        _requestLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("Logs/http-requests.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public async Task Invoke(HttpContext context)
    {
        context.Request.EnableBuffering();

        var request = context.Request;
        string requestBody = "";

        if (request.ContentLength > 0 && request.Body.CanSeek)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        var originalBody = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        _requestLogger.Information(
            "HTTP {Method} {Url} \nRequestBody: {RequestBody} \nStatusCode: {StatusCode} \nResponseBody: {ResponseBody}",
            request.Method,
            request.Path,
            requestBody,
            context.Response.StatusCode,
            responseText
        );

        await responseBody.CopyToAsync(originalBody);
    }
}
