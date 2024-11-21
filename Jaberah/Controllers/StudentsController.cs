using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Students;
using Jaberah.Validations.Students;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Jaberah.Models.DTOs.Students;

namespace Jaberah.Controllers
{
    [Route("api/students")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        public StudentsController(JaberahDBContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
        [HttpGet]
        public async Task<IActionResult> GetStudents([FromQuery] string searchText = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Students.AsNoTracking().Where(x => x.StudentName.Contains(searchText)).Select(x => new GetStudentsForView
            {
                Id = x.Id,
                StudentName = x.StudentName,
                PhoneNumber = x.PhoneNumber,
                SchoolClass = x.SchoolClass,
                SchoolLevel = x.SchoolLevel,
                MemoRate = x.MemoRate,
                GroupName = x.Group.GroupName,
                Notes = x.Notes
            }).AsQueryable();

            var pagedStudents = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                            .ToPagedList(await query.CountAsync(), pageNumber, pageSize);

            return Ok(pagedStudents);
        }
        [HttpGet("groups/{groupId}/students-for-group")]
        public async Task<IActionResult> GetStudentsForGroup([FromRoute] int groupId, [FromQuery] string searchText = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
            {
                return BadRequest(new { message = "لاتوجد حلقة" });
            }
            var query = _db.Students.AsNoTracking().Where(x => (x.GroupId.HasValue && x.GroupId.Value == groupId) && x.StudentName.Contains(searchText))
                .Select(x => new GetStudentsForGroupForView
                {
                    Id = x.Id,
                    StudentName = x.StudentName,
                    PhoneNumber = x.PhoneNumber,
                    SchoolClass = x.SchoolClass,
                    SchoolLevel = x.SchoolLevel,
                    MemoRate = x.MemoRate,
                    Notes = x.Notes
                }).AsQueryable();

            var pagedStudents = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                            .ToPagedList(await query.CountAsync(), pageNumber, pageSize);

            return Ok(pagedStudents);
        }
        [AddStudent]
        [HttpPost]
        public async Task<IActionResult> AddStudent([FromBody] AddStudentDTO model)
        {
            var existingStudent = await _db.Students
                .FirstOrDefaultAsync(s => s.StudentName == model.StudentName);

            if (existingStudent != null)
            {
                return BadRequest(new { message = "الطالب موجود مسبقاً" });
            }

            var groupExists = await _db.Groups
                .FirstOrDefaultAsync(g => g.Id == model.GroupId);

            if (groupExists == null)
            {
                return NotFound(new { message = "لاتوجد حلقة بهذا المعرف" });
            }

            var newStudent = _mapper.Map<Student>(model);

            await _db.Students.AddAsync(newStudent);
            await _db.SaveChangesAsync();

            return StatusCode(201, new { message = "تم اضافة الطالب بنجاح" });
        }
        [UpdateStudent]
        [HttpPut("{studentId}")]
        public async Task<IActionResult> UpdateStudent(int studentId, [FromBody] UpdateStudentDTO model)
        {
            var student = await _db.Students
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound(new { message = "لايوجد طالب" });
            }

            if (model.GroupId.HasValue)
            {
                var groupExists = await _db.Groups
                    .FirstOrDefaultAsync(g => g.Id == model.GroupId.Value);

                if (groupExists == null)
                {
                    return NotFound(new { message = "لاتوجد حلقة بهذا المعرف" });
                }
            }

            student.StudentName = !string.IsNullOrWhiteSpace(model.StudentName) ? model.StudentName : student.StudentName;
            student.PhoneNumber = !string.IsNullOrWhiteSpace(model.PhoneNumber) ? model.PhoneNumber : student.PhoneNumber;
            student.SchoolClass = !string.IsNullOrWhiteSpace(model.SchoolClass) ? model.SchoolClass : student.SchoolClass;
            student.SchoolLevel = !string.IsNullOrWhiteSpace(model.SchoolLevel) ? model.SchoolLevel : student.SchoolLevel;
            student.MemoRate = !string.IsNullOrWhiteSpace(model.MemoRate) ? model.MemoRate : student.MemoRate;
            student.Notes = !string.IsNullOrWhiteSpace(model.Notes) ? model.Notes : student.Notes;
            student.GroupId = model.GroupId.HasValue ? model.GroupId.Value : student.GroupId;

            _db.Students.Update(student);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث بيانات الطالب بنجاح" });
        }

        [HttpDelete("{studentId}")]
        public async Task<IActionResult> DeleteStudent(int studentId)
        {
            var student = await _db.Students
                .FirstOrDefaultAsync(t => t.Id == studentId);

            if (student == null)
            {
                return NotFound(new { message = "لايوجد طالب" });
            }

            _db.Students.Remove(student);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم حذف الطالب بنجاح" });
        }

    }
}
