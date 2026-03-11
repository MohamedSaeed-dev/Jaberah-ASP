using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Jaberah.Helpers;

namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController(JaberahDBContext db, IMapper mapper, FirebaseService firebaseService) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;
        private readonly FirebaseService _firebaseService = firebaseService;

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            try
            {
                await _firebaseService.SendToTopicAsync(message.Title, message.Body, "public");
            }
            catch
            {
                return StatusCode(500, new { message = "حدث خطأ في ارسال الاشعار للمعلمين" });
            }

            var notification = _mapper.Map<Models.JaberahModels.Notification>(message);
            notification.CreatedAt = DateTime.Now;
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

            try
            {
                if (!string.IsNullOrWhiteSpace(teacher.FCMToken))
                    await _firebaseService.SendToTokenAsync(message.Title, message.Body, teacher.FCMToken);
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
            if (id == default)
            {
                return BadRequest(new { message = "البيانات خاطئة" });
            }

            var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id);
            if (notification == null)
            {
                return BadRequest(new { message = "لايوجد اشعار" });
            }

            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم الحذف بنجاح" });
        }
    }
}