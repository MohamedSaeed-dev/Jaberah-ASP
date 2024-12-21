using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jaberah.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportsController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [HttpGet("semester-report")]
        public async Task<IActionResult> GetSemesterReport([FromQuery] int groupId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (fromDate.Equals(default) || toDate.Equals(default))
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            int monthsDifference = (toDate.Year - fromDate.Year) * 12 + toDate.Month - fromDate.Month;

            if (monthsDifference != 4)
            {
                return BadRequest(new { message = "الفارق يجب ان يكون 4 اشهر" });
            }

            var report = (await _db.FollowStudents.AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Join(_db.MidFinals.Where(a => a.FromDate == fromDate && a.ToDate == toDate),
                      a => a.StudentId, b => b.StudentId, (a, b) => new
                      {
                          a.Student,
                          a.FollowStudentsRows,
                          a.Exams,
                          b.Grade
                      })
                .GroupBy(x => x.Student.StudentName)
                .Select(g => new
                {
                    StudentName = g.Key,
                    AttendanceSum = Math.Min( g.SelectMany(x => x.FollowStudentsRows).Sum(r => r.Attendance), 25),
                    BehaviorSum = Math.Min( g.SelectMany(x => x.FollowStudentsRows).Sum(r => r.Behavior),25),
                    GradeSum = Math.Min( g.SelectMany(x => x.FollowStudentsRows).Count() * 0.5, 10),
                    OralGradeSum = g.Sum(x => x.Exams.OralExam),
                    PaperGradeSum = g.Sum(x => x.Exams.PaperExam),
                    MidFinalGrade = g.Sum(x => x.Grade)
                }).ToListAsync())
                .Select(x => new SemesterReportForView
                {
                    StudentName = x.StudentName,
                    AttendanceSum = x.AttendanceSum,
                    BehaviorSum = x.BehaviorSum,
                    GradeSum = x.GradeSum,
                    OralGradeSum = x.OralGradeSum,
                    PaperGradeSum = x.PaperGradeSum,
                    MidFinalGrade = x.MidFinalGrade,
                    Total = (x.MidFinalGrade + x.AttendanceSum + x.BehaviorSum + x.OralGradeSum + x.PaperGradeSum + x.GradeSum) * 100 / 400
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return Ok(report);
        }


        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] int groupId, [FromQuery] int year, [FromQuery] int month)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            var daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth);

            var report = (await _db.FollowStudents.AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Student.StudentName,
                    Save = x.FollowStudentsRows
                        .Where(y => y.WithTeacher != null && y.WithTeacher.From != null && y.WithTeacher.To != null)
                        .OrderBy(y => y.WithTeacher.From.Verse) // Added ordering
                        .Select(y => new
                        {
                            FromSurah = y.WithTeacher.From.SurahName,
                            FromVerse = y.WithTeacher.From.Verse,
                            ToSurah = y.WithTeacher.To.SurahName,
                            ToVerse = y.WithTeacher.To.Verse,
                            y.WithTeacher.Pages,
                            y.WithTeacher.Rate
                        }),
                    Review = x.FollowStudentsRows
                        .Where(y => y.WithFriend != null && y.WithFriend.From != null && y.WithFriend.To != null)
                        .OrderBy(y => y.WithFriend.From.Verse) // Added ordering
                        .Select(y => new
                        {
                            FromSurah = y.WithFriend.From.SurahName,
                            FromVerse = y.WithFriend.From.Verse,
                            ToSurah = y.WithFriend.To.SurahName,
                            ToVerse = y.WithFriend.To.Verse,
                            y.WithFriend.Pages,
                            y.WithFriend.Rate
                        }),
                    SaveGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    ReviewGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    Attendance = Math.Min( x.FollowStudentsRows.Sum(y => y.Attendance), 25),
                    Behavior = Math.Min( x.FollowStudentsRows.Sum(y => y.Behavior), 25),
                    OralExam = x.Exams != null ? x.Exams.OralExam : 0,
                    PaperExam = x.Exams != null ? x.Exams.PaperExam : 0,
                })
                .ToListAsync())
                .Select(x => new GetMonthlyReportForView
                {
                    StudentName = x.StudentName,
                    SaveData = new SaveReviewData
                    {
                        From = new FromToData
                        {
                            SurahName = x.Save.FirstOrDefault()!.FromSurah,
                            Verse = x.Save.FirstOrDefault()!.FromVerse,
                        },
                        To = new FromToData
                        {
                            SurahName = x.Save.LastOrDefault()!.ToSurah,
                            Verse = x.Save.LastOrDefault()!.ToVerse,
                        },
                        Pages = x.Save.Sum(y => y.Pages),
                        Rate = ""
                    },
                    ReviewData = new SaveReviewData
                    {
                        From = new FromToData
                        {
                            SurahName = x.Review.FirstOrDefault()!.FromSurah,
                            Verse = x.Review.FirstOrDefault()!.FromVerse,
                        },
                        To = new FromToData
                        {
                            SurahName = x.Review.LastOrDefault()!.ToSurah,
                            Verse = x.Review.LastOrDefault()!.ToVerse,
                        },
                        Pages = x.Review.Sum(y => y.Pages),
                        Rate = ""
                    },
                    SaveGrade = x.SaveGrade,
                    ReviewGrade = x.ReviewGrade,
                    AttendanceGrade = x.Attendance,
                    BehaviorGrade = x.Behavior,
                    OralGrade = x.OralExam,
                    PaperGrade = x.PaperExam,
                    Total = ((x.SaveGrade + x.ReviewGrade + x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

                })
                .OrderByDescending(x => x.Total).ToList()
                .ToList();

            return Ok(report);
        }


        [HttpGet("best-students-report")]
        public async Task<IActionResult> GetBestStudentsReport([FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            var daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth);

            var grouped = await _db.FollowStudents.AsNoTracking()
                .Where(x => x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Student.StudentName,
                    x.Student.Group.GroupName,
                    Save = x.FollowStudentsRows
                    .Where(y => y.WithTeacher != null && y.WithTeacher.From != null && y.WithTeacher.To != null)
                    .Select(y => new
                    {
                        FromSurah = y.WithTeacher.From.SurahName,
                        FromVerse = y.WithTeacher.From.Verse,
                        ToSurah = y.WithTeacher.To.SurahName,
                        ToVerse = y.WithTeacher.To.Verse,
                        y.WithTeacher.Pages,
                        y.WithTeacher.Rate
                    }),
                    Review = x.FollowStudentsRows
                    .Where(y => y.WithFriend != null && y.WithFriend.From != null && y.WithFriend.To != null)
                    .Select(y => new
                    {
                        FromSurah = y.WithFriend.From.SurahName,
                        FromVerse = y.WithFriend.From.Verse,
                        ToSurah = y.WithFriend.To.SurahName,
                        ToVerse = y.WithFriend.To.Verse,
                        y.WithFriend.Pages,
                        y.WithFriend.Rate
                    }),
                    SaveGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    ReviewGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    Attendance = Math.Min( x.FollowStudentsRows.Sum(y => y.Attendance), 25),
                    Behavior = Math.Min( x.FollowStudentsRows.Sum(y => y.Behavior), 25),
                    OralExam = x.Exams != null ? x.Exams.OralExam : 0,
                    PaperExam = x.Exams != null ? x.Exams.PaperExam : 0,
                }).Take(take).ToListAsync();

            var result = grouped.Select(x => new GetBestStudentsReportForView
            {
                StudentName = x.StudentName,
                GroupName = x.GroupName,
                SaveGrade = x.SaveGrade,
                ReviewGrade = x.ReviewGrade,
                AttendanceGrade = x.Attendance,
                BehaviorGrade = x.Behavior,
                OralGrade = x.OralExam,
                PaperGrade = x.PaperExam,
                Total = ((x.SaveGrade + x.ReviewGrade + x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

            }).OrderByDescending(x => x.Total).ToList();
            return Ok(result);
        }
        [HttpGet("best-students-for-group-report")]
        public async Task<IActionResult> GetBestStudentsForGroupReport([FromQuery] int groupId, [FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            var daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth);

            var grouped = await _db.FollowStudents.AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Student.StudentName,
                    Save = x.FollowStudentsRows
                    .Where(y => y.WithTeacher != null && y.WithTeacher.From != null && y.WithTeacher.To != null)
                    .Select(y => new
                    {
                        FromSurah = y.WithTeacher.From.SurahName,
                        FromVerse = y.WithTeacher.From.Verse,
                        ToSurah = y.WithTeacher.To.SurahName,
                        ToVerse = y.WithTeacher.To.Verse,
                        y.WithTeacher.Pages,
                        y.WithTeacher.Rate
                    }),
                    Review = x.FollowStudentsRows
                    .Where(y => y.WithFriend != null && y.WithFriend.From != null && y.WithFriend.To != null)
                    .Select(y => new
                    {
                        FromSurah = y.WithFriend.From.SurahName,
                        FromVerse = y.WithFriend.From.Verse,
                        ToSurah = y.WithFriend.To.SurahName,
                        ToVerse = y.WithFriend.To.Verse,
                        y.WithFriend.Pages,
                        y.WithFriend.Rate
                    }),
                    SaveGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    ReviewGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    Attendance = Math.Min( x.FollowStudentsRows.Sum(y => y.Attendance),25),
                    Behavior = Math.Min( x.FollowStudentsRows.Sum(y => y.Behavior), 25),
                    OralExam = x.Exams != null ? x.Exams.OralExam : 0,
                    PaperExam = x.Exams != null ? x.Exams.PaperExam : 0,
                }).Take(take).ToListAsync();

            var result = grouped.Select(x => new GetBestStudentsReportForView
            {
                StudentName = x.StudentName,
                GroupName = null,
                SaveGrade = x.SaveGrade,
                ReviewGrade = x.ReviewGrade,
                AttendanceGrade = x.Attendance,
                BehaviorGrade = x.Behavior,
                OralGrade = x.OralExam,
                PaperGrade = x.PaperExam,
                Total = ((x.SaveGrade + x.ReviewGrade + x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

            }).OrderByDescending(x => x.Total).ToList();
            return Ok(result);
        }
    }

}
