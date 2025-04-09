using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private const string LogDirectoryPath = "Logs";
    private const string LogFilePath = $"{LogDirectoryPath}/http-requests.json";

    [HttpGet]
    public IActionResult GetLogs(int pageNumber = 1, int pageSize = 10)
    {
        if (!System.IO.File.Exists(LogFilePath))
            return NotFound("Log file not found.");

        var logs = new List<LogEntry>();

        // Open the file with shared read access
        using (var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var streamReader = new StreamReader(fileStream))
        {
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                try
                {
                    var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                    logs.Add(logEntry);
                }
                catch (JsonException)
                {
                    // Skip invalid JSON lines
                }
            }
        }

        // Order logs by Timestamp descending
        logs = logs.OrderByDescending(log => log.Timestamp).ToList();

        // Apply pagination
        var pagedLogs = logs.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return Ok(pagedLogs);
    }


    [HttpDelete]
    public async Task<IActionResult> DeleteLogs()
    {
        if (!Directory.Exists(LogDirectoryPath))
            return NotFound("Log directory not found.");

        var logFiles = Directory.GetFiles(LogFilePath, "*.json");
        foreach (var file in logFiles)
        {
            System.IO.File.Delete(file);
        }

        return NoContent();
    }
}
