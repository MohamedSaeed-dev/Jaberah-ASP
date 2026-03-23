namespace Jaberah.Models.JaberahModels
{
    public class Group : BaseEntity
    {
        public required string Name { get; set; }
        public int? TeacherId { get; set; }
        public required Period Period { get; set; }

        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public decimal? FlexibleMinutes { get; set; }

        public Teacher? Teacher { get; set; }
        public ICollection<Book>? Books { get; set; }
        public ICollection<Student>? Students { get; set; }

        public ICollection<TeacherAttendance>? TeacherAttendances { get; set; }
    }
    public enum Period
    {
        MORNING = 1,
        EVENING
    }
}
