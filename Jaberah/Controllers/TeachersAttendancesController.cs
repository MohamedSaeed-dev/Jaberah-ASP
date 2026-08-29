using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.TeachersAttendances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Jaberah.Middlewares;

namespace Jaberah.Controllers
{
    [Route("api/teachers-attendances")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    public class TeachersAttendancesController(JaberahDBContext db, FirebaseService firebaseService) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly FirebaseService _firebaseService = firebaseService;

        [IsAdmin]
        [HttpGet("for-month-report")]
        public async Task<IActionResult> GetTeachersAttendancesReportForMonth([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate)
        {
            if (fromDate.Equals(default) || toDate.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            var result = await _db.TeacherAttendances
                .AsNoTracking()
                .Where(a => a.Date >= fromDate && a.Date <= toDate)
                .GroupBy(a => new { a.TeacherId, TName = a.Teacher.Name, a.GroupId, GName = a.Group.Name })
                .Select(g => new GetTeachersAttendancesReportForMonth
                {
                    TeacherName = g.Key.TName,
                    GroupName = g.Key.GName,
                    ExcuseNo = g.Count(x => x.Status == AttendanceStatus.Excused),
                    PresentNo = g.Count(x => x.Status == AttendanceStatus.Present),
                    AbsentNo = g.Count(x => x.Status == AttendanceStatus.Absent),
                    LateNo = g.Count(x => x.Status == AttendanceStatus.Late)
                })
                .ToListAsync();

            return Ok(result);
        }

        [IsAdmin]
        [HttpGet("for-day-report")]
        public async Task<IActionResult> GetTeachersAttendancesForDay([FromQuery] DateOnly date)
        {
            if (date.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            var result = (await _db.Teachers
                .AsNoTracking()
                .SelectMany(
                    teacher => teacher.Groups,
                    (teacher, group) => new { teacher, group }
                )
                .GroupJoin(
                    _db.TeacherAttendances.Where(a => a.Date == date),
                    tg => new { TeacherId = tg.teacher.Id, GroupId = tg.group.Id },
                    a => new { a.TeacherId, a.GroupId },
                    (tg, attendances) => new { tg.teacher, tg.group, attendances }
                )
                .SelectMany(
                    x => x.attendances.DefaultIfEmpty(),
                    (x, a) => new
                    {
                        TeacherId = x.teacher.Id,
                        TeacherName = x.teacher.Name,
                        GroupId = x.group.Id,
                        GroupName = x.group.Name,
                        CheckInTime = a != null ? a.CheckInTime : null,
                        CheckOutTime = a != null ? a.CheckOutTime : null,
                        Status = a != null ? a.Status : AttendanceStatus.Absent
                    }
                )
                .ToListAsync())
                .Select(a => new GetTeachersAttendancesForDay
                {
                    TeacherId = a.TeacherId,
                    TeacherName = a.TeacherName,
                    GroupId = a.GroupId, 
                    GroupName = a.GroupName,
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime,
                    Status = GetAttendanceStatusName((byte)a.Status)
                })
                .ToList();

            return Ok(result);
        }
        [IsAdmin]
        [HttpPost]
        public async Task<IActionResult> UpsertTeacherAttendanceForDay([FromQuery] DateOnly date, [FromBody] UpsertTeachersAttendancesDTO model)
        {
            if (date == default)
                return BadRequest(new { message = "ادخل تاريخ صحيح" });

            if (model == null || model.TeacherId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            var teacher = await _db.Teachers.FindAsync(model.TeacherId);

            if (teacher == null)
                return NotFound(new { message = "المعلم غير موجود" });

            var group = await _db.Groups.FindAsync(model.GroupId);
            if (group == null)
                return NotFound(new { message = "الحلقة غير موجودة" });

            // Calculate status
            AttendanceStatus status;

            if (!model.CheckInTime.HasValue)
            {
                status = AttendanceStatus.Absent;
            }
            else
            {
                var flexibleMinutes = group.FlexibleMinutes ?? 0m;
                var flexible = TimeSpan.FromMinutes((double)flexibleMinutes);

                var windowStart = group.WindowStart.HasValue
                    ? group.WindowStart.Value.Add(-flexible)
                    : TimeOnly.MinValue;

                var windowEnd = group.WindowEnd.HasValue
                    ? group.WindowEnd.Value.Add(flexible)
                    : TimeOnly.MaxValue;

                if (model.CheckInTime < windowStart)
                    status = AttendanceStatus.Present;
                else if (model.CheckInTime > windowEnd)
                    status = AttendanceStatus.Late;
                else
                    status = AttendanceStatus.Present;
            }

            if (model.IsExcused.HasValue && model.IsExcused.Value)
                status = AttendanceStatus.Excused;

            var existing = await _db.TeacherAttendances
                .FirstOrDefaultAsync(a => a.Date == date && a.TeacherId == model.TeacherId && a.GroupId == model.GroupId);

            if (existing != null)
            {
                existing.CheckInTime = model.CheckInTime;
                existing.CheckOutTime = model.CheckOutTime;
                existing.Status = status;
            }
            else
            {
                await _db.TeacherAttendances.AddAsync(new TeacherAttendance
                {
                    TeacherId = model.TeacherId,
                    GroupId = model.GroupId,
                    Date = date,
                    CheckInTime = model.CheckInTime,
                    CheckOutTime = model.CheckOutTime,
                    Status = status
                });
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث الحضور بنجاح" });
        }
        [HttpGet("{teacherId}/for-day")]
        public async Task<IActionResult> GetTeacherAttendanceForDay([FromRoute] int teacherId, [FromQuery] DateOnly date)
        {
            // معلم يقرأ بيانات نفسه فقط؛ المدير يقرأ أي معلم.
            if (!this.CanActOnTeacher(teacherId))
                return Forbid();

            if (date == default)
                return BadRequest(new { message = "يرجى إدخال تاريخ صحيح (سنة وشهر ويوم)" });
            if (teacherId <= 0)
                return BadRequest(new { message = "يرجى إدخال معرف معلم صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
                return NotFound(new { message = "المعلم غير موجود" });

            var result = (await _db.Teachers
                .AsNoTracking()
                .Where(t => t.Id == teacherId)
                .SelectMany(
                    teacher => teacher.Groups,
                    (teacher, group) => new { teacher, group }
                )
                .GroupJoin(
                    _db.TeacherAttendances.Where(a => a.Date == date),
                    tg => new { TeacherId = tg.teacher.Id, GroupId = tg.group.Id },
                    a => new { a.TeacherId, a.GroupId },
                    (tg, attendances) => new { tg.teacher, tg.group, attendances }
                )
                .SelectMany(
                    x => x.attendances.DefaultIfEmpty(),
                    (x, a) => new
                    {
                        GroupId = x.group.Id,
                        GroupName = x.group.Name,
                        CheckInTime = a != null ? a.CheckInTime : null,
                        CheckOutTime = a != null ? a.CheckOutTime : null,
                        Status = a != null ? a.Status : AttendanceStatus.Absent
                    }
                )
                .ToListAsync())
                .Select(a => new
                {
                    a.GroupId,
                    a.GroupName,
                    a.CheckInTime,
                    a.CheckOutTime,
                    Status = GetAttendanceStatusName((byte)a.Status)
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("{teacherId}/for-month")]
        public async Task<IActionResult> GetTeacherAttendanceForMonth([FromRoute] int teacherId, [FromQuery] DateOnly fromDate, DateOnly toDate)
        {
            // معلم يقرأ بيانات نفسه فقط؛ المدير يقرأ أي معلم.
            if (!this.CanActOnTeacher(teacherId))
                return Forbid();

            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "يرجى إدخال تاريخ صحيح (سنة وشهر)" });
            if (teacherId <= 0)
                return BadRequest(new { message = "يرجى إدخال معرف معلم صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
                return NotFound(new { message = "المعلم غير موجود" });

            var groupsOfTeacher = await _db.Groups.Where(g => g.TeacherId == teacherId).Select(g => new
            {
                g.Id,
                g.Name
            }).ToListAsync();

            var result = (await _db.Teachers
                .AsNoTracking()
                .Where(t => t.Id == teacherId)
                .SelectMany(
                    teacher => teacher.Groups,
                    (teacher, group) => new { teacher, group }
                )
                .GroupJoin(
                    _db.TeacherAttendances.Where(a => a.Date >= fromDate && a.Date <= toDate),
                    tg => new { TeacherId = tg.teacher.Id, GroupId = tg.group.Id },
                    a => new { a.TeacherId, a.GroupId },
                    (tg, attendances) => new { tg.teacher, tg.group, attendances }
                )
                .SelectMany(
                    x => x.attendances.DefaultIfEmpty(),
                    (x, a) => new
                    {
                        GroupId = x.group.Id,
                        GroupName = x.group.Name,
                        CheckInTime = a != null ? a.CheckInTime : null,
                        CheckOutTime = a != null ? a.CheckOutTime : null,
                        Status = a != null ? a.Status : AttendanceStatus.Absent,
                        Date = a != null ? a.Date : null
                    }
                )
                .ToListAsync())
                .Select(a => new
                {
                    a.GroupId,
                    a.GroupName,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.Date,
                    Status = GetAttendanceStatusName((byte)a.Status)
                })
                .ToList();

            return Ok(new { groupsOfTeacher, result });
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> TeacherCheckIn([FromBody] TeacherCheckInDTO model)
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (teacherId <= 0 || model.GroupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3).Date);
            var now = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

            var teacher = await _db.Teachers.FindAsync(teacherId);
            if (teacher == null)
                return NotFound(new { message = "المعلم غير موجود" });

            var group = await _db.Groups.FindAsync(model.GroupId);
            if (group == null)
                return NotFound(new { message = "الحلقة غير موجودة" });

            var existing = await _db.TeacherAttendances
                .FirstOrDefaultAsync(a => a.Date == today
                                       && a.TeacherId == teacherId
                                       && a.GroupId == model.GroupId);

            if (existing != null && existing.CheckInTime.HasValue)
                return BadRequest(new { message = "تم تسجيل الحضور مسبقاً" });

            // Calculate status
            var flexibleMinutes = group.FlexibleMinutes ?? 0m;
            var flexible = TimeSpan.FromMinutes((double)flexibleMinutes);

            AttendanceStatus status;

            var windowEnd = group.WindowEnd.HasValue
                ? group.WindowEnd.Value.Add(flexible)
                : TimeOnly.MaxValue;

            if (now > windowEnd)
                status = AttendanceStatus.Late;
            else
                status = AttendanceStatus.Present;

            if (existing != null)
            {
                existing.CheckInTime = now;
                existing.Status = status;
            }
            else
            {
                await _db.TeacherAttendances.AddAsync(new TeacherAttendance
                {
                    TeacherId = teacherId,
                    GroupId = model.GroupId,
                    Date = today,
                    CheckInTime = now,
                    Status = status
                });
            }

            await _db.SaveChangesAsync();

            if (teacher.Role != Role.ADMIN)
                await _firebaseService.SendToTopicAsync(
                        title: "تسجيل حضور معلم",
                        body: $"قام المعلم {teacher.Name} بتسجيل حضوره في حلقة {group.Name} - الحالة: {(status == AttendanceStatus.Late ? "متأخر" : "حاضر")}",
                        topic: "check-attendance"
                );

            return Ok(new { message = "تم تسجيل الحضور بنجاح", checkInTime = now });
        }


        [HttpPost("check-out")]
        public async Task<IActionResult> TeacherCheckOut([FromBody] TeacherCheckOutDTO model)
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (teacherId <= 0 || model.GroupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            var teacher = await _db.Teachers.FindAsync(teacherId);
            if (teacher == null)
                return NotFound(new { message = "المعلم غير موجود" });

            var group = await _db.Groups.FindAsync(model.GroupId);
            if (group == null)
                return NotFound(new { message = "الحلقة غير موجودة" });

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3).Date);
            var now = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

            var existing = await _db.TeacherAttendances
                .FirstOrDefaultAsync(a => a.Date == today
                                       && a.TeacherId == teacherId
                                       && a.GroupId == model.GroupId);

            if (existing == null || !existing.CheckInTime.HasValue)
                return BadRequest(new { message = "لم يتم تسجيل الحضور بعد" });

            if (existing.CheckOutTime.HasValue)
                return BadRequest(new { message = "تم تسجيل الانصراف مسبقاً" });

            existing.CheckOutTime = now;

            await _db.SaveChangesAsync();

            if (teacher.Role != Role.ADMIN)
                await _firebaseService.SendToTopicAsync(
                    title: "تسجيل انصراف معلم",
                    body: $"قام المعلم {teacher.Name} بتسجيل انصرافه من حلقة {group.Name}",
                    topic: "check-attendance"
                );

            return Ok(new { message = "تم تسجيل الانصراف بنجاح", checkOutTime = now });
        }

        [NonAction]
        private string? GetAttendanceStatusName(byte status)
        {
            return Enum.GetName(typeof(AttendanceStatus), status);
        }
    }
}