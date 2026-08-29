using Jaberah.Middlewares;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                .AsNoTracking()
                .OrderByDescending(v => v.UpdatedAt)
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
        // يستدعيها خط النشر فقط، لا التطبيق، والـ CI لا يملك JWT — فالمصادقة هنا
        // بمفتاح النشر لا بالتوكن. [AllowAnonymous] يعطّل سياسة الـ JWT العامة فقط،
        // ويبقى RequireDeployKey حارسًا فعليًا. قبل ذلك كانت النقطة مفتوحة تمامًا:
        // أي أحد يرفع APK فيصبح تحديث التطبيق الرسمي لكل المستخدمين.
        [AllowAnonymous]
        [ServiceFilter(typeof(RequireDeployKeyAttribute))]
        [HttpPut]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UpdateVersion([FromQuery] string version, IFormFile apkFile)
        {
            if (string.IsNullOrWhiteSpace(version) || apkFile == null || apkFile.Length == 0)
                return BadRequest(new { message = "Invalid data" });

            var lastVersion = await _db.Versions.OrderByDescending(v => v.UpdatedAt).FirstOrDefaultAsync();
            lastVersion ??= new Models.JaberahModels.Version
                {
                    LatestVersion = version,
                    MinRequiredVersion = version,
                    URL = ""
                };

            // Refresh Dropbox Access Token
            var accessToken = await _dropboxService.RefreshAccessTokenAsync();

            // Upload APK to Dropbox (بالتدفّق، بلا تحميل الملف في الذاكرة)
            var filePath = $"/jaberah-{version}.apk";
            await using var apkStream = apkFile.OpenReadStream();
            await _dropboxService.UploadFileAsync(accessToken, filePath, apkStream);

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
