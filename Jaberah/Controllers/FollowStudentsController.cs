using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.FollowStudents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jaberah.Controllers
{
    [Route("api/follow-students")]
    [ApiController]
    public class FollowStudentsController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [HttpGet("students/{studentId}/for-day")]
        public async Task<IActionResult> GetFollowStudentForStudentForDay([FromRoute] int studentId, [FromQuery] DateTime date)
        {
            if (date.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            if (studentId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            // Single round-trip: project everything from Student as the root
            var result = await _db.Students
                .AsNoTracking()
                .Where(s => s.Id == studentId)
                .Select(s => new
                {
                    StudentName = s.Name,

                    Attendance = s.StudentAttendances!
                        .Where(a => a.Date.Equals(date))
                        .Select(a => new { a.Attendance, a.Behavior })
                        .FirstOrDefault(),

                    Save = s.SaveLessons!
                        .Where(l => l.Date.Equals(date))
                        .Select(l => new
                        {
                            l.SurahFrom,
                            l.SurahTo,
                            l.VerseFrom,
                            l.VerseTo,
                            l.Pages,
                            l.Rate,
                            l.Notes
                        })
                        .FirstOrDefault(),

                    Review = s.ReviewLessons!
                        .Where(l => l.Date.Equals(date))
                        .Select(l => new
                        {
                            l.SurahFrom,
                            l.SurahTo,
                            l.VerseFrom,
                            l.VerseTo,
                            l.Pages,
                            l.Rate,
                            l.Notes
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            // Student not found
            if (result is null)
                return BadRequest(new { message = "لايوجد طالب" });

            return Ok(new GetFollowStudentForDay
            {
                StudentName = result.StudentName,
                Attendance = result.Attendance?.Attendance ?? 0,
                Behavior = result.Attendance?.Behavior ?? 0,

                SurahFromTeacher = result.Save?.SurahFrom ?? "",
                SurahToTeacher = result.Save?.SurahTo ?? "",
                VerseFromTeacher = result.Save?.VerseFrom ?? 1,
                VerseToTeacher = result.Save?.VerseTo ?? 1,
                PagesTeacher = result.Save?.Pages ?? 0f,
                RateTeacher = result.Save?.Rate ?? "",

                SurahFromFriend = result.Review?.SurahFrom ?? "",
                SurahToFriend = result.Review?.SurahTo ?? "",
                VerseFromFriend = result.Review?.VerseFrom ?? 1,
                VerseToFriend = result.Review?.VerseTo ?? 1,
                PagesFriend = result.Review?.Pages ?? 0f,
                RateFriend = result.Review?.Rate ?? "",

                Notes = result.Save?.Notes ?? result.Review?.Notes ?? ""
            });
        }

        [HttpGet("students/{studentId}/for-month")]
        public async Task<IActionResult> GetFollowStudentForStudentForMonth([FromRoute] int studentId, [FromQuery] DateTime date)
        {
            if (date.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            if (studentId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            // Calculate the first and last day of the month
            var fromDate = new DateTime(date.Year, date.Month, 1);
            var toDate = fromDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

            // Single query — student check folded in, returns null if student not found
            var studentExists = await _db.Students
                .AsNoTracking()
                .AnyAsync(x => x.Id == studentId);

            if (!studentExists)
                return BadRequest(new { message = "لايوجد طالب" });

            // Single round-trip: fetch all three tables for the date range at once
            var attendances = await _db.StudentAttendances
                .AsNoTracking()
                .Where(x => x.StudentId == studentId
                         && x.Date >= fromDate
                         && x.Date < toDate)
                .Select(x => new { x.Date.Day, x.Attendance, x.Behavior })
                .ToListAsync();

            var saveLessons = await _db.SaveLessons
                .AsNoTracking()
                .Where(x => x.StudentId == studentId
                         && x.Date >= fromDate
                         && x.Date < toDate)
                .Select(x => new
                {
                    x.Date.Day,
                    x.SurahFrom,
                    x.SurahTo,
                    x.VerseFrom,
                    x.VerseTo,
                    x.Rate,
                    x.Notes
                })
                .ToListAsync();

            var reviewLessons = await _db.ReviewLessons
                .AsNoTracking()
                .Where(x => x.StudentId == studentId
                         && x.Date >= fromDate
                         && x.Date < toDate)
                .Select(x => new
                {
                    x.Date.Day,
                    x.SurahFrom,
                    x.SurahTo,
                    x.VerseFrom,
                    x.VerseTo,
                    x.Rate,
                    x.Notes
                })
                .ToListAsync();

            // Convert to dictionaries for O(1) lookup per day instead of O(n) FirstOrDefault
            var attendanceMap = attendances.ToDictionary(x => x.Day);
            var saveLessonMap = saveLessons.ToDictionary(x => x.Day);
            var reviewLessonMap = reviewLessons.ToDictionary(x => x.Day);

            // Build result for every day in the month
            var result = Enumerable.Range(1, daysInMonth).Select(day =>
            {
                attendanceMap.TryGetValue(day, out var a);
                saveLessonMap.TryGetValue(day, out var s);
                reviewLessonMap.TryGetValue(day, out var r);

                return new GetFollowStudentForMonth
                {
                    Day = day,
                    Attendance = a?.Attendance ?? 0,
                    Behavior = a?.Behavior ?? 0,

                    SurahFromTeacher = s?.SurahFrom ?? "",
                    SurahToTeacher = s?.SurahTo ?? "",
                    VerseFromTeacher = s?.VerseFrom ?? 1,
                    VerseToTeacher = s?.VerseTo ?? 1,
                    PagesTeacher = 0,
                    RateTeacher = s?.Rate ?? "",

                    SurahFromFriend = r?.SurahFrom ?? "",
                    SurahToFriend = r?.SurahTo ?? "",
                    VerseFromFriend = r?.VerseFrom ?? 1,
                    VerseToFriend = r?.VerseTo ?? 1,
                    PagesFriend = 0,
                    RateFriend = r?.Rate ?? "",

                    Notes = s?.Notes ?? r?.Notes ?? ""
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("groups/{groupId}/for-day")]
        public async Task<IActionResult> GetFollowStudentsForGroupForDay([FromRoute] int groupId, [FromQuery] DateTime date, [FromQuery] string searchText = "")
        {
            if (date == default)
                return BadRequest(new { message = "ادخل تاريخ صحيح" });
            if (groupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            var raw = await _db.Students
                .AsNoTracking()
                .Where(s => s.GroupId == groupId && s.Name.Contains(searchText))
                .Select(s => new
                {
                    s.Id,
                    s.Name,

                    DayAttendance = s.StudentAttendances!
                        .Where(a => a.Date.Equals(date))
                        .FirstOrDefault(),

                    SaveLesson = s.SaveLessons!
                        .Where(l => l.Date.Equals(date))
                        .FirstOrDefault(),

                    ReviewLesson = s.ReviewLessons!
                        .Where(l => l.Date.Equals(date))
                        .FirstOrDefault(),
                })
                .ToListAsync();

            var result = raw.Select(s => new GetFollowStudentForDay
            {
                StudentId = s.Id,
                StudentName = s.Name,

                Attendance = s.DayAttendance?.Attendance ?? default,
                Behavior = s.DayAttendance?.Behavior ?? default,

                SurahFromTeacher = s.SaveLesson?.SurahFrom ?? "",
                SurahToTeacher = s.SaveLesson?.SurahTo ?? "",
                VerseFromTeacher = s.SaveLesson?.VerseFrom ?? 1,
                VerseToTeacher = s.SaveLesson?.VerseTo ?? 1,
                PagesTeacher = s.SaveLesson?.Pages ?? 0f,
                RateTeacher = s.SaveLesson?.Rate ?? "",

                SurahFromFriend = s.ReviewLesson?.SurahFrom ?? "",
                SurahToFriend = s.ReviewLesson?.SurahTo ?? "",
                VerseFromFriend = s.ReviewLesson?.VerseFrom ?? 1,
                VerseToFriend = s.ReviewLesson?.VerseTo ?? 1,
                PagesFriend = s.SaveLesson?.Pages ?? 0f,
                RateFriend = s.ReviewLesson?.Rate ?? "",

                Notes = s.SaveLesson?.Notes
                     ?? s.ReviewLesson?.Notes
                     ?? "",
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> UpsertFollowStudent([FromQuery] DateTime date,[FromBody] UpsertFollowStudentsDTO model)
        {
            if (date.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            if (model.StudentId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Students.AnyAsync(x => x.Id == model.StudentId))
                return BadRequest(new { message = "لايوجد طالب" });

            var saveLesson = await _db.SaveLessons
                .FirstOrDefaultAsync(x => x.StudentId == model.StudentId
                                       && x.Date.Equals(date));

            var reviewLesson = await _db.ReviewLessons
                .FirstOrDefaultAsync(x => x.StudentId == model.StudentId
                                       && x.Date.Equals(date));

            // --- SaveLesson (WithTeacher) ---
            if (saveLesson is not null)
            {
                saveLesson.SurahFrom = model.SurahFromTeacher ?? saveLesson.SurahFrom;
                saveLesson.SurahTo = model.SurahToTeacher ?? saveLesson.SurahTo;
                saveLesson.VerseFrom = model.VerseFromTeacher ?? saveLesson.VerseFrom;
                saveLesson.VerseTo = model.VerseToTeacher ?? saveLesson.VerseTo;
                saveLesson.Pages = model.PagesTeacher ?? saveLesson.Pages;
                saveLesson.Rate = model.RateTeacher ?? saveLesson.Rate;
                saveLesson.Notes = model.Notes ?? saveLesson.Notes;
            }
            else
            {
                await _db.SaveLessons.AddAsync(new SaveLesson
                {
                    StudentId = model.StudentId,
                    Date = date,
                    SurahFrom = model.SurahFromTeacher ?? "",
                    SurahTo = model.SurahToTeacher ?? "",
                    VerseFrom = model.VerseFromTeacher ?? 1,
                    VerseTo = model.VerseToTeacher ?? 1,
                    Rate = model.RateTeacher ?? "",
                    Pages = model.PagesTeacher ?? 0,
                    Notes = model.Notes
                });
            }

            // --- ReviewLesson (WithFriend) ---
            if (reviewLesson is not null)
            {
                reviewLesson.SurahFrom = model.SurahFromFriend ?? reviewLesson.SurahFrom;
                reviewLesson.SurahTo = model.SurahToFriend ?? reviewLesson.SurahTo;
                reviewLesson.VerseFrom = model.VerseFromFriend ?? reviewLesson.VerseFrom;
                reviewLesson.VerseTo = model.VerseToFriend ?? reviewLesson.VerseTo;
                reviewLesson.Rate = model.RateFriend ?? reviewLesson.Rate;
                reviewLesson.Pages = model.PagesFriend ?? reviewLesson.Pages;
                reviewLesson.Notes = model.Notes ?? reviewLesson.Notes;
            }
            else
            {
                await _db.ReviewLessons.AddAsync(new ReviewLesson
                {
                    StudentId = model.StudentId,
                    Date = date,
                    SurahFrom = model.SurahFromFriend ?? "",
                    SurahTo = model.SurahToFriend ?? "",
                    VerseFrom = model.VerseFromFriend ?? 1,
                    VerseTo = model.VerseToFriend ?? 1,
                    Rate = model.RateFriend ?? "",
                    Pages = model.PagesFriend ?? 0,
                    Notes = model.Notes
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "تم حفظ البيانات بنجاح" });
        }

        [HttpPost("attendance")]
        public async Task <IActionResult> UpsertAttendanceAndBehavior([FromQuery] DateTime date, [FromBody] UpsertAttendanceAndBehaviorDTO model)
        {
            if (date.Equals(default))
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            if (model.StudentId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Students.AnyAsync(x => x.Id == model.StudentId))
                return BadRequest(new { message = "لايوجد طالب" });
            var attendance = await _db.StudentAttendances
                .FirstOrDefaultAsync(x => x.StudentId == model.StudentId
                                       && x.Date.Equals(date));
            if (attendance is not null)
            {
                attendance.Attendance = Math.Clamp(model.Attendance ?? attendance.Attendance, 0, 1);
                attendance.Behavior = Math.Clamp(model.Behavior ?? attendance.Behavior, 0, 1);
            }
            else
            {
                await _db.StudentAttendances.AddAsync(new StudentAttendance
                {
                    StudentId = model.StudentId,
                    Date = date,
                    Attendance = Math.Clamp(model.Attendance ?? 0, 0, 1),
                    Behavior = Math.Clamp(model.Behavior ?? 0, 0, 1)
                });
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم حفظ البيانات بنجاح" });
        }
    }
}
