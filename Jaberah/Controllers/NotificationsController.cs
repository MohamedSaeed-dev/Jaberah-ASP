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

namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;
        private readonly GoogleCredential _googleCredential;

        public NotificationsController(JaberahDBContext db, IMapper mapper, IConfiguration config)
        {
            _db = db;
            _mapper = mapper;
            _config = config;
            _googleCredential = GoogleCredential.FromFile(_config["FCM:ServiceAccountFilePath"])
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            var accessToken = await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();

            var teacherTokens = _db.Teachers
                                    .Where(u => u.Role == Role.TEACHER && !string.IsNullOrWhiteSpace(u.FCMToken))
                                    .Select(u => u.FCMToken)
                                    .ToList();

            if (teacherTokens.Count == 0)
            {
                return NotFound(new { message = "لايوجد اجهزة للارسال لهم" });
            }

            var messageNotification = new NotificationMessage
            {
                message = new MessageModel
                {
                    token = "",
                    notification = new NotificationModel
                    {
                        title = message.Title,
                        body = message.Body
                    },
                }
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                foreach (var token in teacherTokens)
                {
                    messageNotification.message.token = token!;
                    var json = JsonConvert.SerializeObject(messageNotification);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync($"https://fcm.googleapis.com/v1/projects/{_config["FCM:projectId"]}/messages:send", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        // logs
                        continue;
                    }
                }

                var notification = _mapper.Map<Notification>(message);
                notification.CreatedAt = GetCurrentHijriDateTime();
                await _db.Notifications.AddAsync(notification);
                await _db.SaveChangesAsync();

                return Ok(new { message = "تم ارسال الاشعار بنجاح" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var notifications = await _db.Notifications.AsNoTracking().Skip((pageNumber - 1) * pageSize).Take(pageSize).OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(notifications);
        }

        [NonAction]
        public static DateTime GetCurrentHijriDateTime()
        {
            HijriCalendar hijriCalendar = new HijriCalendar();

            DateTime currentDateTime = DateTime.Now;

            int hijriYear = hijriCalendar.GetYear(currentDateTime);
            int hijriMonth = hijriCalendar.GetMonth(currentDateTime);
            int hijriDay = hijriCalendar.GetDayOfMonth(currentDateTime);
            int hijriHour = currentDateTime.Hour;
            int hijriMinute = currentDateTime.Minute;
            int hijriSecond = currentDateTime.Second;
            int hijriMillisecond = currentDateTime.Millisecond;

            DateTime gregorianDateTime = hijriCalendar.ToDateTime(hijriYear, hijriMonth, hijriDay, hijriHour, hijriMinute, hijriSecond, hijriMillisecond);

            return gregorianDateTime;
        }


    }
}
