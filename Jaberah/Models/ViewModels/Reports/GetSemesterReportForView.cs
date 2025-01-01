namespace Jaberah.Models.ViewModels.Reports
{
    class SemesterReportForView
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public double GradeSum { get; set; }
        public double AttendanceSum { get; set; }
        public double BehaviorSum { get; set; }
        public double OralGradeSum { get; set; }
        public double PaperGradeSum { get; set; }
        public double MidFinalGrade { get; set; }
        public double Total { get; set; }
    }
}
