using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Validations.TeachersSalaries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jaberah.Controllers
{
    [Route("api/teachers-salaries")]
    [ApiController]
    public class TeachersSalariesController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [HttpGet("for-month")]
        public async Task<IActionResult> GetTeachersSalariesForMonth([FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime date = new DateTime(year, month, 1);

            var salariesQuery = _db.TeacherSalaries.AsNoTracking()
                .Where(x => x.Date == date)
                .SelectMany(x => x.TeachersSalariesRows)
                .Select(x => new
                {
                    x.TeacherId,
                    x.Teacher.TeacherName,
                    x.Salary,
                    x.NetSalary,
                    x.Signature,
                    DaysAbsence = x.DaysAbsence ?? 0
                });

            var allTeachersQuery = _db.Teachers.Select(x => new
            {
                x.Id,
                x.TeacherName
            });

            var missingSalariesQuery = allTeachersQuery
                .Where(t => !_db.TeacherSalaries
                    .Where(ts => ts.Date == date)
                    .SelectMany(ts => ts.TeachersSalariesRows)
                    .Select(sr => sr.TeacherId)
                    .Contains(t.Id))
                .Select(t => new
                {
                    TeacherId = t.Id,
                    t.TeacherName,
                    Salary = 0f,
                    NetSalary = 0f,
                    Signature = false,
                    DaysAbsence = 0
                });

            var combinedQuery = await salariesQuery
                .Union(missingSalariesQuery)
                .OrderBy(x => x.TeacherId)
                .ToListAsync();
            return Ok(combinedQuery);
        }
        [UpsertTeachersSalaries]
        [HttpPost]
        public async Task<IActionResult> UpsertTeachersSalariesForMonth([FromQuery] int year, [FromQuery] int month, [FromBody] UpsertTeachersSalariesDTO model)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            if (model.TeacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime date = new DateTime(year, month, 1);

            var teacherAbsenceCount = await _db.TeacherAttendances
                .Where(a => a.Date.Year == date.Year && a.Date.Month == date.Month)
                .SelectMany(a => a.TeachersAttendancesRows)
                .Where(ar => ar.TeacherId == model.TeacherId && (!ar.Signature ?? false))
                .CountAsync();

            var existingRecord = await _db.TeacherSalaries
                .Where(x => x.Date == date)
                .Include(x => x.TeachersSalariesRows)
                .Select(x => new
                {
                    TeacherSalary = x,
                    SalaryRow = x.TeachersSalariesRows.FirstOrDefault(y => y.Teacher.Id == model.TeacherId)
                })
                .FirstOrDefaultAsync();

            if (existingRecord != null) // Update case
            {
                var salaryRow = existingRecord.SalaryRow;

                if (salaryRow != null)
                {
                    salaryRow.Salary = Math.Max(model.Salary ?? salaryRow.Salary, 0);
                    salaryRow.DaysAbsence = teacherAbsenceCount;
                    salaryRow.NetSalary = (model.Salary.HasValue || teacherAbsenceCount != salaryRow.DaysAbsence)
                        ? Math.Max(0, ((model.Salary ?? salaryRow.Salary) - ((model.Salary ?? salaryRow.Salary) / 30 * teacherAbsenceCount)))
                        : salaryRow.NetSalary;
                    salaryRow.Signature = model.Signature ?? salaryRow.Signature;
                }
                else
                {
                    existingRecord.TeacherSalary.TeachersSalariesRows.Add(new TeachersSalariesRow
                    {
                        TeacherId = model.TeacherId,
                        Salary = Math.Max(model.Salary ?? 0, 0),
                        DaysAbsence = teacherAbsenceCount,
                        NetSalary = Math.Max(0, (model.Salary ?? 0) - ((model.Salary ?? 0) / 30) * teacherAbsenceCount),
                        Signature = model.Signature ?? false
                    });
                }
            }
            else // Insert case
            {
                var newSalaryRecord = new TeachersSalaries
                {
                    Date = date,
                    TeachersSalariesRows = new List<TeachersSalariesRow>
                    {
                        new ()
                        {
                            TeacherId = model.TeacherId,
                            Salary = Math.Max(model.Salary ?? 0, 0),
                            DaysAbsence = teacherAbsenceCount,
                            NetSalary = Math.Max(0, (model.Salary ?? 0) - ((model.Salary ?? 0) / 30) * teacherAbsenceCount),
                            Signature = model.Signature ?? false
                        }
                    }
                };
                await _db.TeacherSalaries.AddAsync(newSalaryRecord);
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الرواتب بنجاح" });
        }



    }
}
