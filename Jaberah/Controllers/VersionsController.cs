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
    public class VersionsController(JaberahDBContext db, DropboxService dropboxService) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly DropboxService _dropboxService = dropboxService;
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
        [RequestSizeLimit(100_000_000)] 
        public async Task<IActionResult> UpdateVersion([FromQuery] string version, IFormFile apkFile)
        {
            if (string.IsNullOrWhiteSpace(version) || apkFile == null || apkFile.Length == 0)
                return BadRequest(new { message = "Invalid data" });

            var lastVersion = await _db.Versions.FirstOrDefaultAsync();
            if (lastVersion == null) return NotFound(new { message = "Version not found" });

            // Read the file as byte array
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await apkFile.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            // Refresh Dropbox Access Token
            var accessToken = await _dropboxService.RefreshAccessTokenAsync();

            // Upload APK to Dropbox
            var filePath = $"/jaberah-{version}.apk";
            await _dropboxService.UploadFileAsync(accessToken, filePath, fileBytes);

            // Get Sharable Link from Dropbox
            var sharableLink = await _dropboxService.GetSharableLinkAsync(accessToken, filePath);

            // Update version info in database
            lastVersion.LatestVersion = version;
            lastVersion.URL = sharableLink.Replace("dl=0", "dl=1"); // Convert link for direct download
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
