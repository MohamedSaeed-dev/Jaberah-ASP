namespace Jaberah.Models.ViewModels.Prayers
{
    public class StudentDailyPrayerDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = default!;
        public string? GroupName { get; set; }
        public List<PrayerStatusDto> Prayers { get; set; } = [];
    }

    public class PrayerStatusDto
    {
        public string PrayerName { get; set; } = default!;
        public byte DefaultRakat { get; set; }
        public PrayerAttendanceInfo AttendanceInfo { get; set; } = new PrayerAttendanceInfo();
    }
    public class PrayerAttendanceInfo
    {
        public byte? RakatsCount { get; set; }
        public bool IsInGroup { get; set; }
    }
}
