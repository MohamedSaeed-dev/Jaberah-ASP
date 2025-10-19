namespace Jaberah.Models.DTOs
{
    public class Students
    {
        public record AddStudentDTO
        {
            public string StudentName { get; set; }
            public string PhoneNumber { get; set; }
            public string? SchoolClass { get; set; }
            public string? SchoolLevel { get; set; }
            public string? StudyLevel { get; set; }
            public int? MemoRate { get; set; }
            public string? Notes { get; set; }
            public int? GroupId { get; set; }
        }
        public record UpdateStudentDTO
        {
            public string? StudentName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? SchoolClass { get; set; }
            public string? SchoolLevel { get; set; }
            public string? StudyLevel { get; set; }
            public int? MemoRate { get; set; }
            public string? Notes { get; set; }
            public int? GroupId { get; set; }
        }
    }
}
