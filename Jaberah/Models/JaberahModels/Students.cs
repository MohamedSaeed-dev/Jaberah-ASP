namespace Jaberah.Models.JaberahModels
{
    public class Student : BaseEntity
    {
        public required string Name { get; set; }
        public required string PhoneNumber { get; set; }
        public string? SchoolClass { get; set; }
        public int? MemoRate { get; set; }
        public string? SchoolLevel { get; set; }
        public string? StudyLevel { get; set; }
        public string? Notes { get; set; }
        public int? GroupId { get; set; }
        public Group? Group { get; set; }
        public ICollection<SaveLesson>? SaveLessons { get; set; }
        public ICollection<ReviewLesson>? ReviewLessons { get; set; }
        public ICollection<StudentAttendance>? StudentAttendances { get; set; }
        public ICollection<MidFinal>? MidFinals { get; set; }
        public ICollection<Exam>? Exams { get; set; }
        public ICollection<PartialExam>? PartialExams { get; set; }
        public ICollection<StudentPrayerAttendance> Attendances { get; set; } = [];
        public ICollection<CleaningLog> CleaningLogs { get; set; } = [];
    }
}
