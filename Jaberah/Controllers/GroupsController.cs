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
using Microsoft.Extensions.Caching.Memory;

namespace Jaberah.Controllers
{
    [Route("api/groups")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private static readonly string CacheKey = "GroupsCache";

        public GroupsController(JaberahDBContext db, IMapper mapper, IMemoryCache cache)
        {
            _db = db;
            _mapper = mapper;
            _cache = cache;
        }

        // GET: api/groups
        [HttpGet]
        public async Task<IActionResult> GetGroups([FromQuery] bool withoutTeacher)
        {
            var cacheKey = withoutTeacher ? $"{CacheKey}_WithoutTeacher" : CacheKey;

            if (!_cache.TryGetValue(cacheKey, out List<GetGroupsForView> groups))
            {
                var query = _db.Groups.AsNoTracking().AsQueryable();

                if (withoutTeacher) query = query.Where(x => x.Teacher == null);

                groups = (await query.Select(x => new
                {
                    x.Id,
                    x.GroupName,
                    x.Period,
                    x.TeacherId,
                    x.Teacher.TeacherName,
                    StudentsCount = x.Students.Count,
                }).ToListAsync())
                .Select(x => new GetGroupsForView
                {
                    Id = x.Id,
                    GroupName = x.GroupName,
                    Period = GetPeriodName((byte)x.Period),
                    TeacherId = x.TeacherId,
                    TeacherName = x.TeacherName,
                    StudentsNo = x.StudentsCount
                }).ToList();

                _cache.Set(cacheKey, groups, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(groups);
        }

        [HttpGet("for-general-use")]
        public async Task<IActionResult> GetGroupsForGeneralUse()
        {
            const string cacheKey = "GroupsForGeneralUse";

            if (!_cache.TryGetValue(cacheKey, out List<GetGroupsForGeneralUse> groups))
            {
                groups = await _db.Groups.AsNoTracking()
                    .Select(x => new GetGroupsForGeneralUse
                    {
                        Id = x.Id,
                        GroupName = x.GroupName,
                    }).ToListAsync();

                _cache.Set(cacheKey, groups, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(groups);
        }

        // GET: api/groups/{groupId}/students
        [HttpGet("{groupId}/students")]
        public async Task<IActionResult> GetStudentsForGroup([FromRoute] int groupId, [FromQuery] string searchText = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
            {
                return BadRequest(new { message = "لاتوجد حلقة" });
            }

            var query = _db.Students.AsNoTracking()
                .Where(x => (x.GroupId.HasValue && x.GroupId.Value == groupId) && x.StudentName.Contains(searchText))
                .Select(x => new GetStudentsForGroupForView
                {
                    Id = x.Id,
                    StudentName = x.StudentName,
                    PhoneNumber = x.PhoneNumber,
                    SchoolClass = x.SchoolClass,
                    SchoolLevel = x.SchoolLevel,
                    MemoRate = x.MemoRate,
                    Notes = x.Notes
                });

            var pagedStudents = (await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync())
                            .ToPagedList(await query.CountAsync(), pageNumber, pageSize);



            return Ok(pagedStudents);
        }

        // GET: api/groups/has-no-teacher-data
        [HttpGet("has-no-teacher-data")]
        public async Task<IActionResult> GetGroupsWithNoTeacher()
        {
            const string cacheKey = "GroupsWithNoTeacher";

            if (!_cache.TryGetValue(cacheKey, out var groups))
            {
                groups = await _db.Groups.AsNoTracking()
                    .Where(g => !g.TeacherId.HasValue)
                    .Select(g => new { g.Id, g.GroupName })
                    .ToListAsync();

                _cache.Set(cacheKey, groups, TimeSpan.FromDays(7));
            }

            return Ok(groups);
        }

        // GET: api/groups/teachers/{teacherId}/has-no-teacher-or-has-teacher
        [HttpGet("teachers/{teacherId}/has-no-teacher-or-has-teacher")]
        public async Task<IActionResult> GetGroupsWithNoTeacherAndTeacherGroups([FromRoute] int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }

            var groups = await _db.Groups.AsNoTracking()
                .Where(g => (g.TeacherId.HasValue && g.TeacherId.Value == teacherId) || !g.TeacherId.HasValue)
                .Select(g => new { g.Id, g.GroupName })
                .ToListAsync();

            return Ok(groups);
        }

        // POST: api/groups
        [HttpPost]
        [AddGroup]
        public async Task<IActionResult> AddGroup([FromBody] AddGroupDTO model)
        {
            var existingGroup = await _db.Groups
                .FirstOrDefaultAsync(g => g.GroupName.Trim() == model.GroupName.Trim());

            if (existingGroup != null)
                return BadRequest(new { message = "الحلقة موجودة مسبقاً" });

            if (model.TeacherId.HasValue && !await _db.Teachers.AnyAsync(x => x.Id == model.TeacherId.Value))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }

            var newGroup = _mapper.Map<Group>(model);

            await _db.Groups.AddAsync(newGroup);
            await _db.SaveChangesAsync();

            InvalidateCache();

            return StatusCode(201, new { message = "تم اضافة الحلقة بنجاح" });
        }

        // PUT: api/groups/{groupId}
        [HttpPut("{groupId}")]
        public async Task<IActionResult> UpdateGroup([FromRoute] int groupId, [FromBody] UpdateGroupDTO model)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var group = await _db.Groups.FindAsync(groupId);
            if (group == null)
                return NotFound(new { message = "لاتوجد حلقة" });

            if (model.TeacherId.HasValue && !await _db.Teachers.AnyAsync(x => x.Id == model.TeacherId.Value))
            {
                return BadRequest(new { message = "لا يوجد معلم" });
            }

            group.GroupName = !string.IsNullOrWhiteSpace(model.GroupName) ? model.GroupName : group.GroupName;
            group.TeacherId = model.TeacherId;
            group.Period = model.Period ?? group.Period;

            _db.Groups.Update(group);
            await _db.SaveChangesAsync();

            InvalidateCache();

            return Ok(new { message = "تم تحديث بيانات الحلقة بنجاح" });
        }

        // DELETE: api/groups/{groupId}
        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup([FromRoute] int groupId)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var group = await _db.Groups.FindAsync(groupId);
            if (group == null)
                return NotFound(new { message = "لاتوجد حلقة" });

            _db.SoftDelete(group);
            await _db.SaveChangesAsync();

            InvalidateCache();

            return Ok(new { message = "تم حذف الحلقة بنجاح" });
        }
        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedGroups()
        {
            var cacheKey = $"{CacheKey}_DeletedGroups";

            if (!_cache.TryGetValue(cacheKey, out var groups))
            {
                var query = _db.Groups.AsNoTracking().IgnoreQueryFilters().Where(x => x.DeletedAt != null).AsQueryable();

                groups = (await query.Select(x => new
                {
                    x.Id,
                    x.GroupName,
                    x.Period,
                    x.Teacher.TeacherName,
                }).ToListAsync())
                .Select(x => new
                {
                    Id = x.Id,
                    GroupName = x.GroupName,
                    Period = GetPeriodName((byte)x.Period),
                    TeacherName = x.TeacherName,
                }).ToList();

                _cache.Set(cacheKey, groups, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(groups);
        }
        
        [HttpPatch("{groupId}/restore")]
        public async Task<IActionResult> RestoreGroup([FromRoute] int groupId)
        {
            if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var group = await _db.Groups.IgnoreQueryFilters().FindAsync(groupId);
            if (group == null)
                return NotFound(new { message = "لاتوجد حلقة" });

            _db.RestoreEntity(group);
            await _db.SaveChangesAsync();
            return Ok(new { message = "تم استرجاع الحلقة بنجاح" });

        }
        // Helper method to invalidate cache
        private void InvalidateCache()
        {
            _cache.Remove(CacheKey);
            _cache.Remove($"{CacheKey}_WithoutTeacher");
            _cache.Remove("GroupsForGeneralUse");
            _cache.Remove("GroupsWithNoTeacher");
            _cache.Remove($"{CacheKey}_DeletedGroups");
        }

        [NonAction]
        private string GetPeriodName(byte period)
        {
            return Enum.GetName(typeof(Period), period) == "MORNING" ? "صباحية" : "مسائية";
        }
    }
}
