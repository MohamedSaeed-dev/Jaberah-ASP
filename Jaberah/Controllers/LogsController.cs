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

    // السجل يُكتب بالإلحاق، فأحدث الأسطر في نهايته. القراءة كانت تحمّل الملف كاملًا
    // في الذاكرة وتفكّ ترميز كل سطر ثم ترتّب ثم تصفّح — تكلفة تنمو خطيًا مع حجم السجل
    // وقد تُسقط الخادم على ملف كبير. الآن يُقرأ ذيل الملف فقط، وتُفَكّ الأسطر المطلوبة.
    private const int MaxScannedLines = 5000;

    [HttpGet]
    public IActionResult GetLogs(int pageNumber = 1, int pageSize = 10)
    {
        if (!System.IO.File.Exists(LogFilePath))
        {
            return NotFound("Log file not found.");
        }

        if (pageNumber < 1) pageNumber = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        // حلقة ثابتة السعة: تحتفظ بآخر MaxScannedLines سطرًا فقط مهما بلغ حجم الملف.
        var recent = new string[MaxScannedLines];
        var seen = 0;

        using (var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fileStream))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                recent[seen % MaxScannedLines] = line;
                seen++;
            }
        }

        var kept = Math.Min(seen, MaxScannedLines);
        var totalCount = kept;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // الأحدث أولًا: نمشي من آخر سطر مكتوب إلى الوراء ونفكّ ترميز الصفحة وحدها.
        var pagedLogs = new List<LogEntry>(pageSize);
        var skipped = 0;
        var toSkip = (pageNumber - 1) * pageSize;

        for (var i = 1; i <= kept && pagedLogs.Count < pageSize; i++)
        {
            if (skipped < toSkip) { skipped++; continue; }

            var line = recent[(seen - i) % MaxScannedLines];
            try
            {
                var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                if (logEntry != null) pagedLogs.Add(logEntry);
            }
            catch (JsonException)
            {
                // سطر مشوَّه (كتابة مقطوعة) — يُتخطّى
            }
        }

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
