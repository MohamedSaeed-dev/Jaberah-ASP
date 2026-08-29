using AutoMapper;
using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels.Groups;
using Jaberah.Models.ViewModels.Teachers;
using Jaberah.Validations.Teachers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static Jaberah.Models.DTOs.Teachers;
using Jaberah.Middlewares;

namespace Jaberah.Controllers
{
    [Route("api/teachers")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    public class TeachersController(JaberahDBContext db, IMapper mapper, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMapper _mapper = mapper;
        private readonly IMemoryCache _cache = cache;

        [IsAdmin]
        [HttpGet]
        public async Task<IActionResult> GetTeachers([FromQuery] string searchText = "", [FromQuery] bool withoutGroups = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Teachers.AsNoTracking().Where(x => x.Name.Contains(searchText)).AsQueryable();
            if (withoutGroups) query = query.Where(x => x.Groups == null || x.Groups.Count < 1).AsQueryable();

            var selectedQuery = query.Select(x => new GetTeachersForView
            {
                Id = x.Id,
                TeacherName = x.Name,
                PhoneNumber = x.PhoneNumber,
                Groups = x.Groups.Select(y => new TeacherGroupsDataForView
                {
                    GroupId = y.Id,
                    GroupName = y.Name,
                }).ToList()
            }).AsQueryable();

            // تصفيح بلا ORDER BY — انظر التعليق نفسه في StudentsController.
            var pagedTeachers = (await selectedQuery
                    .OrderBy(x => x.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync())
                .ToPagedList(await selectedQuery.CountAsync(), pageNumber, pageSize);

            return Ok(pagedTeachers);
        }

        [IsAdmin]
        [HttpGet("for-general-use")]
        public async Task<IActionResult> GetTeachersForGeneralUse()
        {
            const string cacheKey = "TeachersForGeneralUse";

            if (!_cache.TryGetValue(cacheKey, out List<GetTeachersForGeneralUse>? teachers))
            {
                teachers = await _db.Teachers.AsNoTracking()
                    .Select(x => new GetTeachersForGeneralUse
                    {
                        Id = x.Id,
                        TeacherName = x.Name,
                    }).ToListAsync();

                _cache.Set(cacheKey, teachers, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(teachers);
        }

        [HttpGet("{teacherId}/groups")]
        public async Task<IActionResult> GetGroupsOfTeacher([FromRoute] int teacherId)
        {
            // معلم يقرأ بيانات نفسه فقط؛ المدير يقرأ أي معلم.
            if (!this.CanActOnTeacher(teacherId))
                return Forbid();

            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }
            var query = await _db.Groups.AsNoTracking().Where(x => x.TeacherId.HasValue && x.TeacherId.Value == teacherId)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.TeacherId,
                    x.Students.Count,
                    TeacherName = x.Teacher.Name,
                    x.Period,
                }).ToListAsync();

            if (query is null) return BadRequest(new { message = "لاتوجد حلقات لهذا المعلم" });

            return Ok(query.Select(x => new GetGroupForView
            {
                Id = x.Id,
                GroupName = x.Name,
                TeacherId = x.TeacherId,
                StudentsNo = x.Count,
                TeacherName = x.TeacherName,
                Period = GetPeriodName((byte)x.Period),
            }));
        }

        [HttpGet("{teacherId}/groups/for-general-use")]
        public async Task<IActionResult> GetGroupsOfTeacherForGeneralUse([FromRoute] int teacherId)
        {
            // معلم يقرأ بيانات نفسه فقط؛ المدير يقرأ أي معلم.
            if (!this.CanActOnTeacher(teacherId))
                return Forbid();

            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });
            if (!await _db.Teachers.AnyAsync(x => x.Id == teacherId))
            {
                return BadRequest(new { message = "لايوجد معلم" });
            }
            var query = await _db.Groups.AsNoTracking().Where(x => x.TeacherId.HasValue && x.TeacherId.Value == teacherId)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                }).ToListAsync();

            if (query is null) return BadRequest(new { message = "لاتوجد حلقات لهذا المعلم" });

            return Ok(query.Select(x => new GetGroupsOfTeacherForGeneralUse
            {
                Id = x.Id,
                Name = x.Name
            }));
        }


        [IsAdmin]
        [AddTeacher]
        [HttpPost]
        public async Task<IActionResult> AddTeacher([FromBody] AddTeacherDTO model)
        {
            // فحص وجود فقط: AnyAsync لا يجلب أعمدة ولا يتعقّب كيانًا.
            var existingTeacher = await _db.Teachers
                .AnyAsync(t => t.Name == model.TeacherName.Trim());

            if (existingTeacher)
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
                return BadRequest(new { message = "هناك معلمين مرتبطين ببعض هذه الحلقات" });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.PhoneNumber);

            var newTeacher = _mapper.Map<Teacher>(model);
            newTeacher.Name = newTeacher.Name.Trim();
            newTeacher.Password = hashedPassword;
            newTeacher.Role = Role.TEACHER;
            newTeacher.Groups = groups;

            await _db.Teachers.AddAsync(newTeacher);
            await _db.SaveChangesAsync();
            InvalidateCache();
            return StatusCode(201, new { message = "تم اضافة المعلم بنجاح" });
        }
        [UpdateTeacher]
        [HttpPut("{teacherId}")]
        public async Task<IActionResult> UpdateTeacher(int teacherId, [FromBody] UpdateTeacherDTO model)
        {
            // شاشة «تغيير معلومات المعلم» يفتحها المعلم لنفسه والمدير لأي معلم،
            // فلا يصلح تقييدها بالمدير؛ الحصر على الهوية بدلًا من ذلك.
            if (!this.CanActOnTeacher(teacherId))
                return Forbid();

            // إسناد الحلقات صلاحية إدارية، فلا يقبل من معلم يعدّل بياناته.
            if (!this.IsCurrentUserAdmin())
                model = model with { GroupsId = null };

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
                    .AsNoTracking()
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
                        return BadRequest(new { message = "هناك معلمين مرتبطين ببعض هذه الحلقات" });
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

            teacher.Name = string.IsNullOrEmpty(model.TeacherName) ? teacher.Name.Trim() : model.TeacherName.Trim();
            teacher.PhoneNumber = string.IsNullOrEmpty(model.PhoneNumber) ? teacher.PhoneNumber : model.PhoneNumber;
            // حقل GroupsId غير مُرسَل يعني «لا تمسّ الحلقات»، وقائمة فارغة تعني «أفرغها».
            // الشرط السابق كان يسوّي بين الحالتين ويُسند [] في كلتيهما، فكل حفظ من شاشة
            // «تغيير معلومات المعلم» — وهي لا ترسل الحقل أصلًا — يفصل المعلم عن كل حلقاته.
            if (model.GroupsId is not null)
            {
                teacher.Groups = model.GroupsId.Count > 0
                    ? await _db.Groups
                        .Where(g => model.GroupsId.Contains(g.Id))
                        .ToListAsync()
                    : [];
            }
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync();
            InvalidateCache();
            _cache.Remove("GroupsWithNoTeacher");
            return Ok(new { message = "تم تحديث بيانات المعلم بنجاح" });

        }
        [IsAdmin]
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
            teacher.Groups = null;
            _db.SoftDelete(teacher);
            await _db.SaveChangesAsync();
            InvalidateCache();
            _cache.Remove("DeletedTeachers");
            return Ok(new { message = "تم حذف المعلم بنجاح" });
        }

        [IsAdmin]
        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedTeachers()
        {
            var cacheKey = $"DeletedTeachers";

            if (!_cache.TryGetValue(cacheKey, out List<GetDeletedTeachersForView>? teachers))
            {
                var query = _db.Teachers.AsNoTracking().IgnoreQueryFilters().Where(x => x.DeletedAt != null).AsQueryable();

                teachers = (await query.Select(x => new GetDeletedTeachersForView
                {
                    Id = x.Id,
                    TeacherName = x.Name,
                    PhoneNumber = x.PhoneNumber,
                }).ToListAsync());

                _cache.Set(cacheKey, teachers, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                });
            }

            return Ok(teachers);
        }

        [IsAdmin]
        [HttpDelete("{teacherId}/ever")]
        public async Task<IActionResult> DeleteTeacherEver([FromRoute] int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var teacher = await _db.Teachers.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == teacherId);
            if (teacher == null)
                return NotFound(new { message = "لايوجد معلم" });

            _db.Remove(teacher);
            await _db.SaveChangesAsync();

            _cache.Remove("DeletedTeachers");

            return Ok(new { message = "تم حذف المعلم نهائياً بنجاح" });
        }

        [IsAdmin]
        [HttpPatch("{teacherId}/restore")]
        public async Task<IActionResult> RestoreTeacher([FromRoute] int teacherId)
        {
            if (teacherId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var teacher = await _db.Teachers.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == teacherId);
            if (teacher == null)
                return NotFound(new { message = "لايوجد معلم" });

            _db.RestoreEntity(teacher);
            await _db.SaveChangesAsync();
            InvalidateCache();
            _cache.Remove("DeletedTeachers");
            return Ok(new { message = "تم استرجاع المعلم بنجاح" });

        }

        // Helper method to invalidate cache
        private void InvalidateCache()
        {
            _cache.Remove("GroupsCache");
            _cache.Remove("GroupsCache_WithoutTeacher");
            _cache.Remove("TeachersForGeneralUse");
        }

        [NonAction]
        private string GetPeriodName(byte period)
        {
            return (Enum.GetName(typeof(Period), period) == "MORNING" ? "صباحية" : "مسائية");
        }
    }

}
