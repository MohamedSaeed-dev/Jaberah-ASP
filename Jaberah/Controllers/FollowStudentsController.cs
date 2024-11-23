using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.FollowStudents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }
            if (!await _db.Students.AnyAsync(x => x.Id == studentId))
            {
                return BadRequest(new { message = "لايوجد طالب" });
            }
            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime parsedDate = hijriCalendar.ToDateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);
            var followStudentQuery = await _db.FollowStudentsInMonth.AsNoTracking().Where(x => x.StudentId == studentId && parsedDate == x.Date).SelectMany(y => y.FollowStudentInMonthRows
                .Select(x => new GetFollowStudentForDay
                {
                    StudentName = y.Student.StudentName,
                    Attendance = x.Attendance,
                    Behavior = x.Behavior,

                    SurahFromTeacher = x.WithTeacher.From.SurahName,
                    SurahToTeacher = x.WithTeacher.To.SurahName,
                    VerseFromTeacher = x.WithTeacher.From.Verse,
                    VerseToTeacher = x.WithTeacher.To.Verse,
                    PagesTeacher = x.WithTeacher.Pages,
                    RateTeacher = x.WithTeacher.Rate,

                    SurahFromFriend = x.WithFriend.From.SurahName,
                    SurahToFriend = x.WithFriend.To.SurahName,
                    VerseFromFriend = x.WithFriend.From.Verse,
                    VerseToFriend = x.WithFriend.To.Verse,
                    PagesFriend = x.WithFriend.Pages,
                    RateFriend = x.WithFriend.Rate,
                })).FirstOrDefaultAsync();

            if (followStudentQuery is null)
            {
                return Ok(new GetFollowStudentForDay
                {
                    StudentName = (await _db.Students.AsNoTracking().Where(x => x.Id == studentId).Select(x => x.StudentName).FirstOrDefaultAsync()) ?? "",
                    Attendance = 0,
                    Behavior = 0,

                    SurahFromTeacher = "",
                    SurahToTeacher = "",
                    VerseFromTeacher = 1,
                    VerseToTeacher = 1,
                    PagesTeacher = 0,
                    RateTeacher = "",

                    SurahFromFriend = "",
                    SurahToFriend = "",
                    VerseFromFriend = 1,
                    VerseToFriend = 1,
                    PagesFriend = 0,
                    RateFriend = "",
                });
            }

            return Ok(followStudentQuery);

        }

        [HttpGet("students/{studentId}/for-month")]
        public async Task<IActionResult> GetFollowStudentForStudentForMonth([FromRoute] int studentId, [FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            if (!await _db.Students.AnyAsync(x => x.Id == studentId))
            {
                return BadRequest(new { message = "لايوجد طالب" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);
            int daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = hijriCalendar.ToDateTime(year, month, daysInMonth, 23, 59, 59, 0);

            var followStudentData = await _db.FollowStudentsInMonth.AsNoTracking()
                .Where(x => x.StudentId == studentId && x.Date >= fromDate && x.Date <= toDate)
                .SelectMany(x => x.FollowStudentInMonthRows.Select(row => new GetFollowStudentForMonth
                {
                    Day = row.Day,
                    Attendance = row.Attendance,
                    Behavior = row.Behavior,

                    SurahFromTeacher = row.WithTeacher.From.SurahName ?? "",
                    SurahToTeacher = row.WithTeacher.To.SurahName ?? "",
                    VerseFromTeacher = row.WithTeacher.From.Verse,
                    VerseToTeacher = row.WithTeacher.To.Verse,
                    PagesTeacher = row.WithTeacher.Pages,
                    RateTeacher = row.WithTeacher.Rate ?? "",

                    SurahFromFriend = row.WithFriend.From.SurahName ?? "",
                    SurahToFriend = row.WithFriend.To.SurahName ?? "",
                    VerseFromFriend = row.WithFriend.From.Verse,
                    VerseToFriend = row.WithFriend.To.Verse,
                    PagesFriend = row.WithFriend.Pages,
                    RateFriend = row.WithFriend.Rate ?? ""
                }))
                .ToListAsync();

            var result = Enumerable.Range(1, daysInMonth).Select(day =>
            {
                var existing = followStudentData.FirstOrDefault(x => x.Day == day);
                return existing ?? new GetFollowStudentForMonth
                {
                    Day = day,
                    Attendance = 0,
                    Behavior = 0,

                    SurahFromTeacher = "",
                    SurahToTeacher = "",
                    VerseFromTeacher = 1,
                    VerseToTeacher = 1,
                    PagesTeacher = 0,
                    RateTeacher = "",

                    SurahFromFriend = "",
                    SurahToFriend = "",
                    VerseFromFriend = 1,
                    VerseToFriend = 1,
                    PagesFriend = 0,
                    RateFriend = ""
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("groups/{groupId}/for-day")]
        public async Task<IActionResult> GetFollowStudentsForGroupForDay([FromRoute] int groupId, [FromQuery] DateTime date)
        {
            if (date == default)
            {
                return BadRequest(new { message = "ادخل تاريخ صحيح" });
            }

            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
            {
                return BadRequest(new { message = "لاتوجد حلقة" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime parsedDate = hijriCalendar.ToDateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);

            var followStudents = await _db.Students.AsNoTracking()
                .Where(student => student.GroupId == groupId)
                .Select(student => new
                {
                    student.Id,
                    student.StudentName,
                    FollowDetails = _db.FollowStudentsInMonth.AsNoTracking()
                        .Where(f => f.StudentId == student.Id && f.Date == parsedDate)
                        .SelectMany(f => f.FollowStudentInMonthRows.Select(row => new GetFollowStudentForDay
                        {
                            Id = row.Id,
                            StudentName = student.StudentName,
                            Attendance = row.Attendance,
                            Behavior = row.Behavior,

                            SurahFromTeacher = row.WithTeacher.From.SurahName ?? "",
                            SurahToTeacher = row.WithTeacher.To.SurahName ?? "",
                            VerseFromTeacher = row.WithTeacher.From.Verse,
                            VerseToTeacher = row.WithTeacher.To.Verse,
                            PagesTeacher = row.WithTeacher.Pages,
                            RateTeacher = row.WithTeacher.Rate ?? "",

                            SurahFromFriend = row.WithFriend.From.SurahName ?? "",
                            SurahToFriend = row.WithFriend.To.SurahName ?? "",
                            VerseFromFriend = row.WithFriend.From.Verse,
                            VerseToFriend = row.WithFriend.To.Verse,
                            PagesFriend = row.WithFriend.Pages,
                            RateFriend = row.WithFriend.Rate ?? ""
                        })).ToList()
                }).ToListAsync();

            var result = followStudents.SelectMany(student =>
            {
                if (student.FollowDetails.Any())
                {
                    return student.FollowDetails;
                }

                return new List<GetFollowStudentForDay>
                {
                    new GetFollowStudentForDay
                    {
                        Id = student.Id,
                        StudentName = student.StudentName,
                        Attendance = 0,
                        Behavior = 0,

                        SurahFromTeacher = "",
                        SurahToTeacher = "",
                        VerseFromTeacher = 1,
                        VerseToTeacher = 1,
                        PagesTeacher = 0,
                        RateTeacher = "",

                        SurahFromFriend = "",
                        SurahToFriend = "",
                        VerseFromFriend = 1,
                        VerseToFriend = 1,
                        PagesFriend = 0,
                        RateFriend = ""
                    }
                };
            }).ToList();

            return Ok(result);
        }


        [HttpPost]
        public async Task<IActionResult> UpsertFollowStudent([FromQuery] DateTime date, [FromBody] UpsertFollowStudentsDTO model)
        {
            if (date.Equals(default))
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            if (!await _db.Students.AnyAsync(x => x.Id == model.StudentId))
            {
                return BadRequest(new { message = "لايوجد طالب" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime parsedDate = hijriCalendar.ToDateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);

            var existingFollow = await _db.FollowStudentsInMonth
                .Include(f => f.FollowStudentInMonthRows).Include(x => x.Student)
                .Include(x => x.FollowStudentInMonthRows)
                .ThenInclude(y => y.WithTeacher)
                .ThenInclude(x => x.From)
                .Include(x => x.FollowStudentInMonthRows)
                .ThenInclude(x => x.WithTeacher)
                .ThenInclude(x => x.To)
                .Include(x => x.FollowStudentInMonthRows)
                .ThenInclude(y => y.WithFriend)
                .ThenInclude(x => x.From)
                .Include(x => x.FollowStudentInMonthRows)
                .ThenInclude(x => x.WithFriend)
                .ThenInclude(x => x.To)
                .Include(x => x.Exams)
                .FirstOrDefaultAsync(f => parsedDate == f.Date &&
                                           f.StudentId == model.StudentId);

            if (existingFollow != null)
            {
                UpdateFollowStudent(existingFollow, model, parsedDate.Day);
            }
            else
            {
                var newFollow = new FollowStudentInMonth
                {
                    Date = parsedDate,
                    StudentId = model.StudentId,
                    FollowStudentInMonthRows = new List<FollowStudentInMonthRow>
                    {
                        new()
                        {
                            Day = parsedDate.Day,
                            Attendance = model.Attendance ?? 0,
                            Behavior = model.Behavior ?? 0,
                            WithTeacher = new WithTeacherFriend
                            {
                                From = new Surah
                                {
                                    SurahName = model.SurahFromTeacher ?? "",
                                    Verse = model.VerseFromTeacher ?? 0
                                },
                                To = new Surah
                                {
                                    SurahName = model.SurahToTeacher ?? "",
                                    Verse = model.VerseToTeacher ?? 0
                                },
                                Rate = model.RateTeacher ?? "",
                                Pages = model.PagesTeacher ?? 0
                            },
                            WithFriend = new WithTeacherFriend
                            {
                                From = new Surah
                                {
                                    SurahName = model.SurahFromFriend ?? "",
                                    Verse = model.VerseFromFriend ?? 0
                                },
                                To = new Surah
                                {
                                    SurahName = model.SurahToFriend ?? "",
                                    Verse = model.VerseToFriend ?? 0
                                },
                                Rate = model.RateFriend ?? "",
                                Pages = model.PagesFriend ?? 0
                            }
                        }
                    }
                };
                await _db.FollowStudentsInMonth.AddAsync(newFollow);
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "تم حفظ البيانات بنجاح" });
        }

        private void UpdateFollowStudent(FollowStudentInMonth existingFollow, UpsertFollowStudentsDTO model, int day)
        {
            var row = existingFollow.FollowStudentInMonthRows.FirstOrDefault(r => r.Day == day);
            if (row is not null)
            {
                UpdateFollowStudentRow(row, model);
            }
            else
            {
                existingFollow.FollowStudentInMonthRows.Add(CreateFollowStudentRow(model, existingFollow.Id, day));
            }
        }

        private FollowStudentInMonthRow CreateFollowStudentRow(UpsertFollowStudentsDTO model, int followId, int day)
        {
            return new FollowStudentInMonthRow
            {
                Day = day,
                FollowStudentInMonthId = followId,
                Attendance = model.Attendance ?? 0,
                Behavior = model.Behavior ?? 0,
                WithTeacher = new WithTeacherFriend
                {
                    FromId = model.VerseFromTeacher ?? 0,
                    ToId = model.VerseToTeacher ?? 0,
                    Rate = model.RateTeacher ?? "",
                    Pages = model.PagesTeacher ?? 0
                },
                WithFriend = new WithTeacherFriend
                {
                    FromId = model.VerseFromFriend ?? 0,
                    ToId = model.VerseToFriend ?? 0,
                    Rate = model.RateFriend ?? "",
                    Pages = model.PagesFriend ?? 0
                }
            };
        }

        private void UpdateFollowStudentRow(FollowStudentInMonthRow row, UpsertFollowStudentsDTO model)
        {
            row.Attendance = model.Attendance ?? row.Attendance;
            row.Behavior = model.Behavior ?? row.Behavior;

            row.WithTeacher.From.SurahName = model.SurahFromTeacher ?? row.WithTeacher.From.SurahName;
            row.WithTeacher.To.SurahName = model.SurahToTeacher ?? row.WithTeacher.To.SurahName;
            row.WithTeacher.From.Verse = model.VerseFromTeacher ?? row.WithTeacher.From.Verse;
            row.WithTeacher.To.Verse = model.VerseToTeacher ?? row.WithTeacher.To.Verse;

            row.WithTeacher.Rate = model.RateTeacher ?? row.WithTeacher.Rate;
            row.WithTeacher.Pages = model.PagesTeacher ?? row.WithTeacher.Pages;

            row.WithFriend.From.SurahName = model.SurahFromTeacher ?? row.WithFriend.From.SurahName;
            row.WithFriend.To.SurahName = model.SurahToTeacher ?? row.WithFriend.To.SurahName;
            row.WithFriend.From.Verse = model.VerseFromTeacher ?? row.WithFriend.From.Verse;
            row.WithFriend.To.Verse = model.VerseToTeacher ?? row.WithFriend.To.Verse;

            row.WithFriend.Rate = model.RateFriend ?? row.WithFriend.Rate;
            row.WithFriend.Pages = model.PagesFriend ?? row.WithFriend.Pages;
        }
    }
}
