using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.TeachersAttendances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jaberah.Controllers
{
    [Route("api/teachers-attendances")]
    [ApiController]
    public class TeachersAttendancesController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [HttpGet("for-month-report")]
        public async Task<IActionResult> GetTeachersAttendancesReportForMonth([FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            var daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth);

            var attendancesQuery = _db.TeacherAttendances.AsNoTracking().Where(x => x.Date >= fromDate && x.Date <= toDate)
                .SelectMany(x => x.TeachersAttendancesRows)
                .GroupBy(x => x.Teacher.TeacherName)
                .Select(x => new GetTeachersAttendancesReportForMonth
                {
                    TeacherName = x.Key,
                    IsExcuseNo = x.Count(y => y.IsExcuse.HasValue && y.IsExcuse.Value),
                    SignatureNo = x.Count(y => y.Signature.HasValue && y.Signature.Value)
                }).AsQueryable();


            var allTeachersQuery = _db.Teachers.Select(x => new
            {
                x.Id,
                x.TeacherName
            });

            var missingAttendanceQuery = allTeachersQuery
                .Where(t => !_db.TeacherAttendances.AsNoTracking()
                    .Where(x => x.Date >= fromDate && x.Date <= toDate)
                    .SelectMany(ts => ts.TeachersAttendancesRows)
                    .Select(sr => sr.TeacherId)
                    .Contains(t.Id))
                .Select(t => new GetTeachersAttendancesReportForMonth
                {
                    TeacherName = t.TeacherName,
                    IsExcuseNo = 0,
                    SignatureNo = 0,
                });

            var combinedQuery = attendancesQuery
                .Union(missingAttendanceQuery)
                .AsQueryable();

            return Ok(await combinedQuery.ToListAsync());
        }

        [HttpGet("for-day-report")]
        public async Task<IActionResult> GetTeachersAttendancesForDay([FromQuery] DateTime date)
        {
            if (date.Equals(default))
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            var attendancesQuery = _db.TeacherAttendances.AsNoTracking().Where(x => x.Date == date).SelectMany(x => x.TeachersAttendancesRows)
                .Select(x => new GetTeachersAttendancesForDay
                {
                    Id = x.TeacherId,
                    TeacherName = x.Teacher.TeacherName,
                    IsExcuse = x.IsExcuse,
                    Signature = x.Signature,
                });

            var allTeachersQuery = _db.Teachers.Select(x => new
            {
                x.Id,
                x.TeacherName
            });

            var missingAttendanceQuery = allTeachersQuery
                .Where(t => !_db.TeacherAttendances.AsNoTracking()
                    .Where(ts => ts.Date == date)
                    .SelectMany(ts => ts.TeachersAttendancesRows)
                    .Select(sr => sr.TeacherId)
                    .Contains(t.Id))
                .Select(t => new GetTeachersAttendancesForDay
                {
                    Id = t.Id,
                    TeacherName = t.TeacherName,
                    IsExcuse = null,
                    Signature = false,
                });

            var combinedQuery = attendancesQuery
                .Union(missingAttendanceQuery)
                .AsQueryable();

            return Ok(await combinedQuery.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> UpsertTeachersAttendancesForMonth([FromQuery] DateTime date, [FromBody] List<UpsertTeachersAttendancesDTO> model)
        {
            if (date.Equals(default))
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            if (model.Exists(x => x.TeacherId <= 0)) return BadRequest(new { message = "ادخل id صحيح" });

            if (model.Exists(x => !(x.IsExcuse.HasValue ^ x.Signature.HasValue)))
            {
                return BadRequest(new { message = "ادخل فقط قيمة واحدة للمعلم (حاضر أو غائب بعذر)" });
            }

            var existingRecord = await _db.TeacherAttendances
                .Where(x => x.Date == date)
                .Include(x => x.TeachersAttendancesRows)
                .FirstOrDefaultAsync();

            if (existingRecord != null)
            {
                var attendanceRowsDictionary = existingRecord.TeachersAttendancesRows
                    .ToDictionary(x => x.TeacherId);

                foreach (var dto in model)
                {
                    if (attendanceRowsDictionary.TryGetValue(dto.TeacherId, out var attendanceRow))
                    {
                        attendanceRow.IsExcuse = dto.IsExcuse ?? attendanceRow.IsExcuse;
                        attendanceRow.Signature = dto.Signature ?? attendanceRow.Signature;
                    }
                    else
                    {
                        existingRecord.TeachersAttendancesRows.Add(new TeachersAttendancesRow
                        {
                            TeacherId = dto.TeacherId,
                            IsExcuse = dto.IsExcuse,
                            Signature = dto.Signature
                        });
                    }
                }
            }
            else // Insert case
            {
                var newAttendanceRecord = new TeachersAttendances
                {
                    Date = date,
                    TeachersAttendancesRows = model.Select(dto => new TeachersAttendancesRow
                    {
                        TeacherId = dto.TeacherId,
                        IsExcuse = dto.IsExcuse,
                        Signature = dto.Signature
                    }).ToList()
                };

                await _db.TeacherAttendances.AddAsync(newAttendanceRecord);
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث الحضور بنجاح" });
        }




        //[HttpGet("for-day")]
        //public async Task<IActionResult> GetTeachersAttendancesForDay([FromQuery] int year, [FromQuery] int month, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        //{
        //    if (year <= 0 || month <= 0)
        //    {
        //        return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
        //    }

        //    HijriCalendar hijriCalendar = new HijriCalendar();
        //    DateTime date = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

        //    var attendancesQuery = _db.TeacherAttendances.AsNoTracking().Where(x => x.Date == date).SelectMany(x => x.TeachersAttendancesRows).Select(x => new
        //    {
        //        TeacherId = x.Id,
        //        x.Teacher.TeacherName,
        //        x.IsExcuse,
        //        x.Signature
        //    });
        //    var allTeachersQuery = _db.Teachers.Select(x => new
        //    {
        //        x.Id,
        //        x.TeacherName
        //    });

        //    var missingAttendanceQuery = allTeachersQuery
        //        .Where(t => !_db.TeacherAttendances
        //            .Where(ts => ts.Date == date)
        //            .SelectMany(ts => ts.TeachersAttendancesRows)
        //            .Select(sr => sr.TeacherId)
        //            .Contains(t.Id))
        //        .Select(t => new
        //        {
        //            TeacherId = t.Id,
        //            t.TeacherName,
        //            IsExcuse = false,
        //            Signature = false,
        //        });

        //    var combinedQuery = attendancesQuery
        //        .Union(missingAttendanceQuery)
        //        .OrderBy(x => x.TeacherId)
        //        .AsQueryable();

        //    return Ok(await combinedQuery.ToPagedListAsync(pageNumber, pageSize));
        //}
    }
}
