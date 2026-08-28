using Jaberah.Helpers;
using Jaberah.Middlewares;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Jaberah.Models.ViewModels;
using Jaberah.Models.ViewModels.CleaningLogs;
using Jaberah.Validations.CleaningLogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jaberah.Controllers
{
    [Route("api/cleaning-logs")]
    [ApiController]
    [ServiceFilter(typeof(VerifyTokenAttribute))]
    public class CleaningLogsController(JaberahDBContext db, IMemoryCache cache) : ControllerBase
    {
        private readonly JaberahDBContext _db = db;
        private readonly IMemoryCache _cache = cache;

        private const string TasksCacheKey = "allCleaningTasks";
        private const int MaxPageSize = 100;

        // ارقام اخطاء SQL Server عند خرق فهرس فريد
        private const int DuplicateKeyIndexError = 2601;
        private const int DuplicateKeyConstraintError = 2627;

        private UserViewModel CurrentUser => (UserViewModel)HttpContext.Items["User"]!;

        private bool IsCurrentUserAdmin => CurrentUser.Role == nameof(Role.ADMIN);

        #region المهمات (مرجع ديناميكي - API فقط)

        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks([FromQuery] bool onlyActive = true)
        {
            var tasks = await GetAllTasksAsync();
            return Ok(onlyActive ? tasks.Where(t => t.IsActive).ToList() : tasks);
        }

        [IsAdmin]
        [AddCleaningTask]
        [HttpPost("tasks")]
        public async Task<IActionResult> AddTask([FromBody] AddCleaningTaskDTO model)
        {
            var nameAr = model.NameAr!.Trim();
            var nameEn = string.IsNullOrWhiteSpace(model.NameEn) ? null : model.NameEn.Trim();

            if (await _db.CleaningTasks.AnyAsync(t => t.NameAr == nameAr))
                return BadRequest(new { message = "اسم المهمة موجود مسبقا" });

            var displayOrder = model.DisplayOrder;
            if (displayOrder == 0)
            {
                var maxOrder = await _db.CleaningTasks.AsNoTracking().MaxAsync(t => (byte?)t.DisplayOrder) ?? 0;
                displayOrder = (byte)Math.Min(maxOrder + 1, byte.MaxValue);
            }

            var task = new CleaningTask
            {
                NameAr = nameAr,
                NameEn = nameEn,
                DisplayOrder = displayOrder,
                IsActive = true,
            };

            await _db.CleaningTasks.AddAsync(task);
            await _db.SaveChangesAsync();
            _cache.Remove(TasksCacheKey);

            return Ok(new { message = "تم اضافة المهمة بنجاح", task.Id });
        }

        [IsAdmin]
        [UpdateCleaningTask]
        [HttpPut("tasks/{taskId}")]
        public async Task<IActionResult> UpdateTask([FromRoute] int taskId, [FromBody] UpdateCleaningTaskDTO model)
        {
            if (taskId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var task = await _db.CleaningTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task is null) return BadRequest(new { message = "لاتوجد مهمة" });

            if (model.NameAr is not null)
            {
                var nameAr = model.NameAr.Trim();
                if (await _db.CleaningTasks.AnyAsync(t => t.Id != taskId && t.NameAr == nameAr))
                    return BadRequest(new { message = "اسم المهمة موجود مسبقا" });

                task.NameAr = nameAr;
            }

            if (model.NameEn is not null)
                task.NameEn = string.IsNullOrWhiteSpace(model.NameEn) ? null : model.NameEn.Trim();

            if (model.DisplayOrder.HasValue) task.DisplayOrder = model.DisplayOrder.Value;
            if (model.IsActive.HasValue) task.IsActive = model.IsActive.Value;

            await _db.SaveChangesAsync();
            _cache.Remove(TasksCacheKey);

            return Ok(new { message = "تم تعديل المهمة بنجاح" });
        }

        // تعطيل وليس حذف، حتى لا تنكسر تقارير الايام السابقة
        [IsAdmin]
        [HttpDelete("tasks/{taskId}")]
        public async Task<IActionResult> DeactivateTask([FromRoute] int taskId)
        {
            if (taskId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var task = await _db.CleaningTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task is null) return BadRequest(new { message = "لاتوجد مهمة" });

            if (!task.IsActive) return Ok(new { message = "المهمة معطلة مسبقا" });

            task.IsActive = false;
            await _db.SaveChangesAsync();
            _cache.Remove(TasksCacheKey);

            return Ok(new { message = "تم تعطيل المهمة بنجاح" });
        }

        #endregion

        #region كشف النظافة اليومي

        [HttpGet("daily")]
        public async Task<IActionResult> GetDaily([FromQuery] QueryDailyCleaningLogDTO query)
        {
            if (query.Date == default) return BadRequest(new { message = "ادخل تاريخ صحيح" });

            var tasks = (await GetAllTasksAsync()).Where(t => t.IsActive).ToList();
            if (tasks.Count == 0) return Ok(Enumerable.Empty<DailyCleaningTaskDto>());

            var taskIds = tasks.Select(t => t.Id).ToList();

            var logs = await _db.CleaningLogs
                .AsNoTracking()
                .Where(l => l.Date == query.Date && taskIds.Contains(l.CleaningTaskId))
                .Select(l => new
                {
                    LogId = l.Id,
                    l.CleaningTaskId,
                    l.StudentId,
                    StudentName = l.Student.Name,
                    l.Student.GroupId,
                    GroupName = l.Student.Group != null ? l.Student.Group.Name : null,
                    TeacherId = l.Student.Group != null ? l.Student.Group.TeacherId : null,
                    l.IsCompleted,
                    l.Notes,
                })
                .ToDictionaryAsync(l => l.CleaningTaskId);

            var user = CurrentUser;
            var isAdmin = IsCurrentUserAdmin;

            var result = tasks.Select(task =>
            {
                logs.TryGetValue(task.Id, out var log);

                return new DailyCleaningTaskDto
                {
                    CleaningTaskId = task.Id,
                    TaskName = task.NameAr,
                    DisplayOrder = task.DisplayOrder,
                    IsEditableByMe = isAdmin || log is null || log.TeacherId == user.Id,
                    Log = log is null ? null : new CleaningLogInfoDto
                    {
                        LogId = log.LogId,
                        StudentId = log.StudentId,
                        StudentName = log.StudentName,
                        GroupId = log.GroupId,
                        GroupName = log.GroupName,
                        IsCompleted = log.IsCompleted,
                        Notes = log.Notes,
                    },
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("assignable-students")]
        public async Task<IActionResult> GetAssignableStudents([FromQuery] QueryAssignableStudentsDTO query)
        {
            if (query.Date == default) return BadRequest(new { message = "ادخل تاريخ صحيح" });
            if (query.GroupId.HasValue && query.GroupId.Value <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var (pageNumber, pageSize) = (Math.Max(query.PageNumber, 1), Math.Clamp(query.PageSize, 1, MaxPageSize));

            var studentsQuery = _db.Students.AsNoTracking();

            // المعلم لا يرى الا طلاب حلقاته، والمدير يرى الجميع
            if (!IsCurrentUserAdmin)
            {
                var teacherId = CurrentUser.Id;
                studentsQuery = studentsQuery.Where(s => s.Group != null && s.Group.TeacherId == teacherId);
            }

            if (query.GroupId.HasValue)
                studentsQuery = studentsQuery.Where(s => s.GroupId == query.GroupId.Value);

            if (!string.IsNullOrWhiteSpace(query.Search))
                studentsQuery = studentsQuery.Where(s => s.Name.Contains(query.Search));

            var students = await studentsQuery
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.GroupId,
                    GroupName = s.Group != null ? s.Group.Name : null,
                })
                .ToPagedListAsync(pageNumber, pageSize);

            var studentIds = students.Data.Select(s => s.Id).ToList();

            var assignedTasksByStudent = studentIds.Count == 0
                ? []
                : (await _db.CleaningLogs
                    .AsNoTracking()
                    .Where(l => l.Date == query.Date && studentIds.Contains(l.StudentId))
                    .OrderBy(l => l.CleaningTask.DisplayOrder)
                    .Select(l => new { l.StudentId, TaskName = l.CleaningTask.NameAr })
                    .ToListAsync())
                    .GroupBy(l => l.StudentId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.TaskName).ToList());

            var result = new
            {
                Data = students.Data.Select(student => new AssignableStudentDto
                {
                    StudentId = student.Id,
                    StudentName = student.Name,
                    GroupId = student.GroupId,
                    GroupName = student.GroupName,
                    AssignedTaskNames = assignedTasksByStudent.TryGetValue(student.Id, out var names) ? names : [],
                }).ToList(),
                students.TotalCount,
                students.TotalPages,
                students.HasNext,
                students.HasPrevious,
            };

            return Ok(result);
        }

        /// <summary>
        /// تحديث جزئي: يمسّ فقط المهمات المذكورة في الطلب، فلا يمسح معلمٌ اسنادات معلم اخر.
        /// StudentId = null تعني الغاء الاسناد.
        /// </summary>
        [UpsertDailyCleaningLog]
        [HttpPost("upsert-daily")]
        public async Task<IActionResult> UpsertDaily([FromBody] UpsertDailyCleaningLogDTO model)
        {
            var user = CurrentUser;
            var isAdmin = IsCurrentUserAdmin;

            var taskIds = model.Logs.Select(x => x.CleaningTaskId).ToList();

            var activeTasksCount = await _db.CleaningTasks
                .AsNoTracking()
                .CountAsync(t => taskIds.Contains(t.Id) && t.IsActive);

            if (activeTasksCount != taskIds.Count)
                return BadRequest(new { message = "توجد مهمة غير موجودة او معطلة" });

            var requestedStudentIds = model.Logs
                .Where(x => x.StudentId.HasValue)
                .Select(x => x.StudentId!.Value)
                .Distinct()
                .ToList();

            var studentOwners = await GetStudentTeacherIdsAsync(requestedStudentIds);

            foreach (var studentId in requestedStudentIds)
            {
                if (!studentOwners.TryGetValue(studentId, out var teacherId))
                    return BadRequest(new { message = "لايوجد طالب" });

                if (!isAdmin && teacherId != user.Id)
                    return BadRequest(new { message = "لا يمكنك اسناد مهمة لطالب من حلقة اخرى" });
            }

            var existingLogs = await _db.CleaningLogs
                .Where(l => l.Date == model.Date && taskIds.Contains(l.CleaningTaskId))
                .ToListAsync();

            // المعلم لا يعدّل مهمة مسندة اصلا لطالب من حلقة معلم اخر
            if (!isAdmin && existingLogs.Count > 0)
            {
                var currentOwners = await GetStudentTeacherIdsAsync(
                    [.. existingLogs.Select(l => l.StudentId).Distinct()]);

                if (existingLogs.Any(l => !currentOwners.TryGetValue(l.StudentId, out var teacherId) || teacherId != user.Id))
                    return BadRequest(new { message = "المهمة مسندة لحلقة اخرى" });
            }

            foreach (var item in model.Logs)
            {
                var existing = existingLogs.FirstOrDefault(l => l.CleaningTaskId == item.CleaningTaskId);
                var notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

                if (item.StudentId is null)
                {
                    if (existing is not null) _db.SoftDelete(existing);
                    continue;
                }

                if (existing is null)
                {
                    await _db.CleaningLogs.AddAsync(new CleaningLog
                    {
                        CleaningTaskId = item.CleaningTaskId,
                        StudentId = item.StudentId.Value,
                        Date = model.Date,
                        IsCompleted = item.IsCompleted,
                        Notes = notes,
                    });
                }
                else
                {
                    existing.StudentId = item.StudentId.Value;
                    existing.IsCompleted = item.IsCompleted;
                    existing.Notes = notes;
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
            {
                // سباق بين معلمين على نفس المهمة في نفس اليوم
                return Conflict(new { message = "تم اسناد هذه المهمة لطالب اخر اليوم" });
            }

            return Ok(new { message = "تم حفظ كشف النظافة بنجاح" });
        }

        #endregion

        #region تقرير النظافة اليومي

        [HttpGet("daily-report")]
        public async Task<IActionResult> GetDailyReport([FromQuery] QueryCleaningLogDailyReportDTO query)
        {
            if (query.Date == default) return BadRequest(new { message = "ادخل تاريخ صحيح" });
            if (query.GroupId.HasValue && query.GroupId.Value <= 0) return BadRequest(new { message = "ادخل id صحيح" });

            var tasks = (await GetAllTasksAsync()).Where(t => t.IsActive).ToList();

            var logsQuery = _db.CleaningLogs.AsNoTracking().Where(l => l.Date == query.Date);

            if (query.GroupId.HasValue)
                logsQuery = logsQuery.Where(l => l.Student.GroupId == query.GroupId.Value);

            var rows = await logsQuery
                .OrderBy(l => l.CleaningTask.DisplayOrder)
                .ThenBy(l => l.CleaningTaskId)
                .Select(l => new CleaningLogReportRowDTO
                {
                    CleaningTaskId = l.CleaningTaskId,
                    TaskName = l.CleaningTask.NameAr,
                    StudentId = l.StudentId,
                    StudentName = l.Student.Name,
                    GroupId = l.Student.GroupId,
                    GroupName = l.Student.Group != null ? l.Student.Group.Name : null,
                    IsCompleted = l.IsCompleted,
                    Notes = l.Notes,
                })
                .ToListAsync();

            // المهمات غير المسندة تُحسب على مستوى اليوم كاملا، لا على الحلقة المختارة
            var assignedTaskIds = query.GroupId.HasValue
                ? await _db.CleaningLogs
                    .AsNoTracking()
                    .Where(l => l.Date == query.Date)
                    .Select(l => l.CleaningTaskId)
                    .Distinct()
                    .ToListAsync()
                : [.. rows.Select(r => r.CleaningTaskId).Distinct()];

            var completedCount = rows.Count(r => r.IsCompleted);

            // مهمة عُطّلت بعد اسنادها اليوم تبقى ضمن مهمات اليوم حتى لا يقل الاجمالي عن المسند
            var activeTaskIds = tasks.Select(t => t.Id).ToHashSet();
            var deactivatedButAssignedCount = assignedTaskIds.Count(id => !activeTaskIds.Contains(id));

            var report = new CleaningLogDailyReportDTO
            {
                Date = query.Date,
                TotalTasks = tasks.Count + deactivatedButAssignedCount,
                AssignedCount = rows.Count,
                CompletedCount = completedCount,
                NotCompletedCount = rows.Count - completedCount,
                CompletionPercentage = rows.Count == 0
                    ? 0
                    : Math.Round((double)completedCount * 100 / rows.Count, 2),
                Rows = rows,
                UnassignedTasks = [.. tasks.Where(t => !assignedTaskIds.Contains(t.Id))],
            };

            return Ok(report);
        }

        #endregion

        #region مساعدات

        private async Task<List<CleaningTaskDto>> GetAllTasksAsync()
        {
            if (!_cache.TryGetValue(TasksCacheKey, out List<CleaningTaskDto>? tasks) || tasks is null)
            {
                tasks = await _db.CleaningTasks
                    .AsNoTracking()
                    .OrderBy(t => t.DisplayOrder)
                    .ThenBy(t => t.Id)
                    .Select(t => new CleaningTaskDto
                    {
                        Id = t.Id,
                        NameAr = t.NameAr,
                        NameEn = t.NameEn,
                        DisplayOrder = t.DisplayOrder,
                        IsActive = t.IsActive,
                    })
                    .ToListAsync();

                _cache.Set(TasksCacheKey, tasks, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromHours(24)
                });
            }

            return tasks;
        }

        private async Task<Dictionary<int, int?>> GetStudentTeacherIdsAsync(List<int> studentIds)
        {
            if (studentIds.Count == 0) return [];

            return await _db.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, TeacherId = s.Group != null ? s.Group.TeacherId : null })
                .ToDictionaryAsync(s => s.Id, s => s.TeacherId);
        }

        private static bool IsDuplicateKeyViolation(DbUpdateException exception) =>
            exception.InnerException is SqlException sqlException &&
            (sqlException.Number == DuplicateKeyIndexError || sqlException.Number == DuplicateKeyConstraintError);

        #endregion
    }
}
