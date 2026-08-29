using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Students;
using Jaberah.Validations.Students;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static Jaberah.Models.DTOs.Students;
using Jaberah.Middlewares;

namespace Jaberah.Controllers
{
    [Route("api/students")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    [IsAdmin]
    public class StudentsController(JaberahDBContext db, IMapper mapper, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;
        private readonly IMemoryCache _cache = cache;

        [HttpGet]
        public async Task<IActionResult> GetStudents([FromQuery] string searchText = "", [FromQuery] bool withoutGroup = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Students.AsNoTracking().Where(x => x.Name.Contains(searchText)).AsQueryable();
            if (withoutGroup) query = query.Where(x => !x.GroupId.HasValue).AsQueryable();
            var selectedQuery = query.Select(x => new GetStudentsForView
            {
                Id = x.Id,
                StudentName = x.Name,
                PhoneNumber = x.PhoneNumber,
                SchoolClass = x.SchoolClass,
                SchoolLevel = x.SchoolLevel,
                StudyLevel = x.StudyLevel,
                MemoRate = x.MemoRate,
                GroupId = x.GroupId,
                GroupName = x.Group.Name,
                Notes = x.Notes
            }).AsQueryable();

            // الترتيب كان بعد Skip/Take، أي يرتّب الصفحة وحدها بعد اقتطاعها من مجموعة
            // غير مرتّبة؛ SQL Server لا يضمن ترتيبًا بلا ORDER BY فقد يتكرر صف بين
            // الصفحات أو يختفي. الترتيب قبل الاقتطاع، وبمفتاح فريد لفض تعادل المعدّل.
            var pagedStudents = (await selectedQuery
                    .OrderByDescending(s => s.MemoRate)
                    .ThenBy(s => s.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync())
                .ToPagedList(await selectedQuery.CountAsync(), pageNumber, pageSize);

            return Ok(pagedStudents);
        }
        [AddStudent]
        [HttpPost]
        public async Task<IActionResult> AddStudent([FromBody] AddStudentDTO model)
        {
            var studentName = model.StudentName.Trim();
            // فحص وجود فقط: AnyAsync لا يجلب أعمدة ولا يتعقّب كيانًا.
            var existingStudent = await _db.Students
                .AnyAsync(s => s.Name == studentName);

            if (existingStudent)
            {
                return BadRequest(new { message = "الطالب موجود مسبقاً" });
            }

            if (model.GroupId.HasValue)
            {
                var groupExists = await _db.Groups
                    .AnyAsync(g => g.Id == model.GroupId);

                if (!groupExists)
                {
                    return NotFound(new { message = "لاتوجد حلقة بهذا المعرف" });
                }
            }
            model.StudentName = studentName;
            var newStudent = _mapper.Map<Student>(model);

            await _db.Students.AddAsync(newStudent);
            await _db.SaveChangesAsync();

            InvalidateCache();
            return StatusCode(201, new { message = "تم اضافة الطالب بنجاح" });
        }
        [UpdateStudent]
        [HttpPut("{studentId}")]
        public async Task<IActionResult> UpdateStudent(int studentId, [FromBody] UpdateStudentDTO model)
        {
            if (studentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
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

            student.Name = !string.IsNullOrWhiteSpace(model.StudentName) ? model.StudentName : student.Name;
            student.PhoneNumber = !string.IsNullOrWhiteSpace(model.PhoneNumber) ? model.PhoneNumber : student.PhoneNumber;
            student.SchoolClass = model.SchoolClass;
            student.SchoolLevel = model.SchoolLevel;
            student.StudyLevel =  model.StudyLevel;
            student.MemoRate = model.MemoRate > 0 ? model.MemoRate : student.MemoRate;
            student.Notes = model.Notes;
            student.GroupId = model.GroupId;
            _db.Students.Update(student);
            await _db.SaveChangesAsync();
            InvalidateCache();
            return Ok(new { message = "تم تحديث بيانات الطالب بنجاح" });
        }

        [HttpDelete("{studentId}")]
        public async Task<IActionResult> DeleteStudent(int studentId)
        {
            if (studentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var student = await _db.Students
                .FirstOrDefaultAsync(t => t.Id == studentId);

            if (student == null)
            {
                return NotFound(new { message = "لايوجد طالب" });
            }
            student.GroupId = null;
            _db.SoftDelete(student);
            await _db.SaveChangesAsync();
            InvalidateCache();
            _cache.Remove("DeletedStudents");
            return Ok(new { message = "تم حذف الطالب بنجاح" });
        }

        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedStudents()
        {
            var cacheKey = $"DeletedStudents";

            if (!_cache.TryGetValue(cacheKey, out List<GetDeletedStudentsForView>? students))
            {
                var query = _db.Students.AsNoTracking().IgnoreQueryFilters().Where(x => x.DeletedAt != null).AsQueryable();

                students = (await query.Select(x => new GetDeletedStudentsForView
                {
                    Id = x.Id,
                    StudentName = x.Name,
                    PhoneNumber = x.PhoneNumber,
                }).ToListAsync());

                _cache.Set(cacheKey, students, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(students);
        }

        [HttpDelete("{studentId}/ever")]
        public async Task<IActionResult> DeleteStudentEver([FromRoute] int studentId)
        {
            if (studentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var student = await _db.Students.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == studentId);
            if (student == null)
                return NotFound(new { message = "لايوجد طالب" });

            _db.Remove(student);
            await _db.SaveChangesAsync();

            _cache.Remove("DeletedStudents");

            return Ok(new { message = "تم حذف الطالب نهائياً بنجاح" });
        }

        [HttpPatch("{studentId}/restore")]
        public async Task<IActionResult> RestoreStudent([FromRoute] int studentId)
        {
            if (studentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var student = await _db.Students.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == studentId);
            if (student == null)
                return NotFound(new { message = "لايوجد طالب" });

            _db.RestoreEntity(student);
            await _db.SaveChangesAsync();
            InvalidateCache();
            _cache.Remove("DeletedStudents");
            return Ok(new { message = "تم استرجاع الطالب بنجاح" });

        }

        // Helper method to invalidate cache
        private void InvalidateCache()
        {
            _cache.Remove("GroupsCache");
            _cache.Remove("GroupsCache_WithoutTeacher");   
        }
    }
}
