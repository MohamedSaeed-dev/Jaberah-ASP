using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http.Headers;
using Jaberah.Models.ViewModels.Notifications;
using Microsoft.EntityFrameworkCore;
using Jaberah.Helpers;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController(JaberahDBContext db, IMapper mapper, IConfiguration config, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;
        private readonly IConfiguration _config = config;
        private readonly IMemoryCache _cache = cache;
        private readonly GoogleCredential _googleCredential = GoogleCredential.FromFile(config["FCM:ServiceAccountFilePath"])
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

        private readonly string _cacheKey = "notifications";

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            var accessToken = await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            var teacherTokens = await _db.Teachers
                                         .AsNoTracking()
                                         .Where(u => u.Role == Role.TEACHER && !string.IsNullOrWhiteSpace(u.FCMToken))
                                         .Select(u => u.FCMToken!)
                                         .ToListAsync();

            if (teacherTokens.Count == 0)
            {
                return NotFound(new { message = "لايوجد اجهزة للارسال لهم" });
            }

            using var client = new HttpClient
            {
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", accessToken)
                }
            };

            var messageNotification = new
            {
                message = new
                {
                    token = string.Empty,
                    notification = new
                    {
                        title = message.Title,
                        body = message.Body
                    }
                }
            };

            var tasks = teacherTokens.Select(async token =>
            {
                var payload = messageNotification with { message = messageNotification.message with { token = token } };
                var json = JsonConvert.SerializeObject(payload);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"https://fcm.googleapis.com/v1/projects/{_config["FCM:projectId"]}/messages:send", content);

                if (!response.IsSuccessStatusCode)
                {
                    // Log errors
                }
            });

            await Task.WhenAll(tasks);
            var notification = _mapper.Map<Notification>(message);
            notification.CreatedAt = GetCurrentHijriDateTime();
            await _db.Notifications.AddAsync(notification);
            await _db.SaveChangesAsync();
            _cache.Remove(_cacheKey);
            return Ok(new { message = "تم ارسال الاشعار بنجاح" });
        }


        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if(!_cache.TryGetValue(_cacheKey, out PagedList<Notification> notifications))
            {
                var query = await _db.Notifications.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToPagedListAsync(pageNumber, pageSize);
                _cache.Set(_cacheKey, query, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });

            }
            return Ok(notifications);
        }

        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            if(id == default)
            {
                return BadRequest(new { message = "البيانات خاطئة" });
            }
            var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id);
            if(notification == null)
            {
                return BadRequest(new { message = "لايوجد اشعار" });
            }

            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();
            _cache.Remove(_cacheKey);
            return Ok(new { message = "تم الحذف بنجاح" });
        }

        [NonAction]
        public static DateTime GetCurrentHijriDateTime()
        {
            HijriCalendar hijriCalendar = new HijriCalendar();

            DateTime currentDateTime = DateTime.UtcNow.AddHours(3);
            int hijriYear = hijriCalendar.GetYear(currentDateTime);
            int hijriMonth = hijriCalendar.GetMonth(currentDateTime);
            int hijriDay = hijriCalendar.GetDayOfMonth(currentDateTime);

            DateTime hijriDateTime = new(hijriYear, hijriMonth, hijriDay,
                currentDateTime.Hour, currentDateTime.Minute, currentDateTime.Second, currentDateTime.Millisecond);

            return hijriDateTime;
        }


    }
}
