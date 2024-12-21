namespace Jaberah.Models.ViewModels.Reports
{
    public class GetBestStudentsReportForView
    {
        public string StudentName { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public double SaveGrade { get; set; }
        public double ReviewGrade { get; set; }
        public int AttendanceGrade { get; set; }
        public int BehaviorGrade { get; set; }
        public double OralGrade { get; set; }
        public double PaperGrade { get; set; }
        public double Total { get; set; }
    }
}
