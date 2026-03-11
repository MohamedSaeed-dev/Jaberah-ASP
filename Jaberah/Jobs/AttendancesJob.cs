using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.EntityFrameworkCore;

namespace Jaberah.Jobs
{
    public interface IAttendanceJobService
    {
        Task MarkAbsentTeachersAsync();
    }

    public class AttendanceJobService(JaberahDBContext db) : IAttendanceJobService
    {
        private readonly JaberahDBContext _db = db;

        public async Task MarkAbsentTeachersAsync()
        {
            var todayDateTime = DateTime.Today;

            // ⛔ Skip if Friday
            if (todayDateTime.DayOfWeek == DayOfWeek.Friday)
                return;

            var today = DateOnly.FromDateTime(todayDateTime);

            var allTeacherGroups = await _db.Teachers
                .SelectMany(
                    teacher => teacher.Groups,
                    (teacher, group) => new { TeacherId = teacher.Id, GroupId = group.Id }
                )
                .ToListAsync();

            var presentTeacherGroups = await _db.TeacherAttendances
                .Where(a => a.Date == today)
                .Select(a => new { a.TeacherId, a.GroupId })
                .ToListAsync();

            var absentTeacherGroups = allTeacherGroups
                .Where(tg => !presentTeacherGroups
                    .Any(p => p.TeacherId == tg.TeacherId && p.GroupId == tg.GroupId))
                .ToList();

            if (!absentTeacherGroups.Any())
                return;

            var absentRecords = absentTeacherGroups.Select(tg => new TeacherAttendance
            {
                TeacherId = tg.TeacherId,
                GroupId = tg.GroupId,
                Date = today,
                Status = AttendanceStatus.Absent
            }).ToList();

            await _db.TeacherAttendances.AddRangeAsync(absentRecords);
            await _db.SaveChangesAsync();
        }
    }
}
