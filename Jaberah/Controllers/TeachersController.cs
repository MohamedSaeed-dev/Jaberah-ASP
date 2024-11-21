using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Teachers;
using Jaberah.Validations.Teachers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Controllers
{
    [Route("api/teachers")]
    [ApiController]
    public class TeachersController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        public TeachersController(JaberahDBContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
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

            if (model.GroupsId is not null && model.GroupsId.Any())
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
                    //if (await _db.Teachers.AnyAsync(t => t.Groups.Any(g => model.GroupsId.Contains(g.Id))))
                    //{
                    //    return BadRequest(new { message = "هناك معلمين مرتبطين بهذه الحلقات" });
                    //}

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

            if (model.GroupsId != null && model.GroupsId.Any())
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
    }

}
