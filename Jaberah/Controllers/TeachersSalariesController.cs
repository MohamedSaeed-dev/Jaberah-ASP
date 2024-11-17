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
    public class TeachersSalariesController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        public TeachersSalariesController(JaberahDBContext db)
        {
            _db = db;
        }
        [HttpGet("for-month")]
        public async Task<IActionResult> GetTeachersSalariesForMonth([FromQuery] int year, [FromQuery] int month, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime date = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

            var salariesQuery = _db.TeacherSalaries
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

            var combinedQuery = salariesQuery
                .Union(missingSalariesQuery)
                .OrderBy(x => x.TeacherId)
                .AsQueryable();

            var pagedCombinedQuery = await combinedQuery.Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Ok(pagedCombinedQuery.ToPagedList(combinedQuery.Count(), pageNumber, pageSize));
        }
        [UpsertTeachersSalaries]
        [HttpPost]
        public async Task<IActionResult> UpsertTeachersSalariesForMonth([FromQuery] int year, [FromQuery] int month, [FromBody] UpsertTeachersSalariesDTO model)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime date = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

            var existingRecord = await _db.TeacherSalaries
                .Where(x => x.Date == date)
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
                    salaryRow.Salary = model.Salary ?? salaryRow.Salary;
                    salaryRow.DaysAbsence = model.DaysAbsence;
                    salaryRow.NetSalary = (model.Salary.HasValue || model.DaysAbsence.HasValue) ? Math.Max(0, ((model.Salary ?? salaryRow.Salary) - ((model.Salary ?? salaryRow.Salary) / 30 * (model.DaysAbsence ?? salaryRow.DaysAbsence))) ?? 0) : salaryRow.NetSalary;
                    salaryRow.Signature = model.Signature ?? salaryRow.Signature;
                }
                else
                {
                    existingRecord.TeacherSalary.TeachersSalariesRows.Add(new TeachersSalariesRow
                    {
                        TeacherId = model.TeacherId,
                        Salary = model.Salary ?? 0,
                        DaysAbsence = model.DaysAbsence ?? 0,
                        NetSalary = Math.Max(0, (model.Salary ?? 0) - ((model.Salary ?? 0) / 30) * (model.DaysAbsence ?? 0)),
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
                            Salary = model.Salary ?? 0,
                            DaysAbsence = model.DaysAbsence ?? 0,
                            NetSalary = Math.Max(0, (model.Salary ?? 0) - ((model.Salary ?? 0) / 30) * (model.DaysAbsence ?? 0)),
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
