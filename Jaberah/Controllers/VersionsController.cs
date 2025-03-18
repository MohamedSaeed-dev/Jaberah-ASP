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
        public async Task<IActionResult> UpdateVersionURL([FromQuery] string url)
        {
            var version = await _db.Versions.FirstOrDefaultAsync();
            if (version == null) return NotFound(new { message = "version not found" });
            version.URL = url;
            await _db.SaveChangesAsync();
            return Ok(new {message = "Updated Successfully"});
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
