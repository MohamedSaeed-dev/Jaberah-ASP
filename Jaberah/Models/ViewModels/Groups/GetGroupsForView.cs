namespace Jaberah.Models.ViewModels.Groups
{
    public class GetGroupsForView
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
        public string Period { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int StudentsNo { get; set; }

        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public decimal? FlexibleMinutes { get; set; }
    }
}
