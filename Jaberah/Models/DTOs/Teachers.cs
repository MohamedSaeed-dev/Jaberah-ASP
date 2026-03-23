namespace Jaberah.Models.DTOs
{
    public class Teachers
    {
        public record AddTeacherDTO
        {
            public string TeacherName { get; set; }
            public string PhoneNumber { get; set; }
            public ICollection<int>? GroupsId { get; set; }
        }
        public record UpdateTeacherDTO
        {
            public string? TeacherName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? OldPassword { get; set; }
            public string? NewPassword { get; set; }

            public ICollection<int>? GroupsId { get; set; }
        }
    }
}
