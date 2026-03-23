using Jaberah.Helpers;

namespace Jaberah.Models.DTOs
{
    public class QueryDayilyPrayersDTO : PaginationDTO
    {
        public required DateOnly Date { get; set; }
        public List<int?>? GroupsId { get; set; }
        public string? Search { get; set; } = string.Empty;
    }
    public record StudentDailyUpsertDTO
    {
        public int StudentId { get; set; }
        public DateOnly Date { get; set; }
        public List<PrayerUpdateDTO> Prayers { get; set; } = [];
    }

    public class PrayerUpdateDTO
    {
        public int PrayerId { get; set; }
        public byte RakatCount { get; set; }
        public bool IsInGroup { get; set; }
    }

    public class QueryMonthlyPrayersReportDTO : PaginationDTO
    {
        public required DateOnly Date { get; set; }
        public required int DaysInMonth { get; set; }
        public int? GroupId { get; set; }
    }
}
