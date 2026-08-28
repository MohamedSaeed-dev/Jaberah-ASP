namespace Jaberah.Models.ViewModels.CleaningLogs
{
    public class CleaningTaskDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = default!;
        public string? NameEn { get; set; }
        public byte DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class DailyCleaningTaskDto
    {
        public int CleaningTaskId { get; set; }
        public string TaskName { get; set; } = default!;
        public byte DisplayOrder { get; set; }

        /// <summary>يكون null إذا كانت المهمة غير مسندة لأي طالب في ذلك اليوم.</summary>
        public CleaningLogInfoDto? Log { get; set; }

        /// <summary>false إذا كانت المهمة مسندة لطالب خارج حلقات المستخدم الحالي.</summary>
        public bool IsEditableByMe { get; set; }
    }

    public class CleaningLogInfoDto
    {
        public int LogId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = default!;
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public bool IsCompleted { get; set; }
        public string? Notes { get; set; }
    }

    public class AssignableStudentDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = default!;
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }

        /// <summary>المهمات المسندة لهذا الطالب في ذلك اليوم (يجوز أن تكون أكثر من واحدة).</summary>
        public List<string> AssignedTaskNames { get; set; } = [];
    }
}
