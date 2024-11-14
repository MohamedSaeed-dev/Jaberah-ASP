using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
namespace Jaberah.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        public NotificationsController(JaberahDBContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
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