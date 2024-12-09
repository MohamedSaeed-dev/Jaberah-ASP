namespace Jaberah.Models.ViewModels.Students
{
    public class GetStudentsForView
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? SchoolClass { get; set; }
        public string? MemoRate { get; set; }
        public string? SchoolLevel { get; set; }
        public string? Notes { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
    }
}
