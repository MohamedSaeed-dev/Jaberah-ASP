using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jaberah.Controllers
{
    [Route("api/exams")]
    [ApiController]
    public class ExamsController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpPost("monthly-exam")]
        public async Task<IActionResult> UpsertMonthlyExams([FromQuery] int followStudentId, [FromBody] UpsertMonthlyExamsDTO model)
        {
            if (followStudentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.FollowStudents.AnyAsync(x => x.Id == followStudentId))
            {
                return BadRequest(new { message = "لايوجد متابعة الطالب" });
            }
            var exam = await _db.Exams.FirstOrDefaultAsync(x => x.FollowStudentsId == followStudentId);

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
                newExam.FollowStudentsId = followStudentId;
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

            if ((toDate.Month - fromDate.Month + 12 * (toDate.Year - fromDate.Year)) != 4)
            {
                return BadRequest(new { message = "الفارق يجب ان يكون 4 اشهر" });
            }

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
    }
    public record MidFinalGrade
    {
        public float? Grade { get; set; }
    }
}
