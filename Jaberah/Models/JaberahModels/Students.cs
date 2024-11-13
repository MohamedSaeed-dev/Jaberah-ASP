namespace Jaberah.Models.JaberahModels
{
    public class Student
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string SchoolClass { get; set; } = string.Empty;
        public string MemoRate { get; set; } = string.Empty;
        public string SchoolLevel { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public Group Group { get; set; }
        public ICollection<FollowStudentInMonth> FollowStudentInMonth { get; set; }
    }
}
