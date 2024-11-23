using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Groups;
using Jaberah.Models.ViewModels.Teachers;
using Jaberah.Validations.Teachers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Controllers
{
    [Route("api/teachers")]
    [ApiController]
    public class TeachersController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpGet]
        public async Task<IActionResult> GetTeachers([FromQuery] string searchText = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Teachers.AsNoTracking().Where(x => x.Role == Role.TEACHER && x.TeacherName.Contains(searchText))
                .Select(x => new GetTeachersForView
                {
                    Id = x.Id,
                    TeacherName = x.TeacherName,
                    PhoneNumber = x.PhoneNumber,
                    Groups = x.Groups.Select(y => new TeacherGroupsDataForView
                    {
                        GroupId = y.Id,
                        GroupName = y.GroupName,
                    }).ToList()
                }).AsQueryable();

            var pagedTeachers = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                .ToPagedList(await query.CountAsync(), pageNumber, pageSize);

            return Ok(pagedTeachers);
        }
        [HttpGet("{teacherId}/groups")]
        public async Task<IActionResult> GetGroupsOfTeacher([FromRoute] int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }
            var query = await _db.Groups.AsNoTracking().Where(x => x.TeacherId.HasValue && x.TeacherId.Value == teacherId)
                .Select(x => new
                {
                    x.GroupName,
                    x.Teacher.TeacherName,
                    x.Period,
                    StudentCount = x.Students.Count
                }).ToListAsync();

            if (query is null) return BadRequest(new { message = "لاتوجد حلقات لهذا المعلم" });

            return Ok(query.Select(x => new GetGroupForView
            {
                GroupName = x.GroupName,
                TeacherName = x.TeacherName,
                Period = GetPeriodName((byte)x.Period),
                StudentsNo = x.StudentCount
            }));
        }
        [AddTeacher]
        [HttpPost]
        public async Task<IActionResult> AddTeacher([FromBody] AddTeacherDTO model)
        {
            var existingTeacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.TeacherName == model.TeacherName);

            if (existingTeacher != null)
            {
                return BadRequest(new { message = "المعلم موجود مسبقاً" });
            }

            var groups = await _db.Groups
                .Where(g => model.GroupsId.Contains(g.Id))
                .ToListAsync();

            if (groups.Count != model.GroupsId.Count)
            {
                return BadRequest(new { message = "بعض الحلقات ليست موجودة" });
            }

            var conflictingTeacher = await _db.Teachers
                .AnyAsync(t => t.Groups.Any(g => model.GroupsId.Contains(g.Id)));

            if (conflictingTeacher)
            {
                return BadRequest(new { message = "هناك معلمين مرتبطين بهذه الحلقات" });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.PhoneNumber);

            var newTeacher = _mapper.Map<Teacher>(model);
            newTeacher.Password = hashedPassword;
            newTeacher.Role = Role.TEACHER;
            newTeacher.Groups = groups;

            await _db.Teachers.AddAsync(newTeacher);
            await _db.SaveChangesAsync();

            return StatusCode(201, new { message = "تم اضافة المعلم بنجاح" });
        }
        [UpdateTeacher]
        [HttpPut("{teacherId}")]
        public async Task<IActionResult> UpdateTeacher(int teacherId, [FromBody] UpdateTeacherDTO model)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            var teacher = await _db.Teachers
                .Include(t => t.Groups)
                .FirstOrDefaultAsync(t => t.Id == teacherId);

            if (teacher is null)
            {
                return NotFound(new { message = "لايوجد معلم" });
            }

            if (model.GroupsId is not null && model.GroupsId.Count > 0)
            {
                var existingGroups = await _db.Groups
                    .Where(g => model.GroupsId.Contains(g.Id))
                    .ToListAsync();

                if (existingGroups.Count != model.GroupsId.Count)
                {
                    return BadRequest(new { message = "لاتوجد حلقة" });
                }
                else
                {
                    if (await _db.Teachers.AnyAsync(t => t.Id != teacherId && t.Groups.Any(g => model.GroupsId.Contains(g.Id))))
                    {
                        return BadRequest(new { message = "هناك معلمين مرتبطين بهذه الحلقات" });
                    }
                }
            }

            if (!string.IsNullOrEmpty(model.OldPassword))
            {
                var isPasswordCorrect = BCrypt.Net.BCrypt.Verify(model.OldPassword, teacher.Password);

                if (!isPasswordCorrect)
                {
                    return BadRequest(new { message = "كلمة المرور خاطئة" });
                }

                teacher.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }

            teacher.TeacherName = string.IsNullOrEmpty(model.TeacherName) ? teacher.TeacherName : model.TeacherName;
            teacher.PhoneNumber = string.IsNullOrEmpty(model.PhoneNumber) ? teacher.PhoneNumber : model.PhoneNumber;

            if (model.GroupsId != null && model.GroupsId.Count > 0)
            {
                var newGroups = await _db.Groups
                    .Where(g => model.GroupsId.Contains(g.Id))
                    .ToListAsync();

                teacher.Groups = newGroups;
            }

            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث بيانات المعلم بنجاح" });

        }
        [HttpDelete("{teacherId}")]
        public async Task<IActionResult> DeleteTeacher(int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            var teacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.Id == teacherId);

            if (teacher == null)
            {
                return NotFound(new { message = "لايوجد معلم" });
            }

            _db.Teachers.Remove(teacher);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم حذف المعلم بنجاح" });
        }

        [NonAction]
        private string GetPeriodName(byte period)
        {
            return (Enum.GetName(typeof(Period), period) == "MORNING" ? "صباحية" : "مسائية");
        }
    }

}
