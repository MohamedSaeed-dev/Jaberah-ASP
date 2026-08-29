using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.PartialExams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Jaberah.Middlewares;

namespace Jaberah.Controllers
{
    [Route("api/exams")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    public class ExamsController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpPost("monthly-exam")]
        public async Task<IActionResult> UpsertMonthlyExams([FromBody] UpsertMonthlyExamsDTO model)
        {
            if (model.StudentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var exam = await _db.Exams.FirstOrDefaultAsync(x => x.StudentId == model.StudentId && x.Date == model.Date);

            if (exam is not null) // update
            {
                exam.PaperExam = Math.Max(Math.Min(model.PaperExam ?? exam.PaperExam, 20), 0);
                exam.OralExam = Math.Max(Math.Min(model.OralExam ?? exam.OralExam, 10), 0);
                _db.Exams.Update(exam);
            }
            else // insert
            {
                model.PaperExam = Math.Max(Math.Min(model.PaperExam ?? 0, 20), 0);
                model.OralExam = Math.Max(Math.Min(model.OralExam ?? 0, 10), 0);
                var newExam = _mapper.Map<Exam>(model);
                newExam.StudentId = model.StudentId;
                newExam.Date = model.Date;
                await _db.Exams.AddAsync(newExam);
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الاختبار الشهري بنجاح" });
        }
        [HttpPost("mid-final-exam")]
        public async Task<IActionResult> UpsertMidFinalExam([FromQuery] int studentId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromBody] MidFinalGrade grade)
        {
            if (studentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Students.AnyAsync(x => x.Id == studentId))
            {
                return BadRequest(new { message = "لايوجد طالب" });
            }

            if (fromDate.Equals(default) || toDate.Equals(default))
            {
                return BadRequest(new { message = "ادخل تاريخ صحيح" });
            }

            int monthsDifference = (toDate.Year - fromDate.Year) * 12 + toDate.Month - fromDate.Month;
            if (monthsDifference != 4)
                return BadRequest(new { message = "الفارق يجب ان يكون 4 اشهر" });

            var final = await _db.MidFinals.FirstOrDefaultAsync(x => x.StudentId == studentId && x.FromDate == fromDate && x.ToDate == toDate);
            if (final is null)
            {
                await _db.MidFinals.AddAsync(new MidFinal
                {
                    StudentId = studentId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Grade = grade.Grade ?? 0
                });
            }
            else
            {
                final.Grade = grade.Grade ?? final.Grade;
            }
            Console.WriteLine(grade.Grade);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم الحفظ بنجاح" });
        }

        [IsAdmin]
        [HttpPost("partial-exam")]
        public async Task<IActionResult> AddPartialExam([FromBody] CreatePartialExamDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "البيانات غير صحيحة", errors = ModelState });
            }

            var existingExam = await _db.PartialExams
                .FirstOrDefaultAsync(e => e.StudentId == dto.StudentId && e.Date == dto.ExamDate);

            if (existingExam != null)
            {
                return BadRequest(new { message = "يوجد اختبار جزئي لهذا الطالب في نفس التاريخ" });
            }

            var partialExam = new PartialExam
            {
                StudentId = dto.StudentId,
                Date = dto.ExamDate,
                Question1 = dto.Question1,
                Question2 = dto.Question2,
                Question3 = dto.Question3,
                Question4 = dto.Question4,
                Question5 = dto.Question5,
                Question6 = dto.Question6,
                Question7 = dto.Question7,
                Question8 = dto.Question8,
                Question9 = dto.Question9,
                Question10 = dto.Question10,
                Performance = dto.Performance,
                Tester = dto.Tester,
                Part = dto.Part,
                Rate = dto.Rate,
                Notes = dto.Notes,
                TotalScore = dto.TotalScore
            };

            _db.PartialExams.Add(partialExam);
            await _db.SaveChangesAsync();

            return Ok(partialExam);
        }

        [IsAdmin]
        [HttpPut("partial-exam")]
        public async Task<IActionResult> UpdatePartialExam([FromBody] UpdatePartialExamDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "البيانات غير صحيحة", errors = ModelState });
            }
            Console.WriteLine(dto.Rate);
            var partialExam = await _db.PartialExams.FindAsync(dto.Id);

            if (partialExam == null)
            {
                return NotFound(new { message = "الاختبار الجزئي غير موجود" });
            }

            partialExam.Question1 = dto.Question1;
            partialExam.Question2 = dto.Question2;
            partialExam.Question3 = dto.Question3;
            partialExam.Question4 = dto.Question4;
            partialExam.Question5 = dto.Question5;
            partialExam.Question6 = dto.Question6;
            partialExam.Question7 = dto.Question7;
            partialExam.Question8 = dto.Question8;
            partialExam.Question9 = dto.Question9;
            partialExam.Question10 = dto.Question10;
            partialExam.Performance = dto.Performance;
            partialExam.Tester = dto.Tester;
            partialExam.Part = dto.Part;
            partialExam.Rate = dto.Rate;
            partialExam.Notes = dto.Notes;
            partialExam.TotalScore = dto.TotalScore;

            await _db.SaveChangesAsync();

            return Ok(partialExam);
        }

        [IsAdmin]
        [HttpGet("partial-exam/group/{groupId}")]
        public async Task<IActionResult> GetGroupExamsByDateAsync([FromRoute] int groupId, [FromQuery] DateOnly date)
        {
            if (date == default) return BadRequest(new { message = "ادخل تاريخ صحيح" });

            // Get all students in the group
            var students = await _db.Students
                .AsNoTracking()
                .Where(s => s.GroupId == groupId)
                .Select(s => new
                {
                    s.Id,
                    s.Name
                })
                .ToListAsync();

            // Get all exams for these students on the specified date
            var studentIds = students.Select(s => s.Id).ToList();

            var exams = await _db.PartialExams
                .AsNoTracking()
                .Where(e => studentIds.Contains(e.StudentId) && e.Date == date)
                .ToDictionaryAsync(e => e.StudentId, e => e);

            // Merge students with their exams (if any)
            var result = students
                .Select(s =>
                {
                    exams.TryGetValue(s.Id, out var exam);

                    return new GetStudentsPartialExams
                    {
                        StudentId = s.Id,
                        StudentName = s.Name,
                        ExamId = exam?.Id,
                        Question1 = exam?.Question1,
                        Question2 = exam?.Question2,
                        Question3 = exam?.Question3,
                        Question4 = exam?.Question4,
                        Question5 = exam?.Question5,
                        Question6 = exam?.Question6,
                        Question7 = exam?.Question7,
                        Question8 = exam?.Question8,
                        Question9 = exam?.Question9,
                        Question10 = exam?.Question10,
                        Performance = exam?.Performance,
                        Tester = exam?.Tester,
                        Rate = exam?.Rate,
                        Part = exam?.Part,
                        Notes = exam?.Notes,
                        TotalScore = exam?.TotalScore
                    };
                })
                .OrderBy(s => s.StudentName)
                .ToList();

            return Ok(result);
        }

        [IsAdmin]
        [HttpGet("partial-exam/{id}")]
        public async Task<IActionResult> GetByIdAsync([FromRoute] int id)
        {
            var partialExam = await _db.PartialExams
           .AsNoTracking()
           .Include(e => e.Student)
           .FirstOrDefaultAsync(e => e.Id == id);
            return Ok(partialExam);
        }


    }
    public record MidFinalGrade
    {
        public float? Grade { get; set; }
    }
}
