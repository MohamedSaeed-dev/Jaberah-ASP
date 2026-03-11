namespace Jaberah.Models.JaberahModels
{
    public class TeacherAttendance : BaseEntity
    {
        public DateOnly? Date { get; set; }
        public AttendanceStatus Status { get; set; }

        public TimeOnly? CheckInTime { get; set; }
        public TimeOnly? CheckOutTime { get; set; }

        public string? Notes { get; set; }

        // FK
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;
    }

    public enum AttendanceStatus
    {
        Present = 1,
        Absent = 2,
        Late = 3,
        Excused = 4
    }
}