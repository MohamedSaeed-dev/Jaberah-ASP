using Jaberah.Models.JaberahModels;

namespace Jaberah.Models.ViewModels.Teachers
{
    public class AuthTeacher
    {
        public int Id { get; set; }
        public string TeacherName { get; set; }
        public string PhoneNumber { get; set; }
        public Role Role { get; set; }
    }
}
