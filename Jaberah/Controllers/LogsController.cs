using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private const string LogFilePath = "Logs/http-requests.json";

    [HttpGet]
    public IActionResult GetLogs()
    {
        if (!System.IO.File.Exists(LogFilePath))
            return NotFound("Log file not found.");

        var logs = new List<object>();

        // Open the file with shared read access
        using (var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var streamReader = new StreamReader(fileStream))
        {
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                try
                {
                    var logEntry = JsonSerializer.Deserialize<object>(line);
                    logs.Add(logEntry);
                }
                catch (JsonException)
                {
                    // Skip invalid JSON lines
                }
            }
        }

        return Ok(logs);
    }
}
