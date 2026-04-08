using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Jaberah.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportsController(JaberahDBContext db) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;

        [HttpGet("semester-report")]
        public async Task<IActionResult> GetSemesterReport([FromQuery] int groupId,[FromQuery] DateTime fromDate,[FromQuery] DateTime toDate)
        {
            if (groupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });
            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            int monthsDifference = (toDate.Year - fromDate.Year) * 12 + toDate.Month - fromDate.Month;
            if (monthsDifference != 4)
                return BadRequest(new { message = "الفارق يجب ان يكون 4 اشهر" });

            // Single query — pull everything needed per student in one round-trip
            var studentData = await _db.Students
                .AsNoTracking()
                .Where(s => s.GroupId == groupId)
                .Select(s => new
                {
                    s.Id,
                    s.Name,

                    Attendances = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date <= toDate)
                        .Select(a => new { a.Attendance, a.Behavior, a.Date.Year, a.Date.Month })
                        .ToList(),

                    SaveLessons = s.SaveLessons!
                        .Where(l => l.Date >= fromDate && l.Date <= toDate)
                        .Select(l => new { l.Date.Year, l.Date.Month })
                        .ToList(),

                    Exam = s.Exams!
                        .Where(e => e.Date >= fromDate && e.Date <= toDate)
                        .Select(e => new { e.OralExam, e.PaperExam })
                        .ToList(),

                    MidFinal = s.MidFinals!
                        .Where(mf => mf.FromDate == fromDate && mf.ToDate == toDate)
                        .Select(mf => (float?)mf.Grade)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var report = studentData.Select(s =>
            {
                // Attendance & behavior: sum all values in range, capped at 100
                double attendance = Math.Min(s.Attendances.Sum(a => (double)a.Attendance), 100);
                double behavior = Math.Min(s.Attendances.Sum(a => (double)a.Behavior), 100);

                // Monthly grade: per month count SaveLesson records * 0.5, capped at 10, summed across 4 months
                double grade = s.SaveLessons
                    .GroupBy(l => new { l.Year, l.Month })
                    .Sum(monthGroup => Math.Min(monthGroup.Count() * 0.5, 10.0));

                double oral = Math.Min(s.Exam?.Sum(e => e.OralExam) ?? 0, 40);
                double paper = Math.Min(s.Exam?.Sum(e => e.PaperExam) ?? 0, 80);
                double midFinal = s.MidFinal ?? 0;

                double total = (attendance + behavior + grade + oral + paper + midFinal) * 100.0 / 400.0;

                return new SemesterReportForView
                {
                    StudentId = s.Id,
                    StudentName = s.Name,
                    AttendanceSum = Math.Round(attendance, 2),
                    BehaviorSum = Math.Round(behavior, 2),
                    GradeSum = Math.Round(grade, 2),
                    OralGradeSum = Math.Round(oral, 2),
                    PaperGradeSum = Math.Round(paper, 2),
                    MidFinalGrade = Math.Round(midFinal, 2),
                    Total = Math.Round(total, 2)
                };
            })
            .OrderByDescending(x => x.Total)
            .ToList();

            return Ok(report);
        }

        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] int groupId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int take = 5)
        {
            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            var books =await _db.Books
                .AsNoTracking()
                .Where(b => b.GroupId == groupId && b.Date >= fromDate && b.Date <= toDate)
                .Select(b => new BooksData
                {
                    Id = b.Id,
                    Title = b.Title,
                    Date = b.Date,
                    From = b.From,
                    To = b.To
                })
                .ToListAsync();

            var studentsQb = _db.Students.AsNoTracking().Take(take).AsQueryable();
            if(!groupId.Equals(default))
            {
                if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                    return BadRequest(new { message = "لاتوجد حلقة" });

                studentsQb = studentsQb.Where(s => s.GroupId == groupId);
            }

            var students = await studentsQb
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    GroupName = s.Group.Name,
                    // SaveLesson (WithTeacher)
                    SaveLessons = s.SaveLessons!
                        .Where(l => l.Date >= fromDate && l.Date <= toDate
                                 && !string.IsNullOrWhiteSpace(l.SurahFrom)
                                 && !string.IsNullOrWhiteSpace(l.SurahTo))
                        .OrderBy(l => l.Date)
                        .Select(l => new
                        {
                            l.SurahFrom,
                            l.SurahTo,
                            l.VerseFrom,
                            l.VerseTo,
                            l.Rate,
                            l.Pages
                        })
                        .ToList(),

                    // ReviewLesson (WithFriend)
                    ReviewLessons = s.ReviewLessons!
                        .Where(l => l.Date >= fromDate && l.Date <= toDate
                                 && !string.IsNullOrWhiteSpace(l.SurahFrom)
                                 && !string.IsNullOrWhiteSpace(l.SurahTo))
                        .OrderBy(l => l.Date)
                        .Select(l => new
                        {
                            l.SurahFrom,
                            l.SurahTo,
                            l.VerseFrom,
                            l.VerseTo,
                            l.Rate,
                            l.Pages
                        })
                        .ToList(),

                    SaveCount = s.SaveLessons!
                        .Count(l => l.Date >= fromDate && l.Date <= toDate),

                    ReviewCount = s.ReviewLessons!
                        .Count(l => l.Date >= fromDate && l.Date <= toDate),

                    Attendance = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date <= toDate)
                        .Sum(a => (double?)a.Attendance) ?? 0,

                    Behavior = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date <= toDate)
                        .Sum(a => (double?)a.Behavior) ?? 0,

                    Exam = s.Exams!
                        .Where(e => e.Date >= fromDate && e.Date <= toDate)
                        .Select(e => new { e.OralExam, e.PaperExam })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var report = new GetMonthlyReportForView
            {
                Books = books,
                Data = [.. students.Select(s =>
                {
                    double saveGrade = Math.Min(s.SaveCount * 0.5, 10.0);
                    double reviewGrade = Math.Min(s.ReviewCount * 0.5, 10.0);
                    double attendance = Math.Min(s.Attendance, 25.0);
                    double behavior = Math.Min(s.Behavior, 25.0);
                    double oral = s.Exam?.OralExam ?? 0;
                    double paper = s.Exam?.PaperExam ?? 0;
                    double total = Math.Round((saveGrade + reviewGrade + attendance + behavior + oral + paper) * 100.0 / 100.0, 2);

                    // First/Last save lesson for range display
                    var firstSave = s.SaveLessons.FirstOrDefault();
                    var lastSave = s.SaveLessons.LastOrDefault();

                    // First/Last review lesson for range display
                    var firstReview = s.ReviewLessons.FirstOrDefault();
                    var lastReview = s.ReviewLessons.LastOrDefault();

                    var savePages = s.SaveLessons.Sum(sl => sl.Pages);
                    var reviewPages = s.ReviewLessons.Sum(rl => rl.Pages);

                    return new GetMonthlyReportData
                    {
                        StudentId = s.Id,
                        StudentName = s.Name,
                        GroupName = s.GroupName,

                        SaveData = new SaveReviewData
                        {
                            From = new FromToData
                            {
                                SurahName = firstSave?.SurahFrom ?? "",
                                Verse = firstSave?.VerseFrom ?? 1
                            },
                            To = new FromToData
                            {
                                SurahName = lastSave?.SurahTo ?? "",
                                Verse = lastSave?.VerseTo ?? 1
                            },
                            Pages = Math.Round(savePages, 2),
                            Rate = firstSave?.Rate ?? ""
                        },

                        ReviewData = new SaveReviewData
                        {
                            From = new FromToData
                            {
                                SurahName = firstReview?.SurahFrom ?? "",
                                Verse = firstReview ?.VerseFrom ?? 1
                            },
                            To = new FromToData
                            {
                                SurahName = lastReview?.SurahTo ?? "",
                                Verse = lastReview?.VerseTo ?? 1
                            },
                            Pages = Math.Round(reviewPages, 2),
                            Rate = firstReview?.Rate ?? ""
                        },
                        SaveGrade = Math.Round(saveGrade, 2),
                        ReviewGrade = Math.Round(reviewGrade, 2),
                        AttendanceGrade = Math.Round(attendance, 2),
                        BehaviorGrade = Math.Round(behavior, 2),
                        OralGrade = oral,
                        PaperGrade = paper,
                        Total = total
                    };
                })
                .OrderByDescending(x => x.Total)]
            };

            return Ok(report);
        }

        [HttpGet("best-students-report")]
        public async Task<IActionResult> GetBestStudentsReport([FromQuery] int year,[FromQuery] int month,[FromQuery] int take = 5)
        {
            if (year <= 0 || month <= 0)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var students = await _db.Students
                .AsNoTracking()
                .Select(s => new
                {
                    s.Name,
                    GroupName = s.Group != null ? s.Group.Name : null,

                    SaveCount = s.SaveLessons!
                        .Count(l => l.Date >= fromDate && l.Date < toDate),

                    ReviewCount = s.ReviewLessons!
                        .Count(l => l.Date >= fromDate && l.Date < toDate),

                    Attendance = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date < toDate)
                        .Sum(a => (double?)a.Attendance) ?? 0,

                    Behavior = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date < toDate)
                        .Sum(a => (double?)a.Behavior) ?? 0,

                    Exam = s.Exams!
                        .Select(e => new { e.OralExam, e.PaperExam })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = students
                .Select(s =>
                {
                    double saveGrade = Math.Min(s.SaveCount * 0.5, 10.0);
                    double reviewGrade = Math.Min(s.ReviewCount * 0.5, 10.0);
                    double attendance = Math.Min(s.Attendance, 25.0);
                    double behavior = Math.Min(s.Behavior, 25.0);
                    double oral = s.Exam?.OralExam ?? 0;
                    double paper = s.Exam?.PaperExam ?? 0;

                    return new GetBestStudentsReportForView
                    {
                        StudentName = s.Name,
                        GroupName = s.GroupName,
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

            return Ok(result);
        }


        [HttpGet("best-students-for-group-report")]
        public async Task<IActionResult> GetBestStudentsForGroupReport([FromQuery] int groupId,[FromQuery] int year,[FromQuery] int month,[FromQuery] int take = 5)
        {
            if (groupId <= 0)
                return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
                return BadRequest(new { message = "لاتوجد حلقة" });
            if (year <= 0 || month <= 0)
                return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var students = await _db.Students
                .AsNoTracking()
                .Where(s => s.GroupId == groupId)          // only filter differs
                .Select(s => new
                {
                    s.Name,
                    GroupName = s.Group!.Name,
                    SaveCount = s.SaveLessons!
                        .Count(l => l.Date >= fromDate && l.Date <= toDate),

                    ReviewCount = s.ReviewLessons!
                        .Count(l => l.Date >= fromDate && l.Date <= toDate),

                    Attendance = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date <= toDate)
                        .Sum(a => (double?)a.Attendance) ?? 0,

                    Behavior = s.StudentAttendances!
                        .Where(a => a.Date >= fromDate && a.Date <= toDate)
                        .Sum(a => (double?)a.Behavior) ?? 0,

                    Exam = s.Exams!
                        .Select(e => new { e.OralExam, e.PaperExam })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = students
                .Select(s =>
                {
                    double saveGrade = Math.Min(s.SaveCount * 0.5, 10.0);
                    double reviewGrade = Math.Min(s.ReviewCount * 0.5, 10.0);
                    double attendance = Math.Min(s.Attendance, 25.0);
                    double behavior = Math.Min(s.Behavior, 25.0);
                    double oral = s.Exam?.OralExam ?? 0;
                    double paper = s.Exam?.PaperExam ?? 0;

                    return new GetBestStudentsReportForView
                    {
                        StudentName = s.Name,
                        GroupName = s.GroupName,             // intentionally null for group-scoped report
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

            return Ok(result);
        }

        [HttpGet("monthly-partial-exam")]
        public async Task<IActionResult> GetMonthlyPartialExamReport([FromQuery] int? groupId, [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate)
        {
            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "ادخل تاريخ صحيح" });

            if (fromDate > toDate)
                return BadRequest(new { message = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية" });

            if (groupId.HasValue)
            {
                if (groupId.Value <= 0)
                    return BadRequest(new { message = "ادخل id صحيح" });

                if (!await _db.Groups.AnyAsync(x => x.Id == groupId.Value))
                    return NotFound(new { message = "لا توجد حلقة بهذا الرقم" });
            }

            var result = await _db.PartialExams
                .AsNoTracking()
                .Where(pe =>
                    pe.Date >= fromDate &&
                    pe.Date <= toDate &&
                    (!groupId.HasValue || pe.Student.GroupId == groupId.Value))
                .OrderBy(pe => pe.Student.Name)
                .ThenBy(pe => pe.Date)
                .Select(pe => new
                {
                    StudentName = pe.Student.Name,
                    GroupName = pe.Student.Group.Name,
                    pe.Date,
                    pe.Rate,
                    pe.Part,
                    pe.Performance,
                    pe.Score,
                    pe.TotalScore
                })
                .ToListAsync();

            return Ok(result);
        }
    }

}
