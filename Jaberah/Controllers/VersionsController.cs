using FirebaseAdmin.Messaging;
using Google.Api.Gax;
using Jaberah.Models.DTOs;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Jaberah.Models.DTOs.Login;

namespace Jaberah.Controllers
{
    [Route("api/versions")]
    [ApiController]
    public class VersionsController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetLastVersion([FromQuery] string version)
        {
            var appVersion = await _db.Versions
            .FirstOrDefaultAsync();

            if (appVersion == null) return NotFound(new { message = "version not found" });

            bool isUpdateRequired = CompareVersions(version, appVersion.MinRequiredVersion) < 0;
            bool isUpdateAvailable = CompareVersions(version, appVersion.LatestVersion) < 0;

            return Ok(new
            {
                latestVersion = appVersion.LatestVersion,
                minRequiredVersion = appVersion.MinRequiredVersion,
                isUpdateRequired,
                isUpdateAvailable,
                url = appVersion.URL
            });
        }
        [AllowAnonymous]
        [HttpPut]
        public async Task<IActionResult> UpdateVersion([FromQuery] string version, [FromForm] string url)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(url)) return BadRequest(new {message = "invalid data"}); 
            var lastVersion = await _db.Versions.FirstOrDefaultAsync();
            if (lastVersion == null) return NotFound(new { message = "version not found" });


            var versionParts = version.Split('.').Select(int.Parse).ToList();
            var requiredParts = lastVersion.MinRequiredVersion.Split('.').Select(int.Parse).ToList();

            while (versionParts.Count < 3) versionParts.Add(0);
            while (requiredParts.Count < 3) requiredParts.Add(0);

            if (versionParts[0] > requiredParts[0] || versionParts[0] < requiredParts[0])  // Major version increased
            {
                lastVersion.MinRequiredVersion = version;
            }
            else if (versionParts[1] > requiredParts[1] || versionParts[1] < requiredParts[1])  // Minor version increased
            {
                lastVersion.MinRequiredVersion = version;
            }
            else if (versionParts[2] > requiredParts[2] || versionParts[2] < requiredParts[2])  // Patch version increased
            {
                lastVersion.MinRequiredVersion = $"{versionParts[0]}.{versionParts[1]}.0";
            }


            if (lastVersion.LatestVersion != version)
            {
                var messageBuilder = new Message()
                {
                    Notification = new FirebaseAdmin.Messaging.Notification()
                    {
                        Title = "تحديث جديد",
                        Body = $"{version}",
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "topic", "newVersion" },
                        { "version", version },
                        { "url", url },
                        { "minRequired",lastVersion.MinRequiredVersion }
                    },
                    Topic = "newVersion"
                };
                try
                {
                    string response = await FirebaseMessaging.DefaultInstance.SendAsync(messageBuilder);
                }
                catch
                {
                    return StatusCode(500, new { message = "حدث خطأ في ارسال الاشعار" });
                }
            }

            lastVersion.LatestVersion = version;
            lastVersion.URL = url.Contains("dl=0") ? url.Replace("dl=0", "dl=1") : lastVersion.URL;
            await _db.SaveChangesAsync();
            return Ok(lastVersion);
        }
        [NonAction]
        private int CompareVersions(string currentVersion, string requiredVersion)
        {
            var currentParts = currentVersion.Split('.').Select(int.Parse).ToArray();
            var requiredParts = requiredVersion.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(currentParts.Length, requiredParts.Length); i++)
            {
                if (currentParts[i] < requiredParts[i]) return -1;
                if (currentParts[i] > requiredParts[i]) return 1;
            }

            return currentParts.Length.CompareTo(requiredParts.Length);
        }
    }
}
