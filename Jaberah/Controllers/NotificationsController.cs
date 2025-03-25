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
using FirebaseAdmin.Messaging;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;

namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            var messageBuilder = new Message()
            {
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = message.Title,
                    Body = message.Body,
                },
                Data = new Dictionary<string, string>
                {
                    { "topic", "public" },
                },
                Topic = "public"
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(messageBuilder);
            }
            catch
            {
                return StatusCode(500, new { message = "حدث خطأ في ارسال الاشعار للمعلمين" });
            }

            var notification = _mapper.Map<Models.JaberahModels.Notification>(message);
            notification.CreatedAt = GetCurrentHijriDateTime();
            await _db.Notifications.AddAsync(notification);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم ارسال الاشعار بنجاح" });
        }

        [HttpPost("send/{teacherId}")]
        public async Task<IActionResult> SendNotificationToTeacher(int teacherId, [FromBody] NotificationsDTO message)
        {
            var teacher = await _db.Teachers
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(u => u.Role == Role.TEACHER && u.Id == teacherId);

            if (teacher == null)
            {
                return NotFound(new { message = "لايوجد معلم للارسال" });
            }

            var messageBuilder = new Message()
            {
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = message.Title,
                    Body = message.Body,
                },
                Data = new Dictionary<string, string>
                {
                    { "topic", "token" },
                },
                Token = teacher.FCMToken
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(messageBuilder);
            }
            catch
            {
                return StatusCode(500, new { message = "حدث خطأ في ارسال الاشعار للمعلم" });
            }
            
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
