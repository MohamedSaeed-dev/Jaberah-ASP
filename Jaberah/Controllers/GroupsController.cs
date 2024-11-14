using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Groups;
using Jaberah.Validations.Groups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Group = Jaberah.Models.JaberahModels.Group;

namespace Jaberah.Controllers
{
    [Route("api/groups")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        public GroupsController(JaberahDBContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
        [HttpGet]
        public async Task<IActionResult> GetGroups([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Groups
                .Select(x => new
                {
                    x.GroupName,
                    x.Period,
                    x.Teacher.TeacherName,
                    StudnetsCount = x.Students.Count,
                }).AsQueryable();

            var pagedGroups = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                            .Select(x => new GetGroupsForView
                            {
                                GroupName = x.GroupName,
                                Period = GetPeriodName((byte)x.Period),
                                TeacherName = x.TeacherName,
                                StudentsNo = x.StudnetsCount
                            })
                            .ToPagedList(query.Count(), pageNumber, pageSize);

            return Ok(pagedGroups);
        }
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroup([FromRoute] int groupId)
        {
            if (groupId.Equals(default)) return BadRequest(new { message = "ادخل id صحيح" });
            var query = await _db.Groups.Where(x => x.Id == groupId)
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
        [HttpGet("has-no-teacher-data")]
        public async Task<IActionResult> GetGroupsWithNoTeacher()
        {
            var groups = await _db.Groups
            .Where(g => !g.TeacherId.HasValue)
            .Select(g => new { g.Id, g.GroupName })
            .ToListAsync();

            return Ok(groups);
        }
        [HttpGet("{teacherId}/has-no-teacher-or-has-teacher")]
        public async Task<IActionResult> GetGroupsWithNoTeacherAndTeacherGroups([FromRoute] int teacherId)
        {
            if (teacherId.Equals(default)) return BadRequest(new { message = "ادخل id صحيح" });
            var groups = await _db.Groups
                    .Where(g => g.TeacherId == teacherId || !g.TeacherId.HasValue)
                    .Select(g => new { g.Id, g.GroupName })
                    .ToListAsync();

            return Ok(groups);
        }
        [AddGroupValidation]
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
            if (groupId.Equals(default)) return BadRequest(new { message = "ادخل id صحيح" });
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
            if (groupId.Equals(default)) return BadRequest(new { message = "ادخل id صحيح" });
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
