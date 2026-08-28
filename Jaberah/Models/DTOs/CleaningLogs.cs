using Jaberah.Helpers;

namespace Jaberah.Models.DTOs
{
    public class AddCleaningTaskDTO
    {
        public string? NameAr { get; set; }
        public string? NameEn { get; set; }
        public byte DisplayOrder { get; set; }
    }

    public class UpdateCleaningTaskDTO
    {
        public string? NameAr { get; set; }
        public string? NameEn { get; set; }
        public byte? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    public class QueryDailyCleaningLogDTO
    {
        public required DateOnly Date { get; set; }
    }

    public class QueryAssignableStudentsDTO : PaginationDTO
    {
        public required DateOnly Date { get; set; }
        public int? GroupId { get; set; }
        public string? Search { get; set; } = string.Empty;
    }

    public class QueryCleaningLogDailyReportDTO
    {
        public required DateOnly Date { get; set; }
        public int? GroupId { get; set; }
    }

    public record UpsertDailyCleaningLogDTO
    {
        public DateOnly Date { get; set; }
        public List<CleaningLogUpdateDTO> Logs { get; set; } = [];
    }

    public class CleaningLogUpdateDTO
    {
        public int CleaningTaskId { get; set; }

        /// <summary>الطالب المُسنَد للمهمة، و null تعني إلغاء الإسناد.</summary>
        public int? StudentId { get; set; }
        public bool IsCompleted { get; set; }
        public string? Notes { get; set; }
    }
}
