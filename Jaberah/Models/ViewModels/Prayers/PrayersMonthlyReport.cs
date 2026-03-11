namespace Jaberah.Models.ViewModels.Prayers
{
    public class PrayersMonthlyReportDTO
    {
        public int TotalPossiblePrayersPerStudent { get; set; }

        public double AverageCommitmentPercentage { get; set; }

        public List<StudentPrayerMonthlyStatsDTO> Students { get; set; } = [];
    }

    public class StudentPrayerMonthlyStatsDTO
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = default!;
        public string? GroupName { get; set; }
        public int TotalPrayed { get; set; }
        public double TotalPrayedPercentage { get; set; }

        public int TotalGroupPrayed { get; set; }
        public double GroupPercentage { get; set; }

        public int MissedPrayers { get; set; }
        public double MissedPercentage { get; set; }
    }
}
