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

        // Create a generic error response object
        var errorResponse = new
        {
            Message = "An unexpected error occurred. Please try again later.",
            Details = exception.Message  // You can choose to send more details in dev environment
        };

        // Optionally: Don't send detailed exception information to the client in production
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            errorResponse = new
            {
                Message = "An unexpected error occurred.",
                Details = exception.ToString()  // Send detailed stack trace in development
            };
        }

        // Write the error response
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
    }
}
