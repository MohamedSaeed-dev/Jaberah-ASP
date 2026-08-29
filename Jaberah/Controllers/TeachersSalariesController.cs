using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Validations.TeachersSalaries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using Jaberah.Middlewares;

namespace Jaberah.Controllers
{
    [Route("api/teachers-salaries")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    public class TeachersSalariesController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [IsAdmin]
        [HttpGet("for-month")]
        public async Task<IActionResult> GetTeachersSalariesForMonth([FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month <= 0)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            // Single query rooted at Teachers — missing salary defaults to 0/false naturally
            var result = await _db.Teachers
                .AsNoTracking()
                .SelectMany(
                    teacher => teacher.Groups,
                    (teacher, group) => new { teacher, group }
                )
                .OrderBy(t => t.teacher.Name).ThenBy(g => g.group.Name)
                .Select(t => new
                {
                    TeacherId = t.teacher.Id,
                    TeacherName = t.teacher.Name,
                    GroupId = t.group.Id,
                    GroupName = t.group.Name,
                    SalaryRecord = t.teacher.Salaries!.Where(s => s.GroupId == t.group.Id && s.Year == year && s.Month == month).FirstOrDefault()
                })
                .ToListAsync();

            var report = result.Select(t => new
            {
                t.TeacherId,
                t.TeacherName,
                t.GroupId,
                t.GroupName,
                Salary = t.SalaryRecord?.Salary ?? 0,
                IsPaid = t.SalaryRecord?.IsPaid ?? false,
                t.SalaryRecord?.PaidAt
            }).ToList();
            return Ok(report);
        }

        [IsAdmin]
        [UpsertTeachersSalaries]
        [HttpPost]
        public async Task<IActionResult> UpsertTeachersSalariesForMonth(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromBody] UpsertTeachersSalariesDTO model)
        {
            if (year <= 0 || month <= 0)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            if (model.TeacherId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == model.TeacherId))
                return BadRequest(new { message = "المعلم غير موجود" });
            if (!await _db.Groups.AnyAsync(x => x.Id == model.GroupId))
                return BadRequest(new { message = "الحلقة غير موجودة" });

            var existing = await _db.TeacherSalaries
                .FirstOrDefaultAsync(s => s.TeacherId == model.TeacherId
                                       && s.GroupId == model.GroupId
                                       && s.Year == year
                                       && s.Month == month);

            if (existing is not null) // Update
            {
                existing.Salary = model.Salary ?? existing.Salary;
                existing.IsPaid = model.IsPaid ?? existing.IsPaid;
                existing.PaidAt = (model.IsPaid ?? existing.IsPaid) ? DateTime.UtcNow.AddHours(3) : existing.PaidAt;
            }
            else // Insert
            {
                float salary = Math.Max(model.Salary ?? 0, 0);

                await _db.TeacherSalaries.AddAsync(new TeacherSalary
                {
                    TeacherId = model.TeacherId,
                    GroupId = model.GroupId,
                    Year = year,
                    Month = month,
                    Salary = salary,
                    IsPaid = model.IsPaid ?? false,
                    PaidAt = (model.IsPaid ?? false) ? DateTime.UtcNow.AddHours(3) : null
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الرواتب بنجاح" });
        }

        [HttpGet("my-salaries")]
        public async Task<IActionResult> GetMySalaries([FromQuery] int year)
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (teacherId <= 0 || year <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            var result = await _db.TeacherSalaries.AsNoTracking().Where(ts => ts.TeacherId == teacherId && ts.Year == year)
                .Select(ts => new
                {
                    ts.Id,
                    ts.Year,
                    ts.Month,
                    ts.Salary,
                    ts.IsPaid,
                    ts.PaidAt,
                    ts.GroupId,
                    GroupName = ts.Group.Name
                })
                .ToListAsync();
            return Ok(result);
        }

        [HttpPatch("my-salaries/{salaryId}/mark-as-paid")]
        public async Task<IActionResult> MarkAsPaid([FromRoute] int salaryId)
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (teacherId <= 0 || salaryId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            var result = await _db.TeacherSalaries.Where(ts => ts.Id == salaryId && ts.TeacherId == teacherId).FirstOrDefaultAsync();
            if (result == null) return BadRequest(new { message = "لايوجد راتب" });

            result.PaidAt = DateTime.Now;
            result.IsPaid = true;

            await _db.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الراتب كمستلم بنجاح" });
        }
    }
}