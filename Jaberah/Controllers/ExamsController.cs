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
            if (!await _db.FollowStudentsInMonth.AnyAsync(x => x.Id == followStudentId))
            {
                return BadRequest(new { message = "لايوجد متابعة الطالب" });
            }
            var exam = await _db.Exams.FirstOrDefaultAsync(x => x.FollowStudentInMonthId == followStudentId);

            if (exam is not null) // update
            {
                exam.OralExam = model.OralExam ?? exam.OralExam;
                exam.PaperExam = model.PaperExam ?? exam.PaperExam;
                _db.Exams.Update(exam);
            }
            else // insert
            {
                model.PaperExam ??= 0;
                model.OralExam ??= 0;
                var newExam = _mapper.Map<Exam>(model);
                newExam.FollowStudentInMonthId = followStudentId;
                await _db.Exams.AddAsync(newExam);
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الاختبار الشهري بنجاح" });
        }
    }
}
