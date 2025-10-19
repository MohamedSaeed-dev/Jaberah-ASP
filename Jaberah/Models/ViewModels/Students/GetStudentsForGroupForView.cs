namespace Jaberah.Models.ViewModels.Students
{
    public class GetStudentsForGroupForView
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? SchoolClass { get; set; }
        public string? StudyLevel { get; set; }
        public int? MemoRate { get; set; }
        public string? SchoolLevel { get; set; }
        public string? Notes { get; set; }
    }
}
