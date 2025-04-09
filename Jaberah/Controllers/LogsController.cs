using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
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
                { }
            }
        }

        logs = logs.OrderByDescending(log => log.Timestamp).ToList();

        var pagedLogs = logs.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return Ok(pagedLogs);
    }

    [HttpDelete]
    public IActionResult DeleteLogs()
    {
        if (!System.IO.File.Exists(LogFilePath))
        {
            return NotFound("Log file not found.");
        }

        System.IO.File.Delete(LogFilePath);
        return NoContent();
    }
}
