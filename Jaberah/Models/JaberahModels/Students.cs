namespace Jaberah.Models.JaberahModels
{
    public class Student : BaseEntity
    {
        public string StudentName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? SchoolClass { get; set; }
        public string? MemoRate { get; set; }
        public string? SchoolLevel { get; set; }
        public string? Notes { get; set; }
        public int? GroupId { get; set; }
        public Group Group { get; set; }
        public ICollection<FollowStudent> FollowStudents { get; set; }
        public ICollection<MidFinal> MidFinals { get; set; }
    }
}
