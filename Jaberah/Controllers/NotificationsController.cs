using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpPost]
        public async Task<IActionResult> SendNotification([FromBody] NotificationsDTO message)
        {
            var notification = _mapper.Map<Notification>(message);
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم ارسال الاشعار بنجاح" });
        }
    }
}