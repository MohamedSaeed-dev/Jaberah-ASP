namespace Jaberah.Models.JaberahModels
{
    public class Teacher : BaseEntity
    {
        public string TeacherName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FCMToken { get; set; }
        public DateTime LastLogin {  get; set; } = DateTime.Now;
        public Role Role { get; set; }
        public ICollection<Group> Groups { get; set; }
        public ICollection<TeachersSalariesRow> TeachersSalariesRow { get; set; }
        public ICollection<TeachersAttendancesRow> TeachersAttendancesRow { get; set; }
    }
    public enum Role
    {
        ADMIN = 1,
        TEACHER
    }
}
