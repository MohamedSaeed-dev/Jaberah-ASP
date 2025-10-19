using Jaberah.Models.JaberahModels;
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
            if (groupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });

            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });

            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            int monthsDifference = (toDate.Year - fromDate.Year) * 12 + toDate.Month - fromDate.Month + 1;
            if (monthsDifference != 4)
                return BadRequest(new { message = "الفارق يجب ان يكون 4 اشهر" });

            var followData = await _db.FollowStudents
                .AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(f => new
                {
                    StudentId = f.Student.Id,
                    f.Student.StudentName,
                    f.Date,
                    FollowRows = f.FollowStudentsRows.Select(r => new
                    {
                        r.Attendance,
                        r.Behavior
                    }).ToList(),
                    Exams = f.Exams != null ? new
                    {
                        f.Exams.OralExam,
                        f.Exams.PaperExam
                    } : null
                })
                .ToListAsync();

            var midFinals = await _db.MidFinals
                .AsNoTracking()
                .Where(mf => mf.FromDate == fromDate && mf.ToDate == toDate)
                .ToDictionaryAsync(mf => mf.StudentId, mf => mf.Grade);

            var report = followData
                .GroupBy(f => f.StudentId)
                .Select(g =>
                {
                    var studentName = g.First().StudentName;
                    var allRows = g.SelectMany(f => f.FollowRows).ToList();

                    double attendance = Math.Min(allRows.Sum(r => r.Attendance), 100);
                    double behavior = Math.Min(allRows.Sum(r => r.Behavior), 100);

                    var monthlyGrades = g
                        .SelectMany(f => f.FollowRows, (f, row) => new { f.Date, Row = row })
                        .GroupBy(x => new { x.Date.Year, x.Date.Month })
                        .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month)
                        .Select(monthGroup =>
                        {
                            int rowCountForMonth = monthGroup.Count();
                            return Math.Min(rowCountForMonth * 0.5, 10.0);
                        })
                        .ToList();

                    double grade = monthlyGrades.Sum();
                    double oral = Math.Min(g.Sum(x => x.Exams?.OralExam ?? 0), 40);
                    double paper = Math.Min(g.Sum(x => x.Exams?.PaperExam ?? 0), 80);
                    double total = (attendance + behavior + grade + oral + paper) * 100 / 400;

                    midFinals.TryGetValue(g.Key, out float midFinalGrade);

                    return new SemesterReportForView
                    {
                        StudentId = g.Key,
                        StudentName = studentName,
                        AttendanceSum = Math.Round(attendance, 2),
                        BehaviorSum = Math.Round(behavior, 2),
                        GradeSum = Math.Round(grade, 2),
                        OralGradeSum = Math.Round(oral, 2),
                        PaperGradeSum = Math.Round(paper, 2),
                        MidFinalGrade = Math.Round(midFinalGrade, 2),
                        Total = Math.Round(total, 2)
                    };
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

            // 1. Get group books
            var groupBooks = await _db.Books
                .AsNoTracking()
                .Where(b => b.GroupId == groupId && b.Month >= fromDate && b.Month <= toDate)
                .Select(b => new BooksData
                {
                    Id = b.Id,
                    Title = b.Title ?? string.Empty,
                    Month = b.Month,
                    From = b.From ?? string.Empty,
                    To = b.To ?? string.Empty
                })
                .ToListAsync();

            // 2. Get students' monthly reports
            var students = await _db.FollowStudents
                .AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.Id,
                    StudentName = x.Student.StudentName ?? string.Empty,
                    Save = x.FollowStudentsRows
                        .Where(y => y.WithTeacher != null && y.WithTeacher.From != null && !string.IsNullOrWhiteSpace(y.WithTeacher.From.SurahName) && y.WithTeacher.To != null && !string.IsNullOrWhiteSpace(y.WithTeacher.To.SurahName))
                        .OrderBy(y => y.WithTeacher.From.Verse)
                        .Select(y => new
                        {
                            FromSurah = y.WithTeacher.From.SurahName ?? string.Empty,
                            FromVerse = y.WithTeacher.From.Verse,
                            ToSurah = y.WithTeacher.To.SurahName ?? string.Empty,
                            ToVerse = y.WithTeacher.To.Verse,
                            Pages = y.WithTeacher.Pages,
                            Rate = y.WithTeacher.Rate ?? string.Empty
                        }),
                    Review = x.FollowStudentsRows
                        .Where(y => y.WithFriend != null && y.WithFriend.From != null && !string.IsNullOrWhiteSpace(y.WithFriend.From.SurahName) && y.WithFriend.To != null && !string.IsNullOrWhiteSpace(y.WithFriend.To.SurahName))
                        .OrderBy(y => y.WithFriend.From.Verse)
                        .Select(y => new
                        {
                            FromSurah = y.WithFriend.From.SurahName ?? string.Empty,
                            FromVerse = y.WithFriend.From.Verse,
                            ToSurah = y.WithFriend.To.SurahName ?? string.Empty,
                            ToVerse = y.WithFriend.To.Verse,
                            Pages = y.WithFriend.Pages,
                            Rate = y.WithFriend.Rate ?? string.Empty
                        }),
                    SaveGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    ReviewGrade = Math.Min(x.FollowStudentsRows.Count * 0.5, 10.0),
                    Attendance = Math.Min(x.FollowStudentsRows.Sum(y => y.Attendance), 25),
                    Behavior = Math.Min(x.FollowStudentsRows.Sum(y => y.Behavior), 25),
                    OralExam = x.Exams != null ? x.Exams.OralExam : 0f,
                    PaperExam = x.Exams != null ? x.Exams.PaperExam : 0f,

                })
                .ToListAsync();

            // 3. Map DTO
            var report = new GetMonthlyReportForView
            {
                Books = groupBooks,
                Data = students.Select(x => new GetMonthlyReportData
                {
                    FollowStudentId = x.Id,
                    StudentName = x.StudentName ?? string.Empty,
                    SaveData = new SaveReviewData
                    {
                        From = new FromToData
                        {
                            SurahName = x.Save.FirstOrDefault()?.FromSurah ?? string.Empty,
                            Verse = x.Save.FirstOrDefault()?.FromVerse ?? 0,
                        },
                        To = new FromToData
                        {
                            SurahName = x.Save.LastOrDefault()?.ToSurah ?? string.Empty,
                            Verse = x.Save.LastOrDefault()?.ToVerse ?? 0,
                        },
                        Pages = x.Save.Sum(y => y.Pages),
                        Rate = string.Empty
                    },
                    ReviewData = new SaveReviewData
                    {
                        From = new FromToData
                        {
                            SurahName = x.Review.FirstOrDefault()?.FromSurah ?? string.Empty,
                            Verse = x.Review.FirstOrDefault()?.FromVerse ?? 0,
                        },
                        To = new FromToData
                        {
                            SurahName = x.Review.LastOrDefault()?.ToSurah ?? string.Empty,
                            Verse = x.Review.LastOrDefault()?.ToVerse ?? 0,
                        },
                        Pages = x.Review.Sum(y => y.Pages),
                        Rate = string.Empty
                    },
                    SaveGrade = Math.Round( x.SaveGrade,2),
                    ReviewGrade = Math.Round( x.ReviewGrade,2),
                    AttendanceGrade = Math.Round( x.Attendance,2),
                    BehaviorGrade = Math.Round( x.Behavior,2),
                    OralGrade = x.OralExam,
                    PaperGrade = x.PaperExam,
                    Total = Math.Round(((x.SaveGrade + x.ReviewGrade + x.Attendance + x.Behavior + x.OralExam + x.PaperExam) * 100) / 100, 2),
                })
                .OrderByDescending(x => x.Total)
                .ToList()
            };

            return Ok(report);
        }


        [HttpGet("best-students-report")]
        public async Task<IActionResult> GetBestStudentsReport([FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (year <= 0 || month <= 0)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            HijriCalendar hijri = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            int daysInMonth = hijri.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth - 1);

            // Get all FollowStudents rows for the month
            var followRows = await _db.FollowStudents
                .AsNoTracking()
                .Where(x => x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    StudentId = x.StudentId,
                    x.Student.StudentName,
                    GroupName = x.Student.Group.GroupName,
                    x.FollowStudentsRows,
                    Oral = x.Exams != null ? x.Exams.OralExam : 0,
                    Paper = x.Exams != null ? x.Exams.PaperExam : 0
                }).ToListAsync();

            // Aggregate per student
            var grouped = followRows.GroupBy(x => x.StudentId).Select(g =>
            {
                var allRows = g.SelectMany(x => x.FollowStudentsRows).ToList();
                var saveGrade = Math.Min(allRows.Count * 0.5, 10.0);
                var reviewGrade = Math.Min(allRows.Count * 0.5, 10.0);
                var attendance = Math.Min(allRows.Sum(r => r.Attendance), 25);
                var behavior = Math.Min(allRows.Sum(r => r.Behavior), 25);
                var oral = g.Sum(x => x.Oral);
                var paper = g.Sum(x => x.Paper);

                return new GetBestStudentsReportForView
                {
                    StudentName = g.First().StudentName,
                    GroupName = g.First().GroupName,
                    SaveGrade = Math.Round(saveGrade, 2),
                    ReviewGrade = Math.Round(reviewGrade, 2),
                    AttendanceGrade = Math.Round(attendance, 2),
                    BehaviorGrade = Math.Round(behavior, 2),
                    OralGrade = oral,
                    PaperGrade = paper,
                    Total = Math.Round(saveGrade + reviewGrade + attendance + behavior + oral + paper, 2)
                };
            })
            .OrderByDescending(x => x.Total)
            .Take(take)
            .ToList();

            return Ok(grouped);
        }
        [HttpGet("best-students-for-group-report")]
        public async Task<IActionResult> GetBestStudentsForGroupReport([FromQuery] int groupId, [FromQuery] int year, [FromQuery] int month, [FromQuery] int take = 5)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId)) return BadRequest(new { message = "لاتوجد حلقة" });
            if (year <= 0 || month <= 0) return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            HijriCalendar hijri = new HijriCalendar();
            DateTime fromDate = new DateTime(year, month, 1);
            int daysInMonth = hijri.GetDaysInMonth(year, month);
            DateTime toDate = fromDate.AddDays(daysInMonth - 1);

            // Get all FollowStudents rows for the group in the month
            var followRows = await _db.FollowStudents
                .AsNoTracking()
                .Where(x => x.Student.GroupId == groupId && x.Date >= fromDate && x.Date <= toDate)
                .Select(x => new
                {
                    x.StudentId,
                    x.Student.StudentName,
                    x.FollowStudentsRows,
                    Oral = x.Exams != null ? x.Exams.OralExam : 0,
                    Paper = x.Exams != null ? x.Exams.PaperExam : 0
                }).ToListAsync();

            // Aggregate per student same as GetMonthlyReport
            var grouped = followRows.GroupBy(x => x.StudentId).Select(g =>
            {
                var allRows = g.SelectMany(x => x.FollowStudentsRows).ToList();
                var saveGrade = Math.Min(allRows.Count * 0.5, 10.0);
                var reviewGrade = Math.Min(allRows.Count * 0.5, 10.0);
                var attendance = Math.Min(allRows.Sum(r => r.Attendance), 25);
                var behavior = Math.Min(allRows.Sum(r => r.Behavior), 25);
                var oral = g.Sum(x => x.Oral);
                var paper = g.Sum(x => x.Paper);

                return new GetBestStudentsReportForView
                {
                    StudentName = g.First().StudentName,
                    GroupName = null,
                    SaveGrade = Math.Round(saveGrade, 2),
                    ReviewGrade = Math.Round(reviewGrade, 2),
                    AttendanceGrade = Math.Round(attendance, 2),
                    BehaviorGrade = Math.Round(behavior, 2),
                    OralGrade = oral,
                    PaperGrade = paper,
                    Total = Math.Round(saveGrade + reviewGrade + attendance + behavior + oral + paper, 2)
                };
            }).OrderByDescending(x => x.Total)
              .Take(take)
              .ToList();

            return Ok(grouped);
        }
    }

}
