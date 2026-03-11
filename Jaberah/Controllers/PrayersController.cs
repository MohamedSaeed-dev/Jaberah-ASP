using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Prayers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jaberah.Controllers
{
    [Route("api/prayers")]
    [ApiController]
    public class PrayersController(JaberahDBContext db, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMemoryCache _cache = cache;

        [HttpGet]
        public async Task<IActionResult> GetAllPrayers([FromQuery] PaginationDTO query)
        {
            var (skip, take) = (query.PageNumber, query.PageSize);
            const string cacheKey = "allPrayers";

            if (!_cache.TryGetValue(cacheKey, out List<Prayer>? prayers))
            {
                prayers = await _db.Prayers.AsNoTracking()
                    .Select(p => new Prayer
                    {
                        Id = p.Id,
                        NameAr = p.NameAr,
                        NameEn = p.NameEn,
                        DefaultRakats = p.DefaultRakats,
                        DisplayOrder = p.DisplayOrder,
                    })
                    .Skip((skip - 1) * take)
                    .Take(take)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();

                _cache.Set(cacheKey, prayers, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromHours(24)
                });
            }
            return Ok(prayers);
        }

        [HttpGet("daily")]
        public async Task<IActionResult> GetDailyPrayers([FromQuery] QueryDayilyPrayersDTO query)
        {
            if (query.Date.Equals(default)) return BadRequest(new { message = "ادخل تاريخ صحيح" });
            var prayers = await _db.Prayers
                .AsNoTracking()
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new { p.Id, p.NameAr, p.DefaultRakats })
                .ToListAsync();

            if (prayers.Count == 0)
                return Ok(Enumerable.Empty<StudentDailyPrayerDto>());

            var prayerIds = prayers.Select(p => p.Id).ToList();

            // Build student query with filters
            var studentQuery = _db.Students.AsNoTracking().Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize);

            if (!string.IsNullOrEmpty(query.Search))
                studentQuery = studentQuery.Where(s => s.Name.Contains(query.Search));

            if (query.GroupId.HasValue)
                studentQuery = studentQuery.Where(s => s.GroupId == query.GroupId.Value);

            var students = await studentQuery
                .Select(s => new { s.Id, s.Name, GroupName = s.Group != null ? s.Group.Name : null })
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();

            if (studentIds.Count == 0)
                return Ok(Enumerable.Empty<StudentDailyPrayerDto>());

            // Fetch only relevant attendances (scoped to filtered students)
            var attendanceLookup = await _db.StudentPrayerAttendances
                .AsNoTracking()
                .Where(x => x.PrayerDate == query.Date
                         && studentIds.Contains(x.StudentId)
                         && prayerIds.Contains(x.PrayerId))
                .Select(x => new
                {
                    x.StudentId,
                    x.PrayerId,
                    x.RakatsCount,
                    x.IsInGroup
                })
                .ToDictionaryAsync(x => (x.StudentId, x.PrayerId));

            var result = students.Select(student => new StudentDailyPrayerDto
            {
                StudentId = student.Id,
                StudentName = student.Name,
                GroupName = student.GroupName,
                Prayers = [.. prayers.Select(prayer =>
                {
                    attendanceLookup.TryGetValue((student.Id, prayer.Id), out var attendance);
                    return new PrayerStatusDto
                    {
                        PrayerName = prayer.NameAr,
                        DefaultRakat = prayer.DefaultRakats,
                        AttendanceInfo = new PrayerAttendanceInfo
                        {
                            RakatsCount = attendance?.RakatsCount,
                            IsInGroup = attendance?.IsInGroup ?? false
                        }
                    };
                })]
            }).ToList();

            return Ok(result);
        }
        [HttpPost("upsert-daily")]
        public async Task<IActionResult> UpsertDaily(StudentDailyUpsertDTO dto)
        {

            var existingAttendances = await _db.StudentPrayerAttendances
                .Where(x =>
                    x.StudentId == dto.StudentId &&
                    x.PrayerDate == dto.Date)
                .ToListAsync();

            foreach (var prayer in dto.Prayers)
            {
                var existing = existingAttendances
                    .FirstOrDefault(x => x.PrayerId == prayer.PrayerId);

                if (existing == null)
                {
                    _db.StudentPrayerAttendances.Add(new StudentPrayerAttendance
                    {
                        StudentId = dto.StudentId,
                        PrayerId = prayer.PrayerId,
                        PrayerDate = dto.Date,
                        RakatsCount = prayer.RakatCount,
                        IsInGroup = prayer.IsInGroup,
                    });
                }
                else
                {
                    existing.RakatsCount = prayer.RakatCount;
                    existing.IsInGroup = prayer.IsInGroup;
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث البيانات بنجاح" });
        }

        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyPrayersReport([FromQuery] QueryMonthlyPrayersReportDTO query)
        {
            var (date, daysInMonth, groupId, skip, take) = (query.Date, query.DaysInMonth, query.GroupId, query.PageNumber, query.PageSize);

            if (date.Equals(default)) return BadRequest(new { message = "ادخل تاريخ صحيح" });

            var prayersPerDay = await _db.Prayers.AsNoTracking().CountAsync();
            var totalPossible = daysInMonth * prayersPerDay;

            var start = date;
            var end = start.AddDays(daysInMonth);
            Console.WriteLine(start);
            Console.WriteLine(end);

            var studentsQuery = _db.Students.AsNoTracking();

            if (groupId.HasValue)
                studentsQuery = studentsQuery.Where(s => s.GroupId == groupId);

            var students = await studentsQuery.Select(s => new { s.Id, s.Name, GroupName = s.Group != null ? s.Group.Name : null }).Skip((skip - 1) * take).Take(take).ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            var attendances = await _db.StudentPrayerAttendances
                .Where(a =>
                    a.PrayerDate >= start &&
                    a.PrayerDate < end &&
                    studentIds.Contains(a.StudentId))
                .AsNoTracking()
                .Select(s => new { s.StudentId, s.PrayerDate, s.RakatsCount, s.IsInGroup })
                .ToListAsync();

            var groupedByStudent = attendances
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var report = new PrayersMonthlyReportDTO
            {
                TotalPossiblePrayersPerStudent = totalPossible,
            };

            var studentStats = new List<StudentPrayerMonthlyStatsDTO>();

            foreach (var student in students)
            {
                groupedByStudent.TryGetValue(student.Id, out var studentAttendances);
                studentAttendances ??= [];

                var totalPrayed = studentAttendances.Count(p => p.RakatsCount > 0);
                var totalGroup = studentAttendances.Count(a => a.IsInGroup);

                var missedPrayers = totalPossible - totalPrayed;

                var daysGrouped = studentAttendances
                    .GroupBy(a => a.PrayerDate)
                    .ToDictionary(g => g.Key, g => g.Count());

                var totalPercentage = totalPossible == 0
                    ? 0
                    : Math.Round((double)totalPrayed * 100 / totalPossible, 2);

                var groupPercentage = totalPossible == 0
                    ? 0
                    : Math.Round((double)totalGroup * 100 / totalPossible, 2);

                var missedPercentage = totalPossible == 0
                    ? 0
                    : Math.Round((double)missedPrayers * 100 / totalPossible, 2);

                studentStats.Add(new StudentPrayerMonthlyStatsDTO
                {
                    StudentId = student.Id,
                    StudentName = student.Name,
                    GroupName = student.GroupName,
                    TotalPrayed = totalPrayed,
                    TotalPrayedPercentage = totalPercentage,
                    TotalGroupPrayed = totalGroup,
                    GroupPercentage = groupPercentage,
                    MissedPrayers = missedPrayers, 
                    MissedPercentage = missedPercentage,
                });
            }

            report.Students = [.. studentStats.OrderByDescending(s => s.GroupPercentage)];


            report.AverageCommitmentPercentage =
                studentStats.Count == 0
                    ? 0
                    : Math.Round(studentStats.Average(s => s.GroupPercentage), 2);

            var totalPossibleAllStudents = totalPossible * studentStats.Count;

            report.AverageCommitmentPercentage =
                studentStats.Count == 0
                    ? 0
                    : Math.Round(studentStats.Average(s => s.GroupPercentage), 2);

            return Ok(report);
        }
    }
}
