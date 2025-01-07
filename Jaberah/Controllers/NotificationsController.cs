using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System.Text;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Jaberah.Helpers;

namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController(JaberahDBContext db, IMapper mapper, IConfiguration config) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;
        private readonly IConfiguration _config = config;
        private readonly GoogleCredential _googleCredential = GoogleCredential.FromFile(config["FCM:ServiceAccountFilePath"])
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

        private readonly string _cacheKey = "notifications";

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            var accessToken = await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();


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
                    topic = "public",
                    notification = new
                    {
                        title = message.Title,
                        body = message.Body
                    }
                }
            };

            var json = JsonConvert.SerializeObject(messageNotification);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"https://fcm.googleapis.com/v1/projects/{_config["FCM:projectId"]}/messages:send", content);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { message = "حدث خطأ في ارسال الاشعار للمعلمين" });
            }

            var notification = _mapper.Map<Notification>(message);
            notification.CreatedAt = GetCurrentHijriDateTime();
            await _db.Notifications.AddAsync(notification);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم ارسال الاشعار بنجاح" });
        }

        [HttpPost("send/{teacherId}")]
        public async Task<IActionResult> SendNotificationToTeacher(int teacherId, [FromBody] NotificationsDTO message)
        {
            var accessToken = await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            var teacher = await _db.Teachers
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(u => u.Role == Role.TEACHER && u.Id == teacherId);

            if (teacher == null)
            {
                return NotFound(new { message = "لايوجد معلم للارسال" });
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
                    token = teacher.FCMToken,
                    notification = new
                    {
                        title = message.Title,
                        body = message.Body
                    }
                }
            };

            var json = JsonConvert.SerializeObject(messageNotification);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"https://fcm.googleapis.com/v1/projects/{_config["FCM:projectId"]}/messages:send", content);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { message = "حدث خطأ في ارسال الاشعار للمعلم" });
            }

            var notification = _mapper.Map<Notification>(message);
            notification.CreatedAt = GetCurrentHijriDateTime();
            await _db.Notifications.AddAsync(notification);
            return Ok(new { message = "تم ارسال الاشعار بنجاح" });
        }


        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            return Ok(await _db.Notifications.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToPagedListAsync(pageNumber, pageSize));
        }


        [HttpDelete("{id}")]
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
