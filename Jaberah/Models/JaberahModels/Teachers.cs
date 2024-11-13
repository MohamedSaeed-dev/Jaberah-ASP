namespace Jaberah.Models.JaberahModels
{
    public class Teacher
    {
        public int Id { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public Role Role { get; set; }
        public ICollection<Group> Groups { get; set; }
        public ICollection<TeachersSalariesRow> TeachersSalariesRow { get; set; }
        public ICollection<TeachersAttendancesRow> TeachersAttendancesRow { get; set; }
    }
    public enum Role
    {
        ADMIN,
        TEACHER
    }
}
