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
    public class VersionsController(JaberahDBContext db, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMemoryCache _cache = cache;
        
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetLastVersion([FromQuery] string version)
        {
            if (!_cache.TryGetValue("appVer", out var appVer))
{
    var appVersion = await _db.Versions.FirstOrDefaultAsync();

    if (appVersion == null) 
        return NotFound(new { message = "version not found" });

    bool isUpdateRequired = CompareVersions(version, appVersion.MinRequiredVersion) < 0;
    bool isUpdateAvailable = CompareVersions(version, appVersion.LatestVersion) < 0;

    appVer = new
    {
        latestVersion = appVersion.LatestVersion,
        minRequiredVersion = appVersion.MinRequiredVersion,
        isUpdateRequired,
        isUpdateAvailable,
        url = appVersion.URL
    };

    _cache.Set("appVer", appVer, new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
        SlidingExpiration = TimeSpan.FromHours(12)
    });
}

return Ok(appVer);
        }
        [AllowAnonymous]
        [HttpPut]
        public async Task<IActionResult> UpdateVersion([FromQuery] string version, [FromForm] string url)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(url)) return BadRequest(new {message = "invalid data"}); 
            var lastVersion = await _db.Versions.FirstOrDefaultAsync();
            if (lastVersion == null) return NotFound(new { message = "version not found" });
            lastVersion.LatestVersion = version;
            lastVersion.URL = url.Contains("dl=0") ? url.Replace("dl=0", "dl=1") : lastVersion.URL;
            var versionParts = version.Split('.').Select(int.Parse).ToArray();
            var requiredParts = lastVersion.MinRequiredVersion.Split('.').Select(int.Parse).ToArray();
            if (versionParts[0] >  requiredParts[0])
            {
                lastVersion.MinRequiredVersion = version;
            }
            else if (versionParts[1]  > requiredParts[1])
            {
                lastVersion.MinRequiredVersion = $"{versionParts[0]}.{versionParts[1]}.0";
            }
            await _db.SaveChangesAsync();
            _cache.Remove("appVer");
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
