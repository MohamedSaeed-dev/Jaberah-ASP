namespace Jaberah.Models.ViewModels.Teachers
{
    public class GetTeachersForView
    {
        public int Id { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public List<TeacherGroupsDataForView> Groups { get; set; }
    }
    public class TeacherGroupsDataForView
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }
}
