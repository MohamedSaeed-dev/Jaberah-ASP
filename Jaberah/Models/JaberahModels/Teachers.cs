namespace Jaberah.Models.JaberahModels
{
    public class Teacher : BaseEntity
    {
        public required string Name { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Password { get; set; }
        public string? FCMToken { get; set; }
        public DateTime? LastLogin {  get; set; } = DateTime.Now;
        public Role Role { get; set; }
        public ICollection<Group>? Groups { get; set; }
        public ICollection<TeacherSalary>? Salaries { get; set; }
        public ICollection<TeacherAttendance>? Attendances { get; set; }
    }
    public enum Role
    {
        ADMIN = 1,
        TEACHER
    }
}
