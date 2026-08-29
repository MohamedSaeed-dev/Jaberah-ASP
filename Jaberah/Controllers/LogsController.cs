using Jaberah.Middlewares;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

// يعرض أجسام الطلبات والردود الفاشلة، فهو للمدير وحده.
[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(VerifyTokenAttribute))]
[IsAdmin]
public class LogsController : ControllerBase
{
    private const string LogFilePath = "Logs/error-requests.log";

    [HttpGet]
    public IActionResult GetLogs(int pageNumber = 1, int pageSize = 10)
    {
        if (!System.IO.File.Exists(LogFilePath))
        {
            return NotFound("Log file not found.");
        }

        var logs = new List<LogEntry>();

        using (var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fileStream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                try
                {
                    var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (logEntry != null)
                    {
                        logs.Add(logEntry);
                    }
                }
                catch (JsonException)
                {
                    // You may log this if needed
                }
            }
        }

        logs = logs.OrderByDescending(log => log.Timestamp).ToList();

        var totalCount = logs.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var pagedLogs = logs.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        var metadata = new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = pageNumber < totalPages,
            HasPreviousPage = pageNumber > 1,
            Data = pagedLogs
        };

        return Ok(metadata);
    }


    [HttpDelete]
    public IActionResult DeleteLogs()
    {
        if (!System.IO.File.Exists(LogFilePath))
        {
            return NotFound("Log file not found.");
        }

        System.IO.File.WriteAllText(LogFilePath, string.Empty);

        return NoContent();
    }
}
