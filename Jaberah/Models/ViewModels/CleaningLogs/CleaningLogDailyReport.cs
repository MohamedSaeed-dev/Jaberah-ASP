namespace Jaberah.Models.ViewModels.CleaningLogs
{
    public class CleaningLogDailyReportDTO
    {
        public DateOnly Date { get; set; }

        public int TotalTasks { get; set; }
        public int AssignedCount { get; set; }
        public int CompletedCount { get; set; }
        public int NotCompletedCount { get; set; }
        public double CompletionPercentage { get; set; }

        public List<CleaningLogReportRowDTO> Rows { get; set; } = [];

        /// <summary>المهمات النشطة التي لم تُسند لأي طالب في ذلك اليوم.</summary>
        public List<CleaningTaskDto> UnassignedTasks { get; set; } = [];
    }

    public class CleaningLogReportRowDTO
    {
        public int CleaningTaskId { get; set; }
        public string TaskName { get; set; } = default!;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = default!;
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public bool IsCompleted { get; set; }
        public string? Notes { get; set; }
    }
}
