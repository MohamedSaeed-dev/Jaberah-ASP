namespace Jaberah.Models.JaberahModels
{
    public class StudentPrayerAttendance : BaseEntity
    {
        public int StudentId { get; set; }
        public Student Student { get; set; } = default!;

        public int PrayerId { get; set; }
        public Prayer Prayer { get; set; } = default!;

        public DateOnly PrayerDate { get; set; }

        public byte RakatsCount { get; set; }
        public bool IsInGroup { get; set; }
    }
}
