using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Groups;
using Jaberah.Models.ViewModels.Students;
using Jaberah.Validations.Groups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Group = Jaberah.Models.JaberahModels.Group;

namespace Jaberah.Controllers
{
    [Route("api/groups")]
    [ApiController]
    public class GroupsController(JaberahDBContext db, IMapper mapper) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;

        [HttpGet]
        public async Task<IActionResult> GetGroups([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Groups.AsNoTracking()
                .Select(x => new
                {
                    x.GroupName,
                    x.Period,
                    x.Teacher.TeacherName,
                    StudentsCount = x.Students.Count,
                }).AsQueryable();

            var pagedGroups = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                            .Select(x => new GetGroupsForView
                            {
                                GroupName = x.GroupName,
                                Period = GetPeriodName((byte)x.Period),
                                TeacherName = x.TeacherName,
                                StudentsNo = x.StudentsCount
                            })
                            .ToPagedList(await query.CountAsync(), pageNumber, pageSize);

            return Ok(pagedGroups);
        }
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroup([FromRoute] int groupId)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
            {
                return BadRequest(new { message = "لاتوجد حلقة" });
            }
            var query = await _db.Groups.AsNoTracking().Where(x => x.Id == groupId)
                .Select(x => new
                {
                    x.GroupName,
                    x.Period,
                    x.Teacher.TeacherName,
                    StudentCount = x.Students.Count
                })
                .FirstOrDefaultAsync();

            return Ok(new GetGroupForView
            {
                GroupName = query!.GroupName,
                Period = GetPeriodName((byte)query.Period),
                TeacherName = query.TeacherName,
                StudentsNo = query.StudentCount
            });
        }

        [HttpGet("{groupId}/students")]
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

        [HttpGet("has-no-teacher-data")]
        public async Task<IActionResult> GetGroupsWithNoTeacher()
        {
            var groups = await _db.Groups.AsNoTracking()
            .Where(g => !g.TeacherId.HasValue)
            .Select(g => new { g.Id, g.GroupName })
            .ToListAsync();

            return Ok(groups);
        }
        [HttpGet("teachers/{teacherId}/has-no-teacher-or-has-teacher")]
        public async Task<IActionResult> GetGroupsWithNoTeacherAndTeacherGroups([FromRoute] int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Teachers.AllAsync(x => x.Id == teacherId))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }
            var groups = await _db.Groups.AsNoTracking()
                    .Where(g => (g.TeacherId.HasValue && g.TeacherId.Value == teacherId) || !g.TeacherId.HasValue)
                    .Select(g => new { g.Id, g.GroupName })
                    .ToListAsync();

            return Ok(groups);
        }
        [AddGroup]
        [HttpPost]
        public async Task<IActionResult> AddGroup([FromBody] AddGroupDTO model)
        {
            var existingGroup = await _db.Groups
                .FirstOrDefaultAsync(g => g.GroupName.Trim() == model.GroupName.Trim());

            if (existingGroup is not null)
                return BadRequest(new { message = "الحلقة موجودة مسبقاً" });

            var newGroup = _mapper.Map<Group>(model);

            await _db.Groups.AddAsync(newGroup);
            await _db.SaveChangesAsync();

            return StatusCode(201, new { message = "تم اضافة الحلقة بنجاح" });
        }

        [HttpPut("{groupId}")]
        public async Task<IActionResult> UpdateGroup([FromRoute] int groupId, [FromBody] UpdateGroupDTO model)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            var group = await _db.Groups.FindAsync(groupId);
            if (group is null)
                return NotFound(new { message = "لاتوجد حلقة" });

            group.GroupName = !string.IsNullOrWhiteSpace(model.GroupName) ? model.GroupName : group.GroupName;
            group.Period = model.Period.HasValue ? model.Period.Value : group.Period;

            _db.Groups.Update(group);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم تحديث بيانات الحلقة بنجاح" });
        }

        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup([FromRoute] int groupId)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            var group = await _db.Groups.FindAsync(groupId);
            if (group is null)
                return NotFound(new { message = "لاتوجد حلقة" });

            _db.Groups.Remove(group);
            await _db.SaveChangesAsync();

            return Ok(new { message = "تم حذف الحلقة بنجاح" });
        }
        [NonAction]
        private string GetPeriodName(byte period)
        {
            return (Enum.GetName(typeof(Period), period) == "MORNING" ? "صباحية" : "مسائية");
        }
    }

}
