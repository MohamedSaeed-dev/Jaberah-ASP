namespace Jaberah.Models.ViewModels.TeachersAttendances
{
    public class GetTeachersAttendancesReportForMonth
    {
        public string TeacherName { get; set; }
        public string GroupName { get; set; }
        public int ExcuseNo { get; set; }
        public int PresentNo { get; set; }
        public int AbsentNo { get; set; }
        public int LateNo { get; set; }
    }
}
