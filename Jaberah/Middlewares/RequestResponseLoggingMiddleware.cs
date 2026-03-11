using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
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
        string responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            _requestLogger.Information("{@HttpLog}", new
            {
                timestamp = DateTime.UtcNow.AddHours(3).AddHours(3),
                method = context.Request.Method,
                url = context.Request.Path.Value,
                body = requestBody,
                statusCode = context.Response.StatusCode,
                response = responseText
            });
        }

        await responseBody.CopyToAsync(originalBodyStream);
    }
}
