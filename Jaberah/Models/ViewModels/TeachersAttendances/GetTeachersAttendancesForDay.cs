namespace Jaberah.Models.ViewModels.TeachersAttendances
{
    public class GetTeachersAttendancesForDay
    {
        public int Id { get; set; }
        public string TeacherName { get; set; }
        public bool? IsExcuse { get; set; }
        public bool? Signature { get; set; }
    }
}
