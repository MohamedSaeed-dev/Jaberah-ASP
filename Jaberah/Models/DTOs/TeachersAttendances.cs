namespace Jaberah.Models.DTOs
{
    public record UpsertTeachersAttendancesDTO
    {
        public int TeacherId { get; set; }
        public int GroupId { get; set; }
        public TimeOnly? CheckInTime { get; set; }
        public TimeOnly? CheckOutTime { get; set; }
        public bool? IsExcused { get; set; } = null;
    }

    public record TeacherCheckInDTO
    {
        public int GroupId { get; set; }
    }

    public record TeacherCheckOutDTO
    {
        public int GroupId { get; set; }
    }
}
