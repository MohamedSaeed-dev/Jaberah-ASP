using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Jaberah.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private const string LogFilePath = "Logs/http-requests.json";
        [HttpGet]
        public async Task<IActionResult> GetLogs()
        {
            if (!System.IO.File.Exists(LogFilePath))
                return NotFound("Log file not found.");

            var logLines = System.IO.File.ReadAllLines(LogFilePath);
            var logs = new List<object>();

            foreach (var line in logLines)
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

            return Ok(logs);
        }
    }
}