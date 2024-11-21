using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jaberah.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        public ReportsController(JaberahDBContext db)
        {
            _db = db;
        }
        [HttpGet("semester-report")]
        public async Task<IActionResult> GetSemesterReport([FromQuery] int groupId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromGreg = hijriCalendar.ToDateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, 0);

            int daysInMonth = hijriCalendar.GetDaysInMonth(toDate.Year, toDate.Month);
            DateTime toGreg = hijriCalendar.ToDateTime(toDate.Year, toDate.Month, daysInMonth, 23, 59, 59, 0);

            var grouped = await _db.FollowStudentsInMonth
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromGreg && x.Date <= toGreg)
                .GroupBy(x => x.Student.StudentName)
                .Select(g => new
                {
                    StudentName = g.Key,
                    AttendanceSum = g.SelectMany(x => x.FollowStudentInMonthRows).Sum(r => r.Attendance),
                    BehaviorSum = g.SelectMany(x => x.FollowStudentInMonthRows).Sum(r => r.Behavior),
                    FollowRowCount = g.SelectMany(x => x.FollowStudentInMonthRows).Count(),
                    OralGradeSum = g.Sum(x => x.Exams.OralExam),
                    PaperGradeSum = g.Sum(x => x.Exams.PaperExam),
                })
                .ToListAsync();

            var result = grouped.Select(x => new SemesterReportViewModel
            {
                StudentName = x.StudentName,
                AttendanceSum = x.AttendanceSum,
                BehaviorSum = x.BehaviorSum,
                GradeSum = Math.Min(x.FollowRowCount * 0.5, 10.0),
                OralGradeSum = x.OralGradeSum,
                PaperGradeSum = x.PaperGradeSum,
                Total = ((x.AttendanceSum + x.BehaviorSum + x.OralGradeSum + x.PaperGradeSum + Math.Min(x.FollowRowCount * 0.5, 10.0)) * 100) / 400
            }).ToList();

            return Ok(result);
        }

        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] int groupId, [FromQuery] int year, [FromQuery] int month)
        {
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

            int daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = hijriCalendar.ToDateTime(year, month, daysInMonth, 23, 59, 59, 0);

            var grouped = await _db.FollowStudentsInMonth
    .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
    .Select(x => new
    {
        x.Student.StudentName,
        Save = x.FollowStudentInMonthRows
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
        Review = x.FollowStudentInMonthRows
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
        Attendance = x.FollowStudentInMonthRows.Sum(y => (float?)y.Attendance) ?? 0,
        Behavior = x.FollowStudentInMonthRows.Sum(y => (float?)y.Behavior) ?? 0,
        OralExam = x.Exams != null ? x.Exams.OralExam : 0,
        PaperExam = x.Exams != null ? x.Exams.PaperExam : 0,
    })
    .ToListAsync();

            var result = grouped.Select(x => new MonthlyReportViewModel
            {
                StudentName = x.StudentName,
                SaveData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Save.FirstOrDefault()!.FromSurah,
                        Verse = x.Save.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Save.LastOrDefault()!.FromSurah,
                        Verse = x.Save.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Save.Sum(y => y.Pages),
                    Rate = ""
                },
                ReviewData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Review.FirstOrDefault()!.FromSurah,
                        Verse = x.Review.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Review.LastOrDefault()!.FromSurah,
                        Verse = x.Review.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Review.Sum(y => y.Pages),
                    Rate = ""
                },
                AttendanceGrade = x.Attendance,
                BehaviorGrade = x.Behavior,
                OralGrade = x.OralExam,
                PaperGrade = x.PaperExam,
                Total = ((x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

            });
            return Ok(result);
        }

        [HttpGet("best-students-report")]
        public async Task<IActionResult> GetBestStudentsReport([FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }
            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

            int daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = hijriCalendar.ToDateTime(year, month, daysInMonth, 23, 59, 59, 0);

            var grouped = await _db.FollowStudentsInMonth
                .Where(x => x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Student.StudentName,
                    Save = x.FollowStudentInMonthRows.Select(y => new
                    {
                        FromSurah = y.WithTeacher.From.SurahName,
                        FromVerse = y.WithTeacher.From.Verse,
                        ToSuarh = y.WithTeacher.To.SurahName,
                        ToVerse = y.WithTeacher.To.Verse,
                        y.WithTeacher.Pages,
                        y.WithTeacher.Rate
                    }),
                    Review = x.FollowStudentInMonthRows.Select(y => new
                    {
                        FromSurah = y.WithFriend.From.SurahName,
                        FromVerse = y.WithFriend.From.Verse,
                        ToSuarh = y.WithFriend.To.SurahName,
                        ToVerse = y.WithFriend.To.Verse,
                        y.WithFriend.Pages,
                        y.WithFriend.Rate
                    }),
                    Attendance = x.FollowStudentInMonthRows.Sum(y => y.Attendance),
                    Behavior = x.FollowStudentInMonthRows.Sum(y => y.Behavior),
                    x.Exams.OralExam,
                    x.Exams.PaperExam,
                }).Take(take).ToListAsync();

            var result = grouped.Select(x => new MonthlyReportViewModel
            {
                StudentName = x.StudentName,
                SaveData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Save.FirstOrDefault()!.FromSurah,
                        Verse = x.Save.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Save.LastOrDefault()!.FromSurah,
                        Verse = x.Save.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Save.Sum(y => y.Pages),
                    Rate = ""
                },
                ReviewData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Review.FirstOrDefault()!.FromSurah,
                        Verse = x.Review.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Review.LastOrDefault()!.FromSurah,
                        Verse = x.Review.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Review.Sum(y => y.Pages),
                    Rate = ""
                },
                AttendanceGrade = x.Attendance,
                BehaviorGrade = x.Behavior,
                OralGrade = x.OralExam,
                PaperGrade = x.PaperExam,
                Total = ((x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

            }).OrderByDescending(x => x.Total);
            return Ok(result);
        }
        [HttpGet("best-students-for-group-report")]
        public async Task<IActionResult> GetBestStudentsForGroupReport([FromQuery] int groupId, [FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (year <= 0 || month <= 0)
            {
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });
            }

            HijriCalendar hijriCalendar = new HijriCalendar();
            DateTime fromDate = hijriCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);

            int daysInMonth = hijriCalendar.GetDaysInMonth(year, month);
            DateTime toDate = hijriCalendar.ToDateTime(year, month, daysInMonth, 23, 59, 59, 0);

            var grouped = await _db.FollowStudentsInMonth
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Student.StudentName,
                    Save = x.FollowStudentInMonthRows.Select(y => new
                    {
                        FromSurah = y.WithTeacher.From.SurahName,
                        FromVerse = y.WithTeacher.From.Verse,
                        ToSuarh = y.WithTeacher.To.SurahName,
                        ToVerse = y.WithTeacher.To.Verse,
                        y.WithTeacher.Pages,
                        y.WithTeacher.Rate
                    }),
                    Review = x.FollowStudentInMonthRows.Select(y => new
                    {
                        FromSurah = y.WithFriend.From.SurahName,
                        FromVerse = y.WithFriend.From.Verse,
                        ToSuarh = y.WithFriend.To.SurahName,
                        ToVerse = y.WithFriend.To.Verse,
                        y.WithFriend.Pages,
                        y.WithFriend.Rate
                    }),
                    Attendance = x.FollowStudentInMonthRows.Sum(y => y.Attendance),
                    Behavior = x.FollowStudentInMonthRows.Sum(y => y.Behavior),
                    x.Exams.OralExam,
                    x.Exams.PaperExam,
                }).Take(take).ToListAsync();

            var result = grouped.Select(x => new MonthlyReportViewModel
            {
                StudentName = x.StudentName,
                SaveData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Save.FirstOrDefault()!.FromSurah,
                        Verse = x.Save.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Save.LastOrDefault()!.FromSurah,
                        Verse = x.Save.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Save.Sum(y => y.Pages),
                    Rate = ""
                },
                ReviewData = new SaveReviewData
                {
                    From = new FromTo
                    {
                        SurahName = x.Review.FirstOrDefault()!.FromSurah,
                        Verse = x.Review.FirstOrDefault()!.FromVerse,
                    },
                    To = new FromTo
                    {
                        SurahName = x.Review.LastOrDefault()!.FromSurah,
                        Verse = x.Review.LastOrDefault()!.FromVerse,
                    },
                    Pages = x.Review.Sum(y => y.Pages),
                    Rate = ""
                },
                AttendanceGrade = x.Attendance,
                BehaviorGrade = x.Behavior,
                OralGrade = x.OralExam,
                PaperGrade = x.PaperExam,
                Total = ((x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100

            }).OrderByDescending(x => x.Total);
            return Ok(result);
        }



        class SemesterReportViewModel
        {
            public string StudentName { get; set; } = string.Empty;
            public double GradeSum { get; set; }
            public double AttendanceSum { get; set; }
            public double BehaviorSum { get; set; }
            public double OralGradeSum { get; set; }
            public double PaperGradeSum { get; set; }
            public double MidFinalGrade { get; set; }
            public double Total { get; set; }
        }

        class MonthlyReportViewModel
        {
            public string StudentName { get; set; } = string.Empty;
            public SaveReviewData SaveData { get; set; }
            public SaveReviewData ReviewData { get; set; }
            public double AttendanceGrade { get; set; }
            public double BehaviorGrade { get; set; }
            public double OralGrade { get; set; }
            public double PaperGrade { get; set; }
            public double Total { get; set; }
        }

        class SaveReviewData
        {
            public FromTo From { get; set; }
            public FromTo To { get; set; }
            public float Pages { get; set; }
            public string Rate { get; set; } = string.Empty;
        }
        class FromTo
        {
            public string SurahName { get; set; } = string.Empty;
            public int Verse { get; set; }
        }
    }

}
