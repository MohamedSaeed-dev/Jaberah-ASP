using Jaberah.Models.JaberahModels;

namespace Jaberah.Models.ViewModels.TeachersAttendances
{
    public class GetTeachersAttendancesForDay
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public TimeOnly? CheckInTime { get; set; }
        public TimeOnly? CheckOutTime { get; set; }
        public string? Status { get; set; }
    }
}
